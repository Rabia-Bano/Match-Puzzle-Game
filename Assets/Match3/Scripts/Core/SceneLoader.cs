using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [Header("Loading Screen UI")]
    [Tooltip("Full-screen canvas shown while a scene loads.")]
    public GameObject loadingScreenCanvas;

    [Tooltip("Progress bar slider, range 0 to 1.")]
    public Slider progressBar;

    [Tooltip("Minimum seconds to keep the loading screen visible.")]

    private const string SCENE_Login          = "LoginScene";
    private const string SCENE_MAP           = "MapScene";
    private const string SCENE_GAME_BOARD    = "GameBoardScene";
    private const string SCENE_BOSS_ARENA    = "BossArenaScene";
    private const string SCENE_BOSS_GAME_BOARD = "BossGameBoardScene";
    private const string SCENE_STORE         = "StoreScene";
    private const string SCENE_LEADERBOARD   = "LeaderBoardScene";
    private const string SCENE_SETTINGS      = "SettingScene";
    private const string SCENE_PET_COMPANION = "PetCompanionScene";

    private bool _isLoading = false;

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

        GameEvents.OnGameStateChanged += HandleStateChanged;

        if (loadingScreenCanvas != null)
            loadingScreenCanvas.SetActive(false);
    }

    private void OnDestroy() => GameEvents.OnGameStateChanged -= HandleStateChanged;

    private void HandleStateChanged(GameState newState)
    {
        string targetScene = GetSceneForState(newState);
        if (string.IsNullOrEmpty(targetScene)) return;
        if (SceneManager.GetActiveScene().name == targetScene) return;

        if (_isLoading)
        {
            Debug.LogWarning($"[SceneLoader] _isLoading was true when trying to load {targetScene} — resetting.");
            _isLoading = false;
        }

        LoadScene(targetScene, newState);
    }

    private string GetSceneForState(GameState state)
    {
        switch (state)
        {
            case GameState.Login:        return SCENE_Login;
            case GameState.Map:          return SCENE_MAP;
            case GameState.Playing:      return SCENE_GAME_BOARD;
            case GameState.BossArena:    return SCENE_BOSS_ARENA;
            case GameState.BossGameplay: return SCENE_BOSS_GAME_BOARD;
            case GameState.Store:        return SCENE_STORE;
            case GameState.Leaderboard:  return SCENE_LEADERBOARD;
            case GameState.Settings:     return SCENE_SETTINGS;
            case GameState.PetCompanion: return SCENE_PET_COMPANION;
            default:                     return null;
        }
    }

    public void LoadByState(GameState targetState)
    {
        GameManager.Instance.ChangeState(targetState);
    }

    public void ReloadGameBoardScene()
    {
        LoadScene(SCENE_GAME_BOARD, GameState.Playing);
    }

    public void ReloadBossArenaScene()
    {
        LoadScene(SCENE_BOSS_ARENA, GameState.BossArena);
    }

    public void ReloadBossGameBoardScene()
    {
        LoadScene(SCENE_BOSS_GAME_BOARD, GameState.BossGameplay);
    }

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

        _isLoading = false;

        GameEvents.OnSceneLoaded?.Invoke(targetState);
    }
}
