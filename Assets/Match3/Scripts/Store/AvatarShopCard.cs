using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace Match3
{
    public class AvatarShopCard : MonoBehaviour
    {
        [SerializeField] private Image      avatarImage;
        [SerializeField] private TMP_Text   nameText;
        [SerializeField] private Button     actionButton;
        [SerializeField] private TMP_Text   actionLabel;
        [SerializeField] private GameObject lockOverlay;
        [SerializeField] private GameObject equippedBadge;
        [SerializeField] private TMP_Text   badgeText;
        [SerializeField] private GameObject coinIcon;

        [Tooltip("Seconds the 'Confirm?' state waits for the second tap.")]
        [SerializeField] private float confirmWindow = 3f;

        public AvatarPresetData Data { get; private set; }

        public event Action<string> OnMessage;

        public event Action<AvatarPresetData> OnEquipped;

        private bool _awaitingConfirm;
        private Coroutine _confirmRoutine;

        private void OnEnable()  => AvatarShopManager.OnAvatarsChanged += Refresh;
        private void OnDisable() => AvatarShopManager.OnAvatarsChanged -= Refresh;

        public void Setup(AvatarPresetData data)
        {
            Data = data;
            if (avatarImage != null) { avatarImage.sprite = data.sprite; avatarImage.preserveAspect = true; }
            if (nameText    != null) nameText.text = data.NameOrId;
            if (badgeText   != null)
            {
                badgeText.gameObject.SetActive(!string.IsNullOrEmpty(data.badgeText));
                badgeText.text = data.badgeText;
            }

            if (actionButton != null)
            {
                actionButton.onClick.RemoveAllListeners();
                actionButton.onClick.AddListener(OnActionClicked);
            }
            Refresh();
        }

        public void Refresh()
        {
            if (Data == null) return;
            bool owned    = AvatarShopManager.IsOwned(Data);
            bool equipped = AvatarShopManager.IsEquipped(Data);

            if (lockOverlay   != null) lockOverlay.SetActive(!owned);
            if (equippedBadge != null) equippedBadge.SetActive(equipped);
            if (coinIcon      != null) coinIcon.SetActive(!owned);
            if (avatarImage   != null) avatarImage.color = owned ? Color.white : new Color(0.6f, 0.6f, 0.6f, 1f);

            if (actionLabel != null)
            {
                if (equipped)             actionLabel.text = "Equipped";
                else if (owned)           actionLabel.text = "Equip";
                else if (_awaitingConfirm) actionLabel.text = "Confirm?";
                else                      actionLabel.text = Data.price.ToString("N0");
            }
            if (actionButton != null) actionButton.interactable = !equipped;
        }

        private void OnActionClicked()
        {
            AudioManager.Instance?.PlaySFX("button_click");
            if (Data == null) return;

            if (AvatarShopManager.IsOwned(Data))
            {
                if (AvatarShopManager.Equip(Data))
                {
                    transform.DOPunchScale(Vector3.one * 0.08f, 0.25f, 6, 0.6f);
                    OnMessage?.Invoke($"{Data.NameOrId} equipped!");
                    OnEquipped?.Invoke(Data);
                }
                return;
            }

            if (!_awaitingConfirm)
            {
                if (AvatarShopManager.CurrentCoins < Data.price)
                {
                    OnMessage?.Invoke(AvatarShopManager.MessageFor(AvatarShopManager.PurchaseResult.NotEnoughCoins, Data));
                    actionButton?.transform.DOShakePosition(0.3f, new Vector3(12, 0, 0), 20);
                    return;
                }
                _awaitingConfirm = true;
                Refresh();
                if (_confirmRoutine != null) StopCoroutine(_confirmRoutine);
                _confirmRoutine = StartCoroutine(ConfirmTimeout());
                return;
            }

            _awaitingConfirm = false;
            if (_confirmRoutine != null) StopCoroutine(_confirmRoutine);

            var result = AvatarShopManager.TryPurchase(Data);
            OnMessage?.Invoke(AvatarShopManager.MessageFor(result, Data));
            if (result == AvatarShopManager.PurchaseResult.Success)
                transform.DOPunchScale(Vector3.one * 0.15f, 0.4f, 8, 0.6f);
            Refresh();
        }

        private IEnumerator ConfirmTimeout()
        {
            yield return new WaitForSecondsRealtime(confirmWindow);
            _awaitingConfirm = false;
            Refresh();
        }
    }
}
