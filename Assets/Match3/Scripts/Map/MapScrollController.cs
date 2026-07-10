// ============================================================
//  MapScrollController.cs  —  MonoBehaviour on ScrollView
//
//  Attach to: ScrollView GameObject (same as ScrollRect)
//  Purpose:
//    • Clamps vertical scroll to valid range [0, 1]
//    • Adds smooth momentum / inertia feel
//    • Exposes ScrollToNode(RectTransform) for MapManager
// ============================================================

using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(ScrollRect))]
public class MapScrollController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────
    // Inspector
    // ─────────────────────────────────────────────────────────

    [Header("Scroll Settings")]
    [Tooltip("Multiplier for inertia deceleration (higher = stops faster)")]
    [Range(1f, 20f)]
    public float decelerationRate = 8f;

    [Tooltip("Smooth-scroll duration in seconds when centering on a node")]
    [Range(0.1f, 1.5f)]
    public float snapDuration = 0.6f;

    [Tooltip("Minimum scroll position (0 = bottom)")]
    [Range(0f, 0.5f)]
    public float minScrollPos = 0f;

    [Tooltip("Maximum scroll position (1 = top)")]
    [Range(0.5f, 1f)]
    public float maxScrollPos = 1f;

    // ─────────────────────────────────────────────────────────
    // Private
    // ─────────────────────────────────────────────────────────

    private ScrollRect _scrollRect;
    private Tween      _snapTween;

    // ─────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────

    private void Awake()
    {
        _scrollRect = GetComponent<ScrollRect>();
        if (_scrollRect == null) return;

        // We handle deceleration ourselves, so let Unity's inertia assist
        _scrollRect.decelerationRate = 0.135f;  // standard feel
        _scrollRect.scrollSensitivity = 35f;
    }

    private void LateUpdate()
    {
        if (_scrollRect == null) return;

        // Clamp vertical position every frame
        float v = _scrollRect.verticalNormalizedPosition;
        float clamped = Mathf.Clamp(v, minScrollPos, maxScrollPos);
        if (!Mathf.Approximately(v, clamped))
            _scrollRect.verticalNormalizedPosition = clamped;
    }

    // ─────────────────────────────────────────────────────────
    // PUBLIC API
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Smoothly scrolls the map so that 'targetNode' is visible and centred.
    /// Called by MapManager.ScrollToHighestUnlocked().
    /// </summary>
    public void ScrollToNode(RectTransform targetNode, RectTransform content)
    {
        if (targetNode == null || content == null || _scrollRect == null) return;

        _snapTween?.Kill();

        Canvas.ForceUpdateCanvases();

        float contentHeight = content.rect.height;
        if (contentHeight <= 0f) return;

        // Convert node local position to scroll value
        float nodeY      = Mathf.Abs(targetNode.anchoredPosition.y);
        float viewportH  = _scrollRect.viewport.rect.height;
        float scrollable = contentHeight - viewportH;
        if (scrollable <= 0f) return;

        float targetPos = Mathf.Clamp01(1f - (nodeY - viewportH * 0.5f) / scrollable);

        float start = _scrollRect.verticalNormalizedPosition;
        _snapTween = DOTween
            .To(() => _scrollRect.verticalNormalizedPosition,
                 v  => _scrollRect.verticalNormalizedPosition = v,
                 targetPos,
                 snapDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true);
    }

    /// <summary>
    /// Instantly jump scroll position (no animation).
    /// </summary>
    public void JumpTo(float normalizedPos)
    {
        _snapTween?.Kill();
        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = Mathf.Clamp01(normalizedPos);
    }

    private void OnDestroy() => _snapTween?.Kill();
}
