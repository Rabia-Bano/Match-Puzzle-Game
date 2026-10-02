using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.Firebase
{
    public class LoginUIController : MonoBehaviour
    {
        [Header("Login Panel Fields")]
        public TMP_InputField loginEmailInput;
        public TMP_InputField loginPasswordInput;
        public Button loginButton;
        public Button guestButton;

        [Header("Register Panel Fields")]
        public TMP_InputField registerUsernameInput;
        public TMP_InputField registerEmailInput;
        public TMP_InputField registerPasswordInput;
        [Tooltip("NEW — 'Confirm Password' field. Without this wired up, Register() has no confirmPassword value to compare against and will reject the attempt — wire it in the Inspector.")]
        public TMP_InputField registerConfirmPasswordInput;
        public Button registerButton;

        [Header("Email Verification Panel (NEW)")]
        [Tooltip("Shown after Register(), or when Login() finds an unverified account.")]
        public GameObject verificationPanel;
        [Tooltip("Optional — shows the email address being verified, e.g. 'We sent a link to abc@gmail.com'.")]
        public TMP_Text verificationEmailText;
        [Tooltip("'I've verified, continue' button — calls AuthManager.CheckEmailVerifiedAndContinue().")]
        public Button verificationContinueButton;
        [Tooltip("'Resend email' button — calls AuthManager.ResendVerificationEmail().")]
        public Button verificationResendButton;
        [Tooltip("Optional — small text ('Verification email sent again.') shown briefly after a resend.")]
        public TMP_Text verificationResendConfirmText;
        [Tooltip("'Back' button on the verification panel — logs out and returns to the Login panel.")]
        public Button verificationBackButton;

        [Header("Forgot Password Panel")]
        [Tooltip("'Forgot Password?' button/link on the LOGIN panel — opens the Forgot Password panel.")]
        public Button forgotPasswordOpenButton;
        [Tooltip("The Forgot Password panel GameObject.")]
        public GameObject forgotPasswordPanel;
        [Tooltip("Email input field inside the Forgot Password panel.")]
        public TMP_InputField forgotEmailInput;
        [Tooltip("'Send Reset Link' button — calls AuthManager.SendPasswordResetEmail().")]
        public Button forgotSendButton;
        [Tooltip("'Back' button — returns to the Login panel.")]
        public Button forgotBackButton;
        [Tooltip("Optional — message text inside the Forgot Password panel (success / error).")]
        public TMP_Text forgotMessageText;

        [Header("Shared UI")]
        public TMP_Text errorMessageText;
        public GameObject loadingIndicator;

        [Header("Panel Switching")]
        public GameObject loginPanel;
        public GameObject registerPanel;

        [Header("Scene to load after successful login/registration")]
        public string mapSceneName = "MapScene";

        [Header("Loading Watchdog")]
        [Tooltip("If no AuthManager response arrives within this many seconds, the UI unlocks itself with a timeout error instead of staying frozen.")]
        [SerializeField] private float loadingTimeoutSeconds = 15f;

        private Coroutine _loadingWatchdog;

        private bool _lostFocusWhileVerifying = false;

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                if (verificationPanel != null && verificationPanel.activeSelf)
                {
                    _lostFocusWhileVerifying = true;
                    Debug.Log("[LoginUIController] Lost focus while verification panel was open (likely a system dialog or the Mail app opening).");
                }
                return;
            }

            Debug.Log("[LoginUIController] Regained focus.");

            if (_lostFocusWhileVerifying)
            {
                _lostFocusWhileVerifying = false;

                StopWatchdog();
                SetLoading(false);

                if (verificationPanel != null && verificationPanel.activeSelf && AuthManager.Instance != null)
                {
                    Debug.Log("[LoginUIController] Auto re-checking email verification after regaining focus.");
                    AuthManager.Instance.CheckEmailVerifiedAndContinue();
                }
            }
        }

        private void Start()
        {
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

            if (verificationContinueButton != null)
            {
                verificationContinueButton.onClick.RemoveAllListeners();
                verificationContinueButton.onClick.AddListener(OnVerificationContinueClicked);
                verificationContinueButton.onClick.AddListener(() => AudioManager.Instance?.PlaySFX("button_click"));
            }

            if (verificationResendButton != null)
            {
                verificationResendButton.onClick.RemoveAllListeners();
                verificationResendButton.onClick.AddListener(OnVerificationResendClicked);
                verificationResendButton.onClick.AddListener(() => AudioManager.Instance?.PlaySFX("button_click"));
            }

            if (forgotPasswordOpenButton != null)
            {
                forgotPasswordOpenButton.onClick.RemoveAllListeners();
                forgotPasswordOpenButton.onClick.AddListener(ShowForgotPasswordPanel);
                forgotPasswordOpenButton.onClick.AddListener(() => AudioManager.Instance?.PlaySFX("button_click"));
            }

            if (forgotSendButton != null)
            {
                forgotSendButton.onClick.RemoveAllListeners();
                forgotSendButton.onClick.AddListener(OnForgotSendClicked);
                forgotSendButton.onClick.AddListener(() => AudioManager.Instance?.PlaySFX("button_click"));
            }

            if (forgotBackButton != null)
            {
                forgotBackButton.onClick.RemoveAllListeners();
                forgotBackButton.onClick.AddListener(ShowLoginPanel);
                forgotBackButton.onClick.AddListener(() => AudioManager.Instance?.PlaySFX("button_click"));
            }

            if (forgotPasswordPanel != null)
                forgotPasswordPanel.SetActive(false);

            EmailFieldUtility.ConfigureAsEmailField(loginEmailInput);
            EmailFieldUtility.ConfigureAsEmailField(registerEmailInput);
            EmailFieldUtility.ConfigureAsEmailField(forgotEmailInput);

            if (verificationBackButton != null)
            {
                verificationBackButton.onClick.RemoveAllListeners();
                verificationBackButton.onClick.AddListener(OnVerificationBackClicked);
                verificationBackButton.onClick.AddListener(() => AudioManager.Instance?.PlaySFX("button_click"));
            }

            if (errorMessageText != null)
                errorMessageText.text = "";

            if (verificationPanel != null)
                verificationPanel.SetActive(false);

            if (verificationResendConfirmText != null)
                verificationResendConfirmText.gameObject.SetActive(false);

            if (AuthManager.Instance != null)
            {
                AuthManager.Instance.OnLoginSuccess.AddListener(OnLoginSuccess);
                AuthManager.Instance.OnRegisterSuccess.AddListener(OnRegisterSuccess);
                AuthManager.Instance.OnAuthError.AddListener(OnAuthError);
                AuthManager.Instance.OnVerificationRequired.AddListener(OnVerificationRequired);
                AuthManager.Instance.OnVerificationEmailResent.AddListener(OnVerificationEmailResent);
                AuthManager.Instance.OnSessionResumeFailed.AddListener(OnSessionResumeFailed);
                AuthManager.Instance.OnPasswordResetEmailSent.AddListener(OnPasswordResetEmailSent);
            }

            if (AuthManager.IsLoggedIn)
            {
                SetLoading(true);
                AuthManager.Instance.TryResumeSession();
            }
        }

        private void OnDestroy()
        {
            if (AuthManager.Instance != null)
            {
                AuthManager.Instance.OnLoginSuccess.RemoveListener(OnLoginSuccess);
                AuthManager.Instance.OnRegisterSuccess.RemoveListener(OnRegisterSuccess);
                AuthManager.Instance.OnAuthError.RemoveListener(OnAuthError);
                AuthManager.Instance.OnVerificationRequired.RemoveListener(OnVerificationRequired);
                AuthManager.Instance.OnVerificationEmailResent.RemoveListener(OnVerificationEmailResent);
                AuthManager.Instance.OnSessionResumeFailed.RemoveListener(OnSessionResumeFailed);
                AuthManager.Instance.OnPasswordResetEmailSent.RemoveListener(OnPasswordResetEmailSent);
            }

            StopWatchdog();
        }

        public void ShowLoginPanel()
        {
            if (loginPanel != null) loginPanel.SetActive(true);
            if (registerPanel != null) registerPanel.SetActive(false);
            if (verificationPanel != null) verificationPanel.SetActive(false);
            if (forgotPasswordPanel != null) forgotPasswordPanel.SetActive(false);
            ClearError();
        }

        public void ShowRegisterPanel()
        {
            if (loginPanel != null) loginPanel.SetActive(false);
            if (registerPanel != null) registerPanel.SetActive(true);
            if (verificationPanel != null) verificationPanel.SetActive(false);
            if (forgotPasswordPanel != null) forgotPasswordPanel.SetActive(false);
            ClearError();
        }

        private void ShowVerificationPanel(string email)
        {
            if (loginPanel != null) loginPanel.SetActive(false);
            if (registerPanel != null) registerPanel.SetActive(false);
            if (verificationPanel != null) verificationPanel.SetActive(true);
            if (forgotPasswordPanel != null) forgotPasswordPanel.SetActive(false);

            if (verificationEmailText != null)
                verificationEmailText.text = string.IsNullOrEmpty(email)
                    ? "Please verify your email address."
                    : $"We sent a verification link to {email}.\nPlease check your inbox (and Spam folder).";

            if (verificationResendConfirmText != null)
                verificationResendConfirmText.gameObject.SetActive(false);

            ClearError();
        }

        public void ShowForgotPasswordPanel()
        {
            if (loginPanel != null) loginPanel.SetActive(false);
            if (registerPanel != null) registerPanel.SetActive(false);
            if (verificationPanel != null) verificationPanel.SetActive(false);
            if (forgotPasswordPanel != null) forgotPasswordPanel.SetActive(true);

            if (forgotEmailInput != null && loginEmailInput != null &&
                string.IsNullOrWhiteSpace(forgotEmailInput.text))
                forgotEmailInput.text = loginEmailInput.text.Trim();

            if (forgotMessageText != null) forgotMessageText.text = "";
            ClearError();
        }

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
            string confirmPassword = registerConfirmPasswordInput != null ? registerConfirmPasswordInput.text : password;

            AuthManager.Instance.Register(username, email, password, confirmPassword);
        }

        public void OnForgotSendClicked()
        {
            if (forgotEmailInput == null)
            {
                Debug.LogError("[LoginUIController] forgotEmailInput is not assigned in the Inspector.");
                return;
            }

            SetLoading(true);
            ClearError();
            if (forgotMessageText != null) forgotMessageText.text = "";

            AuthManager.Instance.SendPasswordResetEmail(forgotEmailInput.text.Trim());
        }

        public void OnVerificationContinueClicked()
        {
            SetLoading(true);
            ClearError();
            AuthManager.Instance.CheckEmailVerifiedAndContinue();
        }

        public void OnVerificationResendClicked()
        {
            if (verificationResendButton != null) verificationResendButton.interactable = false;
            AuthManager.Instance.ResendVerificationEmail();
        }

        public void OnVerificationBackClicked()
        {
            StopWatchdog();
            SetLoading(false);
            AuthManager.Instance.Logout();
            ShowLoginPanel();
        }

        private void OnLoginSuccess()
        {
            StopWatchdog();
            SetLoading(false);
            Debug.Log("[LoginUIController] Login success -> GameManager will load Map.");
        }

        private void OnRegisterSuccess()
        {
            StopWatchdog();
            SetLoading(false);
            Debug.Log("[LoginUIController] Registration success -> GameManager will load Map.");
        }

        private void OnVerificationRequired(string email)
        {
            StopWatchdog();
            SetLoading(false);
            ShowVerificationPanel(email);
        }

        private void OnVerificationEmailResent()
        {
            if (verificationResendButton != null) verificationResendButton.interactable = true;

            if (verificationResendConfirmText != null)
            {
                verificationResendConfirmText.text = "Verification email sent again.";
                verificationResendConfirmText.gameObject.SetActive(true);
            }
        }

        private void OnPasswordResetEmailSent(string email)
        {
            StopWatchdog();
            SetLoading(false);

            string msg = $"If an account exists for {email}, a password reset link has been sent.\nPlease check your inbox (and Spam folder).";
            if (forgotMessageText != null) forgotMessageText.text = msg;
            else if (errorMessageText != null) errorMessageText.text = msg;
        }

        private void OnSessionResumeFailed()
        {
            StopWatchdog();
            SetLoading(false);
            Debug.Log("[LoginUIController] Session resume failed — showing Login form normally.");
        }

        private void OnAuthError(string message)
        {
            StopWatchdog();
            SetLoading(false);

            if (verificationResendButton != null) verificationResendButton.interactable = true;

            if (errorMessageText != null)
                errorMessageText.text = message;

            if (forgotPasswordPanel != null && forgotPasswordPanel.activeSelf && forgotMessageText != null)
                forgotMessageText.text = message;

            Debug.LogWarning($"[LoginUIController] Auth error: {message}");
        }

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

            if (verificationContinueButton != null)
                verificationContinueButton.interactable = !isLoading;

            if (forgotSendButton != null)
                forgotSendButton.interactable = !isLoading;

            StopWatchdog();
            if (isLoading)
                _loadingWatchdog = StartCoroutine(LoadingWatchdogRoutine());
        }

        private IEnumerator LoadingWatchdogRoutine()
        {
            yield return new WaitForSecondsRealtime(loadingTimeoutSeconds);

            Debug.LogWarning("[LoginUIController] Loading watchdog fired — no response from AuthManager in time. Force-unlocking UI.");
            SetLoading(false);

            if (errorMessageText != null)
                errorMessageText.text = "Request timed out. Check your internet connection and try again.";
        }

        private void StopWatchdog()
        {
            if (_loadingWatchdog != null)
            {
                StopCoroutine(_loadingWatchdog);
                _loadingWatchdog = null;
            }
        }
    }
}
