// ============================================================
//  BossIntroPanel.cs  —  MonoBehaviour
//
//  Boss Arena's "press Start to begin" card — icon + name + weakness
//  tile, with Start (begin fight) and X (back out) buttons.
//
//  UPDATED — no longer subscribes to LevelManager.OnLevelInitialized.
//  That design silently failed whenever this GameObject happened to
//  start INACTIVE in the scene: OnEnable() (where the subscription
//  lived) never runs on an inactive object, so the subscription never
//  happened, and the event fired into nothing — panel never showed,
//  and by the time anyone manually re-activated the object it was too
//  late (the event had already fired once, in the past).
//
//  Now BossController — which reliably runs its own Awake()/Start()
//  every time (proven: regular match damage already worked, so its
//  lifecycle definitely executes) — calls ShowIntro() on this panel
//  DIRECTLY once bossData is confirmed ready. No event, no timing
//  race, no dependency on this GameObject's saved active-state.
//
//  Attach to: "BossIntroPanel" GameObject, a child of BossUICanvas.
//  Its OWN GameObject's active checkbox no longer matters — leave it
//  either way, BossController forces it active before calling in.
//
//  Wire up: panelRoot, bossPortraitImage, bossNameText, weaknessIconImage,
//  weaknessLabelText (optional), startButton, closeButton.
//  (inputHandler / bossController fields are no longer required —
//  BossController now owns BOTH input and the fight-begin call — but
//  are kept as optional overrides in case you want this panel usable
//  standalone outside the normal BossController flow.)
// ============================================================

using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    public class BossIntroPanel : MonoBehaviour
    {
        [Header("Panel Root")]
        [SerializeField] private GameObject panelRoot;

        [Header("Content")]
        [SerializeField] private Image    bossPortraitImage;
        [SerializeField] private TMP_Text bossNameText;
        [SerializeField] private Image    weaknessIconImage;
        [Tooltip("Optional — e.g. \"Weakness: Red\". Leave blank if you only want the icon.")]
        [SerializeField] private TMP_Text weaknessLabelText;

        [Header("Buttons")]
        [Tooltip("Begins the fight — calls BossController.BeginFight().")]
        [SerializeField] private Button startButton;
        [Tooltip("Backs out to the Boss Arena selection list without starting the fight.")]
        [SerializeField] private Button closeButton;

        [Header("Optional overrides (normally left blank — BossController supplies these via ShowIntro())")]
        [SerializeField] private InputHandler inputHandler;
        [SerializeField] private BossController bossController;

        [Header("Animation")]
        [SerializeField] private float showScaleDuration = 0.3f;

        private void Awake()
        {
            // Hide the CARD (not this whole GameObject) until ShowIntro() is
            // called — keeping the top-level object itself active/inert is
            // exactly what makes ShowIntro() reachable no matter what.
            SafeHide();

            startButton?.onClick.RemoveAllListeners();
            closeButton?.onClick.RemoveAllListeners();
            startButton?.onClick.AddListener(HandleStartClicked);
            closeButton?.onClick.AddListener(HandleCloseClicked);
            startButton?.onClick.AddListener(() => AudioManager.Instance?.PlaySFX("button_click"));
            closeButton?.onClick.AddListener(() => AudioManager.Instance?.PlaySFX("button_click"));
        }

        // ─────────────────────────────────────────────────────

        /// <summary>
        /// Populates the card from the given boss and shows it. Called
        /// directly by BossController.Start() once bossData is confirmed —
        /// see the class header for why this replaced the old event-based
        /// design.
        /// </summary>
        public void ShowIntro(BossData data, BossController controller)
        {
            if (controller != null) bossController = controller;

            if (data != null)
            {
                if (bossPortraitImage != null) bossPortraitImage.sprite = data.portrait;
                if (bossNameText != null)      bossNameText.text = data.bossName;

                if (weaknessIconImage != null)
                {
                    weaknessIconImage.sprite = data.weaknessIcon;
                    weaknessIconImage.enabled = data.weaknessIcon != null;
                }
                if (weaknessLabelText != null)
                    weaknessLabelText.text = $"Weakness: {data.weaknessTileType}";
            }
            else
            {
                Debug.LogWarning("[BossIntroPanel] ShowIntro() called with null BossData — showing panel with whatever placeholders are already in the Inspector.", this);
            }

            ShowPanel();
        }

        private void ShowPanel()
        {
            if (panelRoot == null) return;
            panelRoot.SetActive(true);
            panelRoot.transform.localScale = Vector3.zero;
            panelRoot.transform.DOScale(Vector3.one, showScaleDuration).SetEase(Ease.OutBack);
        }

        // ─────────────────────────────────────────────────────

        private void HandleStartClicked()
        {
            SafeHide();

            if (bossController != null)
                bossController.BeginFight();   // owns input + damage subscriptions + loops
            else
                inputHandler?.SetInputEnabled(true); // fallback if used standalone

            Debug.Log("[BossIntroPanel] Start tapped — fight begins.");
        }

        private void HandleCloseClicked()
        {
            SafeHide();
            // Input stays OFF (correct — the player is leaving, not fighting).
            GameManager.Instance?.ChangeState(GameState.BossArena);
        }

        private void SafeHide()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }
    }
}
