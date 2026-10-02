using UnityEngine;
using UnityEngine.UI;

namespace Match3.Theme
{
    public class BossArenaThemeApplier : MonoBehaviour, IThemeApplier
    {
        [SerializeField] private Image background;
        [SerializeField] private Image bgFrame;
        [SerializeField] private Image titleImage;

        public void Apply(ThemeData theme)
        {
            if (theme == null) return;
            if (background && theme.bg3 != null) background.sprite = theme.bg3;
            if (bgFrame && theme.f2 != null) bgFrame.sprite = theme.f2;
            if (titleImage && theme.f3 != null) titleImage.sprite = theme.f3;
        }
    }
}
