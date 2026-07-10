// ============================================================
//  LevelManager.cs  —  FINAL VERSION
//
//  Changes from previous:
//  1. LevelSession se LevelData load karta hai
//  2. OnLevelInitialized event fire karta hai jab sab ready ho
//     (LevelResultManager is event ka wait karta hai)
//  3. Score LevelSession mein sync karta hai
// ============================================================

using UnityEngine;
using UnityEngine.Events;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("Board References")]
    public Match3.BoardGrid    boardGrid;
    public Match3.TileSpawner  tileSpawner;
    public Match3.InputHandler inputHandler;

    [Header("Level Data")]
    [Tooltip("Fallback for Editor testing — LevelSession overrides at runtime")]
    public Match3.LevelData levelData;

    [Header("Phase 5 References")]
    public Match3.GoalTracker goalTracker;
    public Match3.MoveCounter moveCounter;

    [Header("Events")]
    public UnityEvent<int> OnScoreChanged;
    public UnityEvent<int> OnMovesChanged;
    public UnityEvent      OnLevelComplete;
    public UnityEvent      OnGameOver;

    // ── NEW: LevelResultManager waits for this ───────────────
    public static event System.Action OnLevelInitialized;
    // ─────────────────────────────────────────────────────────

    public int Score { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // ── LevelSession se override karo ────────────────────
        if (Match3.LevelSession.CurrentLevel != null)
        {
            levelData = Match3.LevelSession.CurrentLevel;
            Debug.Log($"[LevelManager] LevelData from LevelSession " +
                      $"(Level {Match3.LevelSession.CurrentLevelId})");
        }

        if (levelData == null)
        {
            Debug.LogError("[LevelManager] No LevelData! Assign in Inspector " +
                           "OR load via LevelLoader.LoadLevel()");
            return;
        }

        InitializeLevel();

        // ── Notify LevelResultManager that we are ready ──────
        OnLevelInitialized?.Invoke();
        // ─────────────────────────────────────────────────────
    }

    private void InitializeLevel()
    {
        // Goals
        if (goalTracker != null && levelData.goals != null)
        {
            goalTracker.SetGoals(levelData.goals);
            goalTracker.ResetState();
        }

        // Moves
        if (moveCounter != null)
        {
            moveCounter.Initialize(levelData.moveLimit);
            OnMovesChanged?.Invoke(levelData.moveLimit);
        }

        // Board
        if (boardGrid != null)
            boardGrid.InitializeBoard(levelData.width, levelData.height);

        // Tiles
        if (tileSpawner != null)
        {
            tileSpawner.SetLevel(levelData);
            tileSpawner.FillBoard();
        }

        inputHandler?.SetInputEnabled(false);

        Score = 0;
        OnScoreChanged?.Invoke(Score);

        Debug.Log($"[LevelManager] Initialized — " +
                  $"Board:{levelData.width}x{levelData.height}, " +
                  $"Moves:{levelData.moveLimit}, " +
                  $"Tiles:{levelData.allowedTiles?.Length ?? 0}");
    }

    public void OnMoveCompleted() => moveCounter?.UseMove();

    public void OnTileCleared(Match3.TileData tile) => goalTracker?.OnTileCleared(tile);

    public void AddScore(int points)
    {
        Score += points;
        Match3.LevelSession.CurrentScore = Score;   // ← sync to session
        OnScoreChanged?.Invoke(Score);
        goalTracker?.OnScoreUpdated(Score);
    }
}