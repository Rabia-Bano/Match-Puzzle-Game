// ============================================================
//  PasswordVisibilityToggle.cs  —  MonoBehaviour
//
//  NEW (Rabia's request) — a small reusable "eye" icon toggle for any
//  password TMP_InputField. Attach this script directly to the eye-icon
//  Button GameObject that sits at the side of a password field, and point
//  targetInput at that field — nothing else needs to change.
//
//  Reused on all 5 password fields in the game:
//    • Login screen        → loginPasswordInput
//    • Register screen     → registerPasswordInput, registerConfirmPasswordInput
//    • Guest Register popup → regPasswordInput, regConfirmInput
//  (each gets its OWN eye-icon Button + this script, with targetInput
//  pointing at that specific field — this script doesn't know or care
//  which screen it's on.)
//
//  Hierarchy expected: the eye icon is a Button (with an Image) sitting
//  inside/beside the password TMP_InputField, e.g.:
//    PasswordField (TMP_InputField)
//      └─ EyeToggleButton (Button, this script)  ← Image child shows the sprite
// ============================================================

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
        [SerializeField] private Sprite eyeOpenSprite;    // shown when password IS visible
        [SerializeField] private Sprite eyeClosedSprite;  // shown when password is hidden (default)

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

            // Always start masked, regardless of what the field's Inspector
            // ContentType happened to be left at.
            _isVisible = !startHidden;
            ApplyState();
        }

        private void OnDisable()
        {
            _button?.onClick.RemoveListener(ToggleVisibility);
        }

        /// <summary>Flips between masked ("•••••") and plain-text password display.</summary>
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

            // Re-mask/unmask the text that's already typed, and keep the
            // caret where it was instead of jumping to the start.
            int caret = targetInput.stringPosition;
            targetInput.ForceLabelUpdate();
            targetInput.stringPosition = caret;

            if (eyeIcon != null)
                eyeIcon.sprite = _isVisible ? eyeOpenSprite : eyeClosedSprite;
        }
    }
}
