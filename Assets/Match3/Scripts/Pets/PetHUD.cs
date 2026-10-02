using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    public class PetHUD : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Left blank on purpose — always resolved from PetManager.Instance in OnEnable(). Do not wire manually.")]
        private PetManager petManager;

        [Header("UI Elements")]
        [SerializeField] private Image           petPortrait;
        [SerializeField] private Image           chargeBarFill;
        [SerializeField] private TextMeshProUGUI chargeText;
        [SerializeField] private Button          skillButton;
        [SerializeField] private GameObject      skillButtonGlow;

        [Header("Animation")]
        [SerializeField] private float barTweenDuration = 0.25f;

        [Header("Skill Effect")]
        [SerializeField] private ParticleSystem skillBurstPrefab;

        [Header("Not-Charged Tooltip (NEW — Rabia's request)")]
        [Tooltip("Small message popup shown when the player taps the skill button while " +
                 "the pet isn't charged yet — tells them how to charge it. Works in BOTH " +
                 "gameplay scenes (regular level + Boss Arena) since both reuse this same " +
                 "PetHUD component. Leave notChargedTooltip blank to skip this feature.")]
        [SerializeField] private GameObject      notChargedTooltip;
        [SerializeField] private TextMeshProUGUI notChargedTooltipText;
        [SerializeField] private string          notChargedMessage = "Swap tile to boost pet's energy!";
        [SerializeField] private float           notChargedTooltipDuration = 5f;

        private Coroutine _tooltipRoutine;

        private bool _subscribed;

        private void Start()
        {
            petManager = PetManager.GetOrCreateInstance();

            if (skillButton == null)
                Debug.LogError("[PetHUD] skillButton is NOT assigned in the Inspector — tapping the " +
                                "pet does nothing at all (no shake, no tooltip, no skill use). Assign " +
                                "the Button that sits over the pet portrait/icon.", this);

            petManager.OnChargeChanged += HandleChargeChanged;
            petManager.OnPetReady      += HandlePetReady;
            petManager.OnPetChanged    += HandlePetChanged;
            petManager.OnSkillUsed     += HandleSkillUsed;
            _subscribed = true;

            skillButton?.onClick.AddListener(HandleSkillButtonTapped);

            if (notChargedTooltip != null) notChargedTooltip.SetActive(false);
            else Debug.LogWarning("[PetHUD] notChargedTooltip is NOT assigned in the Inspector — " +
                                   "tapping the skill button while the pet isn't charged will shake " +
                                   "the button but show no message. Create a small popup GameObject " +
                                   "(with a TMP_Text child) under this PetHUD and assign both fields " +
                                   "under 'Not-Charged Tooltip' in the Inspector to enable this.", this);

            HandlePetChanged();
            HandleChargeChanged(petManager.ChargeProgress,
                petManager.EquippedPet != null ? petManager.EquippedPet.chargeRequired : 100);
        }

        private void HandleSkillUsed(PetData pet)
        {
            AudioManager.Instance?.PlaySFX("pet_skill");
            TileVisualController.PlayEffect(skillBurstPrefab, TileVisualController.ScreenCenterWorldPoint(), Color.white);
        }

        private void OnDestroy()
        {
            if (!_subscribed || petManager == null) return;

            petManager.OnChargeChanged -= HandleChargeChanged;
            petManager.OnPetReady      -= HandlePetReady;
            petManager.OnPetChanged    -= HandlePetChanged;
            petManager.OnSkillUsed     -= HandleSkillUsed;

            skillButton?.onClick.RemoveListener(HandleSkillButtonTapped);
        }

        private void HandlePetChanged()
        {
            if (petManager.EquippedPet == null) return;

            if (petPortrait != null)
            {
                petPortrait.sprite = petManager.EquippedPet.sprite;

                petPortrait.transform.DOKill();
                petPortrait.transform.localScale = Vector3.one;
            }

            RefreshSkillButtonInteractable();
            if (skillButtonGlow != null) skillButtonGlow.SetActive(false);
        }

        private void HandleChargeChanged(int current, int max)
        {
            float fraction = max > 0 ? (float)current / max : 0f;

            if (chargeBarFill != null)
                chargeBarFill.DOFillAmount(fraction, barTweenDuration).SetEase(Ease.OutQuad);

            if (chargeText != null)
                chargeText.text = $"{Mathf.RoundToInt(fraction * 100f)}%";

            RefreshSkillButtonInteractable();
        }

        private void HandlePetReady()
        {
            HideNotChargedTooltip();

            if (skillButtonGlow != null)
                skillButtonGlow.SetActive(true);

            if (petPortrait != null)
            {
                petPortrait.transform.DOKill();
                petPortrait.transform.localScale = Vector3.one;
                petPortrait.transform
                    .DOPunchScale(Vector3.one * 0.2f, 0.4f, 6, 0.6f)
                    .OnComplete(() => petPortrait.transform.localScale = Vector3.one);
            }
        }

        private void HandleSkillButtonTapped()
        {
            if (BoosterManager.Instance != null && BoosterManager.Instance.IsTargeting)
            {
                skillButton?.transform.DOShakePosition(0.3f, 5f, 10);
                return;
            }

            if (petManager == null || !petManager.IsCharged)
            {
                skillButton?.transform.DOShakePosition(0.3f, 5f, 10);
                ShowNotChargedTooltip();
                return;
            }

            petManager.UseSkill();

            if (skillButtonGlow != null) skillButtonGlow.SetActive(false);
            if (chargeBarFill   != null) chargeBarFill.fillAmount = 0f;
        }

        private void ShowNotChargedTooltip()
        {
            if (notChargedTooltip == null) return;

            if (notChargedTooltipText != null)
                notChargedTooltipText.text = notChargedMessage;

            notChargedTooltip.SetActive(true);
            notChargedTooltip.transform.DOKill();
            notChargedTooltip.transform.localScale = Vector3.zero;
            notChargedTooltip.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack);

            if (_tooltipRoutine != null) StopCoroutine(_tooltipRoutine);
            _tooltipRoutine = StartCoroutine(HideTooltipAfterDelay());
        }

        private IEnumerator HideTooltipAfterDelay()
        {
            yield return new WaitForSeconds(notChargedTooltipDuration);
            HideNotChargedTooltip();
        }

        private void HideNotChargedTooltip()
        {
            if (notChargedTooltip == null || !notChargedTooltip.activeSelf) return;

            notChargedTooltip.transform.DOKill();
            notChargedTooltip.transform
                .DOScale(Vector3.zero, 0.15f)
                .SetEase(Ease.InBack)
                .OnComplete(() => notChargedTooltip.SetActive(false));

            if (_tooltipRoutine != null)
            {
                StopCoroutine(_tooltipRoutine);
                _tooltipRoutine = null;
            }
        }

        private void RefreshSkillButtonInteractable()
        {
            if (skillButton == null || petManager == null) return;
            skillButton.interactable = !petManager.IsBusy;
        }
    }
}
