using UnityEngine;

/// <summary>
/// Android hardware/OS back button (Unity maps it to KeyCode.Escape) handled
/// game-wide. Lives once in the Preloader scene as DontDestroyOnLoad, listens
/// every frame, and decides what "back" means based on GameManager.CurrentState.
///
/// Attach to a new empty GameObject under "Preloader" (same place as
/// GameManager / SceneLoader). No Inspector fields to wire up.
/// </summary>
public class BackButtonHandler : MonoBehaviour
{
    public static BackButtonHandler Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            HandleBack();
        }
    }

    private void HandleBack()
    {
        if (GameManager.Instance == null) return;

        switch (GameManager.Instance.CurrentState)
        {
            case GameState.Playing:
            case GameState.BossGameplay:
                // Gameplay screen par back = pause menu kholo
                GameEvents.OnGamePaused?.Invoke();
                break;

            case GameState.Paused:
                // Pause menu khula ho to back se resume ho jaye
                GameEvents.OnGameResumed?.Invoke();
                break;

            case GameState.Store:
            case GameState.Leaderboard:
            case GameState.Settings:
            case GameState.PetCompanion:
            case GameState.BossArena:
                // In sab secondary screens se seedha Map par wapas
                GameEvents.OnReturnToMap?.Invoke();
                break;

            case GameState.Map:
            case GameState.Login:
                // Root screens — back se app close
                Application.Quit();
                break;

            default:
                break; // Loading state mein back ignore karo
        }
    }
}