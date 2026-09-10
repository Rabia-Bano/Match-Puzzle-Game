// ============================================================
//  InventoryBoosterSlot.cs  —  MonoBehaviour
//  Attach to: each "owned booster" button in the gameplay power-up bar.
//  This is SEPARATE from BoosterSlotUI.cs (the existing pay-per-tap
//  widget) — this button spends from the Store-purchased inventory via
//  BoosterManager.cs instead of spending coins directly.
//
//  Used in BOTH GameHUD (regular level) and BossArenaHUD (Boss Arena) —
//  same component, same prefab, just wired with a different boosterId
//  per slot in each scene's Inspector.
// ============================================================

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
        [SerializeField] private TextMeshProUGUI countText;        // owned quantity, e.g. "x2"
        [SerializeField] private GameObject      targetingOutline; // highlighted while THIS booster is awaiting a tile tap

        private void OnEnable()
        {
            button?.onClick.AddListener(OnTap);
            LocalSaveManager.OnProfileChanged += HandleInventoryChanged;

            // FIX ("shuffle_2tiles bilkul attach nahi" / boosters randomly disabled):
            // this used to check `if (BoosterManager.Instance != null)` — but
            // BoosterManager is only actually created lazily, inside LevelManager's
            // Start() (via GetOrCreateInstance()). Unity runs every object's OnEnable()
            // BEFORE any Start() in the scene, so on the very first level/fight of a
            // session, Instance was still null right here — this whole subscribe block
            // was silently skipped, and this slot never heard OnTargetingStarted /
            // OnTargetingEnded / OnBoosterUsed for the rest of that session. Whether
            // that happened depended purely on scene-load history (had some earlier
            // scene already created the DontDestroyOnLoad singleton?) — which is
            // exactly why it looked random per booster/per session. Calling
            // GetOrCreateInstance() here — same pattern PetHUD.cs already uses for
            // PetManager — guarantees the singleton exists before we subscribe.
            BoosterManager manager = BoosterManager.GetOrCreateInstance();
            manager.OnTargetingStarted += HandleTargetingStarted;
            manager.OnTargetingEnded   += HandleTargetingEnded;
            manager.OnBoosterUsed      += HandleBoosterUsed;
            manager.OnBound            += Refresh; // re-check CanUse() once BindToLevel() actually finishes

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
                transform.DOShakePosition(0.3f, 5f, 10); // same "not usable right now" feedback BoosterSlotUI.cs uses
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
            // Grey out every OTHER slot while one booster is awaiting a tile tap.
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