using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Match3.Theme
{
    /// <summary>
    /// DontDestroyOnLoad singleton. Lives in Preloader scene alongside GameManager,
    /// FirebaseManager, ProfileManager etc. Decides which ThemeData is active based on
    /// the player's highest completed level, and applies it to every scene automatically
    /// on load (same rebind pattern used by PetManager/BoosterManager BindToLevel()).
    /// </summary>
    public class ThemeManager : MonoBehaviour
    {
        public static ThemeManager Instance { get; private set; }

        [Header("Assign in progression order: index 0 = Level 1-5, index 1 = Level 6-10, ...")]
        [SerializeField] private ThemeData[] themes;

        [Header("Levels required per theme")]
        [SerializeField] private int levelsPerTheme = 5;

        public ThemeData CurrentTheme { get; private set; }
        public int CurrentThemeIndex { get; private set; } = -1;

        /// <summary>Fired whenever the active theme changes, so any listener (like ThemedPrefabPiece
        /// on dynamically spawned prefabs) can re-apply itself.</summary>
        public event Action<ThemeData> OnThemeChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Warn early if the array is empty so Rabia doesn't debug a silent no-op later.
            if (themes == null || themes.Length == 0)
                Debug.LogWarning("[ThemeManager] No ThemeData assigned in Inspector.");

            // Apply immediately here, NOT in Start(). Unity guarantees every object's Awake()
            // finishes (across the whole scene) before ANY object's Start() runs, so doing it
            // here means PreloaderController/SceneLoader can't have transitioned to the next
            // scene yet (that logic lives in their Start()/coroutines). Doing this in Start()
            // instead created a race: if the scene changed before ThemeManager.Start() fired,
            // Preloader's own Background would never get themed at all.
            //
            // We also don't default to theme 0 here - Firebase profile load takes time (network),
            // and Preloader/Login are shown before that finishes. LocalSaveManager reads the
            // last-saved PlayerPrefs profile synchronously (no network), so a returning player
            // sees their actual unlocked theme immediately, even on Preloader. ProfileManager's
            // SyncTheme() will correct this later once the real cloud profile loads, in case
            // local cache is stale (e.g. progress made on another device).
            if (themes != null && themes.Length > 0)
            {
                int cachedLevels = LocalSaveManager.GetOrLoadProfile()?.levelsCompleted ?? 0;
                SetHighestLevelCompleted(cachedLevels);
            }
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // By the time any scene-load event fires, Awake() has already set an
            // initial theme (from local cache), so just re-apply it to the new scene.
            ApplyToCurrentScene();
        }

        /// <summary>
        /// Call this once right after profile/local-save data is loaded (Preloader / ProfileManager),
        /// and again immediately after every level win (LevelResultManager), so the theme
        /// always matches actual progress even after app restarts.
        /// </summary>
        public void SetHighestLevelCompleted(int highestLevelCompleted)
        {
            if (themes == null || themes.Length == 0) return;

            int index = highestLevelCompleted / levelsPerTheme;
            index = Mathf.Clamp(index, 0, themes.Length - 1);
            SetThemeByIndex(index);
        }

        public void SetThemeByIndex(int index)
        {
            if (themes == null || index < 0 || index >= themes.Length) return;

            bool changed = index != CurrentThemeIndex || CurrentTheme == null;
            CurrentThemeIndex = index;
            CurrentTheme = themes[index];

            if (changed)
                OnThemeChanged?.Invoke(CurrentTheme);

            ApplyToCurrentScene();
        }

        private void ApplyToCurrentScene()
        {
            if (CurrentTheme == null) return;

            // Route through the shared IThemeApplier pipeline instead of each scene
            // having its own separate apply logic - same "centralized pipeline" pattern
            // used for special-tile clears.
            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var m in behaviours)
            {
                if (m is IThemeApplier applier)
                    applier.Apply(CurrentTheme);
            }
        }
    }

    /// <summary>Implemented by every per-scene ThemeApplier component.</summary>
    public interface IThemeApplier
    {
        void Apply(ThemeData theme);
    }
}