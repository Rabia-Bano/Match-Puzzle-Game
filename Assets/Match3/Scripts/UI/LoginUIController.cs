using System.Collections;
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
    ///
    /// ---------------------------------------------------------------
    /// UPDATED:
    ///  - New "Email Verification" panel section: shown after a
    ///    successful Register(), or after Login() finds an account
    ///    whose email isn't verified yet. Has Resend + Continue + Back
    ///    buttons. Wire the panel + buttons in the Inspector the same
    ///    way loginPanel/registerPanel are wired.
    ///  - SetLoading(true) now starts a watchdog coroutine that force-
    ///    clears the loading state after LOADING_TIMEOUT_SECONDS if no
    ///    AuthManager event ever arrives (e.g. a dropped connection or
    ///    an unexpected exception). Previously, if no OnAuthError /
    ///    OnLoginSuccess / OnRegisterSuccess ever fired, the buttons
    ///    stayed disabled and the spinner kept spinning forever — which
    ///    is what looked like the game "pausing" on a login error.
    /// ---------------------------------------------------------------
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

        [Header("Shared UI")]
        public TMP_Text errorMessageText;       // Shows validation/auth errors
        public GameObject loadingIndicator;     // Optional spinner shown during auth calls

        [Header("Panel Switching")]
        public GameObject loginPanel;
        public GameObject registerPanel;

        [Header("Scene to load after successful login/registration")]
        public string mapSceneName = "MapScene";

        [Header("Loading Watchdog")]
        [Tooltip("If no AuthManager response arrives within this many seconds, the UI unlocks itself with a timeout error instead of staying frozen.")]
        [SerializeField] private float loadingTimeoutSeconds = 15f;

        private Coroutine _loadingWatchdog;

        // -----------------------------------------------------------
        // NEW — Fix for "Resend Email makes the game pause":
        //
        // There is NO code anywhere in this project (AuthManager,
        // GameManager, GameState) that puts GameState into Paused because
        // of an auth call — that link simply does not exist in the
        // scripts. What you're actually seeing is a genuine Unity/Android
        // lifecycle event: SendEmailVerificationAsync() is a sensitive
        // Firebase Auth operation, and on some devices Google Play
        // Services shows a brief native security check (or the OS itself
        // offers to switch you to your Mail app) — either of those takes
        // focus away from the game for a moment. Unity calls
        // OnApplicationFocus(false) when that happens and
        // OnApplicationFocus(true) when you come back — during that gap
        // NOTHING in Unity runs (no Update, no coroutines), so whatever
        // was on screen looks "frozen"/"paused" the instant focus returns,
        // even though it isn't GameState.Paused.
        //
        // This is expected OS behaviour, not a bug to "turn off" — but the
        // two handlers below make sure coming back from it is always
        // smooth: (1) they log it so you can confirm this is what's
        // happening (check Logcat/Console for "regained focus"), and
        // (2) if the verification panel is open when focus returns, they
        // silently re-check verification status — so if you verified your
        // email while you were away, the game continues automatically
        // instead of waiting for another tap on "Continue".
        // -----------------------------------------------------------
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

                // Safety net: if a watchdog/loading state got left stuck by
                // the focus change, clear it before silently re-checking.
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

            // Subscribe to AuthManager events (can also be wired in Inspector instead)
            if (AuthManager.Instance != null)
            {
                AuthManager.Instance.OnLoginSuccess.AddListener(OnLoginSuccess);
                AuthManager.Instance.OnRegisterSuccess.AddListener(OnRegisterSuccess);
                AuthManager.Instance.OnAuthError.AddListener(OnAuthError);
                AuthManager.Instance.OnVerificationRequired.AddListener(OnVerificationRequired);
                AuthManager.Instance.OnVerificationEmailResent.AddListener(OnVerificationEmailResent);
                AuthManager.Instance.OnSessionResumeFailed.AddListener(OnSessionResumeFailed);
            }

            // NEW — "stay logged in across app restarts": Firebase already
            // persists the session on-device on its own; this is what actually
            // makes use of that instead of always forcing a fresh manual login.
            // If a previous session exists, show the loading spinner over the
            // Login form for a moment while it's checked — the three possible
            // outcomes (auto-login, show verification panel, or show a banned/
            // error message) all arrive through the exact same events wired
            // just above, so nothing else needs to change.
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
            }

            StopWatchdog();
        }

        // -----------------------------------------------------------
        // Panel Switching (wire to LoginTabButton / RegisterTabButton OnClick)
        // -----------------------------------------------------------

        public void ShowLoginPanel()
        {
            if (loginPanel != null) loginPanel.SetActive(true);
            if (registerPanel != null) registerPanel.SetActive(false);
            if (verificationPanel != null) verificationPanel.SetActive(false);
            ClearError();
        }

        public void ShowRegisterPanel()
        {
            if (loginPanel != null) loginPanel.SetActive(false);
            if (registerPanel != null) registerPanel.SetActive(true);
            if (verificationPanel != null) verificationPanel.SetActive(false);
            ClearError();
        }

        private void ShowVerificationPanel(string email)
        {
            if (loginPanel != null) loginPanel.SetActive(false);
            if (registerPanel != null) registerPanel.SetActive(false);
            if (verificationPanel != null) verificationPanel.SetActive(true);

            if (verificationEmailText != null)
                verificationEmailText.text = string.IsNullOrEmpty(email)
                    ? "Please verify your email address."
                    : $"We sent a verification link to {email}.\nPlease check your inbox (and Spam folder).";

            if (verificationResendConfirmText != null)
                verificationResendConfirmText.gameObject.SetActive(false);

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
            string confirmPassword = registerConfirmPasswordInput != null ? registerConfirmPasswordInput.text : password;

            AuthManager.Instance.Register(username, email, password, confirmPassword);
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
            AuthManager.Instance.Logout();   // signs out + fires OnPlayerLoggedOut -> GameManager returns to Login state
            ShowLoginPanel();
        }

        // -----------------------------------------------------------
        // AuthManager Event Callbacks
        // -----------------------------------------------------------

        private void OnLoginSuccess()
        {
            StopWatchdog();
            SetLoading(false);
            Debug.Log("[LoginUIController] Login success -> GameManager will load Map.");
            // GameManager.HandlePlayerLoggedIn() fires on GameEvents.OnPlayerLoggedIn
            // and calls ChangeState(Map) -> SceneLoader loads MapScene automatically.
            // AuthManager already fired GameEvents.OnPlayerLoggedIn before calling this.
        }

        private void OnRegisterSuccess()
        {
            StopWatchdog();
            SetLoading(false);
            Debug.Log("[LoginUIController] Registration success -> GameManager will load Map.");
            // Same as OnLoginSuccess — GameEvents.OnPlayerLoggedIn already fired.
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

        /// <summary>NEW — the previously-missing "unstuck" path: a persisted
        /// session existed but couldn't be refreshed (e.g. no internet on cold
        /// launch). Quietly stop loading and show the normal Login form — no
        /// error message, since nothing is actually broken, the player just
        /// needs to log in manually this time.</summary>
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

            if (verificationContinueButton != null)
                verificationContinueButton.interactable = !isLoading;

            // NEW — watchdog: if isLoading turns on, start a timer that force-clears
            // it if AuthManager never calls back. This is the safety net for issue #2 —
            // no matter what edge case causes a callback to be dropped, the UI can
            // never stay stuck in a "frozen" state again.
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