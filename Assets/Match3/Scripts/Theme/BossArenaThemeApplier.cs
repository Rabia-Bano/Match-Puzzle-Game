using UnityEngine;
using UnityEngine.UI;

namespace Match3.Theme
{
    // Attach to: UICanvas (BossArenaScene)
    // Nav icons + Lives/Coins pills are handled separately by NavBarThemeBinder and
    // TopBarPillThemeBinder - add those too on BottomBarPanel / TopBarPanel.
    public class BossArenaThemeApplier : MonoBehaviour, IThemeApplier
    {
        [SerializeField] private Image background; // BGImage -> BG3
        [SerializeField] private Image bgFrame;      // BossArenaPanel/BGFrame -> F2
        [SerializeField] private Image titleImage;    // BossArenaPanel/TiltleImage -> F3

        public void Apply(ThemeData theme)
        {
            if (theme == null) return;
            if (background && theme.bg3 != null) background.sprite = theme.bg3;
            if (bgFrame && theme.f2 != null) bgFrame.sprite = theme.f2;
            if (titleImage && theme.f3 != null) titleImage.sprite = theme.f3;
        }
    }
}