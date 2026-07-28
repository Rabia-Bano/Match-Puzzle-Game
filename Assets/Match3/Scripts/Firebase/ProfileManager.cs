// ============================================================
//  ProfileManager.cs  —  Singleton MonoBehaviour (DontDestroyOnLoad)
//  Attach to: FirebaseManagers (PreloaderScene)
//  Call: Initialize() from FirebaseInitializer.OnFirebaseReady
//  Access: ProfileManager.Instance.Profile
// ============================================================
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Storage;
using Firebase.Extensions;

namespace Game.Firebase
{
    public class ProfileManager : MonoBehaviour
    {
        public static ProfileManager Instance { get; private set; }

        [Header("Events")]
        public UnityEvent          OnProfileLoaded;
        public UnityEvent          OnProfileSaved;
        public UnityEvent<string>  OnProfileError;
        public UnityEvent<Sprite>  OnAvatarLoaded;

        private const string COLLECTION      = "players";
        private const string STORAGE_AVATARS = "avatars/";
        private const string LOCAL_CACHE_KEY = "CloudProfile_v2";

        public PlayerProfile Profile { get; private set; }
        public bool IsLoaded => Profile != null;

        // Aliases so ProfilePanel.cs works with both naming styles
        public PlayerProfile CurrentProfile => Profile;
        public bool IsProfileLoaded => Profile != null;

        private FirebaseFirestore _db;
        private FirebaseStorage   _storage;
        private bool              _isSaving = false;
        private Coroutine         _pendingSave;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Initialize()
        {
            if (!FirebaseInitializer.IsReady) { Debug.LogError("[ProfileManager] Firebase not ready!"); return; }
            _db      = FirebaseFirestore.DefaultInstance;
            _storage = FirebaseStorage.DefaultInstance;

            if (AuthManager.Instance != null)
            {
                AuthManager.Instance.OnLoginSuccess   .AddListener(OnLoggedIn);
                AuthManager.Instance.OnRegisterSuccess.AddListener(OnLoggedIn);
                AuthManager.Instance.OnLogoutComplete .AddListener(OnLoggedOut);
            }
            Debug.Log("[ProfileManager] Ready.");
        }

        private void OnLoggedIn()
        {
            FirebaseUser user = AuthManager.CurrentUser;
            if (user != null) StartCoroutine(LoadProfileCoroutine(user.UserId));
        }

        private void OnLoggedOut() { Profile = null; }

        // ── CREATE ───────────────────────────────────────────
        public void CreateProfile(FirebaseUser user, string displayName)
        {
            if (_db == null) return;
            var profile = new PlayerProfile
            {
                uid = user.UserId, displayName = displayName,
                email = user.Email ?? "", joinDate = DateTime.UtcNow.ToString("o")
            };
            _db.Collection(COLLECTION).Document(user.UserId)
               .SetAsync(profile.ToFirestoreDict())
               .ContinueWithOnMainThread(t =>
               {
                   if (t.IsFaulted) { OnProfileError?.Invoke("Profile create error."); return; }
                   Profile = profile;
                   CacheLocally(profile);
                   OnProfileLoaded?.Invoke();
               });
        }

        // ── LOAD ─────────────────────────────────────────────
        public void LoadProfile(string uid) => StartCoroutine(LoadProfileCoroutine(uid));

// ── ADD this new private method anywhere in the class ──
/// <summary>Grants any pets the player already qualifies for based on
/// current levelsCompleted — called right after a profile is loaded/created
/// so "unlockAfterLevel = 0" starter pets show up immediately, without
/// needing to wait for the player's first level completion.</summary>
        private void SyncUnlockedPets()
        {
            if (Profile == null) return;

            var petManager = Match3.PetManager.GetOrCreateInstance();
            bool anyNew = false;

            foreach (string petId in petManager.GetUnlockedPetIds(Profile.levelsCompleted))
            {
                if (!Profile.pets.Contains(petId))
                {
                    Profile.AddPet(petId);   // updates Profile.pets + Profile.unlockedPets
                    anyNew = true;
                }
            }

            if (anyNew)
                CacheLocally(Profile);   // persist immediately so ProfilePanel shows it right away
        }

        private IEnumerator LoadProfileCoroutine(string uid)
        {
            // Instant cache
            var cached = LoadFromCache();
            if (cached != null && cached.uid == uid)
            {
                Profile = cached;
                SyncUnlockedPets(); 
                OnProfileLoaded?.Invoke();
                SyncCoinsToGameManager();
                if (!string.IsNullOrEmpty(cached.avatarUrl))
                    StartCoroutine(DownloadAvatarCoroutine(cached.avatarUrl));
            }

            // Fresh from Firestore
            bool done = false;
            _db.Collection(COLLECTION).Document(uid).GetSnapshotAsync()
            .ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted || t.IsCanceled) { done = true; return; }
                if (!t.Result.Exists)             { done = true; return; }

