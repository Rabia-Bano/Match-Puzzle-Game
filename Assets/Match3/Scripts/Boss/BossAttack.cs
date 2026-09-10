// ============================================================
//  BossAttack.cs  —  plain C# (not a MonoBehaviour, not a ScriptableObject)
//
//  One entry in BossData.attackPattern. BossController fires these
//  in order (looping) every N player moves and hands them to
//  BossAttackExecutor, which does the actual board manipulation.
//
//  [System.Serializable] so it shows up nicely as a list in the
//  BossData Inspector — no separate .asset files needed per attack.
// ============================================================

using UnityEngine;

namespace Match3
{
    /// <summary>What kind of hurdle this attack throws at the player.</summary>
    public enum BossAttackType
    {
        /// <summary>Freezes N random normal tiles (TileState.Locked) for a duration — can't be swapped.</summary>
        LockTiles = 0,

        /// <summary>Instantly removes N moves from the level's MoveCounter (if one is wired up for this fight). Not used by BossController's default passive-defense loop.</summary>
        ReduceMoves = 1,

        /// <summary>Drops N hard-tile (rock) blockers onto random board cells.</summary>
        AddObstacles = 2,

        /// <summary>Drops N dropdown-stone obstacles onto random board cells (falls with gravity, blocks that column's colour).</summary>
        StoneTiles = 3,

        /// <summary>NEW — drops a jelly layer onto N random cells (boss "healing itself with goo" flavour). See BossAttackExecutor.ExecuteAddJelly().</summary>
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
