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

        [Header("Blank Cells (holes in the board)")]
        [Tooltip("Grid cells (x,y — 0-indexed, y=0 is the BOTTOM row) that are completely empty holes. " +
                 "No gem spawns there, no obstacle/jelly can ever move there, the player can't swap into " +
                 "them, and falling tiles pass straight THROUGH them. Tip: use the grid painter in the " +
                 "Inspector instead of typing coordinates.")]
        public Vector2Int[] blankPositions;

        [Header("Rules")]
        [Range(5, 200)] public int moveLimit = 30;

        [Header("Timer (regular levels only)")]
        [Tooltip("Turn ON to give this level a countdown timer IN ADDITION to the move limit. " +
                 "If the timer reaches 0 before the goals are complete, the level is LOST — even if moves are left.")]
        public bool useTimer = false;
        [Tooltip("Seconds on the clock when the player taps Start (e.g. 90 = 1:30).")]
        [Range(10, 600)] public int timeLimitSeconds = 90;
        [Tooltip("When this many seconds (or fewer) remain, the timer turns red and starts pulsing / ticking.")]
        [Range(3, 60)] public int timerWarningSeconds = 10;

        [Header("Goals — assign GoalData assets")]
        [Tooltip("All goals must be complete to win. Create GoalData assets via Create > Match3 > Goal Data.")]
        public GoalData[] goals;

        [Header("Tile Availability")]
        [Tooltip("Leave empty to use TileSpawner's default tiles. Do NOT put hard-tile / " +
                 "drop-stone TileData assets here — they're configured separately below " +
                 "and placed only at the fixed positions you specify.")]
        public TileData[] allowedTiles;

        [Header("Obstacles — Jelly")]
        [Tooltip("Grid cells (x,y — 0-indexed, y=0 is the bottom row) that start with a " +
                  "jelly layer underneath the tile there.")]
        public Vector2Int[] jellyPositions;
        [Tooltip("How many jelly layers at every position above (same for all of them for now).")]
        [Range(1, 3)] public int jellyLayers = 1;

        [Header("Obstacles — Hard Tile (Blocker)")]
        [Tooltip("The TileData asset used for hard-tile cells (must have isHardTile = true).")]
        public TileData hardTileData;
        [Tooltip("Grid cells that start with a hard tile instead of a normal colour tile.")]
        public Vector2Int[] hardTilePositions;

        [Header("Obstacles — Dropdown Stone (Ingredient)")]
        [Tooltip("The TileData asset used for dropdown-stone cells (must have isDropStone = true).")]
        public TileData dropStoneData;
        [Tooltip("Grid cells that start with a dropdown stone instead of a normal colour tile.")]
        public Vector2Int[] stonePositions;

        [Header("Star Score Thresholds (optional fallback)")]
        public int scoreTar1 = 500;
        public int scoreTar2 = 1000;
        public int scoreTar3 = 2000;

        public bool IsBlankCell(int x, int y)
        {
            if (blankPositions == null) return false;
            foreach (var p in blankPositions)
                if (p.x == x && p.y == y) return true;
            return false;
        }

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
