using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;

namespace Game.Firebase
{
    [Serializable]
    public class StringUnityEvent : UnityEvent<string> { }

    public class AuthManager : MonoBehaviour
    {
        public static AuthManager Instance { get; private set; }

        private bool _isRegisterBusy = false;

        private bool _isLoginBusy = false;

        private bool _isResetBusy = false;

        [Header("Auth Result Events")]
        [Tooltip("Fired after a new account + profile are created AND the email has been verified (final step — see OnVerificationRequired for the intermediate step).")]
        public UnityEvent OnRegisterSuccess;

        [Tooltip("Fired after login succeeds, the player is not banned, and the email is verified.")]
        public UnityEvent OnLoginSuccess;

        [Tooltip("Fired after logout completes.")]
        public UnityEvent OnLogoutComplete;

        [Tooltip("Fired with a user-friendly error message on any failure.")]
        public StringUnityEvent OnAuthError;

        [Tooltip("NEW — fired right after Register() creates the account (or after Login() finds an unverified account). Passes the email address. UI should show a 'check your inbox' panel with Resend + Continue buttons here — do NOT navigate to Map yet.")]
        public StringUnityEvent OnVerificationRequired;

        [Tooltip("NEW — fired after ResendVerificationEmail() successfully re-sends the mail.")]
        public UnityEvent OnVerificationEmailResent;

        [Tooltip("NEW — fired when TryResumeSession() cannot finish the check (e.g. no internet on a cold launch). UI should just quietly stop loading and show the normal Login form — no scary error, since nothing is actually wrong.")]
        public UnityEvent OnSessionResumeFailed;

        [Tooltip("FORGOT PASSWORD — fired after SendPasswordResetEmail() succeeds. Passes the email address the reset link was sent to.")]
        public StringUnityEvent OnPasswordResetEmailSent;

        private FirebaseAuth _auth;
        private FirebaseFirestore _firestore;
        private FirebaseUser _currentUser;

        private string _pendingEmail;
        private string _pendingUsername;
        private bool   _pendingIsGuestUpgrade;

        private const string PREF_PENDING_UID      = "AuthManager_PendingUid";
        private const string PREF_PENDING_USERNAME = "AuthManager_PendingUsername";
        private const string PREF_PENDING_EMAIL    = "AuthManager_PendingEmail";
        private const string PREF_PENDING_IS_GUEST = "AuthManager_PendingIsGuestUpgrade";

        private void SavePendingRegistration(string uid, string username, string email, bool isGuestUpgrade)
        {
            PlayerPrefs.SetString(PREF_PENDING_UID, uid);
            PlayerPrefs.SetString(PREF_PENDING_USERNAME, username);
            PlayerPrefs.SetString(PREF_PENDING_EMAIL, email);
            PlayerPrefs.SetInt(PREF_PENDING_IS_GUEST, isGuestUpgrade ? 1 : 0);
            PlayerPrefs.Save();

            _pendingUsername = username;
            _pendingEmail = email;
            _pendingIsGuestUpgrade = isGuestUpgrade;
        }

        private void ClearPendingRegistration()
        {
            PlayerPrefs.DeleteKey(PREF_PENDING_UID);
            PlayerPrefs.DeleteKey(PREF_PENDING_USERNAME);
            PlayerPrefs.DeleteKey(PREF_PENDING_EMAIL);
            PlayerPrefs.DeleteKey(PREF_PENDING_IS_GUEST);
            PlayerPrefs.Save();
        }

        private void LoadPendingRegistrationIfNeeded()
        {
            if (_currentUser == null) return;
            if (!string.IsNullOrEmpty(_pendingUsername) && !string.IsNullOrEmpty(_pendingEmail)) return;

            string savedUid = PlayerPrefs.GetString(PREF_PENDING_UID, "");
            if (string.IsNullOrEmpty(savedUid) || savedUid != _currentUser.UserId)
            {
                Debug.LogWarning("[AuthManager] No matching saved pending-registration data found for this account.");
                return;
            }

            _pendingUsername = PlayerPrefs.GetString(PREF_PENDING_USERNAME, "");
            _pendingEmail = PlayerPrefs.GetString(PREF_PENDING_EMAIL, "");
            _pendingIsGuestUpgrade = PlayerPrefs.GetInt(PREF_PENDING_IS_GUEST, 0) == 1;
            Debug.Log("[AuthManager] Restored pending registration data from PlayerPrefs after an app restart.");
        }

