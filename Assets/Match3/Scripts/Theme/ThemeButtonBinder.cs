using UnityEngine;
using UnityEngine.UI;

namespace Match3.Theme
{
    public class ThemeButtonBinder : MonoBehaviour, IThemeApplier
    {
        [SerializeField] private Image targetImage;

        public void Apply(ThemeData theme)
        {
            if (theme == null || targetImage == null) return;
            if (theme.universalButtonSprite != null)
                targetImage.sprite = theme.universalButtonSprite;
        }
    }
}
