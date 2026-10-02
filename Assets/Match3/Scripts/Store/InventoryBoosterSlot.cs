using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace Match3
{
    public class InventoryBoosterSlot : MonoBehaviour
    {
        [Header("Booster Config")]
        [Tooltip("Must match a BoosterManager id constant, e.g. BoosterManager.Hammer.")]
        [SerializeField] private string boosterId = BoosterManager.Hammer;

        [Header("UI Elements")]
        [SerializeField] private Button          button;
        [SerializeField] private Image           iconImage;
        [SerializeField] private TextMeshProUGUI countText;
        [SerializeField] private GameObject      targetingOutline;

        private void OnEnable()
        {
            button?.onClick.AddListener(OnTap);
            LocalSaveManager.OnProfileChanged += HandleInventoryChanged;

            BoosterManager manager = BoosterManager.GetOrCreateInstance();
            manager.OnTargetingStarted += HandleTargetingStarted;
            manager.OnTargetingEnded   += HandleTargetingEnded;
            manager.OnBoosterUsed      += HandleBoosterUsed;
            manager.OnBound            += Refresh;

            Refresh();
        }

        private void OnDisable()
        {
            button?.onClick.RemoveListener(OnTap);
            LocalSaveManager.OnProfileChanged -= HandleInventoryChanged;

            if (BoosterManager.Instance != null)
            {
                BoosterManager.Instance.OnTargetingStarted -= HandleTargetingStarted;
                BoosterManager.Instance.OnTargetingEnded   -= HandleTargetingEnded;
                BoosterManager.Instance.OnBoosterUsed      -= HandleBoosterUsed;
                BoosterManager.Instance.OnBound            -= Refresh;
            }
        }

        private void OnTap()
        {
            if (BoosterManager.Instance == null) return;

            if (!BoosterManager.Instance.CanUse(boosterId))
            {
                transform.DOShakePosition(0.3f, 5f, 10);
                return;
            }

            BoosterManager.Instance.TryActivate(boosterId);
            transform.DOPunchScale(Vector3.one * 0.2f, 0.2f, 5, 0.5f);
        }

        private void HandleInventoryChanged(PlayerProfile profile) => Refresh();
        private void HandleBoosterUsed(string usedId) => Refresh();

        private void HandleTargetingStarted(string activeBoosterId)
        {
            bool isThisOne = activeBoosterId == boosterId;
            if (targetingOutline != null) targetingOutline.SetActive(isThisOne);
            if (button != null && !isThisOne) button.interactable = false;
        }

        private void HandleTargetingEnded() => Refresh();

        public void Refresh()
        {
            int owned = 0;
            if (LocalSaveManager.LoadBoosterInventory().TryGetValue(boosterId, out int c)) owned = c;

            if (countText != null) countText.text = owned > 0 ? owned.ToString() : "0";

            bool usable = BoosterManager.Instance != null && BoosterManager.Instance.CanUse(boosterId);
            if (button != null) button.interactable = usable;
            if (iconImage != null) iconImage.color = usable ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
            if (targetingOutline != null) targetingOutline.SetActive(false);
        }
    }
}
