using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Match3.Theme
{
    public class ThemeManager : MonoBehaviour
    {
        public static ThemeManager Instance { get; private set; }

        [Header("Assign in progression order: index 0 = Level 1-5, index 1 = Level 6-10, ...")]
        [SerializeField] private ThemeData[] themes;

        [Header("Levels required per theme")]
        [SerializeField] private int levelsPerTheme = 5;

        public ThemeData CurrentTheme { get; private set; }
        public int CurrentThemeIndex { get; private set; } = -1;

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

            if (GetComponent<ThemeAmbientFX>() == null)
                gameObject.AddComponent<ThemeAmbientFX>();

            if (themes == null || themes.Length == 0)
                Debug.LogWarning("[ThemeManager] No ThemeData assigned in Inspector.");

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
            ApplyToCurrentScene();
        }

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

            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var m in behaviours)
            {
                if (m is IThemeApplier applier)
                    applier.Apply(CurrentTheme);
            }
        }
    }

    public interface IThemeApplier
    {
        void Apply(ThemeData theme);
    }
}
