using UnityEngine;
using Game.Firebase;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameState CurrentState { get; private set; } = GameState.Loading;

    public GameState PreviousState { get; private set; } = GameState.Loading;

    public int CurrentLevel { get; private set; } = 1;

    public int Coins { get; private set; } = 0;

    private void Awake()
    {
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
        GameEvents.OnBossDefeated    += HandleBossDefeated;
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
        GameEvents.OnBossDefeated    -= HandleBossDefeated;
        GameEvents.OnReturnToMap     -= HandleReturnToMap;
        GameEvents.OnGamePaused      -= HandlePause;
        GameEvents.OnGameResumed     -= HandleResume;
        GameEvents.OnPlayerLoggedIn  -= HandlePlayerLoggedIn;
        GameEvents.OnPlayerLoggedOut -= HandlePlayerLoggedOut;
        GameEvents.OnCoinsChanged    -= HandleCoinsChanged;
    }

    private void OnApplicationQuit() => GameEvents.ClearAllEvents();

    public void ChangeState(GameState newState)
    {
        if (newState == CurrentState) return;

        PreviousState = CurrentState;
        CurrentState  = newState;

        Debug.Log($"[GameManager] State: {PreviousState} → {CurrentState}");

        Time.timeScale = (newState == GameState.Paused) ? 0f : 1f;

        GameEvents.OnGameStateChanged?.Invoke(CurrentState);
    }

    public void SetLevel(int levelIndex)
    {
        CurrentLevel = levelIndex;
        Debug.Log($"[GameManager] Level set to {levelIndex}");
    }

    public void AddCoins(int amount)
    {
        Coins += amount;
        GameEvents.OnCoinsChanged?.Invoke(Coins);
    }

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

    private void HandleBossDefeated()
    {
        Debug.Log("[GameManager] Boss defeated (analytics/logging only — no auto-navigation).");
    }

    private void HandleReturnToMap()
    {
        LivesManager.Instance?.ClearLevelInProgress();
        ChangeState(GameState.Map);
    }

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
        _ = CloudSyncManager.Instance?.SyncOnSessionStartAsync();
        ChangeState(GameState.Map);
    }
    private void HandlePlayerLoggedOut() => ChangeState(GameState.Login);

    private void HandleCoinsChanged(int newAmount) => Coins = newAmount;
}
