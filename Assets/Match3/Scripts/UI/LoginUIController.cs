using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.Firebase
{
    /// <summary>
    /// Connects the Login/Register screen UI elements to AuthManager.
    /// Attach this to a GameObject in your LoginScene UI Canvas
    /// (e.g. on the "AuthContainer" root object) and assign the
    /// references in the Inspector.
    ///
    /// NOTE: Uses TextMeshPro (TMP_InputField / TMP_Text) because
    /// Unity 6's default UI > Input Field / UI > Text creates TMP
    /// components, not the legacy UnityEngine.UI versions.
    /// </summary>
    public class LoginUIController : MonoBehaviour
    {
        [Header("Login Panel Fields")]
        public TMP_InputField loginEmailInput;
        public TMP_InputField loginPasswordInput;
        public Button loginButton;
        public Button guestButton;          // "Play as Guest"

        [Header("Register Panel Fields")]
        public TMP_InputField registerUsernameInput;
        public TMP_InputField registerEmailInput;
        public TMP_InputField registerPasswordInput;
        public Button registerButton;

        [Header("Shared UI")]
        public TMP_Text errorMessageText;       // Shows validation/auth errors
        public GameObject loadingIndicator;     // Optional spinner shown during auth calls

        [Header("Panel Switching")]
        public GameObject loginPanel;
        public GameObject registerPanel;

        [Header("Scene to load after successful login/registration")]
        public string mapSceneName = "MapScene";

        private void Start()
        {
            // RemoveAllListeners first — prevents duplicate registration
            // if Start() is called more than once (scene reload edge case)
            if (loginButton != null)
            {
                loginButton.onClick.RemoveAllListeners();
                loginButton.onClick.AddListener(OnLoginButtonClicked);
                loginButton.onClick.AddListener(() => AudioManager.Instance?.PlaySFX("button_click"));
            }

            if (guestButton != null)
            {
                guestButton.onClick.RemoveAllListeners();
                guestButton.onClick.AddListener(OnGuestButtonClicked);
                guestButton.onClick.AddListener(() => AudioManager.Instance?.PlaySFX("button_click"));
            }

            if (registerButton != null)
            {
                registerButton.onClick.RemoveAllListeners();
                registerButton.onClick.AddListener(OnRegisterButtonClicked);
                registerButton.onClick.AddListener(() => AudioManager.Instance?.PlaySFX("button_click"));
            }

            if (errorMessageText != null)
                errorMessageText.text = "";

            // Subscribe to AuthManager events (can also be wired in Inspector instead)
            if (AuthManager.Instance != null)
            {
                AuthManager.Instance.OnLoginSuccess.AddListener(OnLoginSuccess);
                AuthManager.Instance.OnRegisterSuccess.AddListener(OnRegisterSuccess);
                AuthManager.Instance.OnAuthError.AddListener(OnAuthError);
            }
        }

        private void OnDestroy()
        {
            if (AuthManager.Instance != null)
            {
                AuthManager.Instance.OnLoginSuccess.RemoveListener(OnLoginSuccess);
                AuthManager.Instance.OnRegisterSuccess.RemoveListener(OnRegisterSuccess);
                AuthManager.Instance.OnAuthError.RemoveListener(OnAuthError);
            }
        }

        // -----------------------------------------------------------
        // Panel Switching (wire to LoginTabButton / RegisterTabButton OnClick)
        // -----------------------------------------------------------

        public void ShowLoginPanel()
        {
            if (loginPanel != null) loginPanel.SetActive(true);
            if (registerPanel != null) registerPanel.SetActive(false);
            ClearError();
        }

        public void ShowRegisterPanel()
        {
            if (loginPanel != null) loginPanel.SetActive(false);
            if (registerPanel != null) registerPanel.SetActive(true);
            ClearError();
        }

        // -----------------------------------------------------------
        // Button Handlers
        // -----------------------------------------------------------

        public void OnLoginButtonClicked()
        {
            SetLoading(true);
            ClearError();

            string email = loginEmailInput.text.Trim();
            string password = loginPasswordInput.text;

            AuthManager.Instance.Login(email, password);
        }

        public void OnGuestButtonClicked()
        {
            // Guard against double-click (was causing 2 guest UIDs)
            if (guestButton != null && !guestButton.interactable) return;
            SetLoading(true);
            ClearError();
            AuthManager.Instance.LoginAsGuest();
        }

        public void OnRegisterButtonClicked()
        {
            SetLoading(true);
            ClearError();

            string username = registerUsernameInput.text.Trim();
            string email = registerEmailInput.text.Trim();
            string password = registerPasswordInput.text;

            AuthManager.Instance.Register(username, email, password);
        }

        // -----------------------------------------------------------
        // AuthManager Event Callbacks
        // -----------------------------------------------------------

        private void OnLoginSuccess()
        {
            SetLoading(false);
            Debug.Log("[LoginUIController] Login success -> GameManager will load Map.");
            // GameManager.HandlePlayerLoggedIn() fires on GameEvents.OnPlayerLoggedIn
            // and calls ChangeState(Map) -> SceneLoader loads MapScene automatically.
            // AuthManager already fired GameEvents.OnPlayerLoggedIn before calling this.
        }

        private void OnRegisterSuccess()
        {
            SetLoading(false);
            Debug.Log("[LoginUIController] Registration success -> GameManager will load Map.");
            // Same as OnLoginSuccess — GameEvents.OnPlayerLoggedIn already fired.
        }

        private void OnAuthError(string message)
        {
            SetLoading(false);

            if (errorMessageText != null)
                errorMessageText.text = message;

            Debug.LogWarning($"[LoginUIController] Auth error: {message}");
        }

        // -----------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------

        private void ClearError()
        {
            if (errorMessageText != null)
                errorMessageText.text = "";
        }

        private void SetLoading(bool isLoading)
        {
            if (loadingIndicator != null)
                loadingIndicator.SetActive(isLoading);

            if (loginButton != null)
                loginButton.interactable = !isLoading;

            if (guestButton != null)
                guestButton.interactable = !isLoading;

            if (registerButton != null)
                registerButton.interactable = !isLoading;
        }
    }
}