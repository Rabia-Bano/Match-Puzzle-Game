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
            if (!_initialized)
            {
                Debug.LogWarning("[LeaderboardManager] SubmitScore called before Initialize().");
                return false;
            }

            FirebaseUser user = AuthManager.CurrentUser;
            if (user == null)
            {
                Debug.LogWarning("[LeaderboardManager] SubmitScore: no logged-in user.");
                return false;
            }

            if (NetworkChecker.Instance != null && !await NetworkChecker.Instance.CheckConnectivityAsync())
            {
                if (logVerbose) Debug.Log("[LeaderboardManager] Offline — skipping score submit, will not retry automatically.");
                return false;
            }

            // ── 1) Client-side plausibility check ──────────────────
            int maxPossibleScore = await FetchMaxPossibleScoreAsync(levelId);
            if (maxPossibleScore > 0 && score > maxPossibleScore)
            {
                Debug.LogWarning($"[LeaderboardManager] Score {score} exceeds max {maxPossibleScore} for level '{levelId}' — not submitting.");
                _ = LogAnomalyAsync(user.UserId, levelId, score, maxPossibleScore);
                return false;
            }

            // ── 2) Atomic accumulate into /leaderboard/{uid} ──
            DatabaseReference entryRef = _leaderboardRootRef.Child(user.UserId);

            string displayName = ProfileManager.Instance?.Profile?.displayName ?? "Player";
            string avatarUrl   = ProfileManager.Instance?.Profile?.avatarUrl   ?? "";

            try
            {
                await entryRef.RunTransaction(mutableData =>
                {
                    var dict = mutableData.Value as Dictionary<string, object> ?? new Dictionary<string, object>();
                    long existing = dict.TryGetValue("totalScore", out object v) && long.TryParse(v.ToString(), out long ex) ? ex : 0;

                    dict["uid"]         = user.UserId;
                    dict["displayName"] = displayName;
                    dict["avatarUrl"]   = avatarUrl;
                    dict["totalScore"]  = existing + score;

                    mutableData.Value = dict;
                    return TransactionResult.Success(mutableData);
                });

                if (logVerbose) Debug.Log($"[LeaderboardManager] Score submitted: +{score} (all-time total).");
                return true;
            }
            catch (DatabaseException dex)
            {
                Debug.LogError($"[LeaderboardManager] SubmitScore DatabaseException: {dex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LeaderboardManager] SubmitScore failed: {ex.Message}");
                return false;
            }
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