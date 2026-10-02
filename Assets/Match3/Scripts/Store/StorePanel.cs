using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Match3
{
    public class StorePanel : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] private TMP_Text coinsText;

        [Header("Grid")]
        [SerializeField] private RectTransform contentContainer;
        [SerializeField] private GameObject    cardPrefab;

        [Header("States")]
        [SerializeField] private GameObject loadingIndicator;
        [SerializeField] private GameObject offlineMessage;

        private readonly Dictionary<string, StoreItemCard> _cardsById = new();

        private void OnEnable()
        {
            if (loadingIndicator != null) loadingIndicator.SetActive(true);
            if (offlineMessage   != null) offlineMessage.SetActive(false);

            RefreshHeader();
            LocalSaveManager.OnProfileChanged += HandleProfileChanged;

            if (StoreManager.Instance != null)
            {
                StoreManager.Instance.OnCatalogLoaded         += HandleCatalogLoaded;
                StoreManager.Instance.OnCatalogLoadFailed     += HandleCatalogLoadFailed;
                StoreManager.Instance.OnItemGranted           += HandleItemGranted;
                StoreManager.Instance.OnPurchaseFailedFeedback += HandlePurchaseFailedFeedback;

                _ = StoreManager.Instance.LoadCatalogFromFirestoreAsync();
            }
            else
            {
                Debug.LogError("[StorePanel] StoreManager.Instance is NULL — check FirebaseManagers wiring.");
            }
        }

        private void OnDisable()
        {
            LocalSaveManager.OnProfileChanged -= HandleProfileChanged;

            if (StoreManager.Instance != null)
            {
                StoreManager.Instance.OnCatalogLoaded         -= HandleCatalogLoaded;
                StoreManager.Instance.OnCatalogLoadFailed     -= HandleCatalogLoadFailed;
                StoreManager.Instance.OnItemGranted           -= HandleItemGranted;
                StoreManager.Instance.OnPurchaseFailedFeedback -= HandlePurchaseFailedFeedback;
            }
        }

        private void HandleProfileChanged(PlayerProfile profile) => RefreshHeader(profile);

        private void RefreshHeader() => RefreshHeader(LocalSaveManager.GetOrLoadProfile());

        private void RefreshHeader(PlayerProfile profile)
        {
            if (coinsText != null) coinsText.text = (profile?.coins ?? 0).ToString("N0");

            RefreshOwnedCounts();
        }

        private void HandleCatalogLoaded(List<StoreItem> items)
        {
            if (loadingIndicator != null) loadingIndicator.SetActive(false);
            if (offlineMessage   != null) offlineMessage.SetActive(false);

            if (cardPrefab == null || contentContainer == null)
            {
                Debug.LogError("[StorePanel] cardPrefab / contentContainer not wired in Inspector.");
                return;
            }

            foreach (var kv in _cardsById)
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            _cardsById.Clear();

            Dictionary<string, int> inventory = LocalSaveManager.LoadBoosterInventory();

            foreach (StoreItem item in items)
            {
                GameObject go = Instantiate(cardPrefab, contentContainer);
                StoreItemCard card = go.GetComponent<StoreItemCard>();
                if (card == null)
                {
                    Debug.LogError("[StorePanel] cardPrefab is missing a StoreItemCard component.", go);
                    Destroy(go);
                    continue;
                }

                int owned = inventory.TryGetValue(item.id, out int c) ? c : 0;
                card.Setup(item, owned);
                _cardsById[item.id] = card;
            }
        }

        private void HandleCatalogLoadFailed(string message)
        {
            Debug.LogWarning($"[StorePanel] Catalog load issue: {message}");
        }

        private void HandleItemGranted(StoreItem item, int newOwnedCount)
        {
            if (_cardsById.TryGetValue(item.id, out StoreItemCard card) && card != null)
                card.Setup(item, newOwnedCount);
        }

        private void HandlePurchaseFailedFeedback(string message)
        {
            Debug.Log($"[StorePanel] Purchase feedback: {message}");
        }

        private void RefreshOwnedCounts()
        {
            if (_cardsById.Count == 0) return;
            Dictionary<string, int> inventory = LocalSaveManager.LoadBoosterInventory();

            foreach (var kv in _cardsById)
            {
                if (kv.Value == null || kv.Value.Data == null) continue;
                int owned = inventory.TryGetValue(kv.Key, out int c) ? c : 0;
                kv.Value.Setup(kv.Value.Data, owned);
            }
        }
    }
}
