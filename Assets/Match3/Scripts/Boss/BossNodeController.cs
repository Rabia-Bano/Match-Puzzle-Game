// ============================================================
//  BossNodeController.cs  —  MonoBehaviour on BossNode Prefab
//  (the inline Boss marker that appears ON THE MAIN MAP PATH every
//  N levels — NOT the same as BossLevelNode, which is the numbered
//  circle inside the separate BossArenaScene selection list)
//
//  Attach to: BossNode prefab (root GameObject), instantiated by
//  MapManager.SpawnBossNode()
//  Purpose:
//    • Shows Boss portrait + "BOSS" label
//    • Locked with chain visual until prerequisite level complete
//    • On tap → launches that boss's fight DIRECTLY (skips the
//      BossArenaScene list) via BossLevelLoader
//
//  CORRECTED (previous version mistakenly sent the player to
//  GameState.BossArena / "BossArenaScene" — that scene is actually
//  the boss SELECTION LIST, not the fight. Tapping this inline map
//  marker should jump straight into the fight itself, the same way
//  it always intended to. Now routes through GameState.BossGameplay
//  / "BossGameBoardScene" via the shared BossLevelLoader helper —
//  see BossArenaListManager.cs / BossLevelLoader.cs / the setup guide.)
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

    [Tooltip("NEW — shown only while Locked, e.g. 'Unlocks after Level 12'. " +
             "Same pattern as PetCollectionSlot.lockedLabel in the Pet Companion scene.")]
    public TMP_Text lockedLabel;

    [Tooltip("Glow particle or image — shown when Unlocked")]
    public GameObject glowEffect;

    [Tooltip("Root button component")]
    public Button nodeButton;

    [Header("Boss Portraits")]
    [Tooltip("Sprite array — index 0 = Boss 1, etc.")]
    public Sprite[] bossPortraits;

    [Header("Boss Arena Board")]
    [Tooltip("Shared LevelData asset that defines the Boss Arena board layout. " +
             "Assign the SAME asset here as on the BossLevelNode prefab used by " +
             "BossArenaListManager — the board layout is shared across every boss.")]
    public Match3.LevelData bossBoardLevelData;

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

        // NEW — "Unlocks after Level N" label while locked, exactly like
        // PetCollectionSlot shows "Unlocks after Level X" for a locked pet.
        if (lockedLabel != null) lockedLabel.gameObject.SetActive(!isUnlocked);
        if (lockedLabel != null)
            lockedLabel.text = $"Unlocks after Level {unlockedAfterLevel}";

        // Pulse when unlocked
        KillPulse();
        if (isUnlocked) StartPulse();

        // Button
        if (nodeButton != null)
        {
            nodeButton.onClick.RemoveAllListeners();
            nodeButton.onClick.AddListener(HandleTap);
            nodeButton.onClick.AddListener(() => AudioManager.Instance?.PlaySFX("button_click"));
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

        Debug.Log($"[BossNode] Boss {_bossId} tapped on Map → launching fight directly.");

        // Jumps straight into the fight — bypasses the BossArenaScene
        // selection list entirely, since the player already picked this
        // specific boss by tapping its marker on the path.
        BossLevelLoader.LoadBoss(_bossId, bossBoardLevelData);
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
