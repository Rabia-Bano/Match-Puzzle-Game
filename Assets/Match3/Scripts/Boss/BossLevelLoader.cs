using UnityEngine;

public static class BossLevelLoader
{
    public static void LoadBoss(int bossId, Match3.LevelData bossBoardLevelData = null)
    {
        PlayerPrefs.SetInt("SelectedBossId", bossId);

        if (bossBoardLevelData != null)
        {
            Match3.LevelSession.CurrentLevel   = bossBoardLevelData;
            Match3.LevelSession.CurrentLevelId = 0;
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
