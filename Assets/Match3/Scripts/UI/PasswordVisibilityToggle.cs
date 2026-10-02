using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    public class PasswordVisibilityToggle : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The password TMP_InputField this eye icon controls.")]
        [SerializeField] private TMP_InputField targetInput;

        [Header("Icon (optional — leave blank for a text-only toggle)")]
        [Tooltip("The eye icon's own Image component (can be on this GameObject or a child).")]
        [SerializeField] private Image eyeIcon;
        [SerializeField] private Sprite eyeOpenSprite;
        [SerializeField] private Sprite eyeClosedSprite;

        [Header("Behaviour")]
        [Tooltip("Start hidden (masked) like every other site — should normally stay true.")]
        [SerializeField] private bool startHidden = true;

        private Button _button;
        private bool   _isVisible;

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (_button == null)
                Debug.LogWarning("[PasswordVisibilityToggle] No Button component on this GameObject — " +
                                  "add one so the eye icon is actually tappable.", this);

            if (targetInput == null)
                Debug.LogError("[PasswordVisibilityToggle] targetInput not assigned — " +
                                "this eye icon won't do anything.", this);
        }

        private void OnEnable()
        {
            _button?.onClick.AddListener(ToggleVisibility);

            _isVisible = !startHidden;
            ApplyState();
        }

        private void OnDisable()
        {
            _button?.onClick.RemoveListener(ToggleVisibility);
        }

        public void ToggleVisibility()
        {
            if (targetInput == null) return;

            _isVisible = !_isVisible;
            ApplyState();

            AudioManager.Instance?.PlaySFX("button_click");
        }

        private void ApplyState()
        {
            if (targetInput == null) return;

            targetInput.contentType = _isVisible
                ? TMP_InputField.ContentType.Standard
                : TMP_InputField.ContentType.Password;

            int caret = targetInput.stringPosition;
            targetInput.ForceLabelUpdate();
            targetInput.stringPosition = caret;

            if (eyeIcon != null)
                eyeIcon.sprite = _isVisible ? eyeOpenSprite : eyeClosedSprite;
        }
    }
}
