using UnityEngine;

namespace Match3.Theme
{
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
    }
}