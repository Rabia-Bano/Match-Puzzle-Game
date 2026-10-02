using UnityEngine;
using UnityEngine.UI;

namespace Match3.Theme
{
    public class SettingThemeApplier : MonoBehaviour, IThemeApplier
    {
        [SerializeField] private Image bgImage;
        [SerializeField] private Image headingBack;
        [SerializeField] private Image settingPanel;

        public void Apply(ThemeData theme)
        {
            if (theme == null) return;
            if (bgImage && theme.bg3 != null) bgImage.sprite = theme.bg3;
            if (headingBack && theme.f3 != null) headingBack.sprite = theme.f3;
            if (settingPanel && theme.f2 != null) settingPanel.sprite = theme.f2;
        }
    }
}
