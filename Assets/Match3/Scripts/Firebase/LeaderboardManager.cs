// ============================================================
//  LeaderboardManager.cs  —  Singleton MonoBehaviour (DontDestroyOnLoad)
//  Attach to: "FirebaseManagers" GameObject in PreloaderScene — the
//             SAME object that already has FirebaseInitializer,
//             AuthManager, ProfileManager, CloudSyncManager, NetworkChecker.
//  Call: Initialize() from FirebaseInitializer.OnFirebaseReady
//        (Inspector UnityEvent), same as the other managers.
//  Access: Game.Firebase.LeaderboardManager.Instance
//
//  ALL-TIME LEADERBOARD — no periodic reset. Every player has exactly
//  ONE row at /leaderboard/{uid}, and totalScore accumulates forever
//  across every level ever completed.
// ============================================================

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

using Firebase;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Firestore;
using Firebase.Extensions;

// Both Firebase.Database and Firebase.Firestore have a "Query" type — alias
// it explicitly to the Realtime Database one to remove the CS0104 ambiguity.
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

        /// <summary>Fired every time the RTDB listener receives fresh data,
        /// already sorted descending by totalScore with rank assigned.</summary>
        public event Action<List<LeaderboardEntry>> OnLeaderboardUpdated;

        /// <summary>Fired if the RTDB listener itself errors out (permission
        /// denied, disconnected, etc). UI can show a retry / offline state.</summary>
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

        /// <summary>Call this from FirebaseInitializer.OnFirebaseReady (Inspector).</summary>
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

            RequestSync();   // NEW — catch up anything earned while offline
        }

        // ============================================================
        //  NEW — LEADERBOARD = MIRROR OF THE PLAYER'S PROFILE SCORE
        //
        //  OLD design (bugs Rabia found):
        //   1. Every level win ADDED its score to /leaderboard/{uid} separately
        //      from the profile. Offline wins, failed writes or the anti-cheat
        //      check made the two numbers drift apart, so the leaderboard never
        //      matched the score shown on the Players page / in-game profile.
        //   2. When no profile was loaded yet, the name fell back to "Player"
        //      and OVERWROTE the real name on the leaderboard.
        //
        //  NEW design:
        //   • The player profile (players/{uid}.totalScore — already offline-
        //     safe via LocalSaveManager + CloudSyncManager) is the ONE source
        //     of truth. The leaderboard simply COPIES that number.
        //   • Synced automatically whenever the profile changes, internet
        //     comes back, the player logs in, or a level/boss is won.
        //     Nothing is queued separately, so nothing can be lost or
        //     counted twice.
        //   • The name/avatar are written ONLY from a loaded profile that
        //     belongs to the logged-in uid — never "Player". If no profile is
        //     loaded yet, the sync simply waits.
        //   • totalScore never goes DOWN (same as the RTDB rule), so a
        //     half-loaded profile can't wipe a good leaderboard score.
        // ============================================================

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

        /// <summary>Schedules a (debounced) profile → leaderboard sync.</summary>
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

        /// <summary>The profile that belongs to the CURRENTLY logged-in uid, or null.</summary>
        private static PlayerProfile CurrentPlayersProfile(string uid)
        {
            PlayerProfile p = ProfileManager.Instance?.Profile;
            if (p == null || p.uid != uid) p = LocalSaveManager.GetOrLoadProfile();
            return (p != null && p.uid == uid) ? p : null;
        }

        /// <summary>Copies profile.totalScore + name + avatar to /leaderboard/{uid}.</summary>
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

                    // Never write a placeholder name; keep the stored one instead.
                    string finalName = !string.IsNullOrWhiteSpace(name) ? name : existingName;
                    if (string.IsNullOrWhiteSpace(finalName)) return TransactionResult.Abort();

                    long finalScore = Math.Max(existing, profileScore);   // never goes down
                    bool same = existing == finalScore && existingName == finalName
                                && (dict.TryGetValue("avatarId", out object a) ? a?.ToString() : "") == avatarId;
                    if (same) return TransactionResult.Abort();           // nothing to change

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
                // An aborted transaction (nothing to change) can also land here on some SDK versions.
                if (logVerbose) Debug.Log($"[LeaderboardManager] Leaderboard sync not written: {ex.GetBaseException().Message}");
                return false;
            }
            finally
            {
                _syncInFlight = false;
                if (_syncAgainAfter) { _syncAgainAfter = false; RequestSync(); }
            }
        }

        // ============================================================
        //  LISTENING
        // ============================================================

        /// <summary>
        /// Attaches a ValueChanged listener to /leaderboard/, ordered by
        /// totalScore. Call from LeaderboardPanel.OnEnable(). Safe to call
        /// multiple times — it will not double-subscribe.
        /// </summary>
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

        /// <summary>Detaches the listener. MUST be called from LeaderboardPanel.OnDisable()
        /// (and/or OnDestroy) to avoid callbacks firing on a destroyed UI.</summary>
        public void StopListening()
        {
            if (_currentQuery != null)
            {
                _currentQuery.ValueChanged -= HandleValueChanged;
                _currentQuery = null;
            }
            _isListening = false;
        }

        // ============================================================
        //  AVATAR SYNC
        // ============================================================

        /// <summary>
        /// Patches ONLY the avatar fields into the caller's existing
        /// /leaderboard/{uid} entry, immediately when they change their avatar —
        /// without waiting for their next SubmitScore() call. Without this,
        /// a player who already has an all-time leaderboard row from a previous
        /// session, and then changes their preset avatar, would keep showing the
        /// OLD (or empty) avatar on the leaderboard until they finish another
        /// level. Call this from ProfileManager right after SetPresetAvatar /
        /// after an avatar photo upload completes.
        /// No-ops if the player doesn't have a leaderboard entry yet (hasn't
        /// finished a level) so this never creates a phantom zero-score row.
        /// </summary>
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
                    return; // no leaderboard entry yet — nothing to patch

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

            // OrderByChild in the query only guarantees ascending order and only
            // within what the SDK streamed — sort again client-side to be 100% safe,
            // descending (highest score first), then assign 1-based rank.
            entries.Sort((a, b) => b.totalScore.CompareTo(a.totalScore));
            for (int i = 0; i < entries.Count; i++)
                entries[i].rank = i + 1;

            OnLeaderboardUpdated?.Invoke(entries);
        }

        private void OnDestroy()
        {
            if (Instance == this) StopListening();
        }

        // ============================================================
        //  SUBMIT SCORE
        // ============================================================

        /// <summary>
        /// SPARK-PLAN VERSION (no Cloud Functions / no Blaze required).
        /// Submits a level score to the all-time leaderboard:
        ///   1) Reads levels/{levelId}.maxPossibleScore from Firestore (free on Spark)
        ///      and rejects locally if the score is implausible — logs to
        ///      Firestore "anomaly_log" (best-effort; not tamper-proof since the
        ///      check runs on-device).
        ///   2) Writes to /leaderboard/{uid} using a Realtime Database
        ///      RunTransaction so concurrent writes never clobber each other,
        ///      and accumulates totalScore atomically — forever, no reset.
        /// RTDB security rules restrict this write to the caller's OWN uid
        /// only — nobody can write another player's entry.
        /// </summary>
        public async Task<bool> SubmitScore(int score, string levelId)
        {
            // UPDATED — the level's score has ALREADY been added to the player's
            // profile (ProfileManager.OnLevelCompleted). The leaderboard now just
            // mirrors that profile total, so here we only (1) run the anti-cheat
            // check when online and (2) trigger the sync. Offline? The sync
            // happens automatically as soon as the internet is back.
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

        /// <summary>Reads levels/{levelId}.maxPossibleScore from Firestore. Returns 0
        /// (meaning "no cap configured, skip check") if the field/doc is missing.</summary>
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
                return 0; // fail-open — don't block legit submissions because of a read hiccup
            }
        }

        /// <summary>Best-effort anomaly logging (Firestore is free on Spark).
        /// Not tamper-proof — a modified client could skip this call — but useful
        /// for spotting obvious cheating from normal players during testing.</summary>
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