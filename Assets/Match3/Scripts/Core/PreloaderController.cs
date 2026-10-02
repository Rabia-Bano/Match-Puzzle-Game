using System.Collections;
using UnityEngine;

public class PreloaderController : MonoBehaviour
{
    [Header("Minimum splash duration before checking Firebase")]
    public float splashDuration = 1.5f;

    [Header("Max seconds to wait for Firebase before giving up")]
    public float firebaseTimeout = 10f;

    private void Start()
    {
        AudioManager.Instance?.PlayMusic("ice_world_theme");
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
