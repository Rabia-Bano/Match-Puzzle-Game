using UnityEngine;
using UnityEngine.UI;

namespace Match3.Theme
{
    public class PetCompanionThemeApplier : MonoBehaviour, IThemeApplier
    {
        [SerializeField] private Image bgImage;

        public void Apply(ThemeData theme)
        {
            if (theme == null) return;
            if (bgImage && theme.bg3 != null) bgImage.sprite = theme.bg3;
        }
    }
}
