using UnityEngine;
using UnityEngine.UI;

namespace Match3.Theme
{
    // Attach to: UICanvas (PetCompanionScene)
    // Nav icons + Lives/Coins pills -> NavBarThemeBinder / TopBarPillThemeBinder.
    // PetSlot frame + UseButton -> ThemedPrefabPiece placed directly on the PetSlot prefab.
    public class PetCompanionThemeApplier : MonoBehaviour, IThemeApplier
    {
        [SerializeField] private Image bgImage; // BGImage -> BG3

        public void Apply(ThemeData theme)
        {
            if (theme == null) return;
            if (bgImage && theme.bg3 != null) bgImage.sprite = theme.bg3;
        }
    }
}