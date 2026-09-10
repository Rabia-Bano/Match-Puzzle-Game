/// <summary>
/// Central event bus for the entire game. Every system fires or
/// listens to these static events instead of holding direct
/// references to each other — this keeps everything decoupled.
///
/// Usage:
///   Fire   : GameEvents.OnGameStateChanged?.Invoke(GameState.Playing);
///   Listen : GameEvents.OnGameStateChanged += MyHandler;   (in OnEnable)
///   Remove : GameEvents.OnGameStateChanged -= MyHandler;   (in OnDisable)
/// </summary>
public static class GameEvents
{
    /// <summary>Fired whenever GameState changes. Passes the new state.</summary>
    public static System.Action<GameState> OnGameStateChanged;

    /// <summary>Fired when a scene has fully finished loading. Passes the target GameState.</summary>
    public static System.Action<GameState> OnSceneLoaded;

    /// <summary>Fired repeatedly during async scene load (0.0 to 1.0).</summary>
    public static System.Action<float> OnSceneLoadProgress;

    /// <summary>Fired when the player completes a level. Passes level index.</summary>
    public static System.Action<int> OnLevelCompleted;

    /// <summary>Fired when the player fails a level. Passes level index.</summary>
    public static System.Action<int> OnLevelFailed;

    /// <summary>Fired when coins change. Passes new coin total.</summary>
    public static System.Action<int> OnCoinsChanged;

    /// <summary>Fired when the player wants to pause.</summary>
    public static System.Action OnGamePaused;

    /// <summary>Fired when the player resumes from pause.</summary>
    public static System.Action OnGameResumed;

    /// <summary>Fired when the player wants to return to the map.</summary>
    public static System.Action OnReturnToMap;

    /// <summary>NEW — fired by BossResultManager after a Boss Arena win (after the reward screen is dismissed).</summary>
    public static System.Action OnBossDefeated;

    /// <summary>Fired when the player successfully logs in.</summary>
    public static System.Action OnPlayerLoggedIn;

    /// <summary>Fired when the player logs out.</summary>
    public static System.Action OnPlayerLoggedOut;

    /// <summary>
    /// Fired when the player tries to start a REGULAR level with 0 lives
    /// remaining (LevelLoader.LoadLevel blocks the load and fires this instead).
    /// Boss Arena never fires this — it doesn't consume/require lives.
    /// </summary>
    public static System.Action OnNoLivesBlocked;

    /// <summary>
    /// Clears every subscriber. Call once, from GameManager.OnApplicationQuit(),
    /// so static events don't carry stale references between editor play sessions.
    /// </summary>
    public static void ClearAllEvents()
    {
        OnGameStateChanged  = null;
        OnSceneLoaded       = null;
        OnSceneLoadProgress = null;
        OnLevelCompleted    = null;
        OnLevelFailed       = null;
        OnCoinsChanged      = null;
        OnGamePaused        = null;
        OnGameResumed       = null;
        OnReturnToMap       = null;
        OnBossDefeated      = null;
        OnPlayerLoggedIn    = null;
        OnPlayerLoggedOut   = null;
        OnNoLivesBlocked    = null;
    }
}