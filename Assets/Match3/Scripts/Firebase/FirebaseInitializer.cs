using System;
using UnityEngine;
using UnityEngine.Events;

using Firebase;
using Firebase.Extensions;

namespace Game.Firebase
{
    public class FirebaseInitializer : MonoBehaviour
    {
        public static FirebaseInitializer Instance { get; private set; }

        public static bool IsReady { get; private set; } = false;

        [Header("Firebase Lifecycle Events")]
        [Tooltip("Invoked once Firebase has successfully initialized.")]
        public UnityEvent OnFirebaseReady;

        [Tooltip("Invoked if Firebase dependencies could not be resolved.")]
        public UnityEvent OnFirebaseFailed;

        private FirebaseApp _firebaseApp;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeFirebase();
        }

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

        public FirebaseApp GetApp()
        {
            return IsReady ? _firebaseApp : null;
        }
    }
}
