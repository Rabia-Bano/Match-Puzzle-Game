// ============================================================
//  BossLevelNode.cs  —  MonoBehaviour on the "BossLevelNode" prefab
//
//  One numbered circle in the BossArenaScene list (the mockup screen:
//  a snowflake-style circle with "1", and locked circles with a
//  chain/padlock icon for "2", "3"...). Reused for EVERY boss in the
//  list — BossArenaListManager.Setup() configures each instance.
//
//  This is intentionally simpler than LevelNode.cs (no stars — a
//  boss fight is pass/fail, not star-rated) but follows the exact
//  same static-event + SetState() pattern so it reads the same way
//  to anyone already familiar with LevelNode.
//
//  Attach to: "BossLevelNode" prefab (root GameObject) — save under
//  Assets/Prefabs/UI/BossLevelNode.prefab
// ============================================================

using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BossLevelNode : MonoBehaviour
{
    public enum NodeState { Locked, Unlocked, Defeated }

    [Header("Boss Data")]
    public int      bossId = 1;
    public NodeState State { get; private set; } = NodeState.Locked;

    [Header("UI References")]
    [Tooltip("The numbered circle button itself (the snowflake icon in the mockup).")]
    public Button    nodeButton;
    [Tooltip("Shows the boss number, e.g. '1', '2'.")]
    public TMP_Text  numberText;
    [Tooltip("Chain / padlock overlay — shown only when Locked.")]
    public GameObject lockOverlay;
    [Tooltip("NEW — shown only when Locked, e.g. 'Unlocks after Level 12'. " +
             "Same pattern as PetCollectionSlot.lockedLabel in the Pet Companion scene.")]
    public TMP_Text  lockedLabel;
    [Tooltip("Optional checkmark / crown icon — shown only when already Defeated at least once (replayable).")]
    public GameObject defeatedBadge;
    [Tooltip("Optional glow/pulse image — shown when Unlocked (not yet defeated), draws the eye to the next fight.")]
    public GameObject glowEffect;

    [Header("Theme")]
    [Tooltip("The node's own background circle Image (BGLevelNode) - color changes per theme + state")]
    public Image nodeImage;

    public static event Action<int> OnBossLevelSelected;

    private Tween _pulseTween;

    // ─────────────────────────────────────────────────────

    /// <summary>Called by BossArenaListManager right after Instantiate.</summary>
    /// <param name="unlockedAfterLevel">NEW — the regular level this boss needs
    /// completed first. Only used to populate lockedLabel while State == Locked
    /// (e.g. "Unlocks after Level 12") — pass 0 if you don't want the label.</param>
    public void Setup(int id, NodeState state, int unlockedAfterLevel = 0)
    {
        bossId = id;
        State  = state;

        if (numberText != null) numberText.text = id.ToString();

        SafeSetActive(lockOverlay,   state == NodeState.Locked);
        SafeSetActive(defeatedBadge, state == NodeState.Defeated);
        SafeSetActive(glowEffect,    state == NodeState.Unlocked);

        // NEW — "Unlocks after Level N", same pattern PetCollectionSlot uses
        // for a locked pet ("Unlocks after Level X").
        if (lockedLabel != null) lockedLabel.gameObject.SetActive(state == NodeState.Locked);
        if (lockedLabel != null)
            lockedLabel.text = $"Unlocks after Level {unlockedAfterLevel}";

        ApplyNodeThemeColor(state);

        KillPulse();
        if (state == NodeState.Unlocked) StartPulse();

        if (nodeButton != null)
        {
            nodeButton.onClick.RemoveAllListeners();
            nodeButton.interactable = state != NodeState.Locked;
            nodeButton.onClick.AddListener(HandleTap);
            nodeButton.onClick.AddListener(() => AudioManager.Instance?.PlaySFX("button_click"));
        }
    }

    private void HandleTap()
    {
        if (State == NodeState.Locked)
        {
            transform.DOShakePosition(0.3f, 8f, 20).SetUpdate(true);
            return;
        }
        OnBossLevelSelected?.Invoke(bossId);
    }

    private void ApplyNodeThemeColor(NodeState state)
    {
        if (nodeImage == null) return;

        var theme = Match3.Theme.ThemeManager.Instance != null
            ? Match3.Theme.ThemeManager.Instance.CurrentTheme
            : null;
        if (theme == null) return; // keep whatever color was already there

        nodeImage.color = state switch
        {
            NodeState.Locked   => theme.levelNodeLockedColor,
            NodeState.Unlocked => theme.levelNodeUnlockedColor,
            NodeState.Defeated => theme.levelNodeCompletedColor,
            _ => nodeImage.color
        };
    }

    private void StartPulse()
    {
        if (glowEffect == null) return;
        _pulseTween = glowEffect.transform
            .DOScale(Vector3.one * 1.12f, 0.7f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }

    private void KillPulse()
    {
        _pulseTween?.Kill();
        _pulseTween = null;
        if (glowEffect != null) glowEffect.transform.localScale = Vector3.one;
    }

    private void OnDestroy() => KillPulse();

    private static void SafeSetActive(GameObject go, bool active)
    { if (go != null) go.SetActive(active); }
}