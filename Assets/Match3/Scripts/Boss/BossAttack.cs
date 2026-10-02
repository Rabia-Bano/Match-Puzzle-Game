using UnityEngine;

namespace Match3
{
    public enum BossAttackType
    {
        LockTiles = 0,

        ReduceMoves = 1,

        AddObstacles = 2,

        StoneTiles = 3,

        Jelly = 4
    }

    [System.Serializable]
    public class BossAttack
    {
        public BossAttackType attackType = BossAttackType.LockTiles;

        [Tooltip("Meaning depends on attackType:\n" +
                 "LockTiles / AddObstacles / StoneTiles = how many tiles.\n" +
                 "ReduceMoves = how many moves to remove.")]
        [Min(1)] public int attackParams = 3;

        [Tooltip("LockTiles ONLY — seconds before the frozen tiles automatically thaw. Ignored by every other attack type.")]
        [Min(0.5f)] public float lockDuration = 6f;

        [Tooltip("Shown on BossArenaHUD's warning popup right before this attack executes.")]
        public string warningMessage = "Boss is attacking!";
    }
}
