// ============================================================
//  MoveCounter.cs  —  MonoBehaviour
//
//  Tracks remaining moves for the current level.
//  SwapController calls UseMove() after each valid swap.
//  Fires events for UI updates and game-over detection.
//
//  Attach to: MoveCounter (empty GameObject)
// ============================================================

using UnityEngine;
using UnityEngine.Events;

namespace Match3
{
    public class MoveCounter : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────

        [Header("Settings")]
        [Tooltip("Total moves for this level (overridden by LevelData at runtime).")]
        [SerializeField] private int totalMoves = 30;

        // ── Events ────────────────────────────────────────────

        [Header("Events")]
        [Tooltip("Fires every time moves change. Int = remaining moves.")]
        public UnityEvent<int> OnMovesChanged;

        [Tooltip("Fires when remaining moves reach zero.")]
        public UnityEvent OnMovesExhausted;

        [Tooltip("Fires when 5 or fewer moves remain (warning state).")]
        public UnityEvent<int> OnLowMoves;

        // ── Public state ──────────────────────────────────────

        public int TotalMoves     { get; private set; }
        public int MovesRemaining { get; private set; }
        public int MovesUsed      => TotalMoves - MovesRemaining;
        public bool IsExhausted   => MovesRemaining <= 0;

        [Tooltip("Show low-move warning when remaining <= this value.")]
        [SerializeField] private int lowMoveThreshold = 5;

        // ── Lifecycle ─────────────────────────────────────────

        private void Awake()
        {
            Initialize(totalMoves);
        }

        // ── Public API ────────────────────────────────────────

        /// <summary>
        /// Sets total moves and resets counter.
        /// Called by GameManager/LevelLoader at level start.
        /// </summary>
        public void Initialize(int moves)
        {
            TotalMoves     = moves;
            MovesRemaining = moves;
            OnMovesChanged?.Invoke(MovesRemaining);
        }

        /// <summary>
        /// Decrements remaining moves by 1.
        /// Called by SwapController after each valid swap.
        /// </summary>
        public void UseMove()
        {
            if (MovesRemaining <= 0) return;

            MovesRemaining--;
            OnMovesChanged?.Invoke(MovesRemaining);

            Debug.Log($"[MoveCounter] Moves remaining: {MovesRemaining}/{TotalMoves}");

            // Low-move warning
            if (MovesRemaining <= lowMoveThreshold && MovesRemaining > 0)
                OnLowMoves?.Invoke(MovesRemaining);

            // Out of moves
            if (MovesRemaining <= 0)
            {
                Debug.Log("[MoveCounter] No moves remaining!");
                OnMovesExhausted?.Invoke();
            }
        }

        /// <summary>
        /// Adds bonus moves (e.g. from store purchase or level bonus).
        /// </summary>
        public void AddMoves(int bonus)
        {
            MovesRemaining = Mathf.Min(MovesRemaining + bonus, TotalMoves);
            OnMovesChanged?.Invoke(MovesRemaining);
            Debug.Log($"[MoveCounter] +{bonus} bonus moves. Now: {MovesRemaining}");
        }

        /// <summary>
        /// Returns a 0-1 fraction of moves used (for progress bars).
        /// </summary>
        public float UsedFraction =>
            TotalMoves > 0 ? (float)MovesUsed / TotalMoves : 0f;

        /// <summary>
        /// Star rating based on moves remaining:
        ///   3 stars: >= 50% moves left
        ///   2 stars: >= 20% moves left
        ///   1 star:  any moves left (or just completed)
        /// </summary>
        public int GetStarRating()
        {
            float leftFraction = TotalMoves > 0
                ? (float)MovesRemaining / TotalMoves : 0f;

            if (leftFraction >= 0.5f) return 3;
            if (leftFraction >= 0.2f) return 2;
            return 1;
        }
    }
}
