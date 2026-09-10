using UnityEngine;
using UnityEngine.UI;

namespace Match3.Theme
{
    // Attach to: LeaderBoardPanel (LeaderBoard scene)
    // Nav icons + Lives/Coins pills -> NavBarThemeBinder / TopBarPillThemeBinder.
    // LeaderBoardRow frame -> ThemedPrefabPiece placed directly on the LeaderBoardRow prefab.
    public class LeaderBoardThemeApplier : MonoBehaviour, IThemeApplier
    {
        [SerializeField] private Image bgImage;     // BGImage -> BG3
        [SerializeField] private Image headingBack;  // Image (heading backside) -> F3

        public void Apply(ThemeData theme)
        {
            if (theme == null) return;
            if (bgImage && theme.bg3 != null) bgImage.sprite = theme.bg3;
            if (headingBack && theme.f3 != null) headingBack.sprite = theme.f3;
        }
    }
}