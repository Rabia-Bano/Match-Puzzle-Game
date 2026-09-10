using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Loads scenes asynchronously with a visible progress bar, and
/// automatically maps every GameState to its matching scene name.
///
/// Lives only in the Preloader scene, marked DontDestroyOnLoad —
/// stays alive and listening for the whole app session.
///
/// UPDATED: added SCENE_BOSS_GAME_BOARD ("BossGameBoardScene") mapped
/// to the NEW GameState.BossGameplay — this is the actual boss fight
/// scene. SCENE_BOSS_ARENA ("BossArenaScene") stays mapped to
/// GameState.BossArena — that's the boss SELECTION list screen only.
/// </summary>
public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [Header("Loading Screen UI")]
    [Tooltip("Full-screen canvas shown while a scene loads.")]
    public GameObject loadingScreenCanvas;

    [Tooltip("Progress bar slider, range 0 to 1.")]
    public Slider progressBar;

    [Tooltip("Minimum seconds to keep the loading screen visible.")]

    // Scene names — MUST match Build Settings exactly (case-sensitive).
    private const string SCENE_Login          = "LoginScene";
    private const string SCENE_MAP           = "MapScene";
    private const string SCENE_GAME_BOARD    = "GameBoardScene";
    private const string SCENE_BOSS_ARENA    = "BossArenaScene";       // boss SELECTION list
    private const string SCENE_BOSS_GAME_BOARD = "BossGameBoardScene"; // NEW — boss FIGHT scene
    private const string SCENE_STORE         = "StoreScene";
    private const string SCENE_LEADERBOARD   = "LeaderBoardScene";
    private const string SCENE_SETTINGS      = "SettingScene";
    private const string SCENE_PET_COMPANION = "PetCompanionScene";

    private bool _isLoading = false;

    /// <summary>
    /// Emergency reset — call this if buttons stop working.
    /// Automatically called after 10 seconds if loading gets stuck.
    /// </summary>
    public void ForceResetLoading()
    {
        if (_isLoading)
        {
            Debug.LogWarning("[SceneLoader] _isLoading was stuck TRUE — force resetting!");
            _isLoading = false;
            if (loadingScreenCanvas != null) loadingScreenCanvas.SetActive(false);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Subscribe here — persists for the entire app session
        GameEvents.OnGameStateChanged += HandleStateChanged;

        if (loadingScreenCanvas != null)
            loadingScreenCanvas.SetActive(false);
    }

    // OnEnable/OnDisable unsafe for DontDestroyOnLoad — Unity can call
    // OnDisable mid-scene-transition and silently drop the subscription.
    // Subscribe once in Awake, remove only in OnDestroy.
    private void OnDestroy() => GameEvents.OnGameStateChanged -= HandleStateChanged;

    /// <summary>
    /// Called automatically every time GameManager.ChangeState() runs.
    /// Looks up the scene for the new state and loads it — unless
    /// we're already in that scene, or the state has no scene
    /// (Loading is handled by Preloader itself; Paused has no scene).
    /// </summary>
    private void HandleStateChanged(GameState newState)
    {
        string targetScene = GetSceneForState(newState);
        if (string.IsNullOrEmpty(targetScene)) return;
        if (SceneManager.GetActiveScene().name == targetScene) return;

        // Safety: agar _isLoading galti se stuck tha to reset karo
        if (_isLoading)
        {
            Debug.LogWarning($"[SceneLoader] _isLoading was true when trying to load {targetScene} — resetting.");
            _isLoading = false;
        }

        LoadScene(targetScene, newState);
    }

    /// <summary>One-to-one mapping of GameState → scene name.</summary>
    private string GetSceneForState(GameState state)
    {
        switch (state)
        {
            case GameState.Login:        return SCENE_Login;
            case GameState.Map:          return SCENE_MAP;
            case GameState.Playing:      return SCENE_GAME_BOARD;
            case GameState.BossArena:    return SCENE_BOSS_ARENA;       // selection list
            case GameState.BossGameplay: return SCENE_BOSS_GAME_BOARD; // NEW — the fight itself
            case GameState.Store:        return SCENE_STORE;
            case GameState.Leaderboard:  return SCENE_LEADERBOARD;
            case GameState.Settings:     return SCENE_SETTINGS;
            case GameState.PetCompanion: return SCENE_PET_COMPANION;
            default:                     return null; // Loading, Paused
        }
    }

    /// <summary>
    /// Preferred way to navigate: just change state, SceneLoader
    /// does the rest. e.g. SceneLoader.Instance.LoadByState(GameState.Store);
    /// </summary>
    public void LoadByState(GameState targetState)
    {
        GameManager.Instance.ChangeState(targetState);
    }

    /// <summary>
    /// BUG FIX: Replay / Next Level call this. GameManager.ChangeState(Playing)
    /// silently no-ops when we're already in GameState.Playing (its own state ==
    /// state guard), and HandleStateChanged() ALSO skips loading when we're
    /// already in "GameBoardScene" — so neither path reloads the scene when the
    /// player replays or advances from inside gameplay. This bypasses both
    /// guards and always forces GameBoardScene to reload with whatever new
    /// LevelData LevelSession now holds.
    /// </summary>
    public void ReloadGameBoardScene()
    {
        LoadScene(SCENE_GAME_BOARD, GameState.Playing);
    }

    /// <summary>
    /// Same bypass as ReloadGameBoardScene(), for the boss SELECTION list
    /// screen (e.g. force-refreshing BossArenaScene after a boss is defeated
    /// and a new one unlocks, if you ever need it). Most flows won't need this.
    /// </summary>
    public void ReloadBossArenaScene()
    {
        LoadScene(SCENE_BOSS_ARENA, GameState.BossArena);
    }

    /// <summary>
    /// NEW — same bypass, for BossResultManager's Retry button: forces
    /// BossGameBoardScene to reload even though we're already in it
    /// (GameManager.ChangeState(BossGameplay) would no-op since the state
    /// didn't change).
    /// </summary>
    public void ReloadBossGameBoardScene()
    {
        LoadScene(SCENE_BOSS_GAME_BOARD, GameState.BossGameplay);
    }

    /// <summary>Directly starts an async load by scene name.</summary>
    public void LoadScene(string sceneName, GameState targetState)
    {
        if (_isLoading) return;
        StartCoroutine(LoadSceneAsync(sceneName, targetState));
    }

    private IEnumerator LoadSceneAsync(string sceneName, GameState targetState)
    {
        _isLoading = true;

        if (loadingScreenCanvas != null) loadingScreenCanvas.SetActive(true);
        if (progressBar != null)         progressBar.value = 0f;
        GameEvents.OnSceneLoadProgress?.Invoke(0f);

        // If scene name is invalid, LoadSceneAsync returns null — catch it early
        AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneName);
        if (asyncOp == null)
        {
            Debug.LogError($"[SceneLoader] Scene not found: '{sceneName}' — Check Build Settings!");
            _isLoading = false;
            yield break;
        }

        asyncOp.allowSceneActivation = true;

        while (!asyncOp.isDone)
        {
            float progress = Mathf.Clamp01(asyncOp.progress / 0.9f);
            if (progressBar != null) progressBar.value = progress;
            GameEvents.OnSceneLoadProgress?.Invoke(progress);
            yield return null;
        }

        if (progressBar != null) progressBar.value = 1f;
        Debug.Log($"[SceneLoader] '{sceneName}' loaded for state: {targetState}");

        if (loadingScreenCanvas != null) loadingScreenCanvas.SetActive(false);

        // Always reset — even if scene load had issues
        _isLoading = false;

        GameEvents.OnSceneLoaded?.Invoke(targetState);
    }
}