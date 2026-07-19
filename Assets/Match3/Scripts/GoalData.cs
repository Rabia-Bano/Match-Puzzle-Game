// ============================================================
//  GoalData.cs  —  ScriptableObject
//  Create via: Assets > Create > Match3 > Goal Data
//
//  One GoalData asset = one objective in a level.
//  A level can have multiple goals (all must be complete to win).
//
//  GoalType:
//    CollectTile  — clear N tiles of a specific TileData type
//    ClearJelly   — clear N jelly/blocker tiles (future feature)
//    ReachScore   — accumulate N score points
// ============================================================

using UnityEngine;

namespace Match3
{
    // ── Enums ─────────────────────────────────────────────────

    public enum GoalType
    {
        CollectTile   = 0,   // clear N tiles matching targetTile
        ClearJelly    = 1,   // clear N jelly cells (blocker layer)
        ReachScore    = 2,   // reach requiredAmount score
        ClearHardTile = 3,   // break N hard tile (blocker) obstacles
        CollectStone  = 4    // drop N dropdown stones to the bottom row
    }

    // ── ScriptableObject ──────────────────────────────────────

    [CreateAssetMenu(
        fileName = "GoalData_New",
        menuName  = "Match3/Goal Data",
        order     = 2)]
    public class GoalData : ScriptableObject
    {
        [Header("Goal Type")]
        public GoalType goalType;

        [Header("Target (for CollectTile)")]
        [Tooltip("Which TileData to collect. Only used when GoalType = CollectTile.")]
        public TileData targetTile;

        [Header("Amount")]
        [Tooltip("How many tiles/jellies to clear OR score to reach.")]
        public int requiredAmount = 10;

        [Header("UI")]
        [Tooltip("Icon displayed in the HUD goal panel.")]
        public Sprite goalIcon;

        [Tooltip("Short label shown under the icon (e.g. 'Red x20').")]
        public string goalLabel;

        // ── Runtime (non-serialised) ──────────────────────────

        [System.NonSerialized] public int currentAmount;

        // ── Helpers ───────────────────────────────────────────

        public bool   IsComplete     => currentAmount >= requiredAmount;
        public float  Progress       => requiredAmount > 0
                                         ? Mathf.Clamp01((float)currentAmount / requiredAmount)
                                         : 1f;
        public int    Remaining      => Mathf.Max(0, requiredAmount - currentAmount);

        public void   ResetProgress() => currentAmount = 0;

        /// <summary>
        /// Increments progress by delta. Clamps to requiredAmount.
        /// Returns true if this increment completed the goal.
        /// </summary>
        public bool AddProgress(int delta = 1)
        {
            bool wasDone = IsComplete;
            currentAmount = Mathf.Min(currentAmount + delta, requiredAmount);
            return !wasDone && IsComplete;   // true = just completed
        }

        public override string ToString() =>
            $"{name} [{goalType}] {currentAmount}/{requiredAmount}";
    }
}