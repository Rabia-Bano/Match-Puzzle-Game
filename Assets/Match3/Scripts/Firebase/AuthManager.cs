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
    /// (used here for error messages).
    /// </summary>
    [Serializable]
    public class StringUnityEvent : UnityEvent<string> { }

    /// <summary>
    /// Handles Firebase Authentication (Register/Login/Logout) and
    /// creates/loads the player's Firestore profile document.
    ///
    /// Attach to a GameObject named "AuthManager".
    /// Call Initialize() from FirebaseInitializer's OnFirebaseReady event.
    /// </summary>
    public class AuthManager : MonoBehaviour
    {
        public static AuthManager Instance { get; private set; }

        // Prevents double-click on Register/Login buttons
        private bool _isBusy = false;

        [Header("Auth Result Events")]
        [Tooltip("Fired after a new account + profile are created successfully.")]
        public UnityEvent OnRegisterSuccess;

        [Tooltip("Fired after login succeeds and the player is not banned.")]
        public UnityEvent OnLoginSuccess;

        [Tooltip("Fired after logout completes.")]
        public UnityEvent OnLogoutComplete;

        [Tooltip("Fired with a user-friendly error message on any failure.")]
        public StringUnityEvent OnAuthError;

        private FirebaseAuth _auth;
        private FirebaseFirestore _firestore;
        private FirebaseUser _currentUser;

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
        /// Creates a Firebase Auth account, then a Firestore profile document.
        /// </summary>
        public void Register(string username, string email, string password)
        {
            if (_isBusy) { OnAuthError?.Invoke("Please wait..."); return; }
            if (_auth == null)
            {
                OnAuthError?.Invoke("Firebase is not ready. Check internet");
                return;
            }
            _isBusy = true;

            // ----- Local validation -----
            if (string.IsNullOrWhiteSpace(username))
            {
                OnAuthError?.Invoke("Enter Username.");
                return;
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                OnAuthError?.Invoke("Enter Email.");
                return;
            }

            if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            {
                OnAuthError?.Invoke("Minimun 6 character password.");
                return;
            }

            _auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    string message = GetFirebaseErrorMessage(task.Exception);
                    Debug.LogError($"[AuthManager] Register failed: {message}");
                    _isBusy = false;
                    OnAuthError?.Invoke(message);
                    return;
                }

                AuthResult result = task.Result;
                _currentUser = result.User;

                Debug.Log($"[AuthManager] Account created for UID: {_currentUser.UserId}");
                _isBusy = false;

                // Fire immediately — dont wait for Firestore
                GameEvents.OnPlayerLoggedIn?.Invoke();
                OnRegisterSuccess?.Invoke();

                // Create profile in background (fire and forget)
                CreatePlayerProfile(_currentUser.UserId, username, email);
            });
        }

        /// <summary>
        /// Creates the initial player profile document in Firestore
        /// at players/{uid}, matching the fields from the game design:
        /// username, email, join date, score, level, pets, coins.
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
                { "totalScore", 0 },
                { "highestLevel", 1 },
                { "levelsCompleted", 0 },
                { "coins", 0 },
                { "pets", new List<string>() },
                { "isBanned", false }
            };

            docRef.SetAsync(profileData).ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    Debug.LogError($"[AuthManager] Failed to create player profile: {task.Exception}");
                    OnAuthError?.Invoke("Profile create karne mein error aaya. Dobara try karein.");
                    return;
                }

                Debug.Log("[AuthManager] Player profile created successfully.");
                // OnRegisterSuccess already fired in Register()/LoginAsGuest() immediately
                // Do NOT fire again here — causes double navigation / stuck screen
            });
        }

        // -----------------------------------------------------------
        // LOGIN
        // -----------------------------------------------------------

        /// <summary>
        /// Signs in an existing player with email and password,
        /// then checks the ban status before allowing access.
        /// </summary>
        public void Login(string email, string password)
        {
            if (_auth == null)
            {
                OnAuthError?.Invoke("Firebase is not ready. Check internet.");
                return;
            }

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                OnAuthError?.Invoke("Enter Email and password.");
                return;
            }

            _auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task =>
            {
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
                CheckBanStatusAndProceed(_currentUser.UserId);
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
                    OnAuthError?.Invoke("Profile load karne mein error aaya.");
                    SignOutInternal();
                    return;
                }

                DocumentSnapshot snapshot = task.Result;

                if (!snapshot.Exists)
                {
                    Debug.LogWarning("[AuthManager] No profile document found for this user.");
                    OnAuthError?.Invoke("Player profile nahi mila.");
                    SignOutInternal();
                    return;
                }

                bool isBanned = snapshot.ContainsField("isBanned") && snapshot.GetValue<bool>("isBanned");

                if (isBanned)
                {
                    Debug.LogWarning("[AuthManager] This account is banned.");
                    OnAuthError?.Invoke("Aapka account banned hai.");
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

        /// <summary>
        /// Signs the player in anonymously. Use for "Play as Guest".
        /// Creates a minimal Firestore profile (username = "Guest_xxxx")
        /// just like a normal account so the rest of the game (coins,
        /// pets, score) works unchanged.
        /// </summary>
        public void LoginAsGuest()
        {
            if (_isBusy) { Debug.LogWarning("[AuthManager] LoginAsGuest already in progress."); return; }
            if (_auth == null)
            {
                OnAuthError?.Invoke("Firebase is not ready. Check internet");
                return;
            }
            _isBusy = true;

            _auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    string message = GetFirebaseErrorMessage(task.Exception);
                    Debug.LogError($"[AuthManager] Guest login failed: {message}");
                    _isBusy = false;
                    OnAuthError?.Invoke(message);
                    return;
                }

                AuthResult result = task.Result;
                _currentUser = result.User;

                string guestName = "Guest_" + _currentUser.UserId.Substring(0, 5);
                Debug.Log($"[AuthManager] Guest signed in UID: {_currentUser.UserId}");
                _isBusy = false;

                // Fire immediately — dont wait for Firestore
                // Profile creation happens in background
                GameEvents.OnPlayerLoggedIn?.Invoke();
                OnLoginSuccess?.Invoke();

                // Create profile in background (fire and forget)
                CreatePlayerProfile(_currentUser.UserId, guestName, "");
            });
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
                OnAuthError?.Invoke("Enter Email.");
                return;
            }

            if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            {
                OnAuthError?.Invoke("Minimun 6 character password.");
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
                            OnAuthError?.Invoke("Session expire hogaya. Dobara try karo: " + freshErr);
                            return;
                        }
                        _currentUser = freshTask.Result.User;
                        Debug.Log("[AuthManager] Fresh account created after guest expiry.");
                        GameEvents.OnPlayerLoggedIn?.Invoke();
                        OnRegisterSuccess?.Invoke();
                        CreatePlayerProfile(_currentUser.UserId, username, email);
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
                        OnAuthError?.Invoke("Session expire hogaya. Please logout karke wapas try karo.");
                    else
                        OnAuthError?.Invoke(message);
                    return;
                }

                AuthResult result = task.Result;
                _currentUser = result.User;

                Debug.Log("[AuthManager] Guest account upgraded successfully.");

                // Fire IMMEDIATELY — don't wait for Firestore
                // ProfilePanel.OnGuestUpgradeSuccess updates in-memory + closes panel
                OnRegisterSuccess?.Invoke();

                // Update Firestore in background (fire and forget)
                DocumentReference docRef = _firestore.Collection(PLAYERS_COLLECTION).Document(_currentUser.UserId);
                Dictionary<string, object> updates = new Dictionary<string, object>
                {
                    { "username",    username },
                    { "displayName", username },   // ProfilePanel.RefreshUI reads displayName
                    { "email",       email    }
                };

                docRef.UpdateAsync(updates).ContinueWithOnMainThread(updateTask =>
                {
                    if (updateTask.IsCanceled || updateTask.IsFaulted)
                        Debug.LogError($"[AuthManager] Profile Firestore update failed: {updateTask.Exception}");
                    else
                        Debug.Log("[AuthManager] Firestore profile updated with new username/email.");
                });
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
                    case AuthError.None:
                        return "Session expire hogaya. Logout karke wapas Login karo.";
                    default:
                        return $"Authentication error: {errorCode}";
                }
            }

            return "Something happened wrong. Try again.";
        }
    }
}