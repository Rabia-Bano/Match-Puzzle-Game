using UnityEngine;
using UnityEngine.UI;

namespace Match3.Theme
{
    public class PreloaderThemeApplier : MonoBehaviour, IThemeApplier
    {
        [SerializeField] private Image background;

        public void Apply(ThemeData theme)
        {
            if (theme == null) return;
            if (background && theme.bg1 != null) background.sprite = theme.bg1;
        }
    }
}
