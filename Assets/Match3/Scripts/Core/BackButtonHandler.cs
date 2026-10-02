using UnityEngine;

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
                GameEvents.OnGamePaused?.Invoke();
                break;

            case GameState.Paused:
                GameEvents.OnGameResumed?.Invoke();
                break;

            case GameState.Store:
            case GameState.Leaderboard:
            case GameState.Settings:
            case GameState.PetCompanion:
            case GameState.BossArena:
                GameEvents.OnReturnToMap?.Invoke();
                break;

            case GameState.Map:
            case GameState.Login:
                Application.Quit();
                break;

            default:
                break;
        }
    }
}
