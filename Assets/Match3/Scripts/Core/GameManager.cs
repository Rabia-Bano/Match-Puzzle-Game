using UnityEngine;
using Game.Firebase;   // NEW — CloudSyncManager ke liye

/// <summary>
/// Global singleton that owns the current GameState and a handful
/// of values that must survive scene changes (coins, current level).
///
/// This script ONLY exists in the Preloader scene and is marked
/// DontDestroyOnLoad — so the same instance is alive for the
/// entire app session, across every scene.
///
/// IMPORTANT: This script does NOT touch the match-3 board at all.
/// Board logic (BoardGrid, GoalTracker, MoveCounter, Score, etc.)
/// lives in LevelManager.cs — a separate, scene-local script inside
/// GameBoardScene. LevelManager fires GameEvents.OnLevelCompleted /
/// OnLevelFailed when a level ends; GameManager reacts to those by
/// switching state back to Map. That is the ONLY connection between
/// the two scripts — no direct references either way.
/// </summary>
public class GameManager : MonoBehaviour
{
    /// <summary>Global access point: GameManager.Instance.ChangeState(...)</summary>
    public static GameManager Instance { get; private set; }

    /// <summary>The state the game is currently in.</summary>
    public GameState CurrentState { get; private set; } = GameState.Loading;

    /// <summary>The state before the current one — used to "go back" from Pause.</summary>
    public GameState PreviousState { get; private set; } = GameState.Loading;

    /// <summary>Currently selected level number (set from MapScreen).</summary>
    public int CurrentLevel { get; private set; } = 1;

    /// <summary>Player's coin balance — persists across scenes for this session.</summary>
    public int Coins { get; private set; } = 0;

    private void Awake()
    {
        // Enforce exactly one instance, ever.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Debug.Log("[GameManager] Initialized — persists across all scenes.");
    }

    private void OnEnable()
    {
        GameEvents.OnLevelCompleted  += HandleLevelCompleted;
        GameEvents.OnLevelFailed     += HandleLevelFailed;
        //GameEvents.OnBossDefeated    += HandleBossDefeated;
        GameEvents.OnReturnToMap     += HandleReturnToMap;
        GameEvents.OnGamePaused      += HandlePause;
        GameEvents.OnGameResumed     += HandleResume;
        GameEvents.OnPlayerLoggedIn  += HandlePlayerLoggedIn;
        GameEvents.OnPlayerLoggedOut += HandlePlayerLoggedOut;
        GameEvents.OnCoinsChanged    += HandleCoinsChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnLevelCompleted  -= HandleLevelCompleted;
        GameEvents.OnLevelFailed     -= HandleLevelFailed;
        //GameEvents.OnBossDefeated    -= HandleBossDefeated;
        GameEvents.OnReturnToMap     -= HandleReturnToMap;
        GameEvents.OnGamePaused      -= HandlePause;
        GameEvents.OnGameResumed     -= HandleResume;
        GameEvents.OnPlayerLoggedIn  -= HandlePlayerLoggedIn;
        GameEvents.OnPlayerLoggedOut -= HandlePlayerLoggedOut;
        GameEvents.OnCoinsChanged    -= HandleCoinsChanged;
    }

    private void OnApplicationQuit() => GameEvents.ClearAllEvents();

    // ─────────────────────────────────────────────────────────
    // STATE MACHINE — the one method that drives everything
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Changes the current state and broadcasts it. SceneLoader
    /// reacts by loading the matching scene; UIManager reacts by
    /// showing the matching panel. Nothing else needs to be called.
    /// </summary>
    public void ChangeState(GameState newState)
    {
        if (newState == CurrentState) return;

        PreviousState = CurrentState;
        CurrentState  = newState;

        Debug.Log($"[GameManager] State: {PreviousState} → {CurrentState}");

        // Freeze gameplay time while paused; resume otherwise.
        Time.timeScale = (newState == GameState.Paused) ? 0f : 1f;

        GameEvents.OnGameStateChanged?.Invoke(CurrentState);
    }

    // ─────────────────────────────────────────────────────────
    // PUBLIC HELPERS — called by UI buttons / other systems
    // ─────────────────────────────────────────────────────────

    /// <summary>Called from MapScreen when the player taps a level node.</summary>
    public void SetLevel(int levelIndex)
    {
        CurrentLevel = levelIndex;
        Debug.Log($"[GameManager] Level set to {levelIndex}");
    }

    /// <summary>Adds coins and broadcasts the new total.</summary>
    public void AddCoins(int amount)
    {
        Coins += amount;
        GameEvents.OnCoinsChanged?.Invoke(Coins);
    }

    /// <summary>Spends coins. Returns false (and does nothing) if balance is too low.</summary>
    public bool SpendCoins(int amount)
    {
        if (Coins < amount)
        {
            Debug.LogWarning("[GameManager] Not enough coins.");
            return false;
        }
        Coins -= amount;
        GameEvents.OnCoinsChanged?.Invoke(Coins);
        return true;
    }

    // ─────────────────────────────────────────────────────────
    // EVENT HANDLERS — react to gameplay events by changing state
    // ─────────────────────────────────────────────────────────

    private void HandleLevelCompleted(int level)
    {
        Debug.Log($"[GameManager] Level {level} completed.");
        ChangeState(GameState.Map);
    }

    private void HandleLevelFailed(int level)
    {
        Debug.Log($"[GameManager] Level {level} failed.");
        ChangeState(GameState.Map);
    }

    private void HandleBossDefeated()  => ChangeState(GameState.Map);
    private void HandleReturnToMap()   => ChangeState(GameState.Map);

    private void HandlePause()
    {
        if (CurrentState == GameState.Playing)
            ChangeState(GameState.Paused);
    }

    private void HandleResume()
    {
        if (CurrentState == GameState.Paused)
            ChangeState(GameState.Playing);
    }

    private void HandlePlayerLoggedIn()
    {
        // NEW — login/register hote hi hybrid save sync trigger karo.
        // Fire-and-forget: `_ =` isliye taake ChangeState() turant chale,
        // network sync background mein hoti rahe aur Map screen block na ho.
        _ = CloudSyncManager.Instance?.SyncOnSessionStartAsync();
        ChangeState(GameState.Map);
    }
    private void HandlePlayerLoggedOut() => ChangeState(GameState.Login);

    private void HandleCoinsChanged(int newAmount) => Coins = newAmount;
}