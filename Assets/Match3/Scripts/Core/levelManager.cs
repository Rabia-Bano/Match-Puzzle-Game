using UnityEngine;
using UnityEngine.Events;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("Board References")]
    public Match3.BoardGrid       boardGrid;
    public Match3.BoardController boardController;
    public Match3.TileSpawner     tileSpawner;
    public Match3.InputHandler    inputHandler;

    [Header("Level Data")]
    [Tooltip("Fallback for Editor testing — LevelSession overrides at runtime")]
    public Match3.LevelData levelData;

    [Header("Phase 5 References")]
    public Match3.GoalTracker goalTracker;
    public Match3.MoveCounter moveCounter;

    [Header("Timer (optional — NEW)")]
    [Tooltip("Drag the LevelTimer component here. Only used when the level's LevelData.useTimer is ON.")]
    public Match3.LevelTimer levelTimer;

    [Header("Obstacle Systems (optional — leave blank if unused)")]
    public Match3.JellyManager    jellyManager;
    public Match3.HardTileManager hardTileManager;
    public Match3.StoneManager    stoneManager;

    [Header("Events")]
    public UnityEvent<int> OnScoreChanged;
    public UnityEvent<int> OnMovesChanged;
    public UnityEvent      OnLevelComplete;
    public UnityEvent      OnGameOver;

    public static event System.Action OnLevelInitialized;

    public int Score { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
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

        Debug.Log($"[LevelManager] About to bind PetManager. PetManager.Instance is " +
                  $"{(Match3.PetManager.Instance == null ? "NULL — PetManager not found!" : "found, OK")}. " +
                  $"boardController field is {(boardController == null ? "NULL — not wired in Inspector!" : "assigned, OK")}.");
        Match3.PetManager.Instance?.BindToLevel(boardGrid, boardController, goalTracker, moveCounter);
        Match3.BoosterManager.GetOrCreateInstance().BindToLevel(boardGrid, boardController, inputHandler, goalTracker, moveCounter);

        OnLevelInitialized?.Invoke();
    }

    public void QueueObstacleTutorials()
    {
        var tm = Match3.TutorialManager.Instance;
        if (tm == null) return;

        if (levelData.jellyPositions != null && levelData.jellyPositions.Length > 0)
            tm.RequestTutorial("obstacle_jelly");

        if (levelData.hardTilePositions != null && levelData.hardTilePositions.Length > 0)
            tm.RequestTutorial("obstacle_hardtile");

        if (levelData.stonePositions != null && levelData.stonePositions.Length > 0)
            tm.RequestTutorial("obstacle_stone");
    }

    private void ValidateObstacleGoalCounts()
    {
        if (goalTracker == null || levelData?.goals == null) return;

        int jellyCells    = levelData.jellyPositions?.Length ?? 0;
        int hardTileCells = levelData.hardTilePositions?.Length ?? 0;
        int stoneCells    = levelData.stonePositions?.Length ?? 0;

        foreach (var goal in goalTracker.Goals)
        {
            if (goal == null) continue;

            switch (goal.goalType)
            {
                case Match3.GoalType.ClearJelly when goal.requiredAmount > jellyCells:
                    Debug.LogWarning($"[LevelManager] Goal asks for {goal.requiredAmount} jelly cleared, " +
                                      $"but this level only has {jellyCells} jelly cell(s) — goal can NEVER complete. " +
                                      $"Fix LevelData.jellyPositions or the goal's Required Amount.", this);
                    break;

                case Match3.GoalType.ClearHardTile when goal.requiredAmount > hardTileCells:
                    Debug.LogWarning($"[LevelManager] Goal asks for {goal.requiredAmount} hard tile(s) broken, " +
                                      $"but this level only has {hardTileCells} hard tile(s) — goal can NEVER complete. " +
                                      $"Fix LevelData.hardTilePositions or the goal's Required Amount.", this);
                    break;

                case Match3.GoalType.CollectStone when goal.requiredAmount > stoneCells:
                    Debug.LogWarning($"[LevelManager] Goal asks for {goal.requiredAmount} stone(s) collected, " +
                                      $"but this level only has {stoneCells} stone(s) — goal can NEVER complete. " +
                                      $"Fix LevelData.stonePositions or the goal's Required Amount.", this);
                    break;
            }
        }
    }

    private void ValidateBlankOverlaps()
    {
        if (levelData.blankPositions == null || levelData.blankPositions.Length == 0) return;

        void Check(Vector2Int[] cells, string label)
        {
            if (cells == null) return;
            foreach (var c in cells)
                if (levelData.IsBlankCell(c.x, c.y))
                    Debug.LogWarning($"[LevelManager] {label} at ({c.x},{c.y}) overlaps a BLANK cell — it will be skipped. " +
                                     "Move it in the LevelData asset.", this);
        }

        Check(levelData.jellyPositions,    "Jelly");
        Check(levelData.hardTilePositions, "Hard tile");
        Check(levelData.stonePositions,    "Stone");

        if (boardGrid != null && boardGrid.PlayableCellCount < 9)
            Debug.LogWarning("[LevelManager] Fewer than 9 playable cells — this level may have no possible matches!", this);
    }

    private void InitializeLevel()
    {
        if (goalTracker != null && levelData.goals != null)
        {
            goalTracker.SetGoals(levelData.goals);
            goalTracker.ResetState();
        }

        if (moveCounter != null)
        {
            moveCounter.Initialize(levelData.moveLimit);
            OnMovesChanged?.Invoke(levelData.moveLimit);
        }

        if (boardGrid != null)
        {
            boardGrid.InitializeBoard(levelData.width, levelData.height);

            boardGrid.SetBlankCells(levelData.blankPositions);
            ValidateBlankOverlaps();
        }

        levelTimer?.Initialize(levelData);

        if (tileSpawner != null)
        {
            tileSpawner.SetLevel(levelData);
            tileSpawner.FillBoard();
        }

        jellyManager?.Setup(levelData, boardGrid);
        hardTileManager?.Setup(levelData, boardGrid);
        stoneManager?.Setup(levelData, boardGrid);

        ValidateObstacleGoalCounts();

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

    public void OnJellyCleared()    => goalTracker?.OnJellyCleared();
    public void OnHardTileCleared() => goalTracker?.OnHardTileCleared();
    public void OnStoneCollected()  => goalTracker?.OnStoneCollected();

    public void AddScore(int points)
    {
        Score += points;
        Match3.LevelSession.CurrentScore = Score;
        OnScoreChanged?.Invoke(Score);
        goalTracker?.OnScoreUpdated(Score);
    }
}
