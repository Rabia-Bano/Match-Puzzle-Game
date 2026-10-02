using System.Collections.Generic;
using UnityEngine;

namespace Match3
{
    [System.Serializable]
    public class BossBoosterReward
    {
        [Tooltip("Must exactly match a BoosterManager id constant — e.g. BoosterManager.Hammer " +
                 "(\"hammer\"), BoosterManager.RowBomb (\"row_bomb\"), BoosterManager.ColumnBomb " +
                 "(\"column_bomb\"), BoosterManager.Shuffle2Tiles (\"shuffle_2tiles\"), " +
                 "BoosterManager.ShuffleBoard (\"shuffle_board\"). A typo here silently grants " +
                 "a booster id nothing in the game recognizes, so copy these exactly.")]
        public string boosterId = BoosterManager.Hammer;

        [Tooltip("How many of this booster the player receives on victory.")]
        [Min(1)] public int count = 1;
    }

    [CreateAssetMenu(fileName = "BossData_New", menuName = "Match3/Boss Data", order = 8)]
    public class BossData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique numeric id. Also used as the Resources file name: Resources/Bosses/boss_<id>.asset")]
        public int id;

        public string bossName;

        [TextArea] public string description;

        [Tooltip("Shown in BossArenaHUD and the reward/lose screens.")]
        public Sprite portrait;

        [Header("Combat")]
        [Tooltip("Boss starting / maximum health.")]
        public int health = 100;

        [Tooltip("Only matches of THIS colour damage the boss. Other colours still clear normally " +
                 "but deal no damage — this is what BossController.TakeDamage() checks against.")]
        public TileColor weaknessTileType = TileColor.Red;

        [Tooltip("NEW — the weakness tile's own icon/sprite (drag the matching TileData's " +
                 "sprite here). Shown on BossIntroPanel so the player knows which colour to " +
                 "target before the fight starts. Purely visual — weaknessTileType above is " +
                 "still what BossController actually checks for damage.")]
        public Sprite weaknessIcon;

        [Header("Passive Defense Loop (time-based)")]
        [Tooltip("Boss throws one hurdle at the board every N seconds, forever, regardless of player moves.")]
        [Min(1f)] public float attackIntervalSeconds = 5f;

        [Tooltip("Seconds of warning (HUD popup) before an attack actually executes on the board.")]
        [Min(0f)] public float attackWarningDelay = 1.2f;

        [Tooltip("How long a LockTiles(freeze) attack keeps its tiles frozen.")]
        [Min(0.5f)] public float freezeDuration = 10f;

        [Header("Escalation (severity scales as HP drops)")]
        [Tooltip("Above this HP fraction: severity 1 (e.g. 1 tile/cell per attack).")]
        [Range(0f, 1f)] public float severityTier2HpFraction = 0.66f;
        [Tooltip("Above this HP fraction (and below tier2): severity 2. Below this: severity 3 (desperate).")]
        [Range(0f, 1f)] public float severityTier3HpFraction = 0.33f;

        [Header("Damage Tiers (percent of MaxHealth) — used for BOTH regular matches and special/combo clears")]
        [Tooltip("Damage for a 1–2 weakness-tile hit (special-tile clears only — a regular match is never smaller than 3).")]
        [Range(0f, 100f)] public float damagePercent1To2 = 1f;
        [Tooltip("Damage for a 3-tile match / 3 weakness tiles cleared in one special blast.")]
        [Range(0f, 100f)] public float damagePercent3 = 2f;
        [Tooltip("Damage for a 4-tile match / 4 weakness tiles cleared in one special blast.")]
        [Range(0f, 100f)] public float damagePercent4 = 3f;
        [Tooltip("Damage for a 5+-tile match / 5+ weakness tiles cleared in one special blast.")]
        [Range(0f, 100f)] public float damagePercent5Plus = 5f;

        [Tooltip("A special-tile blast/combo clears weakness tiles one at a time — this window batches " +
                 "all of them from the same blast together before scoring a tier above, so a big blast " +
                 "is scored the same way a big regular match would be (\"according to matching\").")]
        [Min(0.05f)] public float specialClearBatchWindow = 0.2f;

        [Header("Self-Heal Loop (time-based)")]
        [Tooltip("Boss regenerates HP every N seconds, forever, until defeated.")]
        [Min(1f)] public float healIntervalSeconds = 10f;
        [Tooltip("Percent of MaxHealth regenerated per heal tick.")]
        [Range(0f, 100f)] public float healPercentPerTick = 5f;

        [Header("Attack Pool (which hurdle types THIS boss can throw)")]
        [Tooltip("Add ONE entry per attack type you want this boss to be allowed to use — " +
                 "e.g. just a 'Jelly' entry and a 'Lock Tiles' entry means this boss will ONLY " +
                 "ever throw jelly or freeze attacks, picked randomly each tick (severity/HP still " +
                 "escalates the amount, same as before). Attack Params / Lock Duration / Warning " +
                 "Message on each entry are NOT used — severity + bossData.freezeDuration + the " +
                 "built-in warning text still drive those, exactly like before. Leave this list " +
                 "EMPTY to keep the old default: cycle randomly through all four types " +
                 "(Jelly / Stone Tiles / Add Obstacles(rocks) / Lock Tiles).")]
        public List<BossAttack> attackPattern = new List<BossAttack>();

        [Header("Rewards (on victory)")]
        [Tooltip("Coins granted via ProfileManager.OnBossDefeated().")]
        public int rewardCoins = 100;

        [Tooltip("EVERY entry here is granted in full on victory (not random — the player gets " +
                 "all of them). Default below matches the Level 1 example: Hammer x1, Row Bomb x1, " +
                 "Column Bomb x1, Shuffle 2 Tiles x1, Shuffle Board x1. Edit freely per boss — add, " +
                 "remove, or change counts/ids however you want.")]
        public List<BossBoosterReward> boosterRewards = new List<BossBoosterReward>
        {
            new BossBoosterReward { boosterId = BoosterManager.Hammer,        count = 1 },
            new BossBoosterReward { boosterId = BoosterManager.RowBomb,       count = 1 },
            new BossBoosterReward { boosterId = BoosterManager.ColumnBomb,    count = 1 },
            new BossBoosterReward { boosterId = BoosterManager.Shuffle2Tiles, count = 1 },
            new BossBoosterReward { boosterId = BoosterManager.ShuffleBoard,  count = 1 },
        };
    }
}
