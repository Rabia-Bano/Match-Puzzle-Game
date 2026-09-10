// ============================================================
//  BossLevelLoader.cs  —  Static Helper Class
//
//  Purpose (mirrors LevelLoader.cs exactly, for boss fights instead
//  of regular levels):
//    1. Stores which boss this is (PlayerPrefs "SelectedBossId" —
//       BossController.LoadBossFromSelectedId() reads this)
//    2. Stores the shared boss-board LevelData in LevelSession
//       (LevelManager reads this the same way it does for a regular level)
//    3. Tells GameManager to switch to GameState.BossGameplay
//       (SceneLoader then loads BossGameBoardScene automatically)
//
//  Called from:
//    • BossLevelNode tap (via BossArenaListManager) — the boss
//      SELECTION list screen
//    • BossNodeController tap — the inline Boss marker on the main
//      Map path (skips the list, jumps straight into that boss's fight)
//
//  NO MonoBehaviour needed — pure static utility.
//  Place in: Assets/Scripts/Boss/
// ============================================================

using UnityEngine;

public static class BossLevelLoader
{
    /// <summary>
    /// Launches a specific boss fight. bossBoardLevelData is the shared
    /// LevelData asset that defines the fight board's layout (width, height,
    /// allowedTiles, hardTileData, dropStoneData) — assign the SAME asset
    /// on both the BossNode prefab (Map) and BossLevelNode prefab
    /// (BossArenaScene list) in the Inspector, see the setup guide.
    /// </summary>
    public static void LoadBoss(int bossId, Match3.LevelData bossBoardLevelData = null)
    {
        PlayerPrefs.SetInt("SelectedBossId", bossId);

        if (bossBoardLevelData != null)
        {
            Match3.LevelSession.CurrentLevel   = bossBoardLevelData;
            Match3.LevelSession.CurrentLevelId = 0; // 0 = "not a regular level" — boss id tracked via PlayerPrefs instead
            Match3.LevelSession.CurrentScore   = 0;
        }
        else
        {
            Debug.LogWarning("[BossLevelLoader] No bossBoardLevelData passed — " +
                              "make sure Match3.LevelSession.CurrentLevel was already set to the " +
                              "boss board LevelData by the caller, or BossGameBoardScene's " +
                              "LevelManager will have nothing to build the board from.");
        }

        if (GameManager.Instance == null)
        {
            Debug.LogError("[BossLevelLoader] GameManager.Instance is null!");
            return;
        }

        // Same "already in this state" bypass LevelLoader uses for Replay/Next Level —
        // if we're launching a second boss fight directly from BossGameBoardScene's
        // own result screen without passing through Map/BossArena first, ChangeState()
        // would no-op since GameState.BossGameplay hasn't changed.
        if (GameManager.Instance.CurrentState == GameState.BossGameplay)
        {
            if (SceneLoader.Instance != null)
                SceneLoader.Instance.ReloadBossGameBoardScene();
            else
                Debug.LogError("[BossLevelLoader] SceneLoader.Instance is null — cannot reload BossGameBoardScene!");
        }
        else
        {
            GameManager.Instance.ChangeState(GameState.BossGameplay);
        }

        Debug.Log($"[BossLevelLoader] Loading Boss {bossId}.");
    }
}
