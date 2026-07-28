// ============================================================
//  LeaderboardPanel.cs  —  MonoBehaviour
//  Attach to: "LeaderboardPanel" GameObject inside LeaderBoardScene's
//  UICanvas (sibling of TopBarPanel / BottomBarPanel — see setup guide).
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Game.Firebase;

namespace Match3
{
    public class LeaderboardPanel : MonoBehaviour
    {
        [Header("List")]
        [SerializeField] private ScrollRect    scrollRect;
        [SerializeField] private RectTransform contentContainer;   // ScrollRect -> Viewport -> Content
        [SerializeField] private GameObject    rowPrefab;          // needs LeaderboardRow component

        [Header("States")]
        [SerializeField] private GameObject loadingIndicator;
        [SerializeField] private GameObject offlineMessage;
        [SerializeField] private GameObject emptyMessage;

        [Header("Behaviour")]
        [Tooltip("Automatically scroll to the current player's row the first time data arrives.")]
        [SerializeField] private bool autoScrollToMe = true;

        private readonly Dictionary<string, LeaderboardRow> _rowsByUid    = new();
        private readonly Dictionary<string, int>             _lastRankByUid = new();
        private bool _hasScrolledToMe;

        private void OnEnable()
        {
            _hasScrolledToMe = false;

            if (loadingIndicator != null) loadingIndicator.SetActive(true);
            if (offlineMessage   != null) offlineMessage.SetActive(false);
            if (emptyMessage     != null) emptyMessage.SetActive(false);

            if (LeaderboardManager.Instance != null)
            {
                LeaderboardManager.Instance.OnLeaderboardUpdated += HandleLeaderboardUpdated;
                LeaderboardManager.Instance.OnLeaderboardError   += HandleLeaderboardError;
                LeaderboardManager.Instance.StartListening();
            }
            else
            {
                Debug.LogError("[LeaderboardPanel] LeaderboardManager.Instance is NULL — check FirebaseManagers wiring.");
            }

            if (NetworkChecker.Instance != null && !NetworkChecker.Instance.IsOnline)
                ShowOffline();
        }

        private void OnDisable()
        {
            if (LeaderboardManager.Instance != null)
            {
                LeaderboardManager.Instance.OnLeaderboardUpdated -= HandleLeaderboardUpdated;
                LeaderboardManager.Instance.OnLeaderboardError   -= HandleLeaderboardError;
                LeaderboardManager.Instance.StopListening();
            }
        }

        // ============================================================
        //  DATA
        // ============================================================

        private void HandleLeaderboardUpdated(List<LeaderboardEntry> entries)
        {
            if (loadingIndicator != null) loadingIndicator.SetActive(false);
            if (offlineMessage   != null) offlineMessage.SetActive(false);

            if (emptyMessage != null)
                emptyMessage.SetActive(entries.Count == 0);

            string myUid = AuthManager.CurrentUser?.UserId;
            var seenUids = new HashSet<string>();
            LeaderboardRow myRow = null;

            foreach (LeaderboardEntry entry in entries)
            {
                seenUids.Add(entry.uid);

                if (!_rowsByUid.TryGetValue(entry.uid, out LeaderboardRow row))
                {
                    if (rowPrefab == null || contentContainer == null)
                    {
                        Debug.LogError("[LeaderboardPanel] rowPrefab / contentContainer not wired in Inspector.");
                        return;
                    }

                    GameObject go = Instantiate(rowPrefab, contentContainer);
                    row = go.GetComponent<LeaderboardRow>();
                    if (row == null)
                    {
                        Debug.LogError("[LeaderboardPanel] rowPrefab is missing a LeaderboardRow component.", go);
                        Destroy(go);
                        continue;
                    }
                    _rowsByUid[entry.uid] = row;
                }

                // Keep row order in the hierarchy matching rank order (1st on top).
                row.transform.SetSiblingIndex(entry.rank - 1);

                bool isMe = !string.IsNullOrEmpty(myUid) && entry.uid == myUid;
                int prevRank = _lastRankByUid.TryGetValue(entry.uid, out int pr) ? pr : entry.rank;

                row.Setup(entry, isMe, prevRank);
                _lastRankByUid[entry.uid] = entry.rank;

                if (isMe) myRow = row;
            }

            // Remove rows for players no longer present in this snapshot
            // (e.g. banned/removed, or filtered out server-side).
            List<string> stale = new List<string>();
            foreach (var kv in _rowsByUid)
                if (!seenUids.Contains(kv.Key)) stale.Add(kv.Key);

            foreach (string uid in stale)
            {
                Destroy(_rowsByUid[uid].gameObject);
                _rowsByUid.Remove(uid);
                _lastRankByUid.Remove(uid);
            }

            if (autoScrollToMe && !_hasScrolledToMe && myRow != null)
            {
                _hasScrolledToMe = true;
                ScrollToRow(myRow.transform as RectTransform);
            }
        }

        private void HandleLeaderboardError(string message)
        {
            Debug.LogError($"[LeaderboardPanel] Leaderboard error: {message}");
            if (loadingIndicator != null) loadingIndicator.SetActive(false);
            ShowOffline();
        }

        private void ShowOffline()
        {
            if (offlineMessage != null) offlineMessage.SetActive(true);
            if (loadingIndicator != null) loadingIndicator.SetActive(false);
        }

        private void ScrollToRow(RectTransform rowRect)
        {
            if (scrollRect == null || rowRect == null || contentContainer == null) return;

            Canvas.ForceUpdateCanvases();
            float contentHeight  = contentContainer.rect.height;
            float viewportHeight = ((RectTransform)scrollRect.viewport).rect.height;
            if (contentHeight <= viewportHeight) return; // nothing to scroll

            float rowY = -rowRect.anchoredPosition.y;
            float normalized = Mathf.Clamp01(1f - (rowY / (contentHeight - viewportHeight)));
            scrollRect.verticalNormalizedPosition = normalized;
        }

    }
}