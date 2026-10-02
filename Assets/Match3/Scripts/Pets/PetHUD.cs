// ============================================================
//  PetHUD.cs  —  MonoBehaviour
//
//  The pet panel visible during gameplay: portrait, animated
//  charge/battery bar, skill button.
//
//  Attach to: "PetHUD" panel GameObject under your gameplay Canvas
//  (this REPLACES the old "PET PANEL" fields inside GameHUD.cs —
//  see the setup notes for what to remove there).
//
//  Prefab / hierarchy needed under this GameObject:
//    - PetPortrait      (Image)              — pet sprite
//    - ChargeBarFill     (Image, Type=Filled, Fill Method=Horizontal)
//    - ChargeText        (TextMeshProUGUI)    — optional "70%" label
//    - SkillButton       (Button)             — tap to use skill
//    - SkillButtonGlow   (GameObject, optional) — pulsing glow shown only when charged
// ============================================================

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

        // NOTE: this used to live in OnEnable()/OnDisable(), but Unity does not
        // guarantee Awake() has run on OTHER objects before OnEnable() runs on
        // this one — so PetHUD.OnEnable() could fire before PetManager.Awake()
        // sets Instance, depending on hierarchy order, giving a false
        // "No PetManager.Instance found" error. Start() IS guaranteed to run
        // only after every object's Awake() has completed, so we do the lookup
        // and subscription there instead.
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

            if (notChargedTooltip != null) notChargedTooltip.SetActive(false);   // NEW — start hidden
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

        // ── Handlers ──────────────────────────────────────────

        private void HandlePetChanged()
        {
            if (petManager.EquippedPet == null) return;

            if (petPortrait != null)
            {
                petPortrait.sprite = petManager.EquippedPet.sprite;

                // FIX: switching pets while a punch-scale tween is mid-flight (e.g.
                // player changes equipped pet, or a new level rebinds) used to leave
                // whatever scale the old tween was interrupted at. Kill + hard reset
                // here too, same as HandlePetReady below.
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
            // NEW — the pet just became charged, so the "not charged yet"
            // tooltip (if it happened to still be showing) is now moot.
            HideNotChargedTooltip();

            if (skillButtonGlow != null)
                skillButtonGlow.SetActive(true);

            if (petPortrait != null)
            {
                // FIX (icon-keeps-growing bug): DOPunchScale animates AWAY from
                // and back to whatever localScale is AT THE MOMENT it starts. If
                // this handler ever fires again before the previous punch fully
                // finished returning to 1 (or if it fires unexpectedly often),
                // each new punch stacks on top of a slightly-off scale instead of
                // the intended 1,1,1 — and over repeated fires that drift adds up
                // to a permanently oversized icon. Killing any in-flight tween and
                // hard-resetting to Vector3.one first guarantees every punch
                // starts from — and fully returns to — the same baseline, no
                // matter how many times or how quickly this fires.
                petPortrait.transform.DOKill();
                petPortrait.transform.localScale = Vector3.one;
                petPortrait.transform
                    .DOPunchScale(Vector3.one * 0.2f, 0.4f, 6, 0.6f)
                    // Extra safety net: guarantees the icon lands EXACTLY at (1,1,1)
                    // once the punch finishes normally, regardless of float rounding.
                    .OnComplete(() => petPortrait.transform.localScale = Vector3.one);
            }
        }

        private void HandleSkillButtonTapped()
        {
            // FIX — a booster is selected and waiting for its target tile: the pet
            // can't be used until the player taps a tile or presses Cancel on the
            // BoosterTargetingBanner. Charge is NOT lost.
            if (BoosterManager.Instance != null && BoosterManager.Instance.IsTargeting)
            {
                skillButton?.transform.DOShakePosition(0.3f, 5f, 10);
                return;
            }

            if (petManager == null || !petManager.IsCharged)
            {
                skillButton?.transform.DOShakePosition(0.3f, 5f, 10);
                ShowNotChargedTooltip();   // NEW (Rabia's request)
                return;
            }

            petManager.UseSkill();

            if (skillButtonGlow != null) skillButtonGlow.SetActive(false);
            if (chargeBarFill   != null) chargeBarFill.fillAmount = 0f;
        }

        /// <summary>
        /// NEW (Rabia's request) — shown for notChargedTooltipDuration seconds
        /// when the player taps the skill button while the pet isn't charged
        /// yet. Tapping again while it's already showing just restarts the
        /// 5-second timer instead of stacking multiple hide calls.
        /// </summary>
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

        /// <summary>
        /// FIX (Rabia's report — "not-charged tooltip never shows, no warning
        /// either"): this used to also require petManager.IsCharged, which set
        /// skillButton.interactable = false whenever the pet wasn't charged.
        /// A Unity Button with interactable = false NEVER fires onClick at
        /// all — so HandleSkillButtonTapped() (the shake + the tooltip) never
        /// even ran while uncharged, exactly the one moment the tooltip is
        /// supposed to appear. IsCharged is still checked INSIDE
        /// HandleSkillButtonTapped() itself, so the skill still can't be used
        /// early — only IsBusy (skill animation currently playing) still
        /// blocks the tap here, to prevent spamming mid-animation.
        /// </summary>
        private void RefreshSkillButtonInteractable()
        {
            if (skillButton == null || petManager == null) return;
            skillButton.interactable = !petManager.IsBusy;
        }
    }
}