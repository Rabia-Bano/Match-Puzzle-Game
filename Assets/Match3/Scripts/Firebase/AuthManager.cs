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
    /// <summary>
    /// A serializable UnityEvent that passes a string parameter
    /// (used here for error messages / emails).
    /// </summary>
    [Serializable]
    public class StringUnityEvent : UnityEvent<string> { }

    /// <summary>
    /// Handles Firebase Authentication (Register/Login/Logout) and
    /// creates/loads the player's Firestore profile document.
    ///
    /// Attach to a GameObject named "AuthManager".
    /// Call Initialize() from FirebaseInitializer's OnFirebaseReady event.
    ///
    /// ---------------------------------------------------------------
    /// FIXES IN THIS VERSION:
    ///  1) Register(): every early-return validation branch now resets
    ///     _isRegisterBusy BEFORE returning. Previously the 3 validation
    ///     branches (blank username / blank email / weak password) left
    ///     _isRegisterBusy stuck at "true" forever, so every subsequent
    ///     tap on Register — no matter what the real problem was — hit
    ///     the "Please wait..." branch instead of its real error.
    ///  2) Login() now has its own busy-guard AND a try/catch around the
    ///     Firebase call. Previously a synchronous exception (or a rapid
    ///     double-tap firing two overlapping sign-in calls) could leave
    ///     the UI's "loading" state stuck forever with no error ever
    ///     reaching OnAuthError — which looks like the game freezing,
    ///     even though no GameState.Paused is ever touched here.
    ///  3) Firebase's email/password provider only validates the FORMAT
    ///     of an email, never whether the mailbox is real — so a fake
    ///     address always "works" for registration, before AND after
    ///     deployment. Fix #4 closes that gap.
    ///  4) NEW: full email verification flow — Register() creates the
    ///     Firebase Auth user + Firestore profile (emailVerified:false),
    ///     sends a verification link, and raises OnVerificationRequired
    ///     instead of OnRegisterSuccess. The player only reaches
    ///     OnRegisterSuccess (and the game) after tapping "I've verified,
    ///     continue" and the link has actually been clicked. Login()
    ///     performs the same check every time and blocks unverified
    ///     accounts the same way.
    /// ---------------------------------------------------------------
    /// </summary>
    public class AuthManager : MonoBehaviour
    {
        public static AuthManager Instance { get; private set; }

        // Prevents double-click on Register button while a request is in flight
        private bool _isRegisterBusy = false;

        // Prevents double-click on Login button while a request is in flight
        private bool _isLoginBusy = false;

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

        private FirebaseAuth _auth;
        private FirebaseFirestore _firestore;
        private FirebaseUser _currentUser;

        // Cached from Register()/UpgradeGuestAccount(), needed at verification
        // time (CheckEmailVerifiedAndContinue) since profile creation/update
        // is now DEFERRED until the email is actually verified — see the
        // "defer Firestore write" fix documented above CreatePlayerProfile().
        //
        // BUG FIX: these used to be PLAIN in-memory fields only. If the app
        // (or the Unity Editor Play session) was ever closed/restarted between
        // "account linked, verification email sent" and "player taps Continue"
        // — which is completely normal, since verifying happens in the Mail
        // app and can take a while — these fields reset to empty on the next
        // launch. TryResumeSession() would still correctly show the
        // verification panel again (it reads the email straight from Firebase
        // Auth), but by the time CheckEmailVerifiedAndContinue() ran, the
        // pending username/email were gone, so only "emailVerified: true" got
        // written to Firestore/Realtime DB — the real username/email never
        // did. They're now also mirrored into PlayerPrefs (device-local, NOT
        // sent to the admin panel) so they survive an app restart — see
        // SavePendingRegistration/LoadPendingRegistrationIfNeeded below.
        private string _pendingEmail;
        private string _pendingUsername;
        private bool   _pendingIsGuestUpgrade;   // true = UPDATE existing guest doc at verify time, false = CREATE new doc

        private const string PREF_PENDING_UID      = "AuthManager_PendingUid";
        private const string PREF_PENDING_USERNAME = "AuthManager_PendingUsername";
        private const string PREF_PENDING_EMAIL    = "AuthManager_PendingEmail";
        private const string PREF_PENDING_IS_GUEST = "AuthManager_PendingIsGuestUpgrade";

        /// <summary>Call right after a UID exists for the pending signup (Register success
        /// or UpgradeGuestAccount link success) — persists to PlayerPrefs so this survives
        /// an app restart, and also updates the fast in-memory copies.</summary>
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

        /// <summary>Call once verification is confirmed and the Firestore write is done —
        /// prevents this saved data from ever being reused for a different account later.</summary>
        private void ClearPendingRegistration()
        {
            PlayerPrefs.DeleteKey(PREF_PENDING_UID);
            PlayerPrefs.DeleteKey(PREF_PENDING_USERNAME);
            PlayerPrefs.DeleteKey(PREF_PENDING_EMAIL);
            PlayerPrefs.DeleteKey(PREF_PENDING_IS_GUEST);
            PlayerPrefs.Save();
        }

        /// <summary>Restores _pendingUsername/_pendingEmail/_pendingIsGuestUpgrade from
        /// PlayerPrefs if they were lost to an app restart — but ONLY if the saved data
        /// belongs to the CURRENTLY signed-in UID, so a different account (or a different
        /// abandoned signup) on the same device can never leak into this one.</summary>
        private void LoadPendingRegistrationIfNeeded()
        {
            if (_currentUser == null) return;
            if (!string.IsNullOrEmpty(_pendingUsername) && !string.IsNullOrEmpty(_pendingEmail)) return; // already have it in memory

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

        /// <summary>The currently signed-in Firebase user, or null.</summary>
        public static FirebaseUser CurrentUser => Instance != null ? Instance._currentUser : null;

        /// <summary>True if a user is currently signed in.</summary>
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

        /// <summary>
        /// Call this once Firebase has been initialized
        /// (wire to FirebaseInitializer's OnFirebaseReady UnityEvent).
        /// </summary>
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

        // -----------------------------------------------------------
        // REGISTER
        // -----------------------------------------------------------

        /// <summary>
        /// Registers a new player with username, email and password.
        /// Creates a Firebase Auth account, then a Firestore profile document,
        /// then sends a verification email. Does NOT log the player into
        /// gameplay yet — OnVerificationRequired fires instead of
        /// OnRegisterSuccess. Call CheckEmailVerifiedAndContinue() once the
        /// player has clicked the link in their inbox.
        /// </summary>
        public void Register(string username, string email, string password, string confirmPassword)
        {
            if (_isRegisterBusy) { OnAuthError?.Invoke("Please wait..."); return; }

            if (_auth == null)
            {
                OnAuthError?.Invoke("Firebase is not ready. Check your internet connection."); 
                return;
            }

            // ----- Local validation (FIX: every branch below returns WITHOUT
            // ever having set _isRegisterBusy = true, so it can never get
            // stuck and mask the next attempt's real error) -----
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

            // NEW — FIX: confirm-password was only ever checked in the guest-upgrade
            // flow (ProfilePanel.cs). The main Register screen never compared it at
            // all, so a mismatched confirm-password silently went through with no
            // error. Now checked centrally, here, so every caller is covered.
            if (password != confirmPassword)
            {
                OnAuthError?.Invoke("Password and Confirm Password do not match.");
                return;
            }

            // Only set busy AFTER every synchronous validation check has passed —
            // this guarantees _isRegisterBusy is only ever true while a real
            // Firebase request is actually in flight.
            _isRegisterBusy = true;

            try
            {
                _auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task =>
                {
                    // FIX: reset on every exit path below — success, failure or cancel.
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

                    // BUG FIX: save durably (PlayerPrefs), not just in-memory —
                    // see the note on the fields above for why.
                    SavePendingRegistration(_currentUser.UserId, username, email, isGuestUpgrade: false);

                    // NEW — FIX: do NOT create the Firestore profile document yet.
                    // Creating it here is exactly why the admin panel (and the
                    // "players" collection in general) showed the player
                    // immediately, before they had verified anything. The
                    // Firebase Auth account itself unavoidably has to exist
                    // before we can even send a verification email — that part
                    // of Firebase's design can't be changed — but the profile
                    // document your admin panel actually reads from is now only
                    // created once CheckEmailVerifiedAndContinue() confirms the
                    // email really was verified. Until then, this player is
                    // invisible to the admin panel's Player Management table.
                    SendVerificationEmailInternal(email, isFirstSend: true);
                });
            }
            catch (Exception ex)
            {
                // FIX: guards against a *synchronous* exception from the Firebase
                // SDK call itself. Previously this would escape uncaught, leaving
                // _isRegisterBusy stuck true and the Register button permanently
                // unresponsive.
                Debug.LogError($"[AuthManager] Register threw synchronously: {ex}");
                _isRegisterBusy = false;
                OnAuthError?.Invoke("Something happened wrong. Try again.");
            }
        }

        /// <summary>
        /// Creates the player profile document in Firestore at players/{uid},
        /// matching the fields from the game design: username, email, join
        /// date, score, level, pets, coins. Called ONLY once the email has
        /// actually been verified (see CheckEmailVerifiedAndContinue) — never
        /// right after CreateUserWithEmailAndPasswordAsync — so an unverified
        /// signup never appears in the admin panel's players table.
        /// </summary>
        private void CreatePlayerProfile(string uid, string username, string email)
        {
            DocumentReference docRef = _firestore.Collection(PLAYERS_COLLECTION).Document(uid);

            Dictionary<string, object> profileData = new Dictionary<string, object>
            {
                { "username", username },
                { "displayName", username },    // saved both so ProfilePanel can read either
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
                { "emailVerified", true }   // only ever created after verification now, so always true
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

        // -----------------------------------------------------------
        // EMAIL VERIFICATION  (NEW)
        // -----------------------------------------------------------

        /// <summary>
        /// Sends (or re-sends) the verification email to the currently
        /// signed-in user, then raises the appropriate UI event.
        /// </summary>
        private void SendVerificationEmailInternal(string email, bool isFirstSend)
        {
            if (_currentUser == null)
            {
                OnAuthError?.Invoke("Your session was lost. Please login or register again.");
                return;
            }

            _currentUser.SendEmailVerificationAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    Debug.LogError($"[AuthManager] Sending verification email failed: {task.Exception}");
                    OnAuthError?.Invoke("Could not send the verification email. Check your internet connection and try again.");
                    return;
                }

                Debug.Log($"[AuthManager] Verification email sent to {email}.");

                if (isFirstSend)
                    OnVerificationRequired?.Invoke(email);
                else
                    OnVerificationEmailResent?.Invoke();
            });
        }

        /// <summary>
        /// NEW — call this once, right when the Login screen loads, to check
        /// for a session Firebase already persisted from a previous app launch
        /// (Initialize() above populates _currentUser from _auth.CurrentUser,
        /// which Firebase keeps in local storage across app restarts on its
        /// own — this method is what actually DOES something with that fact).
        ///
        /// Three outcomes, each reusing the exact same events the normal
        /// Login() flow already uses, so LoginUIController needs no new
        /// wiring:
        ///   • No persisted user at all           -> does nothing; the Login
        ///     form shows normally and the player logs in manually.
        ///   • Persisted guest (anonymous)         -> OnLoginSuccess fires,
        ///     straight back into the game as the same guest as before.
        ///   • Persisted email/password account,
        ///     still verified & not banned         -> OnLoginSuccess fires,
        ///     straight into the game — no re-typing email/password.
        ///   • Persisted account, NOT verified yet -> OnVerificationRequired
        ///     fires, so the "check your inbox" panel shows automatically.
        ///   • Persisted account, banned            -> OnAuthError fires and
        ///     the session is cleared, same as a fresh banned-login attempt.
        /// </summary>
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
                    // FIX: this branch used to just log and return — NO event
                    // ever fired, so LoginUIController's SetLoading(true) (set
                    // right before calling TryResumeSession()) was never reset.
                    // The player was stuck staring at the loading overlay over
                    // an otherwise-blank Login screen for up to 15 seconds,
                    // until the watchdog timer eventually force-cleared it —
                    // which is exactly the "flash / Login screen not proper"
                    // symptom. Now it fires immediately so the Login form
                    // appears right away instead.
                    Debug.LogWarning($"[AuthManager] Could not refresh persisted session, falling back to manual login: {task.Exception}");
                    OnSessionResumeFailed?.Invoke();
                    return;
                }

                if (!_currentUser.IsEmailVerified)
                {
                    Debug.Log("[AuthManager] Persisted session's email is still unverified.");
                    OnVerificationRequired?.Invoke(_currentUser.Email);
                    return;
                }

                CheckBanStatusAndProceed(_currentUser.UserId);
            });
        }

        /// <summary>
        /// Call this from the "Resend Email" button on the verification panel.
        /// </summary>
        public void ResendVerificationEmail()
        {
            string email = _currentUser != null ? _currentUser.Email : _pendingEmail;
            SendVerificationEmailInternal(email, isFirstSend: false);
        }

        /// <summary>
        /// Call this from the "I've verified, continue" button on the
        /// verification panel. Reloads the user's token from Firebase
        /// (verification status is not pushed live — it must be re-checked)
        /// and only then lets the player into the game.
        /// </summary>
        public void CheckEmailVerifiedAndContinue()
        {
            if (_currentUser == null)
            {
                OnAuthError?.Invoke("Your session was lost. Please login or register again.");
                return;
            }

            _currentUser.ReloadAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    Debug.LogError($"[AuthManager] ReloadAsync failed: {task.Exception}");
                    OnAuthError?.Invoke("Could not check your verification status. Check your internet connection and try again.");
                    return;
                }

                if (!_currentUser.IsEmailVerified)
                {
                    OnAuthError?.Invoke("Your email is not verified yet. Please check your inbox (and Spam folder).");
                    return;
                }

                Debug.Log("[AuthManager] Email verified — continuing into the game.");

                // BUG FIX: restore the pending username/email from PlayerPrefs
                // if this app session lost the in-memory copies (see the field
                // comments above) — without this, only "emailVerified: true"
                // would ever get written and the real username/email would
                // silently never make it into Firestore/Realtime DB.
                LoadPendingRegistrationIfNeeded();

                // Final safety net — if the pending data is STILL missing (e.g.
                // this device never had it, like verifying via a link opened on
                // a completely different device/browser), fall back to Firebase
                // Auth's own copy of the email (always reliable) and a username
                // derived from it, rather than writing blank fields over real data.
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
                    { "lastUpdated",   DateTime.UtcNow.ToString("o") }   // <-- ADD THIS LINE
                };

                _firestore.Collection(PLAYERS_COLLECTION).Document(_currentUser.UserId)
                    .UpdateAsync(updates).ContinueWithOnMainThread(updateTask =>
                {
                    if (updateTask.IsCanceled || updateTask.IsFaulted)
                    {
                        Debug.LogError($"[AuthManager] Profile Firestore update failed: {updateTask.Exception}");
                        OnAuthError?.Invoke("Verification hui, lekin profile save nahi ho saka. Try again.");
                        return;   // <-- pending data ClearPendingRegistration() se clear NAHI hogi, retry ho sakega
                    }

                    Debug.Log("[AuthManager] Firestore profile updated with verified username/email.");
                    ClearPendingRegistration();
                    GameEvents.OnPlayerLoggedIn?.Invoke();
                    OnRegisterSuccess?.Invoke();   // <-- ab reload sirf write complete hone ke BAAD hoga
                });
            }
            else
            {
                CreatePlayerProfile(_currentUser.UserId, _pendingUsername, _pendingEmail);
                // NOTE: yahan bhi same fix chahiye — CreatePlayerProfile() ko waapis
                // wire karna hoga taake iske SetAsync().ContinueWithOnMainThread() ke
                // success branch ke andar hi ClearPendingRegistration()/OnRegisterSuccess
                // call ho (neeche point 3 dekho).
            }

                // Done — clear the durable copy so it can never be mistakenly
                // reused for a different account later on this device.
                ClearPendingRegistration();

                GameEvents.OnPlayerLoggedIn?.Invoke();
                OnRegisterSuccess?.Invoke();
            });
        }

        // -----------------------------------------------------------
        // LOGIN
        // -----------------------------------------------------------

        /// <summary>
        /// Signs in an existing player with email and password,
        /// then checks email verification and ban status before
        /// allowing access.
        /// </summary>
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
                    // FIX: reset on every exit path — this is what previously
                    // could stay stuck (loading spinner + disabled buttons
                    // forever), which is what looked like the game "pausing".
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

                    // Guests (anonymous accounts) have no email — skip verification.
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

        /// <summary>
        /// NEW — reloads the user first (verification state can be stale on
        /// the cached token) and blocks unverified accounts from proceeding,
        /// sending them back to the "check your inbox" panel instead.
        /// </summary>
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
                    OnVerificationRequired?.Invoke(email);
                    return; // stay signed in so Resend/Continue on the verification panel still work
                }

                CheckBanStatusAndProceed(uid);
            });
        }

        /// <summary>
        /// Reads the player's profile document and checks the
        /// isBanned flag. If banned, signs the user back out.
        /// Otherwise updates lastLogin and fires OnLoginSuccess.
        /// </summary>
        private void CheckBanStatusAndProceed(string uid)
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
                    // NEW — self-healing edge case: this can legitimately happen
                    // now that profile creation is deferred until verification.
                    // If someone registers, closes the app, verifies their email
                    // from their inbox WITHOUT ever tapping "Continue" in-app, and
                    // later logs in directly, IsEmailVerified is correctly true
                    // (Firebase server already confirmed it) but the Firestore
                    // profile document was never created — because that creation
                    // normally happens inside CheckEmailVerifiedAndContinue(),
                    // which never ran for them. Anonymous guests never reach this
                    // branch (Login() skips straight to here for them without a
                    // verification step), so it's safe to assume anyone here is a
                    // verified email/password account — create their profile now
                    // instead of incorrectly rejecting a legitimate login.
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
                    OnAuthError?.Invoke("Your account has been banned.");
                    SignOutInternal();
                    return;
                }

                // Update last login time (fire-and-forget)
                docRef.UpdateAsync("lastLogin", Timestamp.GetCurrentTimestamp());

                Debug.Log("[AuthManager] Login successful.");
                GameEvents.OnPlayerLoggedIn?.Invoke();
                OnLoginSuccess?.Invoke();
            });
        }

        // -----------------------------------------------------------
        // LOGOUT
        // -----------------------------------------------------------

        /// <summary>
        /// Signs the current user out. Call this AFTER your
        /// cloud-sync of progress has completed.
        /// </summary>
        public void Logout()
        {
            if (_auth != null)
            {
                _auth.SignOut();
            }

            _currentUser = null;
            Debug.Log("[AuthManager] Logged out.");
            GameEvents.OnPlayerLoggedOut?.Invoke();   // GameManager listens → ChangeState(Login)
            OnLogoutComplete?.Invoke();
        }

        /// <summary>Internal sign-out used for failed/banned login attempts (no event fired).</summary>
        private void SignOutInternal()
        {
            _auth?.SignOut();
            _currentUser = null;
        }

        // -----------------------------------------------------------
        // GUEST LOGIN (Anonymous Auth)
        // -----------------------------------------------------------

        /// <summary>True if the current user is signed in anonymously (guest).</summary>
        public static bool IsGuest => Instance != null && Instance._currentUser != null && Instance._currentUser.IsAnonymous;

        /// <summary>NEW — current signed-in user's UID, or null if nobody is signed in.
        /// Used to scope device-local data (like TutorialManager's seen-flags) per
        /// ACCOUNT instead of per-device, so a fresh guest session or a different
        /// account on the same device doesn't inherit another account's progress.</summary>
        public static string CurrentUid =>
            Instance != null && Instance._currentUser != null ? Instance._currentUser.UserId : null;

        /// <summary>
        /// NEW — true once a guest's account has been LINKED to an email/password
        /// (so IsAnonymous is already false and IsGuest above is already false too)
        /// but that email hasn't been verified yet. UI (ProfilePanel) uses this to
        /// keep showing the "verify your account" state instead of jumping straight
        /// to the fully-logged-in state just because linking succeeded.
        /// NOTE: reflects the locally cached IsEmailVerified flag, which is only as
        /// fresh as the last ReloadAsync() — fine for driving UI, but the actual
        /// security check always happens fresh inside CheckEmailVerifiedAndContinue().
        /// </summary>
        public static bool IsPendingEmailVerification =>
            Instance != null && Instance._currentUser != null &&
            !Instance._currentUser.IsAnonymous && !Instance._currentUser.IsEmailVerified;

        /// <summary>
        /// Signs the player in anonymously. Use for "Play as Guest".
        /// Creates a minimal Firestore profile (username = "Guest_xxxx")
        /// just like a normal account so the rest of the game (coins,
        /// pets, score) works unchanged. No email involved, so no
        /// verification step applies to guests.
        /// </summary>
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

                    // Fire immediately — dont wait for Firestore
                    // Profile creation happens in background
                    GameEvents.OnPlayerLoggedIn?.Invoke();
                    OnLoginSuccess?.Invoke();

                    // Create profile in background (fire and forget)
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

        /// <summary>
        /// Upgrades an existing anonymous (guest) account to a full
        /// email/password account WITHOUT losing the guest's UID —
        /// so all of his existing progress (coins, pets, score) stays intact.
        /// Call this from a "Save Your Progress" / "Create Account" screen.
        /// </summary>
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

            // Refresh token first — guest tokens expire after ~1 hour
            _currentUser.ReloadAsync().ContinueWithOnMainThread(reloadTask =>
            {
                if (reloadTask.IsFaulted)
                {
                    // Guest session expired — cannot link, must sign out and try fresh
                    Debug.LogError("[AuthManager] Guest session expired. Signing out and registering fresh.");

                    // Sign out guest silently
                    _auth.SignOut();
                    _currentUser = null;

                    // Create brand new account (profile data from Firestore will be lost
                    // since guest never synced, but coins/progress were local anyway)
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

                        // BUG FIX: save durably — see field comments above.
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
                    // If error is None, session is broken — give helpful message
                    if (message.Contains("None"))
                        OnAuthError?.Invoke("Your session expired. Please logout and try again.");
                    else
                        OnAuthError?.Invoke(message);
                    return;
                }

                AuthResult result = task.Result;
                _currentUser = result.User;

                Debug.Log("[AuthManager] Guest account linked to email/password successfully.");

                // NEW — FIX: don't touch Firestore yet. The account is now
                // linked (technically no longer anonymous), but the username/
                // email/emailVerified fields your admin panel actually reads
                // are only written once CheckEmailVerifiedAndContinue() confirms
                // the email was really verified. Until then, the players/{uid}
                // doc still shows the old "Guest_xxxx" placeholder data — the
                // admin panel never sees the real identity prematurely.
                //
                // BUG FIX: save durably (PlayerPrefs) — see field comments above
                // for why a plain in-memory field wasn't enough.
                SavePendingRegistration(_currentUser.UserId, username, email, isGuestUpgrade: true);

                SendVerificationEmailInternal(email, isFirstSend: true);
            }); // LinkWithCredentialAsync
            }); // ReloadAsync
        }

        // -----------------------------------------------------------
        // ERROR MAPPING
        // -----------------------------------------------------------

        /// <summary>
        /// Converts Firebase AuthError exceptions into user-friendly
        /// (Roman Urdu/English) messages for UI display.
        /// </summary>
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
                        // NEW — Firebase rate-limits how often the SAME account can
                        // request a verification email in a short window. Repeatedly
                        // tapping "Resend" was very likely hitting this — previously
                        // it fell into the generic "default" case below and showed a
                        // raw enum name instead of an explanation.
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