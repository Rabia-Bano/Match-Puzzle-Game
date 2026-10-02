using System;
using System.Threading.Tasks;
using UnityEngine;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;

namespace Game.Firebase
{
    public class CloudSyncManager : MonoBehaviour
    {
        public static CloudSyncManager Instance { get; private set; }

        private const string COLLECTION = "players";

        [SerializeField] private bool logVerbose = true;

        public event Action<PlayerProfile> OnProfileSynced;

        private FirebaseFirestore _db;
        private bool _initialized = false;

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
                Debug.LogError("[CloudSyncManager] Firebase not ready — cannot initialize.");
                return;
            }
            _db = FirebaseFirestore.DefaultInstance;
            _initialized = true;
            Debug.Log("[CloudSyncManager] Ready.");
        }

        public async Task SyncOnSessionStartAsync()
        {
            FirebaseUser user = AuthManager.CurrentUser;
            if (user == null)
            {
                Debug.LogWarning("[CloudSyncManager] SyncOnSessionStartAsync: no logged-in user, skipping.");
                return;
            }
            if (!_initialized || _db == null)
            {
                Debug.LogWarning("[CloudSyncManager] SyncOnSessionStartAsync: not initialized yet, skipping.");
                return;
            }

            bool online = NetworkChecker.Instance == null || await NetworkChecker.Instance.CheckConnectivityAsync();
            if (!online)
            {
                Debug.LogWarning("[CloudSyncManager] Offline at session start — local save use ho rahi hai, baad mein sync hoga.");
                return;
            }

            PlayerProfile local   = LocalSaveManager.GetOrLoadProfile();
            DateTime      lastSync = LocalSaveManager.GetLastSyncTime();

            if (local != null && !string.IsNullOrEmpty(local.uid) && local.uid != user.UserId)
            {
                Debug.LogWarning($"[CloudSyncManager] Local cache belongs to a different account " +
                                  $"('{local.uid}') than the one logging in now ('{user.UserId}'). " +
                                  $"Discarding stale local cache for this session.");
                LocalSaveManager.ClearAll();
                local = null;
            }

            PlayerProfile cloud;
            try
            {
                cloud = await FetchCloudProfileAsync(user.UserId);
            }
            catch (FirebaseException fex)
            {
                Debug.LogError($"[CloudSyncManager] SyncOnSessionStartAsync fetch failed (FirebaseException {fex.ErrorCode}): {fex.Message}. Local save par continue kar rahe hain.");
                return;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CloudSyncManager] SyncOnSessionStartAsync fetch failed: {ex.Message}. Local save par continue kar rahe hain.");
                return;
            }

            Debug.Log($"[TEMP DEBUG] cloud.isBanned = {(cloud != null ? cloud.isBanned.ToString() : "cloud is null")}");

            if (cloud == null)
            {
                if (local != null)
                {
                    local.uid = user.UserId;
                    await SafePushAsync(local, "session-start (no cloud doc yet)");
                }
                LocalSaveManager.SetLastSyncTime(DateTime.UtcNow);
                return;
            }

            if (local == null)
            {
                ApplyProfile(cloud);
                LocalSaveManager.SetLastSyncTime(DateTime.UtcNow);
                return;
            }

            Debug.Log($"[TEMP DEBUG] local.isBanned = {local.isBanned}, local.uid = '{local.uid}', auth.uid = '{user.UserId}'");

            DateTime localTime = ParseTime(local.lastUpdated);
            DateTime cloudTime = ParseTime(cloud.lastUpdated);

            bool localChangedSinceSync = localTime > lastSync;
            bool cloudChangedSinceSync = cloudTime > lastSync;

            PlayerProfile finalProfile;
            if (localChangedSinceSync && cloudChangedSinceSync)
            {
                if (logVerbose) Debug.Log("[CloudSyncManager] Real conflict — dono sides last sync ke baad change hui hain. Merging via ConflictResolver.");
                finalProfile = ConflictResolver.Resolve(local, cloud);
            }
            else if (cloudTime > localTime)
            {
                if (logVerbose) Debug.Log("[CloudSyncManager] Cloud profile newer hai — cloud load kar rahe hain.");
                finalProfile = cloud;
            }
            else
            {
                if (logVerbose) Debug.Log("[CloudSyncManager] Local profile newer/equal hai — local ko cloud par push kar rahe hain.");
                finalProfile = local;
            }

            Debug.Log($"[TEMP DEBUG] finalProfile.isBanned (about to push) = {finalProfile.isBanned}, finalProfile.uid = '{finalProfile.uid}'");

            ApplyProfile(finalProfile);
            await SafePushAsync(finalProfile, "session-start (post-resolve)");
            LocalSaveManager.SetLastSyncTime(DateTime.UtcNow);
        }

        public async Task SyncAfterLevelAsync()
        {
            try
            {
                FirebaseUser user = AuthManager.CurrentUser;
                if (user == null) return;
                if (!_initialized || _db == null) return;

                bool online = NetworkChecker.Instance == null || await NetworkChecker.Instance.CheckConnectivityAsync();
                if (!online)
                {
                    if (logVerbose) Debug.Log("[CloudSyncManager] Offline — level result sirf local save mein rahega, agli baar online hone par sync hoga.");
                    return;
                }

                PlayerProfile local = LocalSaveManager.GetOrLoadProfile();
                if (local == null) return;
                if (string.IsNullOrEmpty(local.uid)) local.uid = user.UserId;

                await PushProfileAsync(local);
                LocalSaveManager.SetLastSyncTime(DateTime.UtcNow);
                if (logVerbose) Debug.Log("[CloudSyncManager] Post-level cloud sync complete.");
            }
            catch (FirebaseException fex)
            {
                Debug.LogError($"[CloudSyncManager] SyncAfterLevelAsync FirebaseException ({fex.ErrorCode}): {fex.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CloudSyncManager] SyncAfterLevelAsync failed: {ex.Message}");
            }
        }

        public async Task<PlayerProfile> FetchCloudProfileAsync(string uid)
        {
            if (!_initialized || _db == null)
                throw new InvalidOperationException("[CloudSyncManager] FetchCloudProfileAsync: not initialized.");
            if (string.IsNullOrEmpty(uid))
                throw new ArgumentException("uid is null/empty", nameof(uid));

            DocumentSnapshot snap = await _db.Collection(COLLECTION).Document(uid).GetSnapshotAsync();
            if (!snap.Exists) return null;

            PlayerProfile profile = PlayerProfile.FromFirestoreDict(snap.ToDictionary());
            if (string.IsNullOrEmpty(profile.uid)) profile.uid = uid;
            return profile;
        }

        public async Task PushProfileAsync(PlayerProfile profile)
        {
            if (!_initialized || _db == null)
                throw new InvalidOperationException("[CloudSyncManager] PushProfileAsync: not initialized.");
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            if (string.IsNullOrEmpty(profile.uid))
            {
                FirebaseUser user = AuthManager.CurrentUser;
                if (user == null)
                    throw new InvalidOperationException("[CloudSyncManager] PushProfileAsync: profile.uid empty aur koi logged-in user nahi hai.");
                profile.uid = user.UserId;
            }

            profile.lastUpdated = DateTime.UtcNow.ToString("o");

            var dict = profile.ToFirestoreDict();
            Debug.Log($"[TEMP DEBUG] PushProfileAsync — Document(\"{profile.uid}\"), " +
                      $"dict[\"isBanned\"] = {(dict.ContainsKey("isBanned") ? dict["isBanned"].ToString() : "KEY MISSING FROM DICT!")}");

            await _db.Collection(COLLECTION).Document(profile.uid)
                     .SetAsync(dict, SetOptions.MergeAll);
        }

        private async Task SafePushAsync(PlayerProfile profile, string context)
        {
            try
            {
                await PushProfileAsync(profile);
            }
            catch (FirebaseException fex)
            {
                Debug.LogError($"[CloudSyncManager] Push failed [{context}] (FirebaseException {fex.ErrorCode}): {fex.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CloudSyncManager] Push failed [{context}]: {ex.Message}");
            }
        }

        private void ApplyProfile(PlayerProfile profile)
        {
            LocalSaveManager.SaveProfile(profile);
            OnProfileSynced?.Invoke(profile);
        }

        private static DateTime ParseTime(string iso)
        {
            return DateTime.TryParse(
                iso, null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out DateTime t) ? t : DateTime.MinValue;
        }
    }
}
