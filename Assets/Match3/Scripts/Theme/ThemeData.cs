using UnityEngine;

namespace Match3.Theme
{
    /// <summary>NEW — the animated ambient effect drawn over every scene for a theme.</summary>
    public enum ThemeAmbientType
    {
        None      = 0,
        Snow      = 1,   // Ice World
        Leaves    = 2,   // Forest World
        Sand      = 3,   // Desert World (wind-blown sand + dust puffs)
        Stars     = 4,   // Space World (twinkling stars + shooting stars)
        Bubbles   = 5,   // Ocean / Water world
        Embers    = 6,   // Volcano / Lava world
        Petals    = 7,   // Candy / Spring world
        Rain      = 8,   // Jungle / Storm world
        Fireflies = 9    // Night / Magic world
    }

    /// <summary>
    /// One asset = one theme (e.g. "Ice World", "Forest World", "Desert World").
    /// Create via: Assets > Create > Match3 > Theme Data
    /// </summary>
    [CreateAssetMenu(fileName = "ThemeData_", menuName = "Match3/Theme Data")]
    public class ThemeData : ScriptableObject
    {
        [Header("Identity")]
        public string themeId;
        public string themeName;

        [Header("Backgrounds")]
        [Tooltip("Preloader Scene + Login Scene")]
        public Sprite bg1;

        [Tooltip("Map Scene PathBackground + BossArena BGFrame")]
        public Sprite bg2;

        [Tooltip("All other scenes background. Used as both UI Image sprite AND world-space SpriteRenderer sprite (GameBoard / BossGameBoard).")]
        public Sprite bg3;

        [Header("Frames")]
        [Tooltip("Life Pill BG + Coin Pill BG")]
        public Sprite f1;

        [Tooltip("BossArenaBGFrame, PetSlot, SettingPanel, GoalPanel/Start, WinCard, LoseCard, Slot_Pet, InventoryBoosterSlot, BossIntroPanel, BossGameBoard WinPanel/LosePanel")]
        public Sprite f2;

        [Tooltip("Heading backside: BossArena Title, Store heading, LeaderBoard heading, Setting heading")]
        public Sprite f3;

        [Tooltip("StoreItemCard, LeaderBoardRow")]
        public Sprite f4;

        [Header("Buttons")]
        [Tooltip("Universal button image used across every scene/panel")]
        public Sprite universalButtonSprite;

        [Header("Misc themed icons")]
        [Tooltip("Gear icon used in GameBoard SettingsButton and BossGameBoard SettingsButton")]
        public Sprite settingsGearIcon;

        [Header("Bottom Nav Icons (Map / BossArena / Pets / Store / LeaderBoard / Setting)")]
        public Sprite mapIcon;
        public Sprite bossArenaIcon;
        public Sprite petsIcon;
        public Sprite storeIcon;
        public Sprite leaderBoardIcon;
        public Sprite settingIcon;

        [Header("Level Node Colors (Map scene LevelNode_/Image, BossArena BGLevelNode)")]
        public Color levelNodeLockedColor = Color.white;
        public Color levelNodeUnlockedColor = Color.white;
        public Color levelNodeCompletedColor = Color.white;

        [Header("GameBoard TopBar")]
        public Sprite gameBoardTopBarImage;

        [Header("BossGameBoard HUD")]
        [Tooltip("BossArenaHUD panel image, themed the same way as GameBoard TopBar (gameBoardTopBarImage below).")]
        public Sprite bossArenaHUDImage;

        // ── NEW — Theme animation ─────────────────────────────
        [Header("Ambient Animation (NEW — plays in EVERY scene)")]
        [Tooltip("Ice = Snow, Forest = Leaves, Desert = Sand, Space = Stars ... None = off.")]
        public ThemeAmbientType ambientType = ThemeAmbientType.None;

        [Tooltip("Colour of the particles (white snow, green/orange leaves, sandy yellow ...).")]
        public Color ambientTint = Color.white;

        [Tooltip("Optional second colour — each particle picks a random colour between Tint and this. " +
                 "Great for autumn leaves (green → orange) or embers (yellow → red).")]
        public Color ambientTint2 = Color.white;

        [Tooltip("How many particles on screen at once (mobile-friendly: 20–60).")]
        [Range(0, 150)] public int ambientCount = 40;

        [Tooltip("Speed multiplier for the whole effect.")]
        [Range(0.2f, 3f)] public float ambientSpeed = 1f;

        [Tooltip("Sideways wind. Negative = blows left, positive = blows right.")]
        [Range(-1f, 1f)] public float ambientWind = 0f;

        [Tooltip("Optional — your own particle sprite (e.g. a drawn snowflake from Canva). " +
                 "Leave empty to use the built-in generated shape.")]
        public Sprite ambientCustomSprite;

        [Header("Background Animation (NEW)")]
        [Tooltip("Slow zoom / drift on scene backgrounds that have a ThemeBackgroundAnimator component.")]
        public bool animateBackground = true;

        [Tooltip("0 = still, 1 = strong. Recommended 0.3–0.5.")]
        [Range(0f, 1f)] public float backgroundMotionStrength = 0.4f;
    }
}