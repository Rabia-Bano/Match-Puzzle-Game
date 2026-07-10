// ============================================================
//  BoosterSlotUI.cs  —  MonoBehaviour
//
//  Attach to each booster button in the bottom panel.
//  Handles: icon, cooldown overlay, tap activation.
//
//  Bottom panel layout (from image):
//  [Pet Button] [Hammer] [Crystal] [Beam] [Refresh]
//   → Pet button handled by PetSystem.OnPetButtonTapped()
//   → Other 4 are BoosterSlotUI components
//
//  BoosterType enum matches the visual icons in the image:
//    Hammer   — destroys one tile of player's choice
//    Crystal  — adds +2 moves  
//    Beam     — clears center column
//    Refresh  — reshuffles the board
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace Match3
{
    public enum BoosterType
    {
        Hammer  = 0,   // destroy 1 tile
        Crystal = 1,   // +2 moves
        Beam    = 2,   // clear center column
        Refresh = 3    // shuffle board
    }

    public class BoosterSlotUI : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────

        [Header("Booster Config")]
        [SerializeField] private BoosterType boosterType;
        [SerializeField] private int         boosterCost = 50;   // coins

        [Header("UI Elements")]
        [SerializeField] private Button          button;
        [SerializeField] private Image           iconImage;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private GameObject      cooldownOverlay;  // dark overlay when unavailable
        [SerializeField] private TextMeshProUGUI cooldownText;     // "5" countdown
        [SerializeField] private Image           cooldownRadial;   // radial fill cooldown

        [Header("References")]
        [SerializeField] private BoardGrid   boardGrid;
        [SerializeField] private MoveCounter moveCounter;

        // ── Private ───────────────────────────────────────────

        private int  _cooldownMovesLeft = 0;
        private bool _isOnCooldown      => _cooldownMovesLeft > 0;

        // ── Lifecycle ─────────────────────────────────────────

        private void Start()
        {
            button?.onClick.AddListener(OnTap);

            if (costText != null)
                costText.text = boosterCost > 0 ? $"${boosterCost}" : "Free";

            if (moveCounter != null)
                moveCounter.OnMovesChanged.AddListener(OnMoveUsed);

            RefreshVisual();
        }

        // ── Tap handler ───────────────────────────────────────

        private void OnTap()
        {
            if (_isOnCooldown)
            {
                // Shake to indicate not ready
                transform.DOShakePosition(0.3f, 5f, 10);
                return;
            }

            // Check coins (SaveManager)
            if (SaveManager.Instance != null &&
                SaveManager.Instance.Profile.coins < boosterCost)
            {
                Debug.Log("[BoosterSlotUI] Not enough coins!");
                // TODO: show "not enough coins" popup
                transform.DOShakePosition(0.3f, 5f, 10);
                return;
            }

            // Deduct coins
            if (boosterCost > 0 && SaveManager.Instance != null)
            {
                SaveManager.Instance.Profile.coins -= boosterCost;
                SaveManager.Instance.SaveProfile();
            }

            // Activate
            ActivateBooster();

            // Set cooldown
            _cooldownMovesLeft = 3;
            RefreshVisual();

            // Tap animation
            transform.DOPunchScale(Vector3.one * 0.25f, 0.2f, 5, 0.5f);
        }

        // ── Booster logic ─────────────────────────────────────

        private void ActivateBooster()
        {
            switch (boosterType)
            {
                case BoosterType.Hammer:
                    Debug.Log("[Booster] Hammer — tap a tile to destroy it.");
                    // TODO: enter "select tile" mode → BoardController.ActivateHammerMode()
                    break;

                case BoosterType.Crystal:
                    moveCounter?.AddMoves(2);
                    Debug.Log("[Booster] Crystal — +2 moves");
                    break;

                case BoosterType.Beam:
                    Debug.Log("[Booster] Beam — clears center column.");
                    if (boardGrid != null)
                    {
                        int centerCol = boardGrid.Width / 2;
                        // Reuse SpecialTileActivator logic via event or direct call
                        // For now: direct column clear
                    }
                    break;

                case BoosterType.Refresh:
                    Debug.Log("[Booster] Refresh — shuffle board.");
                    // TODO: call BoardController.ShuffleBoard()
                    break;
            }
        }

        // ── Cooldown ──────────────────────────────────────────

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

            // Dim icon when on cooldown
            if (iconImage != null)
                iconImage.color = onCooldown
                    ? new Color(0.5f, 0.5f, 0.5f, 1f)
                    : Color.white;
        }
    }
}
