// ============================================================
//  BoosterTargetingBanner.cs  —  MonoBehaviour
//  Attach to: a small banner UI element in BOTH GameHUD and BossArenaHUD
//  (same component, one instance per scene). Shows "Tap a tile..." style
//  guidance + a Cancel button while BoosterManager is waiting for the
//  player to tap a tile (Hammer / Row Bomb / Column Bomb / Shuffle 2 Tiles).
//  Shuffle Board needs no targeting, so it never triggers this banner.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Match3
{
    public class BoosterTargetingBanner : MonoBehaviour
    {
        [SerializeField] private GameObject bannerRoot;   // parent to show/hide
        [SerializeField] private TMP_Text   messageText;
        [SerializeField] private Button     cancelButton;

        private static readonly Dictionary<string, string> Messages = new()
        {
            { BoosterManager.Hammer,        "Tap a tile to destroy it" },
            { BoosterManager.RowBomb,       "Tap a tile to clear its row" },
            { BoosterManager.ColumnBomb,    "Tap a tile to clear its column" },
            { BoosterManager.Shuffle2Tiles, "Tap two tiles to swap them" },
        };

        private void OnEnable()
        {
            if (bannerRoot != null) bannerRoot.SetActive(false);
            cancelButton?.onClick.AddListener(OnCancelTapped);

            if (BoosterManager.Instance != null)
            {
                BoosterManager.Instance.OnTargetingStarted += HandleTargetingStarted;
                BoosterManager.Instance.OnTargetingEnded   += HandleTargetingEnded;
            }
        }

        private void OnDisable()
        {
            cancelButton?.onClick.RemoveListener(OnCancelTapped);

            if (BoosterManager.Instance != null)
            {
                BoosterManager.Instance.OnTargetingStarted -= HandleTargetingStarted;
                BoosterManager.Instance.OnTargetingEnded   -= HandleTargetingEnded;
            }
        }

        private void HandleTargetingStarted(string boosterId)
        {
            if (bannerRoot != null) bannerRoot.SetActive(true);
            if (messageText != null)
                messageText.text = Messages.TryGetValue(boosterId, out string msg) ? msg : "Select a tile";
        }

        private void HandleTargetingEnded()
        {
            if (bannerRoot != null) bannerRoot.SetActive(false);
        }

        private void OnCancelTapped() => BoosterManager.Instance?.CancelTargeting();
    }
}
