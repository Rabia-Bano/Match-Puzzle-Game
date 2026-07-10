// ============================================================
//  GoalIconRuntime.cs  —  MonoBehaviour
//
//  Attach to: GoalIconPrefab
//  GameHUD instantiates one per goal automatically.
//
//  Layout of GoalIconPrefab:
//  ┌──────────────┐
//  │   [Icon]     │  ← goalIcon Image (80x80)
//  │  [Count: 5]  │  ← countText TMP
//  │ [████░░░]   │  ← progressBar (fill horizontal)
//  │    [✓]       │  ← completeOverlay (hidden initially)
//  └──────────────┘
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace Match3
{
    public class GoalIconRuntime : MonoBehaviour
    {
        // ── Serialised — set in Prefab ────────────────────────

        [Header("UI Elements (set in prefab)")]
        [SerializeField] public Image            goalIconImage;
        [SerializeField] public TextMeshProUGUI  countText;        // remaining count
        [SerializeField] public Image            progressBar;      // fillAmount 0→1
        [SerializeField] public GameObject       completeOverlay;  // tick/checkmark
        [SerializeField] public GameObject       background;       // rounded card bg

        // ── Runtime data ──────────────────────────────────────

        private GoalData _goal;

        // ── Public API ────────────────────────────────────────

        public void Initialize(GoalData goal)
        {
            _goal = goal;

            // Set icon from GoalData asset
            if (goalIconImage != null)
            {
                if (goal.goalIcon != null)
                    goalIconImage.sprite = goal.goalIcon;
                else if (goal.targetTile != null && goal.targetTile.sprite != null)
                    goalIconImage.sprite = goal.targetTile.sprite;  // fallback: tile sprite
            }

            if (completeOverlay != null)
                completeOverlay.SetActive(false);

            // Animate in
            transform.localScale = Vector3.zero;
            transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);

            Refresh();
        }

        /// <summary>Called by GameHUD.OnGoalUpdated — refreshes count + bar.</summary>
        public void Refresh()
        {
            if (_goal == null) return;

            // Count text: show remaining
            if (countText != null)
                countText.text = _goal.IsComplete ? "✓" : _goal.Remaining.ToString();

            // Progress bar
            if (progressBar != null)
            {
                progressBar.DOKill();
                progressBar.DOFillAmount(_goal.Progress, 0.25f).SetEase(Ease.OutCubic);

                // Color bar based on progress
                progressBar.color = _goal.IsComplete
                    ? new Color(0.2f, 0.85f, 0.3f)   // green when done
                    : new Color(1f, 0.7f, 0.1f);       // orange when pending
            }

            // Complete overlay
            if (completeOverlay != null)
                completeOverlay.SetActive(_goal.IsComplete);
        }

        /// <summary>Called when this specific goal is completed — plays celebration.</summary>
        public void PlayCompleteAnim()
        {
            if (completeOverlay != null)
            {
                completeOverlay.SetActive(true);
                completeOverlay.transform.localScale = Vector3.zero;
                completeOverlay.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack);
            }

            // Icon bounce
            goalIconImage?.transform.DOPunchScale(Vector3.one * 0.4f, 0.4f, 6, 0.5f);

            // Background flash
            if (background != null)
            {
                var img = background.GetComponent<Image>();
                if (img != null)
                    img.DOColor(new Color(0.3f, 1f, 0.4f, 1f), 0.2f)
                       .OnComplete(() => img.DOColor(Color.white, 0.3f));
            }
        }
    }
}
