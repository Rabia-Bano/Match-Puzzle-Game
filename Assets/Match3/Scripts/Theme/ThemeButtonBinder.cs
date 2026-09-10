using UnityEngine;
using UnityEngine.UI;

namespace Match3.Theme
{
    /// <summary>
    /// Attach directly to any static (non-prefab-spawned) Button that must use the
    /// universal themed button sprite - e.g. WinPanel/LosePanel/SettingPanel buttons.
    /// For buttons inside dynamically spawned prefabs (PetSlot UseButton,
    /// StoreItemCard BuyButton) use ThemedPrefabPiece instead.
    /// </summary>
    public class ThemeButtonBinder : MonoBehaviour, IThemeApplier
    {
        [SerializeField] private Image targetImage; // usually the Button's own Image component

        public void Apply(ThemeData theme)
        {
            if (theme == null || targetImage == null) return;
            if (theme.universalButtonSprite != null)
                targetImage.sprite = theme.universalButtonSprite;
        }
    }
}