using UnityEngine;
using UnityEngine.UI;

namespace Match3.Theme
{
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
