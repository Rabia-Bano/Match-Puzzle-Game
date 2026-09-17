using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Firebase; // NEW — for AuthManager.CurrentUid

namespace Match3
{
    [System.Serializable]
    public class TutorialEntry
    {
        [Tooltip("Must exactly match the key passed to RequestTutorial() from code.")]
        public string key;

        public string title;

        [TextArea(2, 4)]
        public string message;

        [Tooltip("Optional — shown in the card/callout. Leave blank for none.")]
        public Sprite icon;
    }

    public class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        [Header("Content — one entry per tutorial key (see file header for the list)")]
        [SerializeField] private TutorialEntry[] entries;

        [Header("Overlay Root")]
        [Tooltip("Parent GameObject for the whole popup — starts SetActive(false) in the scene.")]
        [SerializeField] private GameObject overlayRoot;
        [Tooltip("Full-screen semi-transparent Image. Must have Raycast Target ON — " +
                 "this is what blocks touches from reaching the board underneath.")]
        [SerializeField] private Image dimBackground;
        [Tooltip("The card itself (icon + title + message + button) — this is what gets " +
                 "moved/scaled. For an obstacle intro it stays centered; for a special-tile " +
                 "callout it's repositioned next to the spotlight ring.")]
        [SerializeField] private RectTransform card;
        [Tooltip("Optional — small glow/ring sprite placed over the newly-created special " +
                 "tile's screen position. Hidden for obstacle intros (no world position).")]
        [SerializeField] private RectTransform spotlightRing;

        [Header("Card Contents")]
        [SerializeField] private Image    iconImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button   gotItButton;

        [Header("Canvas (for spotlight world→screen conversion)")]
        [Tooltip("The Canvas this overlay lives on. Required only if you use worldPos callouts.")]
        [SerializeField] private Canvas overlayCanvas;
        [Tooltip("Camera that renders the game board. Defaults to Camera.main if left blank.")]
        [SerializeField] private Camera worldCamera;

        // NEW — no longer a plain const. Tutorial-seen flags used to be saved as
        // PlayerPrefs "Tut_<key>", which is DEVICE-wide, not account-wide — so once
        // dismissed, a tutorial stayed hidden forever on that device even after
        // logout, even for a brand-new guest session. GetPrefPrefix() folds the
        // current account's Firebase UID into the key so each account (including
        // each fresh guest session, which gets its own UID) sees tutorials again.
        private string GetPrefPrefix()
        {
            string uid = AuthManager.CurrentUid;
            return "Tut_" + (string.IsNullOrEmpty(uid) ? "local" : uid) + "_";
        }

        private Dictionary<string, TutorialEntry> _lookup;
        private readonly Queue<(string key, Vector3? worldPos)> _queue = new();
        private bool _showing;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _lookup = new Dictionary<string, TutorialEntry>();
            if (entries != null)
                foreach (var e in entries)
                    if (e != null && !string.IsNullOrEmpty(e.key))
                        _lookup[e.key] = e;

            if (worldCamera == null) worldCamera = Camera.main;
            if (overlayRoot != null) overlayRoot.SetActive(false);
            if (gotItButton != null) gotItButton.onClick.AddListener(OnGotItPressed);
        }

        // ── Public API ────────────────────────────────────────
        public bool HasSeen(string key) => PlayerPrefs.GetInt(GetPrefPrefix() + key, 0) == 1;

        public void RequestTutorial(string key, Vector3? worldPos = null)
        {
            if (string.IsNullOrEmpty(key) || HasSeen(key)) return;

            if (!_lookup.ContainsKey(key))
            {
                Debug.LogWarning($"[TutorialManager] No TutorialEntry configured for key '{key}' — " +
                                  "add one in the Inspector list, or this tutorial will never show.");
                return;
            }

            _queue.Enqueue((key, worldPos));
            if (!_showing) ShowNext();
        }

        // ── Internal ──────────────────────────────────────────

        private void ShowNext()
        {
            if (_queue.Count == 0) { _showing = false; return; }

            var (key, worldPos) = _queue.Dequeue();
            TutorialEntry entry = _lookup[key];
            _showing = true;

            if (titleText   != null) titleText.text   = entry.title;
            if (messageText != null) messageText.text = entry.message;
            if (iconImage   != null)
            {
                iconImage.sprite  = entry.icon;
                iconImage.enabled = entry.icon != null;
            }

            // Remember which key is currently on screen so OnGotItPressed can mark it seen.
            _currentKey = key;
            if (worldPos.HasValue && spotlightRing != null && overlayCanvas != null && worldCamera != null)
            {
                Vector2 screenPoint = worldCamera.WorldToScreenPoint(worldPos.Value);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)overlayCanvas.transform, screenPoint,
                    overlayCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : worldCamera,
                    out Vector2 localPoint);

                spotlightRing.gameObject.SetActive(true);
                spotlightRing.anchoredPosition = localPoint;
            }
            else
            {
                if (spotlightRing != null) spotlightRing.gameObject.SetActive(false);
            }

            if (card != null) card.anchoredPosition = Vector2.zero; // ALWAYS centered now

            overlayRoot.SetActive(true);
            card.localScale = Vector3.zero;
            card.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack);
        }

        private string _currentKey;

        private void OnGotItPressed()
        {
            if (!string.IsNullOrEmpty(_currentKey))
                PlayerPrefs.SetInt(GetPrefPrefix() + _currentKey, 1);
            PlayerPrefs.Save();

            card.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack).OnComplete(() =>
            {
                overlayRoot.SetActive(false);
                ShowNext(); // shows the next queued one, if any; otherwise stays hidden
            });
        }
    }
}