        public static FirebaseUser CurrentUser => Instance != null ? Instance._currentUser : null;

        public static bool IsLoggedIn => Instance != null && Instance._currentUser != null;

        private const string PLAYERS_COLLECTION = "players";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Initialize()
        {
            if (!FirebaseInitializer.IsReady)
            {
                Debug.LogError("[AuthManager] Firebase is not ready. Cannot initialize AuthManager.");
                return;
            }

            _auth = FirebaseAuth.DefaultInstance;
            _firestore = FirebaseFirestore.DefaultInstance;
            _currentUser = _auth.CurrentUser;

            Debug.Log("[AuthManager] Initialized successfully.");
        }

        public void Register(string username, string email, string password, string confirmPassword)
        {
            if (_isRegisterBusy) { OnAuthError?.Invoke("Please wait..."); return; }

            if (_auth == null)
            {
                OnAuthError?.Invoke("Firebase is not ready. Check your internet connection.");
                return;
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                OnAuthError?.Invoke("Please enter a username.");
                return;
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                OnAuthError?.Invoke("Please enter an email address.");
                return;
            }

            if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            {
                OnAuthError?.Invoke("Password must be at least 6 characters.");
                return;
            }

            if (password != confirmPassword)
            {
                OnAuthError?.Invoke("Password and Confirm Password do not match.");
                return;
            }

            _isRegisterBusy = true;

            try
            {
                _auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task =>
                {
                    _isRegisterBusy = false;

                    if (task.IsCanceled || task.IsFaulted)
                    {
                        string message = GetFirebaseErrorMessage(task.Exception);
                        Debug.LogError($"[AuthManager] Register failed: {message}");
                        OnAuthError?.Invoke(message);
                        return;
                    }

                    AuthResult result = task.Result;
                    _currentUser = result.User;

                    Debug.Log($"[AuthManager] Account created for UID: {_currentUser.UserId}");

                    SavePendingRegistration(_currentUser.UserId, username, email, isGuestUpgrade: false);

                    SendVerificationEmailInternal(email, isFirstSend: true);
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AuthManager] Register threw synchronously: {ex}");
                _isRegisterBusy = false;
                OnAuthError?.Invoke("Something happened wrong. Try again.");
            }
        }

        private void CreatePlayerProfile(string uid, string username, string email)
        {
            DocumentReference docRef = _firestore.Collection(PLAYERS_COLLECTION).Document(uid);

            Dictionary<string, object> profileData = new Dictionary<string, object>
            {
                { "username", username },
                { "displayName", username },
                { "email", email },
                { "joinDate", Timestamp.GetCurrentTimestamp() },
                { "lastLogin", Timestamp.GetCurrentTimestamp() },
                { "lastUpdated", DateTime.UtcNow.ToString("o") },
                { "totalScore", 0 },
                { "highestLevel", 1 },
                { "levelsCompleted", 0 },
                { "coins", 0 },
                { "pets", new List<string>() },
                { "isBanned", false },
                { "emailVerified", true }
            };

            docRef.SetAsync(profileData).ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    Debug.LogError($"[AuthManager] Failed to create player profile: {task.Exception}");
                    OnAuthError?.Invoke("Something went wrong while creating your profile. Please try again.");
                    return;
                }

