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
            SafeHide();

            startButton?.onClick.RemoveAllListeners();
            closeButton?.onClick.RemoveAllListeners();
            startButton?.onClick.AddListener(HandleStartClicked);
            closeButton?.onClick.AddListener(HandleCloseClicked);
            startButton?.onClick.AddListener(() => AudioManager.Instance?.PlaySFX("button_click"));
            closeButton?.onClick.AddListener(() => AudioManager.Instance?.PlaySFX("button_click"));
        }

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

        private void HandleStartClicked()
        {
            SafeHide();

            if (bossController != null)
                bossController.BeginFight();
            else
                inputHandler?.SetInputEnabled(true);

            Debug.Log("[BossIntroPanel] Start tapped — fight begins.");
        }

        private void HandleCloseClicked()
        {
            SafeHide();
            GameManager.Instance?.ChangeState(GameState.BossArena);
        }

        private void SafeHide()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }
    }
}
