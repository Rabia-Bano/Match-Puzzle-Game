using UnityEngine;
using UnityEngine.UI;

namespace Match3.Theme
{
    // Attach to: LoginCanvas (Login scene)
    public class LoginThemeApplier : MonoBehaviour, IThemeApplier
    {
        [SerializeField] private Image background;     // LoginCanvas/Background -> BG1
        [SerializeField] private Image authContainer;   // LoginCanvas/AuthContainer -> F2

        public void Apply(ThemeData theme)
        {
            if (theme == null) return;
            if (background && theme.bg1 != null) background.sprite = theme.bg1;
            if (authContainer && theme.f2 != null) authContainer.sprite = theme.f2;
        }
    }
}