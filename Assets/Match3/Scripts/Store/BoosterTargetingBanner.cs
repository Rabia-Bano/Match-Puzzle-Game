using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Match3
{
    public class BoosterTargetingBanner : MonoBehaviour
    {
        [SerializeField] private GameObject bannerRoot;
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
