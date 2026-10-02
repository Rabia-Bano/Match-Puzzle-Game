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

        public void LoadProfile(string uid) => StartCoroutine(LoadProfileCoroutine(uid));

        private void SyncUnlockedPets()
        {
            if (Profile == null) return;

            var petManager = Match3.PetManager.GetOrCreateInstance();
            bool anyNew = false;

            foreach (string petId in petManager.GetUnlockedPetIds(Profile.levelsCompleted))
            {
                if (!Profile.pets.Contains(petId))
                {
                    Profile.AddPet(petId);
                    anyNew = true;
                }
            }

            if (anyNew)
                CacheLocally(Profile);
        }

        private IEnumerator LoadProfileCoroutine(string uid)
        {
            var cached = LoadFromCache();
            if (cached != null && cached.uid == uid)
            {
                Profile = cached;
                SyncUnlockedPets();
                SyncTheme();
                OnProfileLoaded?.Invoke();
                SyncCoinsToGameManager();
                if (!string.IsNullOrEmpty(cached.avatarId))
                    ApplyPresetAvatar(cached.avatarId);
                else if (!string.IsNullOrEmpty(cached.avatarUrl))
                    StartCoroutine(DownloadAvatarCoroutine(cached.avatarUrl));
            }

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
                SyncTheme();
                if (!string.IsNullOrEmpty(Profile.avatarId))
                    ApplyPresetAvatar(Profile.avatarId);
                else if (!string.IsNullOrEmpty(Profile.avatarUrl))
                    StartCoroutine(DownloadAvatarCoroutine(Profile.avatarUrl));
                OnProfileLoaded?.Invoke();
                done = true;
            });
            yield return new WaitUntil(() => done);
        }

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

        public void UpdateDisplayName(string newName)
        {
            if (Profile == null) return;
            newName = newName.Trim();
            if (newName.Length < 3 || newName.Length > 20) { OnProfileError?.Invoke("Name must be 3-20 chars."); return; }
            Profile.displayName = newName;
            UpdateField("displayName", newName);
            CacheLocally(Profile);
        }

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
                        LeaderboardManager.Instance?.SyncAvatarToLeaderboard("", Profile.avatarUrl);
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

        public void SetPresetAvatar(string avatarId)
        {
            if (Profile == null || string.IsNullOrEmpty(avatarId)) return;

            var preset = Resources.Load<Match3.AvatarPresetData>("Avatars/" + avatarId);
            if (preset != null && !Match3.AvatarShopManager.IsOwned(preset))
            {
                Debug.LogWarning($"[ProfileManager] Avatar '{avatarId}' is not owned yet — buy it in the Avatar Shop first.");
                OnProfileError?.Invoke("Buy this avatar first!");
                return;
            }

            Profile.avatarId = avatarId;
            Profile.avatarUrl = "";

            UpdateField("avatarId", avatarId);
            UpdateField("avatarUrl", "");
            CacheLocally(Profile);

            LeaderboardManager.Instance?.SyncAvatarToLeaderboard(avatarId, "");

            ApplyPresetAvatar(avatarId);
        }

        private void ApplyPresetAvatar(string avatarId)
        {
            var preset = Resources.Load<Match3.AvatarPresetData>("Avatars/" + avatarId);
            if (preset != null && preset.sprite != null)
                OnAvatarLoaded?.Invoke(preset.sprite);
            else
                Debug.LogWarning($"[ProfileManager] Avatar preset '{avatarId}' not found under Resources/Avatars/.");
        }

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
            SyncTheme();

            var petManager = Match3.PetManager.GetOrCreateInstance();
            foreach (string petId in petManager.GetUnlockedPetIds(Profile.levelsCompleted))
                if (!Profile.pets.Contains(petId))
                    OnPetUnlocked(petId);

            if (GameManager.Instance != null) GameManager.Instance.AddCoins(coinsEarned);
            SaveProfile();
            LocalSaveManager.SaveProfile(Profile);
        }
        public void OnBossDefeated(int bossIndex, int rewardCoins)
        {
            if (Profile == null) return;
            Profile.highestBossDefeated = Mathf.Max(Profile.highestBossDefeated, bossIndex);
            Profile.coins += rewardCoins;
            if (GameManager.Instance != null) GameManager.Instance.AddCoins(rewardCoins);
            SaveProfile();
        }

        public void OnPetUnlocked(string petName)
        {
            if (Profile == null) return;
            Profile.AddPet(petName);
            UpdateField("unlockedPets", Profile.unlockedPets);
            CacheLocally(Profile);
        }

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

        public void SaveSettings(bool sound, bool music, bool vibration)
        {
            if (Profile == null) return;
            Profile.soundEnabled = sound; Profile.musicEnabled = music; Profile.vibrationEnabled = vibration;
            _db?.Collection(COLLECTION).Document(Profile.uid).UpdateAsync(new Dictionary<string, object>
                { {"soundEnabled",sound}, {"musicEnabled",music}, {"vibrationEnabled",vibration} });
            CacheLocally(Profile);
        }

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

        private void SyncCoinsToGameManager()
        {
            if (GameManager.Instance == null || Profile == null) return;
            int diff = Profile.coins - GameManager.Instance.Coins;
            if (diff > 0) GameManager.Instance.AddCoins(diff);
        }

        private void SyncTheme()
        {
            if (Profile == null) return;
            Match3.Theme.ThemeManager.Instance?.SetHighestLevelCompleted(Profile.levelsCompleted);
        }

        private static Sprite TexToSprite(Texture2D tex) =>
            Sprite.Create(tex, new UnityEngine.Rect(0,0,tex.width,tex.height), new UnityEngine.Vector2(0.5f,0.5f));
    }
}
