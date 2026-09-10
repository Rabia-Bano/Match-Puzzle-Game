using UnityEngine;
using UnityEngine.UI;

namespace Match3.Theme
{
    // Attach to: UICanvas (Map scene)
    // Nav icons + Lives/Coins pills are handled separately by NavBarThemeBinder (on
    // BottomBarPanel) and TopBarPillThemeBinder (on TopBarPanel) - add those too.
    public class MapThemeApplier : MonoBehaviour, IThemeApplier
    {
        [SerializeField] private Image pathBackground; // Map Scroll View/Viewport/Content/PathBackground -> BG2

        public void Apply(ThemeData theme)
        {
            if (theme == null) return;
            if (pathBackground && theme.bg2 != null) pathBackground.sprite = theme.bg2;
        }
    }
}