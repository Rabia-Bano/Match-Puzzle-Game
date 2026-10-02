using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace Match3
{
    public class GoalIconRuntime : MonoBehaviour
    {
        [Header("UI Elements (set in prefab)")]
        [SerializeField] public Image            goalIconImage;
        [SerializeField] public TextMeshProUGUI  countText;
        [SerializeField] public Image            progressBar;
        [SerializeField] public GameObject       completeOverlay;
        [SerializeField] public GameObject       background;

        private GoalData _goal;

        public void Initialize(GoalData goal)
        {
            _goal = goal;

            if (goalIconImage != null)
            {
                if (goal.goalIcon != null)
                    goalIconImage.sprite = goal.goalIcon;
                else if (goal.targetTile != null && goal.targetTile.sprite != null)
                    goalIconImage.sprite = goal.targetTile.sprite;
            }

            if (completeOverlay != null)
                completeOverlay.SetActive(false);

            transform.localScale = Vector3.zero;
            transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);

            Refresh();
        }

        public void Refresh()
        {
            if (_goal == null) return;

            if (countText != null)
                countText.text = _goal.IsComplete ? "✓" : _goal.Remaining.ToString();

            if (progressBar != null)
            {
                progressBar.DOKill();
                progressBar.DOFillAmount(_goal.Progress, 0.25f).SetEase(Ease.OutCubic);

                progressBar.color = _goal.IsComplete
                    ? new Color(0.2f, 0.85f, 0.3f)
                    : new Color(1f, 0.7f, 0.1f);
            }

            if (completeOverlay != null)
                completeOverlay.SetActive(_goal.IsComplete);
        }

        public void PlayCompleteAnim()
        {
            if (completeOverlay != null)
            {
                completeOverlay.SetActive(true);
                completeOverlay.transform.localScale = Vector3.zero;
                completeOverlay.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack);
            }

            goalIconImage?.transform.DOPunchScale(Vector3.one * 0.4f, 0.4f, 6, 0.5f);

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
