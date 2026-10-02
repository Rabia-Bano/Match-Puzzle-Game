using UnityEngine;
using UnityEngine.UI;

namespace Match3.Theme
{
    public class MapThemeApplier : MonoBehaviour, IThemeApplier
    {
        [SerializeField] private Image pathBackground;

        public void Apply(ThemeData theme)
        {
            if (theme == null) return;
            if (pathBackground && theme.bg2 != null) pathBackground.sprite = theme.bg2;
        }
    }
}
