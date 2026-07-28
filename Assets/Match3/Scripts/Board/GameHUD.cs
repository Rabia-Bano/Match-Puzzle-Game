// ============================================================
//  GameHUD.cs  —  Updated: GameManager → LevelManager
//
//  gameManager.OnScoreChanged → levelManager.OnScoreChanged
//  gameManager.Score          → levelManager.Score
//
//  UPDATE (Pet system swap): the old PetSystem-based "PET PANEL"
//  block (petHPBarFill / petHPText / petAvatarImage / petHPBarPanel /
//  petSystem field + OnPetHPChanged/RefreshPetHP) has been removed.
//  That whole panel is now its own component — PetHUD.cs — driven by
//  PetManager's charge events instead of PetSystem's HP events. Put
//  PetHUD.cs on its own panel GameObject in the same Canvas and wire
//  it up separately; GameHUD no longer needs to know pets exist.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace Match3
{
    public class GameHUD : MonoBehaviour
    {
        [Header("━━ TOP BAR ━━")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private Image           scoreProgressBar;
        [SerializeField] private TextMeshProUGUI moveNumberText;
        [SerializeField] private TextMeshProUGUI moveLabelText;
        [SerializeField] private Color           normalMoveColor = Color.white;
        [SerializeField] private Color           lowMoveColor    = new Color(1f, 0.25f, 0.25f);

        [Header("━━ AUTO GOAL PANEL ━━")]
        [SerializeField] private Transform       goalIconContainer;
        [SerializeField] private GameObject      goalIconPrefab;

        [Header("━━ BOOSTER BOTTOM PANEL ━━")]
        [SerializeField] private BoosterSlotUI[] boosterSlots;

        [Header("━━ SETTINGS BUTTON ━━")]
        [SerializeField] private Button settingsButton;

        [Header("━━ DATA SOURCES ━━")]
        [SerializeField] private GoalTracker  goalTracker;
        [SerializeField] private MoveCounter  moveCounter;
        [SerializeField] private LevelManager levelManager;  // ← was GameManager

        [Header("━━ SCORE BAR ━━")]
        [SerializeField] private int scoreBarMaxValue = 2000;

        private int _displayedScore;
        private readonly List<GoalIconRuntime> _goalIcons = new();

        private void Start()
        {
            if (moveCounter != null)
            {
                moveCounter.OnMovesChanged.AddListener(OnMovesChanged);
                moveCounter.OnLowMoves.AddListener(OnLowMoves);
                OnMovesChanged(moveCounter.MovesRemaining);
            }

            if (levelManager != null)
            {
                levelManager.OnScoreChanged.AddListener(OnScoreChanged);  // ← was gameManager
                OnScoreChanged(0);
            }

            if (goalTracker != null)
            {
                goalTracker.OnGoalProgressUpdated.AddListener(OnGoalUpdated);
                goalTracker.OnGoalCompleted.AddListener(OnGoalCompleted);
                StartCoroutine(BuildGoalIconsDeferred());
            }

            settingsButton?.onClick.AddListener(OnSettingsClicked);
        }

        private void OnMovesChanged(int remaining)
        {
            if (moveNumberText == null) return;
            moveNumberText.text  = remaining.ToString();
            // FIX: was only ever SET to red inside OnLowMoves() below and never
            // reset back — so once a level triggered the low-moves warning,
            // the text stayed red forever after (through boosters adding moves,
            // and into the next level), since nothing recalculated the colour
            // on a normal moves-changed update. Recalculate it fresh every time.
            moveNumberText.color = remaining <= 5 ? lowMoveColor : normalMoveColor;
            moveNumberText.transform.DOKill();
            moveNumberText.transform.localScale = Vector3.one;
            moveNumberText.transform.DOPunchScale(Vector3.one * 0.3f, 0.2f, 5, 0.5f);
        }

        private void OnLowMoves(int remaining)
        {
            if (moveNumberText == null) return;
            moveNumberText.transform.DOShakePosition(0.5f, 8f, 12);
        }

        private void OnScoreChanged(int newScore)
        {
            if (scoreText != null)
            {
                int from = _displayedScore;
                DOTween.To(() => from,
                    v => scoreText.text = $"Score: {v:N0}",
                    newScore, 0.4f).SetEase(Ease.OutQuad);
            }
            if (scoreProgressBar != null)
            {
                float target = Mathf.Clamp01((float)newScore / scoreBarMaxValue);
                scoreProgressBar.DOFillAmount(target, 0.35f).SetEase(Ease.OutQuad);
            }
            _displayedScore = newScore;
        }

        private IEnumerator BuildGoalIconsDeferred()
        {
            yield return null;
            if (goalIconContainer == null || goalIconPrefab == null) yield break;
            foreach (Transform child in goalIconContainer) Destroy(child.gameObject);
            _goalIcons.Clear();

            var goals = goalTracker.Goals;
            for (int i = 0; i < goals.Count; i++)
            {
                GameObject go = Instantiate(goalIconPrefab, goalIconContainer);
                go.name = $"GoalIcon_{i}";
                var runtime = go.GetComponent<GoalIconRuntime>() ?? go.AddComponent<GoalIconRuntime>();
                runtime.Initialize(goals[i]);
                _goalIcons.Add(runtime);
            }
        }

        private void OnGoalUpdated(int goalIndex)
        { if (goalIndex >= 0 && goalIndex < _goalIcons.Count) _goalIcons[goalIndex].Refresh(); }

        private void OnGoalCompleted(int goalIndex)
        { if (goalIndex >= 0 && goalIndex < _goalIcons.Count) _goalIcons[goalIndex].PlayCompleteAnim(); }

        public void RefreshBoosterSlot(int index)
        {
            if (index >= 0 && index < boosterSlots.Length) boosterSlots[index].Refresh();
        }

        private void OnSettingsClicked() => Debug.Log("[GameHUD] Settings clicked.");
    }
}