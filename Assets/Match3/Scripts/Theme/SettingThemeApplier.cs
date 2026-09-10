using UnityEngine;
using UnityEngine.UI;

namespace Match3.Theme
{
    // Attach to: UICanvas (SettingScene)
    // Nav icons + Lives/Coins pills -> NavBarThemeBinder / TopBarPillThemeBinder.
    public class SettingThemeApplier : MonoBehaviour, IThemeApplier
    {
        [SerializeField] private Image bgImage;       // BGImage -> BG3
        [SerializeField] private Image headingBack;    // "Image" (heading backside) -> F3
        [SerializeField] private Image settingPanel;    // SettingPanel/Image -> F2

        public void Apply(ThemeData theme)
        {
            if (theme == null) return;
            if (bgImage && theme.bg3 != null) bgImage.sprite = theme.bg3;
            if (headingBack && theme.f3 != null) headingBack.sprite = theme.f3;
            if (settingPanel && theme.f2 != null) settingPanel.sprite = theme.f2;
        }
    }
}