// ============================================================
//  LevelNode.cs — STAR FIX
//
//  FIX 3: filledStar aur emptyStar assign na hone par
//  stars show nahi hote the. Ab:
//  - filledStar null hone par star Image ka color yellow karta hai
//  - emptyStar null hone par gray color karta hai
//  - Sprite assign hai to use karta hai
//  - starContainer properly active/inactive hota hai
// ============================================================

using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelNode : MonoBehaviour
{
    public enum NodeState { Locked, Unlocked, Completed }

    [Header("Level Data")]
    public int       levelId   = 1;
    public NodeState State     { get; private set; } = NodeState.Locked;

    [Header("UI References")]
    public GameObject lockIcon;
    public GameObject starContainer;
    public Button     playButton;
    public TMP_Text   levelNumberText;

    [Tooltip("3 star images — index 0,1,2")]
    public List<Image> starImages = new List<Image>(3);

    [Header("Star Sprites (optional — color fallback if null)")]
    public Sprite filledStar;   // ← assign filled star sprite
    public Sprite emptyStar;    // ← assign empty/gray star sprite

    [Header("Boss Node")]
    public bool isBossNode = false;

    [Header("Theme")]
    [Tooltip("The node's own background Image (LevelNode_/Image) - color changes per theme + state")]
    public Image nodeImage;

    public static event Action<int> OnLevelSelected;

    private Tween _bounceTween;

    public void SetState(NodeState newState, int earnedStars = 0)
    {
        State = newState;

        if (levelNumberText != null)
            levelNumberText.text = isBossNode ? "BOSS" : levelId.ToString();

        SafeSetActive(lockIcon,      newState == NodeState.Locked);
        SafeSetActive(starContainer, newState == NodeState.Completed);

        ApplyNodeThemeColor(newState);

        if (playButton != null)
            playButton.gameObject.SetActive(newState == NodeState.Unlocked);

        if (newState == NodeState.Completed)
            RefreshStars(earnedStars);

        KillBounce();
        if (newState == NodeState.Unlocked) StartBounce();

        if (playButton != null)
        {
            playButton.onClick.RemoveAllListeners();
            if (newState == NodeState.Unlocked)
                playButton.onClick.AddListener(HandleTap);
                playButton.onClick.AddListener(() => AudioManager.Instance?.PlaySFX("button_click"));
        }

        var rootBtn = GetComponent<Button>();
        if (rootBtn != null)
        {
            rootBtn.onClick.RemoveAllListeners();
            rootBtn.onClick.AddListener(HandleTap);
            rootBtn.onClick.AddListener(() => AudioManager.Instance?.PlaySFX("button_click"));
        }
    }

    private void HandleTap()
    {
        if (State == NodeState.Locked)
        {
            transform.DOShakePosition(0.3f, 8f, 20).SetUpdate(true);
            return;
        }
        OnLevelSelected?.Invoke(levelId);
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
            NodeState.Locked    => theme.levelNodeLockedColor,
            NodeState.Unlocked  => theme.levelNodeUnlockedColor,
            NodeState.Completed => theme.levelNodeCompletedColor,
            _ => nodeImage.color
        };
    }

    // ── FIX 3: Stars refresh ──────────────────────────────────
    private void RefreshStars(int earned)
    {
        if (starImages == null) return;

        // Make sure starContainer is active
        if (starContainer != null) starContainer.SetActive(true);

        for (int i = 0; i < starImages.Count; i++)
        {
            if (starImages[i] == null) continue;

            bool got = i < earned;

            if (got)
            {
                // Filled star
                if (filledStar != null)
                {
                    starImages[i].sprite = filledStar;
                    starImages[i].color  = Color.white;
                }
                else
                {
                    // Color fallback — golden yellow star
                    starImages[i].color = new Color(1f, 0.85f, 0.1f, 1f);
                }
            }
            else
            {
                // Empty star
                if (emptyStar != null)
                {
                    starImages[i].sprite = emptyStar;
                    starImages[i].color  = Color.white;
                }
                else
                {
                    // Color fallback — gray
                    starImages[i].color = new Color(0.5f, 0.5f, 0.5f, 0.4f);
                }
            }

            // Make sure star Image is active
            starImages[i].gameObject.SetActive(true);
        }
    }

    private void StartBounce()
    {
        float baseY = transform.localPosition.y;
        _bounceTween = transform
            .DOLocalMoveY(baseY + 10f, 0.65f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }

    private void KillBounce()
    {
        if (_bounceTween == null) return;
        _bounceTween.Kill();
        _bounceTween = null;
    }

    private static void SafeSetActive(GameObject go, bool active)
    { if (go != null) go.SetActive(active); }

    private void OnDestroy() => KillBounce();
}