public static class GameEvents
{
    public static System.Action<GameState> OnGameStateChanged;

    public static System.Action<GameState> OnSceneLoaded;

    public static System.Action<float> OnSceneLoadProgress;

    public static System.Action<int> OnLevelCompleted;

    public static System.Action<int> OnLevelFailed;

    public static System.Action<int> OnCoinsChanged;

    public static System.Action OnGamePaused;

    public static System.Action OnGameResumed;

    public static System.Action OnReturnToMap;

    public static System.Action OnBossDefeated;

    public static System.Action OnPlayerLoggedIn;

    public static System.Action OnPlayerLoggedOut;

    public static System.Action OnNoLivesBlocked;

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
