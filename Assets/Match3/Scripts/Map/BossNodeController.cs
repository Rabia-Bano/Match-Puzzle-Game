// ============================================================
//  BossNodeController.cs  —  MonoBehaviour on BossNode Prefab
//
//  Attach to: BossNode prefab (root GameObject)
//  Purpose:
//    • Shows Boss portrait + "BOSS" label
//    • Locked with chain visual until prerequisite level complete
//    • On tap → navigates to BossArenaScene via GameManager
// ============================================================

using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BossNodeController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────
    // Inspector Fields
    // ─────────────────────────────────────────────────────────

    [Header("UI References")]
    [Tooltip("Boss portrait Image")]
    public Image bossPortrait;

    [Tooltip("'BOSS' label TMP text")]
    public TMP_Text bossLabel;

    [Tooltip("Boss number label, e.g. '1', '2'")]
    public TMP_Text bossNumberText;

    [Tooltip("Chain / lock overlay — shown when Locked")]
    public GameObject chainOverlay;

    [Tooltip("Glow particle or image — shown when Unlocked")]
    public GameObject glowEffect;

    [Tooltip("Root button component")]
    public Button nodeButton;

    [Header("Boss Portraits")]
    [Tooltip("Sprite array — index 0 = Boss 1, etc.")]
    public Sprite[] bossPortraits;

    // ─────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────

    private int  _bossId;
    private int  _unlockedAfterLevel;   // regular level that must be complete
    private bool _isUnlocked;
    private Tween _pulseTween;

    // ─────────────────────────────────────────────────────────
    // PUBLIC API
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Called by MapManager right after Instantiate.
    /// </summary>
    /// <param name="bossId">1, 2, 3 …</param>
    /// <param name="unlockedAfterLevel">Must complete this level first</param>
    /// <param name="isUnlocked">True if prerequisite level is completed</param>
    public void Setup(int bossId, int unlockedAfterLevel, bool isUnlocked)
    {
        _bossId            = bossId;
        _unlockedAfterLevel = unlockedAfterLevel;
        _isUnlocked        = isUnlocked;

        // Label
        if (bossLabel != null)
            bossLabel.text = "BOSS";
        if (bossNumberText != null)
            bossNumberText.text = bossId.ToString();

        // Portrait
        if (bossPortrait != null && bossPortraits != null)
        {
            int idx = Mathf.Clamp(bossId - 1, 0, bossPortraits.Length - 1);
            if (idx < bossPortraits.Length && bossPortraits[idx] != null)
                bossPortrait.sprite = bossPortraits[idx];
        }

        // Visual state
        SafeSetActive(chainOverlay, !isUnlocked);
        SafeSetActive(glowEffect,    isUnlocked);

        // Pulse when unlocked
        KillPulse();
        if (isUnlocked) StartPulse();

        // Button
        if (nodeButton != null)
        {
            nodeButton.onClick.RemoveAllListeners();
            nodeButton.onClick.AddListener(HandleTap);
        }
    }

    // ─────────────────────────────────────────────────────────
    // TAP HANDLER
    // ─────────────────────────────────────────────────────────

    private void HandleTap()
    {
        if (!_isUnlocked)
        {
            Debug.Log($"[BossNode] Boss {_bossId} locked — complete Level {_unlockedAfterLevel} first.");
            transform.DOShakePosition(0.3f, 8f, 20).SetUpdate(true);
            return;
        }

        Debug.Log($"[BossNode] Boss {_bossId} tapped → BossArena");

        if (GameManager.Instance != null)
        {
            // Store which boss this is so BossArena scene knows
            PlayerPrefs.SetInt("SelectedBossId", _bossId);
            GameManager.Instance.ChangeState(GameState.BossArena);
        }
    }

    // ─────────────────────────────────────────────────────────
    // ANIMATIONS
    // ─────────────────────────────────────────────────────────

    private void StartPulse()
    {
        if (glowEffect == null) return;
        _pulseTween = glowEffect.transform
            .DOScale(Vector3.one * 1.15f, 0.7f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }

    private void KillPulse()
    {
        _pulseTween?.Kill();
        _pulseTween = null;
        if (glowEffect != null)
            glowEffect.transform.localScale = Vector3.one;
    }

    private void OnDestroy() => KillPulse();

    // ─────────────────────────────────────────────────────────
    // UTILITY
    // ─────────────────────────────────────────────────────────

    private static void SafeSetActive(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
    }
}
