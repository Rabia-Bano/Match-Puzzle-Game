// ============================================================
//  JuiceManager.cs  —  MonoBehaviour, DontDestroyOnLoad singleton
//
//  Small "game feel" helper: camera shake + full-screen colour flash.
//  Same singleton pattern as AudioManager / GameManager.
//
//  ShakeCamera() moves the CURRENT Camera.main's local position
//  around its original position for `duration` seconds, then snaps
//  it back exactly — safe to call while the camera is also being
//  used for normal gameplay (it caches the starting position itself,
//  so it doesn't fight with anything else that reads camera position
//  between shakes).
//
//  FlashScreen() needs a full-screen UI Image assigned in the
//  Inspector (see setup notes below) — it fades that Image's alpha
//  up to the given colour and back down over `duration` seconds.
//
//  Attach to: an empty GameObject named "JuiceManager" in
//  PreloaderScene (same place as AudioManager).
// ============================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class JuiceManager : MonoBehaviour
{
    public static JuiceManager Instance { get; private set; }

    [Header("Screen Flash")]
    [Tooltip("Full-screen UI Image, transparent by default, stretched to fill the " +
             "screen. Lives on a Canvas with a high Sort Order so it draws over " +
             "everything. See setup notes for exactly how to build this.")]
    [SerializeField] private Image flashOverlay;

    // Tracks how many ShakeCamera() calls are currently in-flight, so
    // overlapping shakes (e.g. a match shake firing again before the
    // first finishes) don't fight over "what is the original position".
    private int _activeShakes;
    private Vector3 _cameraRestPosition;
    private Transform _cameraTransform;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (flashOverlay != null)
        {
            var c = flashOverlay.color;
            flashOverlay.color = new Color(c.r, c.g, c.b, 0f);
            flashOverlay.raycastTarget = false; // never blocks touch input
        }
    }

    // ─────────────────────────────────────────────────────
    //  CAMERA SHAKE
    // ─────────────────────────────────────────────────────

    /// <summary>
    /// Shakes Camera.main for `duration` seconds with the given `magnitude`
    /// (world units for a 2D/orthographic camera). Safe to call multiple
    /// times back-to-back — the camera always ends up back at its true
    /// resting position once every overlapping shake has finished.
    /// Usage: StartCoroutine(JuiceManager.Instance.ShakeCamera(0.2f, 0.15f));
    /// </summary>
    public IEnumerator ShakeCamera(float duration, float magnitude)
    {
        Camera cam = Camera.main;
        if (cam == null) yield break;

        // First shake to start records the true rest position; later
        // overlapping shakes just extend the effect around that same anchor.
        if (_activeShakes == 0)
        {
            _cameraTransform = cam.transform;
            _cameraRestPosition = _cameraTransform.localPosition;
        }

        _activeShakes++;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float damper = 1f - Mathf.Clamp01(elapsed / duration); // ease out
            Vector2 offset = Random.insideUnitCircle * magnitude * damper;
            _cameraTransform.localPosition = _cameraRestPosition + new Vector3(offset.x, offset.y, 0f);
            yield return null;
        }

        _activeShakes--;
        if (_activeShakes <= 0)
        {
            _activeShakes = 0;
            _cameraTransform.localPosition = _cameraRestPosition;
        }
    }

    /// <summary>Convenience overload so callers don't need their own StartCoroutine boilerplate.</summary>
    public void Shake(float duration, float magnitude)
    {
        StartCoroutine(ShakeCamera(duration, magnitude));
    }

    // ─────────────────────────────────────────────────────
    //  SCREEN FLASH
    // ─────────────────────────────────────────────────────

    /// <summary>
    /// Fades the full-screen overlay up to `color` and back to transparent
    /// over `duration` seconds total (half up, half down). Requires
    /// `flashOverlay` to be assigned in the Inspector — logs a warning and
    /// does nothing otherwise.
    /// </summary>
    public void FlashScreen(Color color, float duration)
    {
        if (flashOverlay == null)
        {
            Debug.LogWarning("[JuiceManager] FlashScreen called but flashOverlay is not assigned.");
            return;
        }

        flashOverlay.DOKill();
        Color target = new Color(color.r, color.g, color.b, color.a > 0f ? color.a : 0.5f);
        Color clear  = new Color(color.r, color.g, color.b, 0f);

        flashOverlay.color = clear;
        Sequence seq = DOTween.Sequence();
        seq.Append(flashOverlay.DOColor(target, duration * 0.5f));
        seq.Append(flashOverlay.DOColor(clear, duration * 0.5f));
    }
}
