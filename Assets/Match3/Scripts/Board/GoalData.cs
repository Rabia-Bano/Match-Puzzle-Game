using UnityEngine;

namespace Match3
{
    public enum GoalType
    {
        CollectTile   = 0,
        ClearJelly    = 1,
        ReachScore    = 2,
        ClearHardTile = 3,
        CollectStone  = 4
    }

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

        [System.NonSerialized] public int currentAmount;

        public bool   IsComplete     => currentAmount >= requiredAmount;
        public float  Progress       => requiredAmount > 0
                                         ? Mathf.Clamp01((float)currentAmount / requiredAmount)
                                         : 1f;
        public int    Remaining      => Mathf.Max(0, requiredAmount - currentAmount);

        public void   ResetProgress() => currentAmount = 0;

        public bool AddProgress(int delta = 1)
        {
            bool wasDone = IsComplete;
            currentAmount = Mathf.Min(currentAmount + delta, requiredAmount);
            return !wasDone && IsComplete;
        }

        public override string ToString() =>
            $"{name} [{goalType}] {currentAmount}/{requiredAmount}";
    }
}
