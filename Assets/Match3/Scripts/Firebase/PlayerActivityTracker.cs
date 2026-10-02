using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using Firebase.Extensions;
using Firebase.Firestore;

namespace Game.Firebase
{
    public class PlayerActivityTracker : MonoBehaviour
    {
        public enum Result { Win, LoseMoves, LoseTime }

        public const string COLLECTION = "playerStats";

        private const string PREF_PENDING_PREFIX = "PlayerActivity_Pending_";
        private const string PREF_IN_PROGRESS    = "PlayerActivity_InProgress";

        private static PlayerActivityTracker _instance;
        private static bool _quitting;

        public static PlayerActivityTracker Instance
        {
            get
            {
                if (_instance == null && !_quitting)
                {
                    _instance = FindFirstObjectByType<PlayerActivityTracker>();
                    if (_instance == null)
                    {
                        var go = new GameObject("PlayerActivityTracker");
                        _instance = go.AddComponent<PlayerActivityTracker>();
                    }
                }
                return _instance;
            }
        }

        [Serializable]
        private class Pending
        {
            public Dictionary<string, long> counters = new Dictionary<string, long>();
            public Dictionary<string, string> strings = new Dictionary<string, string>();
            public Dictionary<string, long> numbers = new Dictionary<string, long>();
            public bool IsEmpty => counters.Count == 0 && strings.Count == 0 && numbers.Count == 0;
        }

        private bool  _flushing;
        private float _flushStartedAt;
        private const float FLUSH_TIMEOUT = 20f;

        private static readonly string SessionId = Guid.NewGuid().ToString("N");
        private static bool _abandonCheckDone;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()  => GameEvents.OnPlayerLoggedIn += HandleLoggedIn;

        private void OnDisable()
        {
            GameEvents.OnPlayerLoggedIn -= HandleLoggedIn;
            if (NetworkChecker.Instance != null)
                NetworkChecker.Instance.OnConnectivityChanged -= HandleConnectivityChanged;
        }

        private void HandleConnectivityChanged(bool online)
        {
            if (online) Flush();
        }

