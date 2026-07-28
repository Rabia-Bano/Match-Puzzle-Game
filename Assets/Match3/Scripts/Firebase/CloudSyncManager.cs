// ============================================================
//  CloudSyncManager.cs  —  Singleton MonoBehaviour (DontDestroyOnLoad)
//  Attach to: FirebaseManagers GameObject (PreloaderScene) — same
//             object as FirebaseInitializer, AuthManager, ProfileManager,
//             NetworkChecker.
//  Call: Initialize() from FirebaseInitializer.OnFirebaseReady (Inspector
//        UnityEvent), AFTER AuthManager.Initialize() aur ProfileManager.Initialize().
//  Access: CloudSyncManager.Instance.SyncOnSessionStartAsync() / SyncAfterLevelAsync()
//
//  Ye class LocalSaveManager (offline PlayerPrefs) aur Firestore
//  (players/{uid}) ke beech ka "hybrid save" pull karti hai, jaisa
//  scenario mein maanga gaya tha:
//    - Naye session (login/app-resume) par jo bhi newer hai (ya agar
//      dono sides last-sync ke baad change hui hain to safe MERGE via
//      ConflictResolver) wo load hota hai.
//    - Har level complete hone ke baad local profile background mein
//      (fire-and-forget) Firestore par push hota hai — UI kabhi block
//      nahi hoti, aur fail hone par sirf log hota hai, game nahi rukta.
// ============================================================

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

        /// <summary>Fires har baar jab local profile ko cloud/merge se update kiya jata hai
        /// (SyncOnSessionStartAsync ke andar). UI (ProfilePanel, TopBarHUD) is par subscribe
        /// karke turant refresh ho sakti hai — LocalSaveManager.OnProfileChanged bhi automatically
        /// fire hoga kyunki hum ApplyProfile() ke andar LocalSaveManager.SaveProfile() call karte hain.</summary>
        public event Action<PlayerProfile> OnProfileSynced;

        private FirebaseFirestore _db;
        private bool _initialized = false;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>Call this from FirebaseInitializer.OnFirebaseReady UnityEvent (Inspector).</summary>
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

        // ============================================================
        //  1) SESSION START SYNC
        //  Login hone ke turant baad (ya app resume/foreground par) call karo.
        //  AuthManager.OnLoginSuccess / OnRegisterSuccess dono se hook karna best hai.
        // ============================================================
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

            // ── Case: Firestore par abhi tak profile document hi nahi hai ──
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

            // ── Case: local save hi nahi hai (fresh install / naya device) ──
            if (local == null)
            {
                ApplyProfile(cloud);
                LocalSaveManager.SetLastSyncTime(DateTime.UtcNow);
                return;
            }

            // ── Dono maujood hain — decide karo: newer le lo, ya agar dono
            //    last-sync ke baad independently change hui hain to safe merge karo ──
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

            ApplyProfile(finalProfile);
            await SafePushAsync(finalProfile, "session-start (post-resolve)");
            LocalSaveManager.SetLastSyncTime(DateTime.UtcNow);
        }

        // ============================================================
        //  2) AFTER-LEVEL SYNC
        //  LevelResultManager se level win/lose ke baad call karo.
        //  FIRE-AND-FORGET: caller `_ = CloudSyncManager.Instance.SyncAfterLevelAsync();`
        //  se call kare, `await` na kare — taake Win/Lose panel turant dikhe,
        //  network call background mein chale.
        // ============================================================
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

        // ============================================================
        //  3) RAW FETCH / PUSH — dusri classes bhi direct use kar sakti hain
        // ============================================================

        /// <summary>Firestore se ek profile fetch karta hai. Document exist nahi karta to null.</summary>
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

        /// <summary>Local profile ko Firestore par push karta hai (merge write — kisi aur field ko overwrite nahi karta).</summary>
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

            await _db.Collection(COLLECTION).Document(profile.uid)
                     .SetAsync(profile.ToFirestoreDict(), SetOptions.MergeAll);
        }

        // ============================================================
        //  Helpers
        // ============================================================

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

        /// <summary>Local cache update karta hai aur listeners ko inform karta hai.
        /// Jaan-boojh kar ProfileManager ko seedha nahi chherta — LocalSaveManager.OnProfileChanged
        /// aur is class ka apna OnProfileSynced event dono UI ko refresh karne ke liye kaafi hain,
        /// bina do managers ko tightly couple kiye.</summary>
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