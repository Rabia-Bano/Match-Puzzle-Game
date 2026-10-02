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
            flashOverlay.raycastTarget = false;
        }
    }

    public IEnumerator ShakeCamera(float duration, float magnitude)
    {
        Camera cam = Camera.main;
        if (cam == null) yield break;

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
            float damper = 1f - Mathf.Clamp01(elapsed / duration);
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

    public void Shake(float duration, float magnitude)
    {
        StartCoroutine(ShakeCamera(duration, magnitude));
    }

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