        private async void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus || NetworkChecker.Instance == null) return;
            bool online = await NetworkChecker.Instance.CheckConnectivityAsync(forceRefresh: true);
            if (online) Flush();
        }
        private void OnApplicationQuit() => _quitting = true;

        private void Start()
        {
            if (NetworkChecker.Instance != null)
                NetworkChecker.Instance.OnConnectivityChanged += HandleConnectivityChanged;

            if (AuthManager.IsLoggedIn) HandleLoggedIn();
        }

        private void HandleLoggedIn()
        {
            DetectAbandonedLevel();
            Flush();
        }

        public void RecordLevelStart(int levelId)
        {
            string uid = AuthManager.CurrentUid;
            if (string.IsNullOrEmpty(uid) || levelId <= 0) return;

            Pending p = LoadPending(uid);
            Add(p, "totalStarts", 1);
            Add(p, $"levelAttempts.L{levelId}", 1);

            int completed = ProfileManager.Instance?.Profile?.levelsCompleted ?? 0;
            if (levelId <= completed) Add(p, "replaysOfCompletedLevels", 1);

            p.strings["lastPlayedAt"] = DateTime.UtcNow.ToString("o");
            p.strings["lastEvent"]    = $"start_L{levelId}";
            SavePending(uid, p);

            PlayerPrefs.SetString(PREF_IN_PROGRESS, $"{uid}|{levelId}|{SessionId}");
            PlayerPrefs.Save();

            Flush();
        }

        public void RecordLevelResult(int levelId, Result result)
        {
            string uid = AuthManager.CurrentUid;
            ClearInProgress();
            if (string.IsNullOrEmpty(uid) || levelId <= 0) return;

            Pending p = LoadPending(uid);
            switch (result)
            {
                case Result.Win:       Add(p, "totalWins", 1); break;
                case Result.LoseMoves: Add(p, "totalLosses", 1); break;
                case Result.LoseTime:  Add(p, "totalLosses", 1); Add(p, "totalTimeouts", 1); break;
            }
            p.strings["lastPlayedAt"] = DateTime.UtcNow.ToString("o");
            p.strings["lastEvent"]    = $"{result.ToString().ToLowerInvariant()}_L{levelId}";
            SavePending(uid, p);
            Flush();
        }

        public void RecordLevelQuit(int levelId, string reason)
        {
            string uid = AuthManager.CurrentUid;
            ClearInProgress();
            if (string.IsNullOrEmpty(uid) || levelId <= 0) return;
            RecordQuitFor(uid, levelId, reason);
            Flush();
        }

        private void RecordQuitFor(string uid, int levelId, string reason)
        {
            Pending p = LoadPending(uid);
            Add(p, "totalQuits", 1);
            Add(p, $"levelQuits.L{levelId}", 1);
            Add(p, reason == "appClosed" ? "appClosedQuits" : "exitButtonQuits", 1);
            p.strings["lastQuitAt"]  = DateTime.UtcNow.ToString("o");
            p.numbers["lastQuitLevel"] = levelId;
            p.strings["lastEvent"]   = $"quit_{reason}_L{levelId}";
            SavePending(uid, p);
            Debug.Log($"[PlayerActivityTracker] Quit recorded — Level {levelId} ({reason}).");
        }

        private void DetectAbandonedLevel()
        {
            if (_abandonCheckDone) return;
            _abandonCheckDone = true;

            string raw = PlayerPrefs.GetString(PREF_IN_PROGRESS, "");
            if (string.IsNullOrEmpty(raw)) return;

            string[] parts = raw.Split('|');
            if (parts.Length >= 3 && parts[2] == SessionId) return;

            ClearInProgress();
            if (parts.Length < 2 || !int.TryParse(parts[1], out int levelId)) return;

            RecordQuitFor(parts[0], levelId, "appClosed");
        }

        private static void ClearInProgress()
        {
            if (!PlayerPrefs.HasKey(PREF_IN_PROGRESS)) return;
            PlayerPrefs.DeleteKey(PREF_IN_PROGRESS);
            PlayerPrefs.Save();
        }

        public void Flush()
        {
            string uid = AuthManager.CurrentUid;
            if (string.IsNullOrEmpty(uid) || !FirebaseInitializer.IsReady) return;

            if (NetworkChecker.Instance != null && !NetworkChecker.Instance.IsOnline) return;
            if (_flushing && Time.realtimeSinceStartup - _flushStartedAt < FLUSH_TIMEOUT) return;

            Pending snapshot = LoadPending(uid);
            if (snapshot.IsEmpty) return;

            SavePending(uid, new Pending());
            _flushing = true;
            _flushStartedAt = Time.realtimeSinceStartup;

            Dictionary<string, object> doc = BuildDoc(uid, snapshot);

            FirebaseFirestore.DefaultInstance.Collection(COLLECTION).Document(uid)
                .SetAsync(doc, SetOptions.MergeAll)
                .ContinueWithOnMainThread(task =>
                {
                    _flushing = false;
                    if (task.IsFaulted || task.IsCanceled)
                    {
                        Debug.LogWarning($"[PlayerActivityTracker] Flush failed (will retry later): {task.Exception?.GetBaseException()?.Message}");
                        Pending current = LoadPending(uid);
                        MergeInto(current, snapshot);
                        SavePending(uid, current);
                        return;
                    }
                    Debug.Log("[PlayerActivityTracker] Activity stats synced to Firestore.");

                    if (!LoadPending(uid).IsEmpty) Flush();
                });
        }

        private static Dictionary<string, object> BuildDoc(string uid, Pending p)
        {
            var doc = new Dictionary<string, object>
            {
                { "uid", uid },
                { "updatedAt", DateTime.UtcNow.ToString("o") }
            };

            PlayerProfile profile = ProfileManager.Instance?.Profile;
            if (profile != null && profile.uid == uid)
            {
                doc["displayName"] = profile.displayName ?? "";
                doc["email"]       = profile.email ?? "";
            }
            doc["isGuest"] = AuthManager.IsGuest;

            foreach (var kv in p.counters)
            {
                string[] path = kv.Key.Split('.');
                if (path.Length == 1)
                {
                    doc[path[0]] = FieldValue.Increment(kv.Value);
                }
                else
                {
                    if (!(doc.TryGetValue(path[0], out object mapObj) && mapObj is Dictionary<string, object> map))
                    {
                        map = new Dictionary<string, object>();
                        doc[path[0]] = map;
                    }
                    map[path[1]] = FieldValue.Increment(kv.Value);
                }
            }
            foreach (var kv in p.strings) doc[kv.Key] = kv.Value;
            foreach (var kv in p.numbers) doc[kv.Key] = kv.Value;
            return doc;
        }

        private static void Add(Pending p, string key, long delta)
        {
            p.counters.TryGetValue(key, out long cur);
            p.counters[key] = cur + delta;
        }

        private static void MergeInto(Pending target, Pending src)
        {
            foreach (var kv in src.counters) Add(target, kv.Key, kv.Value);
            foreach (var kv in src.strings) if (!target.strings.ContainsKey(kv.Key)) target.strings[kv.Key] = kv.Value;
            foreach (var kv in src.numbers) if (!target.numbers.ContainsKey(kv.Key)) target.numbers[kv.Key] = kv.Value;
        }

        private static Pending LoadPending(string uid)
        {
            string json = PlayerPrefs.GetString(PREF_PENDING_PREFIX + uid, "");
            if (string.IsNullOrEmpty(json)) return new Pending();
            try { return JsonConvert.DeserializeObject<Pending>(json) ?? new Pending(); }
            catch { return new Pending(); }
        }

        private static void SavePending(string uid, Pending p)
        {
            PlayerPrefs.SetString(PREF_PENDING_PREFIX + uid, JsonConvert.SerializeObject(p));
            PlayerPrefs.Save();
        }
    }
}
