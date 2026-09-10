using UnityEngine;
using UnityEngine.UI;

namespace Match3.Theme
{
    // Attach to: LoadingCanvas (Preloader scene)
    public class PreloaderThemeApplier : MonoBehaviour, IThemeApplier
    {
        [SerializeField] private Image background; // LoadingCanvas/Background -> BG1

        public void Apply(ThemeData theme)
        {
            if (theme == null) return;
            if (background && theme.bg1 != null) background.sprite = theme.bg1;
        }
    }
}