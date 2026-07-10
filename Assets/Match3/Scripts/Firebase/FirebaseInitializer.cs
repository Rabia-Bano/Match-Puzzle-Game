using System;
using UnityEngine;
using UnityEngine.Events;

// Core Firebase namespaces
using Firebase;
using Firebase.Extensions;

// Other Firebase service namespaces are imported here so this file
// compiles cleanly once the corresponding .unitypackage files are
// imported into the project. They are NOT used directly in this
// script, but are listed here for reference / quick copy-paste into
// other manager scripts (AuthManager, FirestoreManager, etc.)
//
// using Firebase.Auth;
// using Firebase.Firestore;
// using Firebase.Database;
// using Firebase.Storage;
// using Firebase.Functions;

namespace Game.Firebase
{
    /// <summary>
    /// Bootstraps the Firebase SDK for the game.
    /// Attach this to a single GameObject named "FirebaseManager"
    /// placed in the first scene (e.g. LoginScene).
    /// </summary>
    public class FirebaseInitializer : MonoBehaviour
    {
        // -----------------------------------------------------------
        // Singleton access (so other managers can reference IsReady
        // without needing a scene reference)
        // -----------------------------------------------------------
        public static FirebaseInitializer Instance { get; private set; }

        /// <summary>
        /// True once Firebase dependencies have been checked/fixed
        /// and FirebaseApp.DefaultInstance is ready to use.
        /// </summary>
        public static bool IsReady { get; private set; } = false;

        [Header("Firebase Lifecycle Events")]
        [Tooltip("Invoked once Firebase has successfully initialized.")]
        public UnityEvent OnFirebaseReady;

        [Tooltip("Invoked if Firebase dependencies could not be resolved.")]
        public UnityEvent OnFirebaseFailed;

        // Reference to the initialized FirebaseApp instance
        private FirebaseApp _firebaseApp;

        private void Awake()
        {
            // Enforce a single instance across scenes
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeFirebase();
        }

        /// <summary>
        /// Checks Firebase dependencies (Google Play Services on Android,
        /// required frameworks on iOS) and fixes them automatically if possible.
        /// </summary>
        private void InitializeFirebase()
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                try
                {
                    DependencyStatus dependencyStatus = task.Result;

                    if (dependencyStatus == DependencyStatus.Available)
                    {
                        _firebaseApp = FirebaseApp.DefaultInstance;
                        IsReady = true;

                        Debug.Log("[FirebaseInitializer] Firebase initialized successfully.");
                        OnFirebaseReady?.Invoke();
                    }
                    else
                    {
                        IsReady = false;
                        Debug.LogError(
                            $"[FirebaseInitializer] Could not resolve all Firebase dependencies: {dependencyStatus}. " +
                            "Firebase-dependent features (Auth, Firestore, Realtime DB) will not work.");

                        OnFirebaseFailed?.Invoke();
                    }
                }
                catch (Exception ex)
                {
                    IsReady = false;
                    Debug.LogError($"[FirebaseInitializer] Exception during Firebase initialization: {ex}");
                    OnFirebaseFailed?.Invoke();
                }
            });
        }

        /// <summary>
        /// Returns the initialized FirebaseApp instance, or null if not yet ready.
        /// </summary>
        public FirebaseApp GetApp()
        {
            return IsReady ? _firebaseApp : null;
        }
    }
}
