using UnityEngine;
using UnityEngine.UI;

namespace Match3.Theme
{
    public class GameBoardThemeApplier : MonoBehaviour, IThemeApplier
    {
        [SerializeField] private SpriteRenderer background;
        [SerializeField] private Image topBarImage;
        [SerializeField] private Image[] petAndBoosterSlots;
        [SerializeField] private Image settingIcon;
        [SerializeField] private Image goalPanelStart;
        [SerializeField] private Image settingPanel;
        [SerializeField] private Image winCard;
        [SerializeField] private Image loseCard;

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
