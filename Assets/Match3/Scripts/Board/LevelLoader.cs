using System.Collections.Generic;
using UnityEngine;

namespace Match3
{
    public static class LevelLoader
    {
        private const string LEVELS_PATH = "Levels/Level_";

        public static void LoadLevel(int levelId,
                                      List<BoosterType> selectedBoosters = null)
        {
            if (LivesManager.Instance != null && !LivesManager.Instance.HasLives)
            {
                Debug.Log("[LevelLoader] Blocked — 0 lives remaining. " +
                          "Firing GameEvents.OnNoLivesBlocked for UI to show a popup.");
                GameEvents.OnNoLivesBlocked?.Invoke();
                return;
            }

            string path = $"{LEVELS_PATH}{levelId}";
            LevelData data = Resources.Load<LevelData>(path);

            if (data == null)
            {
                Debug.LogError(
                    $"[LevelLoader] LevelData not found at: Resources/{path}.asset\n" +
                    $"Make sure file is named exactly 'Level_{levelId}.asset' " +
                    $"inside Assets/Resources/Levels/ folder.");
                return;
            }

            data.ResetGoals();

            LevelSession.Begin(data, levelId, selectedBoosters);

            if (GameManager.Instance != null)
                GameManager.Instance.SetLevel(levelId);

            if (GameManager.Instance != null)
            {
                if (GameManager.Instance.CurrentState == GameState.Playing)
                {
                    if (SceneLoader.Instance != null)
                        SceneLoader.Instance.ReloadGameBoardScene();
                    else
                        Debug.LogError("[LevelLoader] SceneLoader.Instance is null — cannot reload GameBoardScene!");
                }
                else
                {
                    GameManager.Instance.ChangeState(GameState.Playing);
                }
            }
            else
            {
                Debug.LogError("[LevelLoader] GameManager.Instance is null!");
            }

            Debug.Log($"[LevelLoader] Loading Level {levelId} — " +
                      $"Board: {data.width}x{data.height}, " +
                      $"Moves: {data.moveLimit}");
        }
    }
}
