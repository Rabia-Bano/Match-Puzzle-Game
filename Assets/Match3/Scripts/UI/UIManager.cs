// ============================================================
//  UIManager.cs  —  FINAL
//
//  KEY FIX: HandleStateChanged() ab sirf mainPanel ko
//  SHOW karta hai jab state match ho — lekin kabhi bhi
//  mainPanel ko HIDE nahi karta GameBoardScene mein.
//  
//  GameBoardScene mein GameCanvas hamesha visible rehna chahiye.
//  GoalPanel/Start, WinPanel, LosePanel — yeh LevelResultManager
//  handle karta hai, UIManager nahi.
//
//  mainPanelState = Playing set karo GameBoardScene ke UIManager mein.
// ============================================================

using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Main Panel For This Scene")]
    public GameObject mainPanel;

    [Header("Pause Overlay (only needed in GameBoardScene)")]
    public GameObject pausePanel;

    [Header("Popups (only needed in GameBoardScene)")]
    public GameObject levelCompletePopup;
    public GameObject levelFailedPopup;

    [Header("No Lives Popup (needed in MapScene — shown when a regular level is tapped with 0 lives)")]
    public GameObject noLivesPopup;

    [Header("Which state should show mainPanel")]
    public GameState mainPanelState;

    [Header("Settings")]
    [Tooltip("ON karo GameBoardScene mein — mainPanel kabhi hide nahi hoga")]
    public bool alwaysShowMainPanel = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        GameEvents.OnGameStateChanged += HandleStateChanged;
        GameEvents.OnLevelCompleted   += HandleLevelCompleted;
        GameEvents.OnLevelFailed      += HandleLevelFailed;
        GameEvents.OnNoLivesBlocked   += HandleNoLivesBlocked;
    }

    private void OnDisable()
    {
        GameEvents.OnGameStateChanged -= HandleStateChanged;
        GameEvents.OnLevelCompleted   -= HandleLevelCompleted;
        GameEvents.OnLevelFailed      -= HandleLevelFailed;
        GameEvents.OnNoLivesBlocked   -= HandleNoLivesBlocked;
    }

    private void Start()
    {
        if (GameManager.Instance == null)
            Debug.LogWarning("[UIManager] GameManager.Instance is NULL — " +
                             "PreloaderScene se start karo.");
        else
            Debug.Log($"[UIManager] Scene: {gameObject.scene.name} | " +
                      $"State: {GameManager.Instance.CurrentState} | " +
                      $"PanelState: {mainPanelState}");

        if (SceneLoader.Instance == null)
            Debug.LogWarning("[UIManager] SceneLoader.Instance is NULL.");

        if (GameManager.Instance != null)
            HandleStateChanged(GameManager.Instance.CurrentState);
        else
            if (mainPanel != null) mainPanel.SetActive(true);
    }

    private void HandleStateChanged(GameState newState)
    {
        if (mainPanel == null) return;

        // ── KEY FIX ───────────────────────────────────────────
        // alwaysShowMainPanel = true hone par mainPanel hamesha
        // active rehta hai (GameBoardScene ke liye).
        // LevelResultManager khud GoalPanel/Start manage karta hai.
        if (alwaysShowMainPanel)
        {
            mainPanel.SetActive(true);
            return;
        }
        // ──────────────────────────────────────────────────────

        mainPanel.SetActive(
            newState == mainPanelState ||
            (newState == GameState.Paused && mainPanelState == GameState.Playing));

        if (pausePanel != null)
            pausePanel.SetActive(newState == GameState.Paused);
    }

    private void HandleLevelCompleted(int level)
    {
        if (levelCompletePopup != null) levelCompletePopup.SetActive(true);
    }

    private void HandleLevelFailed(int level)
    {
        if (levelFailedPopup != null) levelFailedPopup.SetActive(true);
    }

    private void HandleNoLivesBlocked()
    {
        if (noLivesPopup != null) noLivesPopup.SetActive(true);
    }

    public void OnOpenMapPressed()          => Navigate(GameState.Map);
    public void OnOpenPetCompanionPressed() => Navigate(GameState.PetCompanion);
    public void OnOpenBossArenaPressed()    => Navigate(GameState.BossArena);
    public void OnOpenStorePressed()        => Navigate(GameState.Store);
    public void OnOpenLeaderboardPressed()  => Navigate(GameState.Leaderboard);
    public void OnOpenSettingsPressed()     => Navigate(GameState.Settings);
    public void OnPauseButtonPressed()      => GameEvents.OnGamePaused?.Invoke();
    public void OnResumeButtonPressed()     => GameEvents.OnGameResumed?.Invoke();
    public void OnReturnToMapPressed()      => Navigate(GameState.Map);
    public void HidePopup(GameObject popup) { if (popup != null) popup.SetActive(false); }

    private void Navigate(GameState target)
    {
        if (GameManager.Instance == null)
        { Debug.LogError($"[UIManager] GameManager NULL"); return; }
        if (SceneLoader.Instance == null)
        { Debug.LogError($"[UIManager] SceneLoader NULL — PreloaderScene se start karo!"); return; }
        GameManager.Instance.ChangeState(target);
    }
}