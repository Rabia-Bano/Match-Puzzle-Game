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
    public Match3.BoardGrid       boardGrid;
    public Match3.BoardController boardController;   // NEW — needed so PetManager can bind to this level
    public Match3.TileSpawner     tileSpawner;
    public Match3.InputHandler    inputHandler;

    [Header("Level Data")]
    [Tooltip("Fallback for Editor testing — LevelSession overrides at runtime")]
    public Match3.LevelData levelData;

    [Header("Phase 5 References")]
    public Match3.GoalTracker goalTracker;
    public Match3.MoveCounter moveCounter;

    [Header("Obstacle Systems (optional — leave blank if unused)")]
    public Match3.JellyManager    jellyManager;
    public Match3.HardTileManager hardTileManager;
    public Match3.StoneManager    stoneManager;

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

        // ── NEW: first-time "what is this obstacle?" tutorial cards ──
        // FIX: this used to be queued here (in Start(), same frame as the
        // goal/"Start" panel) — so the tutorial's full-screen dim overlay
        // covered the goal panel before the player ever got to read it or
        // tap Start. Per Rabia's request, the obstacle tutorial must not
        // appear until AFTER the player taps the goal panel's Start button.
        // So this call is REMOVED from here — LevelResultManager now calls
        // the public QueueObstacleTutorials() itself, right when
        // OnStartClicked() finishes closing the Start panel and enabling
        // input. See LevelResultManager.cs.
        // ──────────────────────────────────────────────────────────

        // ── NEW: re-bind PetManager to THIS level's board objects ────
        Debug.Log($"[LevelManager] About to bind PetManager. PetManager.Instance is " +
                  $"{(Match3.PetManager.Instance == null ? "NULL — PetManager not found!" : "found, OK")}. " +
                  $"boardController field is {(boardController == null ? "NULL — not wired in Inspector!" : "assigned, OK")}.");
        Match3.PetManager.Instance?.BindToLevel(boardGrid, boardController, goalTracker, moveCounter);
        Match3.BoosterManager.GetOrCreateInstance().BindToLevel(boardGrid, boardController, inputHandler, goalTracker, moveCounter);
        // ───────────────────────────────────────────────────────────

        // ── Notify LevelResultManager that we are ready ──────
        OnLevelInitialized?.Invoke();
        // ─────────────────────────────────────────────────────
    }

    /// <summary>
    /// Queues a first-time explainer card for each obstacle type present in
    /// this level's LevelData (Jelly / Hard Tile / Dropdown Stone), via
    /// TutorialManager. Each card only shows once ever per player — repeat
    /// calls (replaying a level, or a later level with the same obstacle) are
    /// harmless no-ops once TutorialManager has recorded it as seen.
    ///
    /// PUBLIC — called by LevelResultManager.OnStartClicked(), right after the
    /// goal panel's Start button closes the panel, so obstacle tutorials only
    /// ever appear AFTER the player has seen the goals and pressed Start.
    /// </summary>
    public void QueueObstacleTutorials()
    {
        var tm = Match3.TutorialManager.Instance;
        if (tm == null) return; // no TutorialManager in this scene — feature simply not used

        if (levelData.jellyPositions != null && levelData.jellyPositions.Length > 0)
            tm.RequestTutorial("obstacle_jelly");

        if (levelData.hardTilePositions != null && levelData.hardTilePositions.Length > 0)
            tm.RequestTutorial("obstacle_hardtile");

        if (levelData.stonePositions != null && levelData.stonePositions.Length > 0)
            tm.RequestTutorial("obstacle_stone");
    }

    /// <summary>
    /// Sanity check run once per level load: if a goal asks for more
    /// jelly/hard-tile/stone progress than the level actually placed, that
    /// goal can PHYSICALLY NEVER complete — the obstacle finishes clearing
    /// but the goal panel stays stuck below 100%. This looks exactly like a
    /// bug ("it cleared but the goal never finished") but is really a level
    /// data mismatch, so we catch it loudly here instead of silently.
    /// </summary>
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

        // Obstacles — placed AFTER FillBoard so they overwrite whatever
        // normal tile landed at their configured positions. Order matters:
        // jelly first (it's just an overlay, doesn't touch the Grid array),
        // then hard tiles / stones (which DO replace the grid's tile there).
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
        Match3.LevelSession.CurrentScore = Score;   // ← sync to session
        OnScoreChanged?.Invoke(Score);
        goalTracker?.OnScoreUpdated(Score);
    }
}