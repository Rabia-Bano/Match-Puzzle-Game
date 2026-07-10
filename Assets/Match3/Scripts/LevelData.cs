// ============================================================
//  LevelData.cs  —  Phase 5 Update
//  Now uses GoalData[] assets instead of inline GoalData structs.
//  Create via: Assets > Create > Match3 > Level Data
// ============================================================

using UnityEngine;

namespace Match3
{
    [CreateAssetMenu(
        fileName = "LevelData_01",
        menuName  = "Match3/Level Data",
        order     = 1)]
    public class LevelData : ScriptableObject
    {
        [Header("Board Layout")]
        [Range(4, 12)] public int width  = 8;
        [Range(4, 12)] public int height = 8;

        [Header("Rules")]
        [Range(5, 200)] public int moveLimit = 30;

        [Header("Goals — assign GoalData assets")]
        [Tooltip("All goals must be complete to win. Create GoalData assets via Create > Match3 > Goal Data.")]
        public GoalData[] goals;

        [Header("Tile Availability")]
        [Tooltip("Leave empty to use TileSpawner's default tiles.")]
        public TileData[] allowedTiles;

        [Header("Star Score Thresholds (optional fallback)")]
        public int scoreTar1 = 500;
        public int scoreTar2 = 1000;
        public int scoreTar3 = 2000;

        // ── Helpers ────────────────────────────────────────────

        public bool AllGoalsComplete()
        {
            if (goals == null) return false;
            foreach (var g in goals)
                if (!g.IsComplete) return false;
            return true;
        }

        public void ResetGoals()
        {
            if (goals == null) return;
            foreach (var g in goals)
                g.ResetProgress();
        }

        public int GetStarRating(int score)
        {
            if (score >= scoreTar3) return 3;
            if (score >= scoreTar2) return 2;
            if (score >= scoreTar1) return 1;
            return 0;
        }
    }
}