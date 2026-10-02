using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace Match3
{
    public enum BoosterType
    {
        Hammer  = 0,
        Crystal = 1,
        Beam    = 2,
        Refresh = 3
    }

    public class BoosterSlotUI : MonoBehaviour
    {
        [Header("Booster Config")]
        [SerializeField] private BoosterType boosterType;
        [SerializeField] private int         boosterCost = 50;

        [Header("UI Elements")]
        [SerializeField] private Button          button;
        [SerializeField] private Image           iconImage;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private GameObject      cooldownOverlay;
        [SerializeField] private TextMeshProUGUI cooldownText;
        [SerializeField] private Image           cooldownRadial;

        [Header("References")]
        [SerializeField] private BoardGrid   boardGrid;
        [SerializeField] private MoveCounter moveCounter;

        private int  _cooldownMovesLeft = 0;
        private bool _isOnCooldown      => _cooldownMovesLeft > 0;

        private void Start()
        {
            button?.onClick.AddListener(OnTap);

            if (costText != null)
                costText.text = boosterCost > 0 ? $"${boosterCost}" : "Free";

            if (moveCounter != null)
                moveCounter.OnMovesChanged.AddListener(OnMoveUsed);

            RefreshVisual();
        }

        private void OnTap()
        {
            if (_isOnCooldown)
            {
                transform.DOShakePosition(0.3f, 5f, 10);
                return;
            }

            PlayerProfile profile = LocalSaveManager.GetOrLoadProfile();
            if (profile == null)
            {
                Debug.LogWarning("[BoosterSlotUI] No local profile found — treating as 0 coins.");
                transform.DOShakePosition(0.3f, 5f, 10);
                return;
            }

            if (profile.coins < boosterCost)
            {
                Debug.Log("[BoosterSlotUI] Not enough coins!");
                transform.DOShakePosition(0.3f, 5f, 10);
                return;
            }

            if (boosterCost > 0)
            {
                profile.coins -= boosterCost;
                LocalSaveManager.SaveProfile(profile);
            }

            ActivateBooster();

            _cooldownMovesLeft = 3;
            RefreshVisual();

            transform.DOPunchScale(Vector3.one * 0.25f, 0.2f, 5, 0.5f);
        }

        private void ActivateBooster()
        {
            switch (boosterType)
            {
                case BoosterType.Hammer:
                    Debug.Log("[Booster] Hammer — tap a tile to destroy it.");
                    break;

                case BoosterType.Crystal:
                    moveCounter?.AddBonusMoves(2);
                    Debug.Log("[Booster] Crystal — +2 moves");
                    break;

                case BoosterType.Beam:
                    Debug.Log("[Booster] Beam — clears center column.");
                    if (boardGrid != null)
                    {
                        int centerCol = boardGrid.Width / 2;
                    }
                    break;

                case BoosterType.Refresh:
                    Debug.Log("[Booster] Refresh — shuffle board.");
                    break;
            }
        }

        private void OnMoveUsed(int remaining)
        {
            if (_cooldownMovesLeft > 0)
            {
                _cooldownMovesLeft--;
                RefreshVisual();
            }
        }

        public void Refresh() => RefreshVisual();

        private void RefreshVisual()
        {
            bool onCooldown = _isOnCooldown;

            if (cooldownOverlay != null)
                cooldownOverlay.SetActive(onCooldown);

            if (cooldownText != null)
                cooldownText.text = onCooldown ? _cooldownMovesLeft.ToString() : "";

            if (cooldownRadial != null)
                cooldownRadial.fillAmount = onCooldown
                    ? (float)_cooldownMovesLeft / 3f
                    : 0f;

            if (button != null)
                button.interactable = !onCooldown;

            if (iconImage != null)
                iconImage.color = onCooldown
                    ? new Color(0.5f, 0.5f, 0.5f, 1f)
                    : Color.white;
        }
    }
}
