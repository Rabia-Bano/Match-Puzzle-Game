using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Match3
{
    public class StoreItemCard : MonoBehaviour
    {
        [Header("Visuals")]
        [SerializeField] private Image    iconImage;
        [SerializeField] private TMP_Text ownedCountText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text priceText;

        [Header("Buy Button")]
        [SerializeField] private Button   buyButton;
        [SerializeField] private TMP_Text buyButtonText;

        public StoreItem Data { get; private set; }

        private void Awake()
        {
            buyButton?.onClick.AddListener(OnBuyTapped);
            buyButton?.onClick.AddListener(() => AudioManager.Instance?.PlaySFX("button_click"));
        }

        public void Setup(StoreItem item, int ownedCount)
        {
            Data = item;

            if (ownedCountText != null) ownedCountText.text = ownedCount.ToString();
            if (nameText != null)       nameText.text = item.displayName;

            if (iconImage != null)
            {
                Sprite icon = Resources.Load<Sprite>($"StoreIcons/{item.spriteKey}");
                if (icon != null) iconImage.sprite = icon;
            }

            if (priceText != null) priceText.text = $"Coins : {item.coinPrice}";
            if (buyButtonText != null) buyButtonText.text = "Buy";
            if (buyButton != null) buyButton.interactable = true;
        }

        private async void OnBuyTapped()
        {
            if (Data == null) return;
            if (buyButton != null) buyButton.interactable = false;

            bool success = await StoreManager.Instance.BuyWithCoins(Data.id, Data.coinPrice);

            if (buyButton != null) buyButton.interactable = true;

            if (success) transform.DOPunchScale(Vector3.one * 0.15f, 0.2f, 5, 0.5f);
        }
    }
}
