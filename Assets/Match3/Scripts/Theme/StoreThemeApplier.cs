using UnityEngine;
using UnityEngine.UI;

namespace Match3.Theme
{
    // Attach to: StorePanel (Store scene)
    // Nav icons + Lives/Coins pills -> NavBarThemeBinder / TopBarPillThemeBinder.
    // StoreItemCard frame + BuyButton -> ThemedPrefabPiece placed directly on the StoreItemCard prefab.
    public class StoreThemeApplier : MonoBehaviour, IThemeApplier
    {
        [SerializeField] private Image bgImage;      // BGImage -> BG3
        [SerializeField] private Image headingBack;   // StorePanel/Image (heading backside) -> F3

        public void Apply(ThemeData theme)
        {
            if (theme == null) return;
            if (bgImage && theme.bg3 != null) bgImage.sprite = theme.bg3;
            if (headingBack && theme.f3 != null) headingBack.sprite = theme.f3;
        }
    }
}