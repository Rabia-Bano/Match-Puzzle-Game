using UnityEngine;
using UnityEngine.UI;

namespace Match3.Theme
{
    public class BossGameBoardThemeApplier : MonoBehaviour, IThemeApplier
    {
        [SerializeField] private SpriteRenderer background;
        [SerializeField] private Image bossArenaHUD;
        [SerializeField] private Image[] petAndBoosterSlots;
        [SerializeField] private Image settingIcon;
        [SerializeField] private Image settingPanel;
        [SerializeField] private Image bossIntroPanel;
        [SerializeField] private Image winPanel;
        [SerializeField] private Image losePanel;

        public void Apply(ThemeData theme)
        {
            if (theme == null) return;
            if (background && theme.bg3) background.sprite = theme.bg3;
            if (bossArenaHUD && theme.bossArenaHUDImage != null) bossArenaHUD.sprite = theme.bossArenaHUDImage;

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
