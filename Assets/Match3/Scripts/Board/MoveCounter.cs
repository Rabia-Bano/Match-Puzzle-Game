using UnityEngine;
using UnityEngine.Events;

namespace Match3
{
    public class MoveCounter : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Total moves for this level (overridden by LevelData at runtime).")]
        [SerializeField] private int totalMoves = 30;

        [Header("Events")]
        [Tooltip("Fires every time moves change. Int = remaining moves.")]
        public UnityEvent<int> OnMovesChanged;

        [Tooltip("Fires when remaining moves reach zero.")]
        public UnityEvent OnMovesExhausted;

        [Tooltip("Fires when 5 or fewer moves remain (warning state).")]
        public UnityEvent<int> OnLowMoves;

        public int TotalMoves     { get; private set; }
        public int MovesRemaining { get; private set; }
        public int MovesUsed      => TotalMoves - MovesRemaining;
        public bool IsExhausted   => MovesRemaining <= 0;

        [Tooltip("Show low-move warning when remaining <= this value.")]
        [SerializeField] private int lowMoveThreshold = 5;

        private void Awake()
        {
            Initialize(totalMoves);
        }

        public void Initialize(int moves)
        {
            TotalMoves     = moves;
            MovesRemaining = moves;
            OnMovesChanged?.Invoke(MovesRemaining);
        }

        public void UseMove()
        {
            if (MovesRemaining <= 0) return;

            MovesRemaining--;
            OnMovesChanged?.Invoke(MovesRemaining);

            Debug.Log($"[MoveCounter] Moves remaining: {MovesRemaining}/{TotalMoves}");

            if (MovesRemaining <= lowMoveThreshold && MovesRemaining > 0)
                OnLowMoves?.Invoke(MovesRemaining);

            if (MovesRemaining <= 0)
            {
                Debug.Log("[MoveCounter] No moves remaining!");
                OnMovesExhausted?.Invoke();
            }
        }

        public void ReduceMoves(int amount)
        {
            if (amount <= 0 || MovesRemaining <= 0) return;

            MovesRemaining = Mathf.Max(0, MovesRemaining - amount);
            OnMovesChanged?.Invoke(MovesRemaining);

            Debug.Log($"[MoveCounter] Boss attack removed {amount} move(s). Remaining: {MovesRemaining}/{TotalMoves}");

            if (MovesRemaining <= lowMoveThreshold && MovesRemaining > 0)
                OnLowMoves?.Invoke(MovesRemaining);

            if (MovesRemaining <= 0)
            {
                Debug.Log("[MoveCounter] No moves remaining (boss attack)!");
                OnMovesExhausted?.Invoke();
            }
        }

        public void AddMoves(int bonus)
        {
            MovesRemaining = Mathf.Min(MovesRemaining + bonus, TotalMoves);
            OnMovesChanged?.Invoke(MovesRemaining);
            Debug.Log($"[MoveCounter] +{bonus} bonus moves. Now: {MovesRemaining}");
        }

        public void AddBonusMoves(int bonus)
        {
            TotalMoves     += bonus;
            MovesRemaining += bonus;
            OnMovesChanged?.Invoke(MovesRemaining);
            Debug.Log($"[MoveCounter] +{bonus} guaranteed bonus moves. Now: {MovesRemaining}/{TotalMoves}");
        }

        public float UsedFraction =>
            TotalMoves > 0 ? (float)MovesUsed / TotalMoves : 0f;

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
