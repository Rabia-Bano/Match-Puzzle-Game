// ============================================================
//  BossArenaHUD.cs  —  MonoBehaviour
//
//  Boss Arena's in-fight HUD: boss portrait + animated health bar,
//  an attack warning popup, and a player-score-vs-boss-health readout.
//  Mirrors PetHUD.cs's DOFillAmount bar-tween pattern on purpose.
//
//  Attach to: "BossArenaHUD" panel GameObject under the BossArenaScene
//  Canvas (sibling of TopBarPanel in the hierarchy you showed).
//  Wire up: bossController, levelManager (optional, for score text),
//  and every UI reference below. See the setup guide for full hierarchy.
// ============================================================

using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    public class BossArenaHUD : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Left blank on purpose — resolved from BossController.Instance in Start(). Do not wire manually.")]
        private BossController bossController;

        [Tooltip("Optional — if assigned, shows the player's live score next to the boss health bar.")]
        [SerializeField] private LevelManager levelManager;

        [Header("Boss Health Bar")]
        [SerializeField] private Image           bossPortrait;
        [SerializeField] private TextMeshProUGUI bossNameText;
        [SerializeField] private Image           healthBarFill;   // Image, Type=Filled, Fill Method=Horizontal
        [SerializeField] private TextMeshProUGUI healthText;      // e.g. "60 / 100"

        [Header("Player Score vs Boss Health")]
        [SerializeField] private TextMeshProUGUI playerScoreText;

        [Header("Attack Warning Popup")]
        [SerializeField] private GameObject      attackWarningPanel;
        [SerializeField] private TextMeshProUGUI attackWarningText;
        [SerializeField] private float           warningVisibleSeconds = 1.1f;

        [Header("Animation")]
        [SerializeField] private float barTweenDuration = 0.3f;
        [SerializeField] private float damageShakeStrength = 10f;

        private Tween _warningHideTween;

        // ─────────────────────────────────────────────────────

        private void Start()
        {
            bossController = BossController.Instance;
            if (bossController == null)
            {
                Debug.LogError("[BossArenaHUD] No BossController.Instance found — is BossController in this scene and does it run before this HUD's Start()?", this);
                return;
            }

            if (bossController.BossData != null)
            {
                if (bossPortrait != null) bossPortrait.sprite = bossController.BossData.portrait;
                if (bossNameText != null) bossNameText.text   = bossController.BossData.bossName;
            }

            bossController.OnHealthChanged += HandleHealthChanged;
            bossController.OnDamageTaken   += HandleDamageTaken;
            bossController.OnBossAttack.AddListener(HandleBossAttack);

            if (levelManager != null)
                levelManager.OnScoreChanged.AddListener(HandleScoreChanged);

            SafeHide(attackWarningPanel);

            HandleHealthChanged(bossController.CurrentHealth, bossController.MaxHealth);
            HandleScoreChanged(levelManager != null ? levelManager.Score : 0);
        }

        private void OnDestroy()
        {
            if (bossController == null) return;
            bossController.OnHealthChanged -= HandleHealthChanged;
            bossController.OnDamageTaken   -= HandleDamageTaken;
            bossController.OnBossAttack.RemoveListener(HandleBossAttack);

            if (levelManager != null)
                levelManager.OnScoreChanged.RemoveListener(HandleScoreChanged);
        }

        // ─────────────────────────────────────────────────────
        // HANDLERS
        // ─────────────────────────────────────────────────────

        private void HandleHealthChanged(int current, int max)
        {
            float fraction = max > 0 ? (float)current / max : 0f;

            if (healthBarFill != null)
                healthBarFill.DOFillAmount(fraction, barTweenDuration).SetEase(Ease.OutQuad);

            if (healthText != null)
                healthText.text = $"{Mathf.Max(0, current)} / {max}";
        }

        private void HandleDamageTaken(int amount)
        {
            if (bossPortrait != null)
            {
                bossPortrait.transform.DOKill();
                bossPortrait.transform.DOShakePosition(0.3f, damageShakeStrength, 12);
            }
            if (healthBarFill != null)
            {
                // FIX (HP fill image keeps growing bug): DOPunchScale below runs on
                // healthBarFill.transform, but this line was calling DOKill() on
                // healthBarFill (the Image component) instead — that kills the FILL
                // tween, not the punch-scale tween, so the previous punch was NEVER
                // actually interrupted. On top of that there was no scale reset
                // before starting a new punch. Result: every damage tick during a
                // fast cascade (multiple matches hitting the boss in quick succession)
                // stacked a fresh punch on top of whatever scale the still-running
                // previous punch was at, so the bar's image kept growing bigger and
                // bigger and never settled back to its normal size. Same pattern
                // PetHUD.cs's icon already had fixed — mirroring that fix here.
                healthBarFill.transform.DOKill();
                healthBarFill.transform.localScale = Vector3.one;
                healthBarFill.transform
                    .DOPunchScale(Vector3.one * 0.06f, 0.2f, 4, 0.5f)
                    .OnComplete(() => healthBarFill.transform.localScale = Vector3.one);
            }
        }

        private void HandleBossAttack()
        {
            if (attackWarningPanel == null) return;

            string message = bossController.LastAttack != null
                ? bossController.LastAttack.warningMessage
                : "Boss is attacking!";

            if (attackWarningText != null) attackWarningText.text = message;

            _warningHideTween?.Kill();
            attackWarningPanel.SetActive(true);
            attackWarningPanel.transform.localScale = Vector3.zero;
            attackWarningPanel.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);

            _warningHideTween = DOVirtual.DelayedCall(warningVisibleSeconds, () =>
            {
                attackWarningPanel.transform
                    .DOScale(Vector3.zero, 0.2f)
                    .SetEase(Ease.InBack)
                    .OnComplete(() => attackWarningPanel.SetActive(false));
            });
        }

        private void HandleScoreChanged(int score)
        {
            if (playerScoreText != null)
                playerScoreText.text = $"Score: {score:N0}";
        }

        private static void SafeHide(GameObject go) { if (go != null) go.SetActive(false); }
    }
}