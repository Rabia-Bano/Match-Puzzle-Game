// ============================================================
//  ThemeBackgroundAnimator.cs  —  NEW
//
//  Makes a scene BACKGROUND feel alive: very slow zoom-in/out and a
//  gentle drift (the "Ken Burns" effect), strength taken from the
//  active ThemeData (animateBackground / backgroundMotionStrength).
//  Also plays a soft "pop" + fade when the theme changes.
//
//  Attach to: the background object of every scene —
//    • a UI Image (Login BG, Map PathBackground, Store BG ...) OR
//    • a world SpriteRenderer (GameBoard / BossGameBoard Background).
//  No wiring needed. Make the background ~10% bigger than the screen
//  (or set its RectTransform to stretch with a small negative margin)
//  so the zoom never shows the edges.
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Match3.Theme
{
    public class ThemeBackgroundAnimator : MonoBehaviour
    {
        [Tooltip("Seconds for one full zoom-in + zoom-out cycle.")]
        [SerializeField] private float cycleSeconds = 16f;
        [Tooltip("Max extra zoom at strength 1 (0.08 = 8%).")]
        [SerializeField] private float maxZoom = 0.08f;
        [Tooltip("Max drift distance at strength 1 (UI pixels for Images, world units x100 for sprites).")]
        [SerializeField] private float maxDrift = 30f;

        private Vector3 _baseScale;
        private Vector3 _basePos;
        private bool    _isUI;
        private float   _t;
        private float   _strength;
        private bool    _enabled = true;

        private void Awake()
        {
            _isUI      = GetComponent<RectTransform>() != null && GetComponent<Graphic>() != null;
            _baseScale = transform.localScale;
            _basePos   = _isUI ? (Vector3)((RectTransform)transform).anchoredPosition : transform.localPosition;
            _t         = Random.Range(0f, 100f);
        }

        private void OnEnable()
        {
            ReadTheme(ThemeManager.Instance != null ? ThemeManager.Instance.CurrentTheme : null);
            if (ThemeManager.Instance != null) ThemeManager.Instance.OnThemeChanged += HandleThemeChanged;
        }

        private void OnDisable()
        {
            if (ThemeManager.Instance != null) ThemeManager.Instance.OnThemeChanged -= HandleThemeChanged;
            transform.DOKill();
        }

        private void ReadTheme(ThemeData theme)
        {
            _enabled  = theme == null || theme.animateBackground;
            _strength = theme != null ? theme.backgroundMotionStrength : 0.4f;
        }

        private void HandleThemeChanged(ThemeData theme)
        {
            ReadTheme(theme);
            // small celebratory pop when the world changes
            transform.DOKill();
            transform.DOPunchScale(_baseScale * 0.06f, 0.6f, 4, 0.5f).SetUpdate(true);
        }

        private void Update()
        {
            if (!_enabled || _strength <= 0f)
            {
                transform.localScale = _baseScale;
                return;
            }

            _t += Time.unscaledDeltaTime;
            float w = Mathf.PI * 2f / Mathf.Max(1f, cycleSeconds);

            float zoom  = 1f + maxZoom * _strength * (0.5f + 0.5f * Mathf.Sin(_t * w));
            float driftX = Mathf.Sin(_t * w * 0.7f) * maxDrift * _strength;
            float driftY = Mathf.Cos(_t * w * 0.5f) * maxDrift * 0.5f * _strength;

            if (!DOTween.IsTweening(transform))
                transform.localScale = _baseScale * zoom;

            if (_isUI)
                ((RectTransform)transform).anchoredPosition = (Vector2)_basePos + new Vector2(driftX, driftY);
            else
                transform.localPosition = _basePos + new Vector3(driftX, driftY, 0f) * 0.01f;
        }
    }
}
