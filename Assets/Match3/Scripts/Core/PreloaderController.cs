using System.Collections;
using UnityEngine;

/// <summary>
/// Lives only in the Preloader scene. Waits briefly for splash,
/// then waits for Firebase to finish initializing, then transitions
/// to MainMenu (Login screen). This is the very first script that
/// runs when the app opens.
/// </summary>
public class PreloaderController : MonoBehaviour
{
    [Header("Minimum splash duration before checking Firebase")]
    public float splashDuration = 1.5f;

    [Header("Max seconds to wait for Firebase before giving up")]
    public float firebaseTimeout = 10f;

    private void Start()
    {
        AudioManager.Instance?.PlayMusic("ice_world_theme");   // ← ADD THIS — game shuru hote hi music
        StartCoroutine(InitializeGame());
    }

    private IEnumerator InitializeGame()
    {
        Debug.Log("[PreloaderController] Game starting...");

        yield return new WaitForSeconds(splashDuration);

        float waited = 0f;
        while (!Game.Firebase.FirebaseInitializer.IsReady && waited < firebaseTimeout)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        if (!Game.Firebase.FirebaseInitializer.IsReady)
            Debug.LogWarning("[PreloaderController] Firebase timeout — continuing to MainMenu anyway.");

        GameManager.Instance.ChangeState(GameState.Login);
    }
}