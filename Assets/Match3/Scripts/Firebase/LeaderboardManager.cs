using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

using Firebase;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Firestore;
using Firebase.Extensions;

using DbQuery = Firebase.Database.Query;

using Match3;

namespace Game.Firebase
{
    public class LeaderboardManager : MonoBehaviour
    {
        public static LeaderboardManager Instance { get; private set; }

        private const string LEADERBOARD_ROOT = "leaderboard";

        [Tooltip("Set true for verbose debug logs while wiring this up.")]
        [SerializeField] private bool logVerbose = true;

        [Header("Realtime Database URL (fallback)")]
        [Tooltip("OPTIONAL. Only needed if google-services.json was downloaded BEFORE Realtime " +
                 "Database was enabled in the Firebase Console (older config files miss the " +
                 "databaseURL, causing 'Specify DatabaseURL within FirebaseApp' errors). " +
                 "Paste the URL shown at the top of Firebase Console → Realtime Database → Data " +
                 "(looks like https://your-project-default-rtdb.REGION.firebasedatabase.app/).")]
        [SerializeField] private string databaseUrlOverride = "";

        public event Action<List<LeaderboardEntry>> OnLeaderboardUpdated;

        public event Action<string> OnLeaderboardError;

        private DatabaseReference _leaderboardRootRef;
        private DbQuery           _currentQuery;
        private bool              _initialized;
        private bool              _isListening;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Initialize()
        {
            if (!FirebaseInitializer.IsReady)
            {
                Debug.LogError("[LeaderboardManager] Firebase not ready — cannot initialize.");
                return;
            }

            FirebaseDatabase db = string.IsNullOrWhiteSpace(databaseUrlOverride)
                ? FirebaseDatabase.DefaultInstance
                : FirebaseDatabase.GetInstance(FirebaseApp.DefaultInstance, databaseUrlOverride.Trim());

            _leaderboardRootRef = db.RootReference.Child(LEADERBOARD_ROOT);
            _initialized = true;
            if (logVerbose) Debug.Log("[LeaderboardManager] Ready.");

            RequestSync();
        }

        [Header("Profile → Leaderboard sync (NEW)")]
        [Tooltip("Waits this long after the last profile change before writing (batches rapid saves).")]
        [SerializeField] private float syncDebounceSeconds = 1.5f;

        private bool      _syncInFlight;
        private bool      _syncAgainAfter;
        private Coroutine _debounceRoutine;

        private void OnEnable()
        {
            GameEvents.OnPlayerLoggedIn       += RequestSync;
            LocalSaveManager.OnProfileChanged += HandleProfileChanged;
        }

        private void Start()
        {
            if (NetworkChecker.Instance != null)
                NetworkChecker.Instance.OnConnectivityChanged += HandleConnectivityChanged;
            if (CloudSyncManager.Instance != null)
                CloudSyncManager.Instance.OnProfileSynced += HandleProfileChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnPlayerLoggedIn       -= RequestSync;
            LocalSaveManager.OnProfileChanged -= HandleProfileChanged;
            if (NetworkChecker.Instance != null)
                NetworkChecker.Instance.OnConnectivityChanged -= HandleConnectivityChanged;
            if (CloudSyncManager.Instance != null)
                CloudSyncManager.Instance.OnProfileSynced -= HandleProfileChanged;
        }

        private void HandleProfileChanged(PlayerProfile _) => RequestSync();

        private void HandleConnectivityChanged(bool online)
        {
            if (online) RequestSync();
        }

        public void RequestSync()
        {
            if (!isActiveAndEnabled) return;
            if (_debounceRoutine != null) StopCoroutine(_debounceRoutine);
            _debounceRoutine = StartCoroutine(DebouncedSync());
        }

        private System.Collections.IEnumerator DebouncedSync()
        {
            yield return new WaitForSecondsRealtime(syncDebounceSeconds);
            _debounceRoutine = null;
            _ = SyncFromProfileAsync();
        }