                Debug.Log("[AuthManager] Player profile created successfully.");
            });
        }

        private void SendVerificationEmailInternal(string email, bool isFirstSend)
        {
            if (_currentUser == null)
            {
                OnAuthError?.Invoke("Your session was lost. Please login or register again.");
                return;
            }

            if (isFirstSend)
            {
                DoSendVerificationEmail(email, isFirstSend: true, previousResendCount: 0);
                return;
            }

            string uid = _currentUser.UserId;
            VerificationRequestTracker.CheckCanResend(uid, (decision, count) =>
            {
                switch (decision)
                {
                    case VerificationRequestTracker.ResendDecision.Banned:
                        OnAuthError?.Invoke("Your account has been banned. You can't request more verification emails.");
                        SignOutInternal();
                        return;

                    case VerificationRequestTracker.ResendDecision.LimitReached:
                        OnAuthError?.Invoke($"You have already requested {count} verification emails. " +
                                            "Please verify using the email already sent (check Spam too). " +
                                            "Your account has been sent to the admin for review.");
                        return;

                    case VerificationRequestTracker.ResendDecision.NetworkError:
                        OnAuthError?.Invoke("Could not send the verification email. Check your internet connection and try again.");
                        return;

                    default:
                        DoSendVerificationEmail(email, isFirstSend: false, previousResendCount: count);
                        return;
                }
            });
        }

        private void DoSendVerificationEmail(string email, bool isFirstSend, int previousResendCount)
        {
            if (_currentUser == null) return;
            FirebaseUser user = _currentUser;

            user.SendEmailVerificationAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    Debug.LogError($"[AuthManager] Sending verification email failed: {task.Exception}");
                    string msg = GetFirebaseErrorMessage(task.Exception);
                    if (!msg.StartsWith("Too many"))
                        msg = "Could not send the verification email. Check your internet connection and try again.";
                    OnAuthError?.Invoke(msg);
                    return;
                }

                Debug.Log($"[AuthManager] Verification email sent to {email}.");

                LoadPendingRegistrationIfNeeded();
                VerificationRequestTracker.RecordSend(user.UserId, email, _pendingUsername, isFirstSend, previousResendCount);

                if (isFirstSend)
                    RaiseVerificationRequired(email);
                else
                    OnVerificationEmailResent?.Invoke();
            });
        }

        private void ShowVerificationPanelUnlessBanned(string email)
        {
            if (_currentUser == null) return;
            VerificationRequestTracker.CheckBanned(_currentUser.UserId, (banned, reason) =>
            {
                if (banned)
                {
                    Debug.LogWarning("[AuthManager] Unverified account is banned by admin.");
                    OnAuthError?.Invoke(string.IsNullOrEmpty(reason)
                        ? "Your account has been banned."
                        : $"Your account has been banned. Reason: {reason}");
                    SignOutInternal();
                    return;
                }
                RaiseVerificationRequired(email);
            });
        }

        public void TryResumeSession()
        {
            if (_currentUser == null)
            {
                Debug.Log("[AuthManager] No persisted session found — showing Login screen normally.");
                return;
            }

            Debug.Log($"[AuthManager] Persisted session found for UID: {_currentUser.UserId} — attempting to resume.");

            if (_currentUser.IsAnonymous)
            {
                CheckBanStatusAndProceed(_currentUser.UserId);
                return;
            }

            _currentUser.ReloadAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    Debug.LogWarning($"[AuthManager] Could not refresh persisted session, falling back to manual login: {task.Exception}");
                    OnSessionResumeFailed?.Invoke();
                    return;
                }

                if (!_currentUser.IsEmailVerified)
                {
                    Debug.Log("[AuthManager] Persisted session's email is still unverified.");
                    ShowVerificationPanelUnlessBanned(_currentUser.Email);
                    return;
                }

                CheckBanStatusAndProceed(_currentUser.UserId);
            });
        }

        public void ResendVerificationEmail()
        {
            string email = _currentUser != null ? _currentUser.Email : _pendingEmail;
            SendVerificationEmailInternal(email, isFirstSend: false);
        }

        public void CheckEmailVerifiedAndContinue() => CheckVerifiedInternal(silent: false);

        [Header("Verification auto-check (NEW)")]
        [Tooltip("While the 'verify your email' panel is open, check every N seconds.")]
        [SerializeField] private float verificationPollSeconds = 4f;

        private bool      _awaitingVerification;
        private bool      _verifyCheckBusy;
        private Coroutine _verifyPollRoutine;

        public bool IsAwaitingVerification => _awaitingVerification;

        private void RaiseVerificationRequired(string email)
        {
            OnVerificationRequired?.Invoke(email);
            _awaitingVerification = true;
            if (_verifyPollRoutine != null) StopCoroutine(_verifyPollRoutine);
            _verifyPollRoutine = StartCoroutine(VerificationPollLoop());
        }

        private void StopVerificationPolling()
        {
            _awaitingVerification = false;
            if (_verifyPollRoutine != null) StopCoroutine(_verifyPollRoutine);
            _verifyPollRoutine = null;
        }

        private System.Collections.IEnumerator VerificationPollLoop()
        {
            var wait = new WaitForSecondsRealtime(Mathf.Max(2f, verificationPollSeconds));
            while (_awaitingVerification && _currentUser != null)
            {
                yield return wait;
                if (_awaitingVerification && !_verifyCheckBusy) CheckVerifiedInternal(silent: true);
            }
            _verifyPollRoutine = null;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && _awaitingVerification && !_verifyCheckBusy && _currentUser != null)
                CheckVerifiedInternal(silent: true);
        }

        private void CheckVerifiedInternal(bool silent)
        {
            if (_currentUser == null)
            {
                if (!silent) OnAuthError?.Invoke("Your session was lost. Please login or register again.");
                StopVerificationPolling();
                return;
            }
            if (_verifyCheckBusy) return;
            _verifyCheckBusy = true;

            FirebaseUser user = _currentUser;
            user.ReloadAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    _verifyCheckBusy = false;
                    Debug.LogWarning($"[AuthManager] ReloadAsync failed: {task.Exception?.GetBaseException()?.Message}");
                    if (!silent) OnAuthError?.Invoke("Could not check your verification status. Check your internet connection and try again.");
                    return;
                }

                if (_auth?.CurrentUser != null) _currentUser = _auth.CurrentUser;

                if (_currentUser.IsEmailVerified)
                {
                    OnVerifiedDetected();
                    return;
                }

                _currentUser.TokenAsync(true).ContinueWithOnMainThread(_ =>
                {
                    FirebaseUser u2 = _auth?.CurrentUser ?? _currentUser;
                    u2.ReloadAsync().ContinueWithOnMainThread(t2 =>
                    {
                        if (_auth?.CurrentUser != null) _currentUser = _auth.CurrentUser;

                        if (_currentUser != null && _currentUser.IsEmailVerified)
                        {
                            OnVerifiedDetected();
                            return;
                        }

                        _verifyCheckBusy = false;
                        if (!silent)
                            OnAuthError?.Invoke("Your email is not verified yet. Please check your inbox (and Spam folder).");
                    });
                });
            });
        }

        private void OnVerifiedDetected()
        {
            StopVerificationPolling();
            Debug.Log("[AuthManager] Email verified — checking admin ban list before continuing.");

            VerificationRequestTracker.CheckBanned(_currentUser.UserId, (banned, reason) =>
            {
                _verifyCheckBusy = false;
                if (banned)
                {
                    OnAuthError?.Invoke(string.IsNullOrEmpty(reason)
                        ? "Your account has been banned."
                        : $"Your account has been banned. Reason: {reason}");
                    SignOutInternal();
                    return;
                }

                VerificationRequestTracker.MarkVerified(_currentUser.UserId);
                ContinueAfterVerified();
            });
        }

        private void ContinueAfterVerified()
        {
            LoadPendingRegistrationIfNeeded();

            if (string.IsNullOrEmpty(_pendingEmail))
                _pendingEmail = _currentUser.Email ?? "";
            if (string.IsNullOrEmpty(_pendingUsername))
                _pendingUsername = !string.IsNullOrEmpty(_pendingEmail) ? _pendingEmail.Split('@')[0] : "Player";

            if (_pendingIsGuestUpgrade)
            {
                Dictionary<string, object> updates = new Dictionary<string, object>
                {
                    { "username",      _pendingUsername },
                    { "displayName",   _pendingUsername },
                    { "email",         _pendingEmail },
                    { "emailVerified", true },
                    { "lastUpdated",   DateTime.UtcNow.ToString("o") }
                };

                _firestore.Collection(PLAYERS_COLLECTION).Document(_currentUser.UserId)
                    .UpdateAsync(updates).ContinueWithOnMainThread(updateTask =>
                {
                    if (updateTask.IsCanceled || updateTask.IsFaulted)
                    {
                        Debug.LogError($"[AuthManager] Profile Firestore update failed: {updateTask.Exception}");
                        OnAuthError?.Invoke("Verification hui, lekin profile save nahi ho saka. Try again.");
                        return;
                    }

                    Debug.Log("[AuthManager] Firestore profile updated with verified username/email.");
                    ClearPendingRegistration();
                    GameEvents.OnPlayerLoggedIn?.Invoke();
                    OnRegisterSuccess?.Invoke();
                });
                return;
            }

            CreatePlayerProfile(_currentUser.UserId, _pendingUsername, _pendingEmail);

            ClearPendingRegistration();

            GameEvents.OnPlayerLoggedIn?.Invoke();
            OnRegisterSuccess?.Invoke();
        }

        public void SendPasswordResetEmail(string email)
        {
            if (_isResetBusy) { OnAuthError?.Invoke("Please wait..."); return; }

            if (_auth == null)
            {
                OnAuthError?.Invoke("Firebase is not ready. Check internet.");
                return;
            }

            email = email?.Trim();
            if (string.IsNullOrWhiteSpace(email))
            {
                OnAuthError?.Invoke("Please enter your email.");
                return;
            }

            _isResetBusy = true;

            try
            {
                _auth.SendPasswordResetEmailAsync(email).ContinueWithOnMainThread(task =>
                {
                    _isResetBusy = false;

                    if (task.IsCanceled || task.IsFaulted)
                    {
                        string message = GetFirebaseErrorMessage(task.Exception);
                        Debug.LogError($"[AuthManager] Password reset failed: {message}");
                        OnAuthError?.Invoke(message);
                        return;
                    }

                    Debug.Log($"[AuthManager] Password reset email sent to {email}");
                    OnPasswordResetEmailSent?.Invoke(email);
                });
            }
            catch (Exception ex)
            {
                _isResetBusy = false;
                Debug.LogError($"[AuthManager] Password reset exception: {ex}");
                OnAuthError?.Invoke("Something happened wrong. Try again.");
            }
        }

        public void Login(string email, string password)
        {
            if (_isLoginBusy) { OnAuthError?.Invoke("Please wait..."); return; }

            if (_auth == null)
            {
                OnAuthError?.Invoke("Firebase is not ready. Check internet.");
                return;
            }

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                OnAuthError?.Invoke("Please enter your email and password.");
                return;
            }

            _isLoginBusy = true;

            try
            {
                _auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task =>
                {
                    _isLoginBusy = false;

                    if (task.IsCanceled || task.IsFaulted)
                    {
                        string message = GetFirebaseErrorMessage(task.Exception);
                        Debug.LogError($"[AuthManager] Login failed: {message}");
                        OnAuthError?.Invoke(message);
                        return;
                    }

                    AuthResult result = task.Result;
                    _currentUser = result.User;

                    Debug.Log($"[AuthManager] Signed in UID: {_currentUser.UserId}");

                    if (_currentUser.IsAnonymous)
                    {
                        CheckBanStatusAndProceed(_currentUser.UserId);
                        return;
                    }

                    CheckEmailVerifiedThenBanStatus(_currentUser.UserId, email);
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AuthManager] Login threw synchronously: {ex}");
                _isLoginBusy = false;
                OnAuthError?.Invoke("Something happened wrong. Try again.");
            }
        }

        private void CheckEmailVerifiedThenBanStatus(string uid, string email)
        {
            _currentUser.ReloadAsync().ContinueWithOnMainThread(reloadTask =>
            {
                if (reloadTask.IsCanceled || reloadTask.IsFaulted)
                {
                    Debug.LogError($"[AuthManager] ReloadAsync (login) failed: {reloadTask.Exception}");
                    OnAuthError?.Invoke("Could not check your verification status. Check your internet connection and try again.");
                    SignOutInternal();
                    return;
                }

                if (!_currentUser.IsEmailVerified)
                {
                    Debug.LogWarning("[AuthManager] Login blocked — email not verified.");
                    ShowVerificationPanelUnlessBanned(email);
                    return;
                }

                CheckBanStatusAndProceed(uid);
            });
        }

        private void CheckBanStatusAndProceed(string uid)
        {
            VerificationRequestTracker.CheckBanned(uid, (banned, reason) =>
            {
                if (banned)
                {
                    Debug.LogWarning("[AuthManager] This account is banned (verificationRequests).");
                    OnAuthError?.Invoke(string.IsNullOrEmpty(reason)
                        ? "Your account has been banned."
                        : $"Your account has been banned. Reason: {reason}");
                    SignOutInternal();
                    return;
                }
                if (_currentUser != null && _currentUser.IsEmailVerified && !_currentUser.IsAnonymous)
                    VerificationRequestTracker.MarkVerifiedIfPending(uid);

                CheckProfileBanAndProceed(uid);
            });
        }

        private void CheckProfileBanAndProceed(string uid)
        {
            DocumentReference docRef = _firestore.Collection(PLAYERS_COLLECTION).Document(uid);

            docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    Debug.LogError($"[AuthManager] Failed to fetch profile: {task.Exception}");
                    OnAuthError?.Invoke("Something went wrong while loading your profile.");
                    SignOutInternal();
                    return;
                }

                DocumentSnapshot snapshot = task.Result;

                if (!snapshot.Exists)
                {
                    Debug.LogWarning("[AuthManager] No profile document found for a verified account — creating one now.");
                    string fallbackUsername = !string.IsNullOrEmpty(_currentUser.DisplayName)
                        ? _currentUser.DisplayName
                        : (!string.IsNullOrEmpty(_currentUser.Email) ? _currentUser.Email.Split('@')[0] : "Player");

                    CreatePlayerProfile(uid, fallbackUsername, _currentUser.Email ?? "");
                    GameEvents.OnPlayerLoggedIn?.Invoke();
                    OnLoginSuccess?.Invoke();
                    return;
                }

                bool isBanned = snapshot.ContainsField("isBanned") && snapshot.GetValue<bool>("isBanned");

                if (isBanned)
                {
                    Debug.LogWarning("[AuthManager] This account is banned.");
                    string banReason = snapshot.ContainsField("banReason") ? snapshot.GetValue<string>("banReason") : null;
                    OnAuthError?.Invoke(string.IsNullOrEmpty(banReason)
                        ? "Your account has been banned."
                        : $"Your account has been banned. Reason: {banReason}");
                    SignOutInternal();
                    return;
                }

                docRef.UpdateAsync("lastLogin", Timestamp.GetCurrentTimestamp());

                Debug.Log("[AuthManager] Login successful.");
                GameEvents.OnPlayerLoggedIn?.Invoke();
                OnLoginSuccess?.Invoke();
            });
        }

        public void Logout()
        {
            if (_auth != null)
            {
                _auth.SignOut();
            }

            _currentUser = null;
            Debug.Log("[AuthManager] Logged out.");
            GameEvents.OnPlayerLoggedOut?.Invoke();
            OnLogoutComplete?.Invoke();
        }

        private void SignOutInternal()
        {
            StopVerificationPolling();
            _auth?.SignOut();
            _currentUser = null;
        }

        public static bool IsGuest => Instance != null && Instance._currentUser != null && Instance._currentUser.IsAnonymous;

        public static string CurrentUid =>
            Instance != null && Instance._currentUser != null ? Instance._currentUser.UserId : null;

        public static bool IsPendingEmailVerification =>
            Instance != null && Instance._currentUser != null &&
            !Instance._currentUser.IsAnonymous && !Instance._currentUser.IsEmailVerified;

        public void LoginAsGuest()
        {
            if (_isLoginBusy) { Debug.LogWarning("[AuthManager] LoginAsGuest already in progress."); return; }
            if (_auth == null)
            {
                OnAuthError?.Invoke("Firebase is not ready. Check internet");
                return;
            }
            _isLoginBusy = true;

            try
            {
                _auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(task =>
                {
                    _isLoginBusy = false;

                    if (task.IsCanceled || task.IsFaulted)
                    {
                        string message = GetFirebaseErrorMessage(task.Exception);
                        Debug.LogError($"[AuthManager] Guest login failed: {message}");
                        OnAuthError?.Invoke(message);
                        return;
                    }

                    AuthResult result = task.Result;
                    _currentUser = result.User;

                    string guestName = "Guest_" + _currentUser.UserId.Substring(0, 5);
                    Debug.Log($"[AuthManager] Guest signed in UID: {_currentUser.UserId}");

                    GameEvents.OnPlayerLoggedIn?.Invoke();
                    OnLoginSuccess?.Invoke();

                    CreatePlayerProfile(_currentUser.UserId, guestName, "");
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AuthManager] LoginAsGuest threw synchronously: {ex}");
                _isLoginBusy = false;
                OnAuthError?.Invoke("Something happened wrong. Try again.");
            }
        }

        public void UpgradeGuestAccount(string username, string email, string password)
        {
            if (_auth == null || _currentUser == null || !_currentUser.IsAnonymous)
            {
                OnAuthError?.Invoke("No guest account to upgrade.");
                return;
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                OnAuthError?.Invoke("Please enter an email address.");
                return;
            }

            if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            {
                OnAuthError?.Invoke("Password must be at least 6 characters.");
                return;
            }

            _currentUser.ReloadAsync().ContinueWithOnMainThread(reloadTask =>
            {
                if (reloadTask.IsFaulted)
                {
                    Debug.LogError("[AuthManager] Guest session expired. Signing out and registering fresh.");

                    _auth.SignOut();
                    _currentUser = null;

                    _auth.CreateUserWithEmailAndPasswordAsync(email, password)
                         .ContinueWithOnMainThread(freshTask =>
                    {
                        if (freshTask.IsFaulted || freshTask.IsCanceled)
                        {
                            string freshErr = GetFirebaseErrorMessage(freshTask.Exception);
                            Debug.LogError($"[AuthManager] Fresh register after guest expiry failed: {freshErr}");
                            OnAuthError?.Invoke("Your session expired. Please try again: " + freshErr);
                            return;
                        }
                        _currentUser = freshTask.Result.User;
                        Debug.Log("[AuthManager] Fresh account created after guest expiry.");

                        SavePendingRegistration(_currentUser.UserId, username, email, isGuestUpgrade: false);

                        SendVerificationEmailInternal(email, isFirstSend: true);
                    });
                    return;
                }

                Credential credential = EmailAuthProvider.GetCredential(email, password);
                _currentUser.LinkWithCredentialAsync(credential).ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    string message = GetFirebaseErrorMessage(task.Exception);
                    Debug.LogError($"[AuthManager] Guest upgrade failed: {message}");
                    if (message.Contains("None"))
                        OnAuthError?.Invoke("Your session expired. Please logout and try again.");
                    else
                        OnAuthError?.Invoke(message);
                    return;
                }

                AuthResult result = task.Result;
                _currentUser = result.User;

                Debug.Log("[AuthManager] Guest account linked to email/password successfully.");

                SavePendingRegistration(_currentUser.UserId, username, email, isGuestUpgrade: true);

                SendVerificationEmailInternal(email, isFirstSend: true);
            });
            });
        }

        private string GetFirebaseErrorMessage(Exception exception)
        {
            if (exception?.GetBaseException() is FirebaseException firebaseEx)
            {
                AuthError errorCode = (AuthError)firebaseEx.ErrorCode;

                switch (errorCode)
                {
                    case AuthError.InvalidEmail:
                        return "Email format is not correct.";
                    case AuthError.WrongPassword:
                        return "Wrong Password.";
                    case AuthError.UserNotFound:
                        return "This Email account is not registered.";
                    case AuthError.EmailAlreadyInUse:
                        return "This Email is already used.";
                    case AuthError.WeakPassword:
                        return "Weak Password (at least 6 characters).";
                    case AuthError.MissingEmail:
                        return "Enter Email.";
                    case AuthError.MissingPassword:
                        return "Enter Password.";
                    case AuthError.NetworkRequestFailed:
                        return "Check internet connection.";
                    case AuthError.CredentialAlreadyInUse:
                        return "This Email already belongs to another account.";
                    case AuthError.ProviderAlreadyLinked:
                        return "This account is already linked.";
                    case AuthError.TooManyRequests:
                        return "Too many attempts. Please wait a few minutes before requesting another email.";
                    case AuthError.None:
                        return "Your session expired. Please logout and login again.";
                    default:
                        return $"Authentication error: {errorCode}";
                }
            }

            return "Something happened wrong. Try again.";
        }
    }
}
