// ============================================================
//  LevelHUD.cs  —  Updated: GameManager → LevelManager
//
//  gameManager.OnScoreChanged → levelManager.OnScoreChanged
//  gameManager.Score          → levelManager.Score
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace Match3
{
    public class LevelHUD : MonoBehaviour
    {
        [Header("Move Counter UI")]
        [SerializeField] private TextMeshProUGUI moveCountText;
        [SerializeField] private Image           moveCountIcon;
        [SerializeField] private Color           normalMoveColor = Color.white;
        [SerializeField] private Color           lowMoveColor    = Color.red;

        [Header("Score UI")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private float           scoreRollDuration = 0.4f;

        [Header("Rotation HUD")]
        [SerializeField] private GameObject      rotationPanel;
        [SerializeField] private TextMeshProUGUI rotationCountText;
        [SerializeField] private Image           rotationFillBar;

        [Header("Goal Panels")]
        [SerializeField] private List<GoalPanelUI> goalPanels;

        [Header("Data Sources")]
        [SerializeField] private MoveCounter   moveCounter;
        [SerializeField] private GoalTracker   goalTracker;
        [SerializeField] private LevelManager  levelManager;   // ← was GameManager
        [SerializeField] private BoardRotation boardRotation;

        private int   _displayedScore;
        private Tween _scoreTween;

        private void Start()
        {
            if (moveCounter != null)
            {
                moveCounter.OnMovesChanged.AddListener(UpdateMoveCount);
                moveCounter.OnLowMoves.AddListener(OnLowMovesWarning);
                UpdateMoveCount(moveCounter.MovesRemaining);
            }

            if (levelManager != null)
            {
                levelManager.OnScoreChanged.AddListener(UpdateScore);  // ← was gameManager
                UpdateScore(0);
            }

            if (goalTracker != null)
            {
                goalTracker.OnGoalProgressUpdated.AddListener(UpdateGoalPanel);
                InitGoalPanels();
            }

            if (boardRotation != null)
            {
                boardRotation.OnBeforeRotation += OnBeforeRotation;
                boardRotation.OnAfterRotation  += OnAfterRotation;
                UpdateRotationHUD();
            }
            else if (rotationPanel != null)
                rotationPanel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (boardRotation != null)
            {
                boardRotation.OnBeforeRotation -= OnBeforeRotation;
                boardRotation.OnAfterRotation  -= OnAfterRotation;
            }
        }

        private void UpdateMoveCount(int remaining)
        {
            if (moveCountText == null) return;
            moveCountText.text  = remaining.ToString();
            moveCountText.color = remaining <= 5 ? lowMoveColor : normalMoveColor;
            moveCountText.transform.DOKill();
            moveCountText.transform.localScale = Vector3.one;
            moveCountText.transform.DOPunchScale(Vector3.one * 0.25f, 0.2f, 4, 0.5f);
            UpdateRotationHUD();
        }

        private void OnLowMovesWarning(int remaining)
        {
            if (moveCountText == null) return;
            moveCountText.transform.DOKill();
            moveCountText.transform.DOShakePosition(0.4f, strength: 6f, vibrato: 10);
        }

        private void UpdateScore(int newScore)
        {
            if (scoreText == null) return;
            _scoreTween?.Kill();
            int from = _displayedScore;
            _scoreTween = DOTween.To(
                () => from,
                v  => { scoreText.text = v.ToString("N0"); },
                newScore, scoreRollDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => _displayedScore = newScore);
        }

        private void InitGoalPanels()
        {
            var goals = goalTracker.Goals;
            for (int i = 0; i < goalPanels.Count; i++)
            {
                if (i < goals.Count)
                { goalPanels[i].gameObject.SetActive(true); goalPanels[i].Initialize(goals[i]); }
                else
                    goalPanels[i].gameObject.SetActive(false);
            }
        }

        private void UpdateGoalPanel(int goalIndex)
        {
            if (goalIndex < 0 || goalIndex >= goalPanels.Count) return;
            goalPanels[goalIndex].Refresh();
        }

        private void UpdateRotationHUD()
        {
            if (boardRotation == null || rotationPanel == null) return;
            int until  = boardRotation.MovesUntilRotation;
            int perRot = 5;
            if (rotationCountText != null) rotationCountText.text = until.ToString();
            if (rotationFillBar   != null) rotationFillBar.fillAmount = 1f - ((float)until / perRot);
        }

        private void OnBeforeRotation(int count)
        {
            if (rotationPanel == null) return;
            rotationPanel.transform.DOKill();
            rotationPanel.transform.DOPunchScale(Vector3.one * 0.3f, 0.3f, 5, 0.5f);
        }

        private void OnAfterRotation(int count) => UpdateRotationHUD();

        private void Update()
        {
            if (boardRotation != null) UpdateRotationHUD();
        }
    }

    [System.Serializable]
    public class GoalPanelUI
    {
        [Header("Goal Panel UI Elements")]
        public GameObject       gameObject;
        public Image            iconImage;
        public Image            progressBar;
        public TextMeshProUGUI  countText;
        public GameObject       completeOverlay;

        private GoalData _goal;

        public void Initialize(GoalData goal)
        {
            _goal = goal;
            if (iconImage != null && goal.goalIcon != null)
                iconImage.sprite = goal.goalIcon;
            if (completeOverlay != null) completeOverlay.SetActive(false);
            Refresh();
        }

        public void Refresh()
        {
            if (_goal == null) return;
            if (progressBar != null)
            { progressBar.DOKill(); progressBar.DOFillAmount(_goal.Progress, 0.25f).SetEase(Ease.OutQuad); }
            if (countText != null)
                countText.text = _goal.IsComplete ? "Done!" : $"{_goal.Remaining}";
            if (completeOverlay != null && _goal.IsComplete)
            {
                completeOverlay.SetActive(true);
                completeOverlay.transform.localScale = Vector3.zero;
                completeOverlay.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
            }
        }
    }
}