        private static PlayerProfile CurrentPlayersProfile(string uid)
        {
            PlayerProfile p = ProfileManager.Instance?.Profile;
            if (p == null || p.uid != uid) p = LocalSaveManager.GetOrLoadProfile();
            return (p != null && p.uid == uid) ? p : null;
        }

        public async Task<bool> SyncFromProfileAsync()
        {
            if (!_initialized || _leaderboardRootRef == null) return false;

            FirebaseUser user = AuthManager.CurrentUser;
            if (user == null) return false;

            if (_syncInFlight) { _syncAgainAfter = true; return false; }
            _syncInFlight = true;

            try
            {
                PlayerProfile profile = CurrentPlayersProfile(user.UserId);
                if (profile == null)
                {
                    if (logVerbose) Debug.Log("[LeaderboardManager] Sync skipped — this player's profile isn't loaded yet.");
                    return false;
                }

                if (NetworkChecker.Instance != null && !await NetworkChecker.Instance.CheckConnectivityAsync())
                {
                    if (logVerbose) Debug.Log("[LeaderboardManager] Offline — leaderboard will sync when internet returns.");
                    return false;
                }

                long   profileScore = Math.Max(0, profile.totalScore);
                string name         = profile.displayName;
                string avatarId     = profile.avatarId  ?? "";
                string avatarUrl    = profile.avatarUrl ?? "";
                string uid          = user.UserId;
                bool   wrote        = false;

                await _leaderboardRootRef.Child(uid).RunTransaction(mutableData =>
                {
                    var dict = mutableData.Value as Dictionary<string, object> ?? new Dictionary<string, object>();

                    long existing = dict.TryGetValue("totalScore", out object v) && long.TryParse(v?.ToString(), out long ex) ? ex : 0;
                    string existingName = dict.TryGetValue("displayName", out object n) ? n?.ToString() : null;

                    string finalName = !string.IsNullOrWhiteSpace(name) ? name : existingName;
                    if (string.IsNullOrWhiteSpace(finalName)) return TransactionResult.Abort();

                    long finalScore = Math.Max(existing, profileScore);
                    bool same = existing == finalScore && existingName == finalName
                                && (dict.TryGetValue("avatarId", out object a) ? a?.ToString() : "") == avatarId;
                    if (same) return TransactionResult.Abort();

                    dict["uid"]         = uid;
                    dict["displayName"] = finalName;
                    dict["avatarId"]    = avatarId;
                    dict["avatarUrl"]   = avatarUrl;
                    dict["totalScore"]  = finalScore;

                    mutableData.Value = dict;
                    wrote = true;
                    return TransactionResult.Success(mutableData);
                });

                if (wrote && logVerbose)
                    Debug.Log($"[LeaderboardManager] Leaderboard synced from profile: {name} = {profileScore:N0}");
                return true;
            }
            catch (Exception ex)
            {
                if (logVerbose) Debug.Log($"[LeaderboardManager] Leaderboard sync not written: {ex.GetBaseException().Message}");
                return false;
            }
            finally
            {
                _syncInFlight = false;
                if (_syncAgainAfter) { _syncAgainAfter = false; RequestSync(); }
            }
        }

        public void StartListening()
        {
            if (!_initialized || _leaderboardRootRef == null)
            {
                Debug.LogWarning("[LeaderboardManager] StartListening called before Initialize().");
                return;
            }

            if (_isListening)
            {
                if (logVerbose) Debug.Log("[LeaderboardManager] Already listening — ignoring duplicate StartListening().");
                return;
            }

            _currentQuery = _leaderboardRootRef.OrderByChild("totalScore");
            _currentQuery.ValueChanged += HandleValueChanged;
            _isListening = true;

            if (logVerbose) Debug.Log("[LeaderboardManager] Listening on /leaderboard (all-time, no reset).");
        }

        public void StopListening()
        {
            if (_currentQuery != null)
            {
                _currentQuery.ValueChanged -= HandleValueChanged;
                _currentQuery = null;
            }
            _isListening = false;
        }

