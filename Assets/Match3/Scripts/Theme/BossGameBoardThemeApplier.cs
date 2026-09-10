using UnityEngine;
using UnityEngine.UI;

namespace Match3.Theme
{
    // Attach to: BossGameHUD (BossGameBoard scene)
    public class BossGameBoardThemeApplier : MonoBehaviour, IThemeApplier
    {
        [SerializeField] private SpriteRenderer background; // world-space "Background" 2D sprite -> BG3
        [SerializeField] private Image bossArenaHUD;          // BossArenaHUD -> color only
        [SerializeField] private Image[] petAndBoosterSlots;  // Slot_Pet + InventoryBoosterSlot -> F2
        [SerializeField] private Image settingIcon;             // SettingsButton/Icon
        [SerializeField] private Image settingPanel;            // SettingPanel -> F2
        [SerializeField] private Image bossIntroPanel;           // BossIntroPanel -> F2
        [SerializeField] private Image winPanel;                 // WinPanel -> F2
        [SerializeField] private Image losePanel;                 // LosePanel -> F2
        // Buttons inside WinPanel/LosePanel/SettingPanel/BossIntroPanel: add ThemeButtonBinder on each button.

        public void Apply(ThemeData theme)
        {
            if (theme == null) return;
            if (background && theme.bg3) background.sprite = theme.bg3;
            if (bossArenaHUD) bossArenaHUD.color = theme.bossArenaHUDColor;

            if (petAndBoosterSlots != null)
                foreach (var img in petAndBoosterSlots)
                    if (img && theme.f2) img.sprite = theme.f2;

            if (settingIcon && theme.settingsGearIcon) settingIcon.sprite = theme.settingsGearIcon;
            if (settingPanel && theme.f2) settingPanel.sprite = theme.f2;
            if (bossIntroPanel && theme.f2) bossIntroPanel.sprite = theme.f2;
            if (winPanel && theme.f2) winPanel.sprite = theme.f2;
            if (losePanel && theme.f2) losePanel.sprite = theme.f2;
        }
    }
}