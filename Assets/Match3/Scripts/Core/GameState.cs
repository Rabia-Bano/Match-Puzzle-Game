/// <summary>
/// Every possible state the game can be in.
/// One state = one scene (except Paused, which is an overlay on Playing).
/// GameManager holds the current value. SceneLoader and UIManager
/// both react to it automatically whenever it changes.
///
/// UPDATED: BossArena and BossGameplay are now TWO separate states,
/// mirroring how Map (level selection) and Playing (level gameplay)
/// are already two separate states for regular levels:
///
///   Map          (level selection)  ↔  Playing      (level gameplay)
///   BossArena    (boss selection)   ↔  BossGameplay (boss fight)   [NEW]
///
/// BossArena = the "Puzzle Boss Arena" list screen (numbered boss
/// circles, locked/unlocked — reached from the bottom nav bar icon).
/// BossGameplay = the actual boss board/fight scene, reached by
/// tapping an unlocked boss number in that list (or by tapping an
/// inline Boss node directly on the main Map path).
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

    /// <summary>Boss Arena selection screen — list of boss fights (numbered, locked/unlocked).</summary>
    BossArena,

    /// <summary>NEW — the actual Boss Arena fight/board scene (reached from BossArena's list, or an inline Boss node on the Map).</summary>
    BossGameplay,

    /// <summary>In-game store.</summary>
    Store,

    /// <summary>Leaderboard screen.</summary>
    Leaderboard,

    /// <summary>Settings screen (Sound/Music/Vibration).</summary>
    Settings,

    /// <summary>Pet Companion collection screen.</summary>
    PetCompanion
}