                PlayerProfile cloudProfile = PlayerProfile.FromFirestoreDict(t.Result.ToDictionary());
                if (string.IsNullOrEmpty(cloudProfile.uid)) cloudProfile.uid = uid;

                bool cloudIsNewer = IsNewer(cloudProfile.lastUpdated, Profile?.lastUpdated);

                if (Profile != null && !cloudIsNewer)
                {
                    Debug.LogWarning("[ProfileManager] Local save cloud se newer hai — local progress rakh raha hoon aur Firestore par push kar raha hoon.");
                    SaveProfile();
                }
                else
                {
                    Profile = cloudProfile;
                    CacheLocally(Profile);
                    LocalSaveManager.SaveProfile(Profile);
                }

                SyncCoinsToGameManager();
                if (!string.IsNullOrEmpty(Profile.avatarUrl))
                    StartCoroutine(DownloadAvatarCoroutine(Profile.avatarUrl));
                OnProfileLoaded?.Invoke();
                done = true;
            });
            yield return new WaitUntil(() => done);        
        }

        // ── SAVE ─────────────────────────────────────────────
        public void SaveProfile()
        {
            if (Profile == null) return;
            if (_pendingSave != null) StopCoroutine(_pendingSave);
            _pendingSave = StartCoroutine(SaveDebounced(0.5f));
        }

        private IEnumerator SaveDebounced(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (_isSaving) yield break;
            _isSaving = true;

            if (GameManager.Instance != null) Profile.coins = GameManager.Instance.Coins;
            Profile.lastUpdated = DateTime.UtcNow.ToString("o");

            bool done = false;
            _db.Collection(COLLECTION).Document(Profile.uid)
               .SetAsync(Profile.ToFirestoreDict(), SetOptions.MergeAll)
               .ContinueWithOnMainThread(t =>
               {
                   _isSaving = false;
                   if (!t.IsFaulted) { CacheLocally(Profile); OnProfileSaved?.Invoke(); }
                   done = true;
               });
            yield return new WaitUntil(() => done);
        }

        public void UpdateField(string field, object value)
        {
            if (Profile == null || _db == null) return;

            // uid empty hone par AuthManager se fill karo
            if (string.IsNullOrEmpty(Profile.uid))
            {
                var user = Game.Firebase.AuthManager.CurrentUser;
                if (user != null)
                {
                    Profile.uid = user.UserId;
                    Debug.LogWarning("[ProfileManager] uid was empty — filled from AuthManager.");
                }
                else
                {
                    Debug.LogError("[ProfileManager] UpdateField: uid is empty and AuthManager has no user. Skipping.");
                    return;
                }
            }

            _db.Collection(COLLECTION).Document(Profile.uid)
               .UpdateAsync(field, value)
               .ContinueWithOnMainThread(t => {
                   if (t.IsFaulted)
                       Debug.LogError($"[ProfileManager] UpdateField '{field}' failed: {t.Exception?.GetBaseException()?.Message}");
               });
        }

        // ── DISPLAY NAME ─────────────────────────────────────
        public void UpdateDisplayName(string newName)
        {
            if (Profile == null) return;
            newName = newName.Trim();
            if (newName.Length < 3 || newName.Length > 20) { OnProfileError?.Invoke("Name must be 3-20 chars."); return; }
            Profile.displayName = newName;
            UpdateField("displayName", newName);
            CacheLocally(Profile);
        }

        // ── AVATAR ───────────────────────────────────────────
        public void UploadAvatar(Texture2D tex)
        {
            if (_storage == null || Profile == null || tex == null) return;
            StartCoroutine(UploadAvatarCoroutine(tex));
        }

        private IEnumerator UploadAvatarCoroutine(Texture2D tex)
        {
            var path = _storage.RootReference.Child(STORAGE_AVATARS + Profile.uid + ".jpg");
            bool done = false;
            path.PutBytesAsync(tex.EncodeToJPG(75)).ContinueWithOnMainThread(upload =>
            {
                if (upload.IsFaulted) { done = true; return; }
                path.GetDownloadUrlAsync().ContinueWithOnMainThread(url =>
                {
                    if (!url.IsFaulted)
                    {
                        Profile.avatarUrl = url.Result.ToString();
                        UpdateField("avatarUrl", Profile.avatarUrl);
                        CacheLocally(Profile);
                        OnAvatarLoaded?.Invoke(TexToSprite(tex));
                    }
                    done = true;
                });
            });
            yield return new WaitUntil(() => done);
        }

        private IEnumerator DownloadAvatarCoroutine(string url)
        {
            using var req = UnityWebRequestTexture.GetTexture(url);
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) yield break;
            OnAvatarLoaded?.Invoke(TexToSprite(DownloadHandlerTexture.GetContent(req)));
        }

        // ── GAME EVENTS ──────────────────────────────────────

        /// <summary>Call after every level completion from LevelResultManager.</summary>
        public void OnLevelCompleted(int levelIndex, int stars, int score, int coinsEarned)
        {
            if (Profile == null) return;
            Profile.SetStars(levelIndex, stars);
            Profile.totalScore     += score;
            Profile.coins          += coinsEarned;
            Profile.levelsCompleted = Mathf.Max(Profile.levelsCompleted, levelIndex);
            Profile.level           = Profile.levelsCompleted + 1;

            int newTheme = Profile.levelsCompleted / 5;
            if (newTheme != Profile.currentThemeIndex)
            {
                Profile.currentThemeIndex = newTheme;
            }

            // ── FIXED: pass Profile.levelsCompleted directly (fresh, just-updated
            //    value) instead of letting GetUnlockedPetIds() fall back to the
            //    possibly one-level-stale LocalSaveManager cache. ──
            var petManager = Match3.PetManager.GetOrCreateInstance();
            foreach (string petId in petManager.GetUnlockedPetIds(Profile.levelsCompleted))
                if (!Profile.pets.Contains(petId))
                    OnPetUnlocked(petId);

            if (GameManager.Instance != null) GameManager.Instance.AddCoins(coinsEarned);
            SaveProfile();
            LocalSaveManager.SaveProfile(Profile);
        }
        /// <summary>Call when Boss Arena is won from BossArenaManager.</summary>
        public void OnBossDefeated(int bossIndex, int rewardCoins)
        {
            if (Profile == null) return;
            Profile.highestBossDefeated = Mathf.Max(Profile.highestBossDefeated, bossIndex);
            Profile.coins += rewardCoins;
            if (GameManager.Instance != null) GameManager.Instance.AddCoins(rewardCoins);
            SaveProfile();
        }

        /// <summary>Call when pet unlocked (every 3 levels) from ThemeManager.</summary>
        public void OnPetUnlocked(string petName)
        {
            if (Profile == null) return;
            Profile.AddPet(petName);
            UpdateField("unlockedPets", Profile.unlockedPets);
            CacheLocally(Profile);
        }

        // ── STORE ────────────────────────────────────────────
        public void AddBooster(string boosterId)
        {
            if (Profile == null) return;
            Profile.AddBooster(boosterId);
            UpdateField("boosters", Profile.boosters);
            CacheLocally(Profile);
        }

        public bool UseBooster(string boosterId)
        {
            if (Profile == null || !Profile.HasBooster(boosterId)) return false;
            Profile.UseBooster(boosterId);
            UpdateField("boosters", Profile.boosters);
            CacheLocally(Profile);
            return true;
        }

        // ── SETTINGS ─────────────────────────────────────────
        public void SaveSettings(bool sound, bool music, bool vibration)
        {
            if (Profile == null) return;
            Profile.soundEnabled = sound; Profile.musicEnabled = music; Profile.vibrationEnabled = vibration;
            _db?.Collection(COLLECTION).Document(Profile.uid).UpdateAsync(new Dictionary<string, object>
                { {"soundEnabled",sound}, {"musicEnabled",music}, {"vibrationEnabled",vibration} });
            CacheLocally(Profile);
        }

        // ── LOCAL CACHE ──────────────────────────────────────
        
        private void CacheLocally(PlayerProfile p) => LocalSaveManager.SaveProfile(p);
        
        private bool IsNewer(string aIso, string bIso)
        {
            bool aOk = DateTime.TryParse(aIso, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime a);
            bool bOk = DateTime.TryParse(bIso, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime b);
            if (!aOk) return false;
            if (!bOk) return true;
            return a > b;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) FlushSaveImmediately();
        }

        private void OnApplicationQuit()
        {
            FlushSaveImmediately();
        }

        private void FlushSaveImmediately()
        {
            if (Profile == null || _db == null) return;
            if (_pendingSave != null) { StopCoroutine(_pendingSave); _pendingSave = null; }

            LocalSaveManager.SaveProfile(Profile);

            if (GameManager.Instance != null) Profile.coins = GameManager.Instance.Coins;
            Profile.lastUpdated = DateTime.UtcNow.ToString("o");

            _db.Collection(COLLECTION).Document(Profile.uid)
            .SetAsync(Profile.ToFirestoreDict(), SetOptions.MergeAll);
        }
        private PlayerProfile LoadFromCache()       => LocalSaveManager.LoadProfile();

        // ── SYNC HELPERS ─────────────────────────────────────
        private void SyncCoinsToGameManager()
        {
            if (GameManager.Instance == null || Profile == null) return;
            int diff = Profile.coins - GameManager.Instance.Coins;
            if (diff > 0) GameManager.Instance.AddCoins(diff);
        }


        private static Sprite TexToSprite(Texture2D tex) =>
            Sprite.Create(tex, new UnityEngine.Rect(0,0,tex.width,tex.height), new UnityEngine.Vector2(0.5f,0.5f));
    }
}