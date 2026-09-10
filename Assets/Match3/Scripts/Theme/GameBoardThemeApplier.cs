using UnityEngine;
using UnityEngine.UI;

namespace Match3.Theme
{
    // Attach to: GameHUD (GameBoard scene)
    public class GameBoardThemeApplier : MonoBehaviour, IThemeApplier
    {
        [SerializeField] private SpriteRenderer background; // world-space "Background" 2D sprite -> BG3
        [SerializeField] private Image topBarImage;          // GameHUD/TopBar Image
        [SerializeField] private Image[] petAndBoosterSlots; // Slot_Pet + all InventoryBoosterSlot images -> F2
        [SerializeField] private Image settingIcon;            // SettingsButton/Icon
        [SerializeField] private Image goalPanelStart;         // GoalPanel/Start -> F2
        [SerializeField] private Image settingPanel;           // SettingPanel -> F2
        [SerializeField] private Image winCard;                // WinPanel/WinCard -> F2
        [SerializeField] private Image loseCard;                // LosePanel/LoseCard -> F2
        // Buttons inside WinPanel/LosePanel/SettingPanel: add ThemeButtonBinder on each button.

        public void Apply(ThemeData theme)
        {
            if (theme == null) return;
            if (background && theme.bg3 != null) background.sprite = theme.bg3;
            if (topBarImage && theme.gameBoardTopBarImage != null) topBarImage.sprite = theme.gameBoardTopBarImage;

            if (petAndBoosterSlots != null)
                foreach (var img in petAndBoosterSlots)
                    if (img && theme.f2 != null) img.sprite = theme.f2;

            if (settingIcon && theme.settingsGearIcon != null) settingIcon.sprite = theme.settingsGearIcon;
            if (goalPanelStart && theme.f2 != null) goalPanelStart.sprite = theme.f2;
            if (settingPanel && theme.f2 != null) settingPanel.sprite = theme.f2;
            if (winCard && theme.f2 != null) winCard.sprite = theme.f2;
            if (loseCard && theme.f2 != null) loseCard.sprite = theme.f2;
        }
    }
}