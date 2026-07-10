/// <summary>
/// Every possible state the game can be in.
/// One state = one scene (except Paused, which is an overlay on Playing).
/// GameManager holds the current value. SceneLoader and UIManager
/// both react to it automatically whenever it changes.
/// </summary>
public enum GameState
{
    /// <summary>App just opened — Firebase initializing, assets loading.</summary>
    Loading,

    /// <summary>Login / Register screen.</summary>
    Login,

    /// <summary>Level map — player picks a level.</summary>
    Map,

    /// <summary>Active match-3 gameplay.</summary>
    Playing,

    /// <summary>Game paused — overlay only, no scene change.</summary>
    Paused,

    /// <summary>Boss Arena mini-game.</summary>
    BossArena,

    /// <summary>In-game store.</summary>
    Store,

    /// <summary>Leaderboard screen.</summary>
    Leaderboard,

    /// <summary>Settings screen (Sound/Music/Vibration).</summary>
    Settings,

    /// <summary>Pet Companion collection screen.</summary>
    PetCompanion
}