        public void SyncAvatarToLeaderboard(string avatarId, string avatarUrl)
        {
            if (!_initialized || _leaderboardRootRef == null)
            {
                Debug.LogWarning("[LeaderboardManager] SyncAvatarToLeaderboard called before Initialize().");
                return;
            }

            FirebaseUser user = AuthManager.CurrentUser;
            if (user == null) return;

            DatabaseReference entryRef = _leaderboardRootRef.Child(user.UserId);
            entryRef.GetValueAsync().ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted || t.IsCanceled || !t.Result.Exists)
                    return;

                var updates = new Dictionary<string, object>
                {
                    { "avatarId",  avatarId  ?? "" },
                    { "avatarUrl", avatarUrl ?? "" }
                };

                entryRef.UpdateChildrenAsync(updates).ContinueWithOnMainThread(u =>
                {
                    if (u.IsFaulted)
                        Debug.LogError($"[LeaderboardManager] SyncAvatarToLeaderboard failed: {u.Exception?.GetBaseException()?.Message}");
                    else if (logVerbose)
                        Debug.Log("[LeaderboardManager] Leaderboard avatar synced.");
                });
            });
        }

        private void HandleValueChanged(object sender, ValueChangedEventArgs args)
        {
            if (args.DatabaseError != null)
            {
                Debug.LogError($"[LeaderboardManager] RTDB error: {args.DatabaseError.Message}");
                OnLeaderboardError?.Invoke(args.DatabaseError.Message);
                return;
            }

            var entries = new List<LeaderboardEntry>();
            foreach (DataSnapshot child in args.Snapshot.Children)
            {
                var entry = LeaderboardEntry.FromSnapshot(child);
                if (entry != null) entries.Add(entry);
            }

            entries.Sort((a, b) => b.totalScore.CompareTo(a.totalScore));
            for (int i = 0; i < entries.Count; i++)
                entries[i].rank = i + 1;

            OnLeaderboardUpdated?.Invoke(entries);
        }

        private void OnDestroy()
        {
            if (Instance == this) StopListening();
        }

        public async Task<bool> SubmitScore(int score, string levelId)
        {
            if (!_initialized) return false;

            FirebaseUser user = AuthManager.CurrentUser;
            if (user == null) return false;

            bool online = NetworkChecker.Instance == null || await NetworkChecker.Instance.CheckConnectivityAsync();
            if (online && score > 0)
            {
                int maxPossibleScore = await FetchMaxPossibleScoreAsync(levelId);
                if (maxPossibleScore > 0 && score > maxPossibleScore)
                {
                    Debug.LogWarning($"[LeaderboardManager] Score {score} exceeds max {maxPossibleScore} for level '{levelId}' — logged as anomaly.");
                    _ = LogAnomalyAsync(user.UserId, levelId, score, maxPossibleScore);
                }
            }

            RequestSync();
            return online;
        }

        private async Task<int> FetchMaxPossibleScoreAsync(string levelId)
        {
            if (string.IsNullOrEmpty(levelId)) return 0;

            try
            {
                var db = FirebaseFirestore.DefaultInstance;
                var snap = await db.Collection("levels").Document(levelId).GetSnapshotAsync();
                if (!snap.Exists) return 0;
                if (snap.TryGetValue("maxPossibleScore", out int max)) return max;
                return 0;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LeaderboardManager] Could not fetch maxPossibleScore for '{levelId}': {ex.Message}");
                return 0;
            }
        }

        private async Task LogAnomalyAsync(string uid, string levelId, int score, int maxPossibleScore)
        {
            try
            {
                var db = FirebaseFirestore.DefaultInstance;
                var entry = new Dictionary<string, object>
                {
                    { "uid",              uid },
                    { "levelId",          levelId },
                    { "score",            score },
                    { "maxPossibleScore", maxPossibleScore },
                    { "reason",           "score_exceeds_max_client_side" },
                    { "timestamp",        Timestamp.GetCurrentTimestamp() }
                };
                await db.Collection("anomaly_log").AddAsync(entry);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LeaderboardManager] LogAnomalyAsync failed: {ex.Message}");
            }
        }
    }
}
