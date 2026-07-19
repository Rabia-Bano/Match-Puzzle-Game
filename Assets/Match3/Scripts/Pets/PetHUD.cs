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

            petManager.OnChargeChanged += HandleChargeChanged;
            petManager.OnPetReady      += HandlePetReady;
            petManager.OnPetChanged    += HandlePetChanged;
            _subscribed = true;

            skillButton?.onClick.AddListener(HandleSkillButtonTapped);

            HandlePetChanged();
            HandleChargeChanged(petManager.ChargeProgress,
                petManager.EquippedPet != null ? petManager.EquippedPet.chargeRequired : 100);
        }

        private void OnDestroy()
        {
            if (!_subscribed || petManager == null) return;

            petManager.OnChargeChanged -= HandleChargeChanged;
            petManager.OnPetReady      -= HandlePetReady;
            petManager.OnPetChanged    -= HandlePetChanged;

            skillButton?.onClick.RemoveListener(HandleSkillButtonTapped);
        }

        // ── Handlers ──────────────────────────────────────────

        private void HandlePetChanged()
        {
            if (petManager.EquippedPet == null) return;

            if (petPortrait != null)
                petPortrait.sprite = petManager.EquippedPet.sprite;

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
            if (skillButtonGlow != null)
                skillButtonGlow.SetActive(true);

            if (petPortrait != null)
                petPortrait.transform.DOPunchScale(Vector3.one * 0.2f, 0.4f, 6, 0.6f);
        }

        private void HandleSkillButtonTapped()
        {
            if (petManager == null || !petManager.IsCharged)
            {
                skillButton?.transform.DOShakePosition(0.3f, 5f, 10);
                return;
            }

            petManager.UseSkill();

            if (skillButtonGlow != null) skillButtonGlow.SetActive(false);
            if (chargeBarFill   != null) chargeBarFill.fillAmount = 0f;
        }

        private void RefreshSkillButtonInteractable()
        {
            if (skillButton == null || petManager == null) return;
            skillButton.interactable = petManager.IsCharged && !petManager.IsBusy;
        }
    }
}