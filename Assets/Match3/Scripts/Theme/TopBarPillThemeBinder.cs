using UnityEngine;
using UnityEngine.UI;

namespace Match3.Theme
{
    /// <summary>
    /// Attach to TopBarPanel in every scene that has Lives/Coins pills.
    /// Applies F1 to both pill backgrounds.
    /// </summary>
    public class TopBarPillThemeBinder : MonoBehaviour, IThemeApplier
    {
        [SerializeField] private Image livePillBG;
        [SerializeField] private Image coinPillBG;

        public void Apply(ThemeData theme)
        {
            if (theme == null) return;
            if (livePillBG && theme.f1 != null) livePillBG.sprite = theme.f1;
            if (coinPillBG && theme.f1 != null) coinPillBG.sprite = theme.f1;
        }
    }
}