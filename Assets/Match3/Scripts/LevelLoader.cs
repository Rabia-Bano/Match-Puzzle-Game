// ============================================================
//  LevelLoader.cs  —  Static Helper Class
//
//  Purpose:
//    1. Loads LevelData ScriptableObject from Resources
//    2. Stores it in LevelSession
//    3. Tells GameManager to switch to Playing state
//       (SceneLoader then loads GameBoardScene automatically)
//
//  LevelData naming convention:
//    Assets/Resources/Levels/Level_1.asset
//    Assets/Resources/Levels/Level_2.asset  ... etc.
//
//  NO MonoBehaviour needed — pure static utility.
//  Place in: Assets/Scripts/
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace Match3
{
    public static class LevelLoader
    {
        private const string LEVELS_PATH = "Levels/Level_";

        // ─────────────────────────────────────────────────────
        // MAIN ENTRY POINT
        // Called by PreLevelPanel "Play" button
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// Loads LevelData from Resources, stores in LevelSession,
        /// then triggers GameBoardScene load via GameManager.
        /// </summary>
        public static void LoadLevel(int levelId,
                                      List<BoosterType> selectedBoosters = null)
        {
            // 1. Load ScriptableObject from Resources/Levels/Level_{id}
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

            // 2. Reset goals for fresh start
            data.ResetGoals();

            // 3. Store in LevelSession (static bridge to GameBoardScene)
            LevelSession.Begin(data, levelId, selectedBoosters);

            // 4. Tell GameManager which level we're on
            if (GameManager.Instance != null)
                GameManager.Instance.SetLevel(levelId);

            // 5. Switch to GameBoardScene.
            //
            // FIX: if we're called from the result panel (Replay / Next Level),
            // GameManager.CurrentState is ALREADY GameState.Playing — the state
            // itself isn't changing, only the level DATA is. ChangeState() has an
            // "if (newState == CurrentState) return;" guard that silently no-ops
            // in that exact situation, so nothing ever told SceneLoader to reload
            // the scene. Force the reload directly in that case instead.
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