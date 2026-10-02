using UnityEngine;
using UnityEngine.UI;

namespace Match3.Theme
{
    public class ThemedPrefabPiece : MonoBehaviour
    {
        public enum FrameType { F1, F2, F3, F4, UniversalButton }

        [SerializeField] private Image targetImage;
        [SerializeField] private FrameType frameType;

        private void OnEnable()
        {
            Apply();
            if (ThemeManager.Instance != null)
                ThemeManager.Instance.OnThemeChanged += OnThemeChanged;
        }

        private void OnDisable()
        {
            if (ThemeManager.Instance != null)
                ThemeManager.Instance.OnThemeChanged -= OnThemeChanged;
        }

        private void OnThemeChanged(ThemeData theme) => Apply();

        private void Apply()
        {
            if (targetImage == null) return;
            var theme = ThemeManager.Instance != null ? ThemeManager.Instance.CurrentTheme : null;
            if (theme == null) return;

            Sprite resolved = frameType switch
            {
                FrameType.F1 => theme.f1,
                FrameType.F2 => theme.f2,
                FrameType.F3 => theme.f3,
                FrameType.F4 => theme.f4,
                FrameType.UniversalButton => theme.universalButtonSprite,
                _ => targetImage.sprite
            };

            if (resolved != null)
                targetImage.sprite = resolved;
        }
    }
}
