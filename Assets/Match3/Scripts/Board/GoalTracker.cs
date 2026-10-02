using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Match3
{
    public class GoalTracker : MonoBehaviour
    {
        [Header("Goals (set at runtime via SetGoals)")]
        [SerializeField] private GoalData[] goals;

        [Header("Events")]
        public UnityEvent<int> OnGoalProgressUpdated;
        public UnityEvent      OnAllGoalsComplete;
        public UnityEvent<int> OnGoalCompleted;

        public bool AllGoalsComplete
        {
            get
            {
                if (goals == null || goals.Length == 0) return false;
                foreach (var g in goals)
                    if (g == null || !g.IsComplete) return false;
                return true;
            }
        }

        public IReadOnlyList<GoalData> Goals => goals;

        private void Awake() {   }

        public void SetGoals(GoalData[] levelGoals)
        {
            goals = levelGoals;
            if (goals == null) { Debug.LogError("[GoalTracker] SetGoals: null array!"); return; }
            foreach (var g in goals)
                if (g != null) g.ResetProgress();
            Debug.Log($"[GoalTracker] {goals.Length} goals set.");
        }

        public void OnTileCleared(TileData tile)
        {
            if (tile == null || goals == null) return;

            for (int i = 0; i < goals.Length; i++)
            {
                GoalData goal = goals[i];
                if (goal == null || goal.IsComplete) continue;

                bool relevant = false;
                switch (goal.goalType)
                {
                    case GoalType.CollectTile:
                        if (goal.targetTile == null)
                            relevant = true;
                        else if (goal.targetTile == tile)
                            relevant = true;
                        else if (goal.targetTile.color != TileColor.None
                              && goal.targetTile.color == tile.color)
                            relevant = true;
                        break;
                    case GoalType.ClearJelly:
                        break;
                    case GoalType.ReachScore:
                        break;
                }
                if (relevant) UpdateGoal(i, 1);
            }
        }

        public void OnJellyCleared()
        {
            if (goals == null) return;
            for (int i = 0; i < goals.Length; i++)
            {
                GoalData goal = goals[i];
                if (goal == null || goal.IsComplete) continue;
                if (goal.goalType == GoalType.ClearJelly) UpdateGoal(i, 1);
            }
        }

        public void OnHardTileCleared()
        {
            if (goals == null) return;
            for (int i = 0; i < goals.Length; i++)
            {
                GoalData goal = goals[i];
                if (goal == null || goal.IsComplete) continue;
                if (goal.goalType == GoalType.ClearHardTile) UpdateGoal(i, 1);
            }
        }

        public void OnStoneCollected()
        {
            if (goals == null) return;
            bool matchedAnyGoal = false;
            for (int i = 0; i < goals.Length; i++)
            {
                GoalData goal = goals[i];
                if (goal == null || goal.IsComplete) continue;
                if (goal.goalType == GoalType.CollectStone)
                {
                    matchedAnyGoal = true;
                    UpdateGoal(i, 1);
                }
            }
            if (!matchedAnyGoal)
                Debug.Log("[GoalTracker] OnStoneCollected() called but no incomplete CollectStone goal exists " +
                          "on this level (either there's no such goal, or it's already complete).");
        }

        public void OnScoreUpdated(int totalScore)
        {
            if (goals == null) return;
            for (int i = 0; i < goals.Length; i++)
            {
                GoalData goal = goals[i];
                if (goal == null || goal.goalType != GoalType.ReachScore) continue;
                if (goal.IsComplete) continue;

                goal.currentAmount = Mathf.Min(totalScore, goal.requiredAmount);
                OnGoalProgressUpdated?.Invoke(i);

                if (goal.IsComplete)
                {
                    OnGoalCompleted?.Invoke(i);
                    Debug.Log($"[GoalTracker] Score Goal {i} COMPLETE!");
                }
                CheckAllComplete();
            }
        }

        private void UpdateGoal(int index, int delta)
        {
            GoalData goal = goals[index];
            bool justCompleted = goal.AddProgress(delta);
            OnGoalProgressUpdated?.Invoke(index);
            if (justCompleted)
            {
                OnGoalCompleted?.Invoke(index);
                Debug.Log($"[GoalTracker] Goal {index} COMPLETE!");
            }
            CheckAllComplete();
        }

        private bool _allCompleteFired = false;

        private void CheckAllComplete()
        {
            if (_allCompleteFired || !AllGoalsComplete) return;
            _allCompleteFired = true;
            OnAllGoalsComplete?.Invoke();
            Debug.Log("[GoalTracker] ALL GOALS COMPLETE!");
        }

        public void ResetState()
        {
            _allCompleteFired = false;
            if (goals == null) return;
            foreach (var g in goals)
                if (g != null) g.ResetProgress();
        }
    }
}
