// ============================================================
//  LevelResultManager.cs  —  COMPLETE FINAL VERSION
//
//  Yeh ek hi script sab handle karti hai:
//    1. Start Panel (Pre-Level info) — LevelManager.OnLevelInitialized event pe
//    2. Win Panel — star reveal with DOTween
//    3. Lose Panel
//    4. Navigation (Next, Replay, Map)
//    5. ProfileManager se save
//
//  IMPORTANT:
//    • ResultPanel.cs GameObject scene se DELETE karo
//    • UIManager ka mainPanel = GoalPanel set karo
//    • mainPanelState = Playing set karo
//
//  Race condition fix:
//    LevelManager.OnLevelInitialized static event fire karta hai
//    jab InitializeLevel() complete ho jaye — tab Start panel show hota hai
//
//  FIX (bug report ke baad — "goals complete ho gaye phir bhi Loss dikha"):
//    CheckLoseDeferred() pehle sirf 0.5s ki FIXED wait karta tha, phir
//    goalTracker.AllGoalsComplete check karta tha. Agar aakhri move par
//    koi combo/cascade chal raha ho (chained specials, multiple sweep
//    passes, gravity+refill) jo 0.5s se zyada le, to yeh check goals
//    complete hone SE PEHLE hi chal jata tha aur Lose dikha deta tha —
//    console mein "ALL GOALS COMPLETE!" thodi dair BAAD print hota tha,
//    lekin tab tak _resultShown already true ho chuka hota tha to
//    OnWin() kuch nahi karta tha. Ab yeh OnWin()'s ShowWinRoutine() ki
//    tarah pehle board ke MUKAMMAL settle (boardController.IsBusy ==
//    false — combos/cascades included) hone ka wait karta hai, phir
//    goals check karta hai.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Game.Firebase;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    public class LevelResultManager : MonoBehaviour
    {
        // ─── Dependencies ─────────────────────────────────────
        [Header("— Dependencies —")]
        [SerializeField] private GoalTracker     goalTracker;
        [SerializeField] private MoveCounter     moveCounter;
        [SerializeField] private LevelManager    levelManager;
        [SerializeField] private InputHandler    inputHandler;
        [SerializeField] private BoardController boardController;  // NEW — used to wait for the board to fully settle before showing results

        [Header("Config")]
        [SerializeField] private int coinsPerStar = 25;

        // ─── START PANEL ──────────────────────────────────────
        [Header("— Start Panel —")]
        [SerializeField] private GameObject startPanel;
        [SerializeField] private TMP_Text   startLevelTitle;    // "Level 1"
        [SerializeField] private TMP_Text   startMovesText;     // "Moves: 30"
        [SerializeField] private Transform  startGoalsContainer;
        [SerializeField] private GameObject goalRowPrefab;
        [SerializeField] private Button     startButton;
        [SerializeField] private Button     closeButton;

        // ─── WIN PANEL ────────────────────────────────────────
        [Header("— Win Panel —")]
        [SerializeField] private GameObject winPanel;
        [SerializeField] private TMP_Text   winLevelText;
        [SerializeField] private TMP_Text   winScoreText;
        [SerializeField] private TMP_Text   winCoinsText;
        [SerializeField] private TMP_Text   winMovesLeftText;
        [SerializeField] private Image[]    starImages;
        [SerializeField] private Sprite     starFilled;
        [SerializeField] private Sprite     starEmpty;
        [SerializeField] private Button     nextLevelButton;
        [SerializeField] private Button     winReplayButton;
        [SerializeField] private Button     winMapButton;

        // ─── LOSE PANEL ───────────────────────────────────────
        [Header("— Lose Panel —")]
        [SerializeField] private GameObject losePanel;
        [SerializeField] private TMP_Text   loseTitleText;
        [SerializeField] private TMP_Text   loseScoreText;
        [SerializeField] private Button     loseReplayButton;
        [SerializeField] private Button     loseMapButton;

        // ─── UNLOCK POPUP ─────────────────────────────────────
        [Header("— Unlock Popup (optional) —")]
        [SerializeField] private GameObject unlockPopup;
        [SerializeField] private TMP_Text   unlockTitleText;
        [SerializeField] private TMP_Text   unlockNameText;
        [SerializeField] private Button     unlockOkButton;

        // ─── ANIMATION ────────────────────────────────────────
        [Header("Animation")]
        [SerializeField] private float panelShowDelay  = 0.6f;
        [SerializeField] private float starRevealDelay = 0.35f;

        // ─── Private ──────────────────────────────────────────
        private bool _resultShown = false;
        private int  _currentLevelId;

        // ─────────────────────────────────────────────────────
        // AWAKE
        // ─────────────────────────────────────────────────────
        private void Awake()
        {
            // Hide all panels at start
            SafeHide(startPanel);
            SafeHide(winPanel);
            SafeHide(losePanel);
            SafeHide(unlockPopup);

            // Disable input until Start is pressed
            inputHandler?.SetInputEnabled(false);

            // Get level ID from session
            _currentLevelId = LevelSession.CurrentLevelId > 0
                ? LevelSession.CurrentLevelId
                : (GameManager.Instance != null ? GameManager.Instance.CurrentLevel : 1);

            ValidateButtonWiring();
        }

        /// <summary>
        /// Logs a clear error for any button field left unassigned in the Inspector.
        /// A button that "does nothing when tapped" almost always means either this
        /// field is null (nothing subscribed in OnEnable) or the GameObject it's
        /// assigned to isn't the same button the player is actually tapping (e.g. a
        /// leftover duplicate button sitting on top of it, or the assigned button's
        /// Collider/RaycastTarget is disabled). This won't catch a scene wiring
        /// mistake, but it WILL tell you immediately if the field itself is empty.
        /// </summary>
        private void ValidateButtonWiring()
        {
            if (startButton      == null) Debug.LogError("[LevelResultManager] startButton is not assigned!", this);
            if (nextLevelButton  == null) Debug.LogError("[LevelResultManager] nextLevelButton is not assigned!", this);
            if (winReplayButton  == null) Debug.LogError("[LevelResultManager] winReplayButton is not assigned!", this);
            if (winMapButton     == null) Debug.LogError("[LevelResultManager] winMapButton is not assigned!", this);
            if (loseReplayButton == null) Debug.LogError("[LevelResultManager] loseReplayButton is not assigned!", this);
            if (loseMapButton    == null) Debug.LogError("[LevelResultManager] loseMapButton is not assigned!", this);
        }

        // ─────────────────────────────────────────────────────
        // ENABLE / DISABLE — event subscriptions
        // ─────────────────────────────────────────────────────
        private void OnEnable()
        {
            // ── KEY FIX: LevelManager.OnLevelInitialized wait karo ─
            LevelManager.OnLevelInitialized += OnLevelReady;

            goalTracker?.OnAllGoalsComplete.AddListener(OnWin);
            moveCounter?.OnMovesExhausted .AddListener(OnMovesExhausted);

            startButton?     .onClick.RemoveAllListeners();
            closeButton?     .onClick.RemoveAllListeners();
            nextLevelButton? .onClick.RemoveAllListeners();
            winReplayButton? .onClick.RemoveAllListeners();
            winMapButton?    .onClick.RemoveAllListeners();
            loseReplayButton?.onClick.RemoveAllListeners();
            loseMapButton?   .onClick.RemoveAllListeners();
            unlockOkButton?  .onClick.RemoveAllListeners();

            startButton?     .onClick.AddListener(OnStartClicked);
            closeButton?     .onClick.AddListener(GoToMap);
            nextLevelButton? .onClick.AddListener(GoToNextLevel);
            winReplayButton? .onClick.AddListener(ReplayLevel);
            winMapButton?    .onClick.AddListener(GoToMap);
            loseReplayButton?.onClick.AddListener(ReplayLevel);
            loseMapButton?   .onClick.AddListener(GoToMap);
            unlockOkButton?  .onClick.AddListener(HideUnlockPopup);
        }

        private void OnDisable()
        {
            LevelManager.OnLevelInitialized -= OnLevelReady;
            goalTracker?.OnAllGoalsComplete.RemoveListener(OnWin);
            moveCounter?.OnMovesExhausted .RemoveListener(OnMovesExhausted);
        }

        // ─────────────────────────────────────────────────────
        // LEVEL READY — called after LevelManager.InitializeLevel()
        // ─────────────────────────────────────────────────────
        private void OnLevelReady()
        {
            // Update level ID (now LevelManager has set everything)
            if (LevelSession.CurrentLevelId > 0)
                _currentLevelId = LevelSession.CurrentLevelId;

            StartCoroutine(ShowStartPanelDeferred());
        }

        private IEnumerator ShowStartPanelDeferred()
        {
            // 1 frame wait — layout rebuild ke liye
            yield return null;
            BuildAndShowStartPanel();
        }

        // ─────────────────────────────────────────────────────
        // START PANEL BUILD + SHOW
        // ─────────────────────────────────────────────────────
        private void BuildAndShowStartPanel()
        {
            // Level title
            if (startLevelTitle != null)
                startLevelTitle.text = $"Level {_currentLevelId}";

            // Moves
            if (startMovesText != null)
            {
                int moves = moveCounter?.MovesRemaining
                    ?? LevelSession.CurrentLevel?.moveLimit
                    ?? 30;
                startMovesText.text = $"Moves: {moves}";
            }

            // Goal rows — wrapped in try-catch so panel shows
            // even if GoalRowPrefab has a missing script issue
            try { BuildGoalRows(); }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[LevelResultManager] BuildGoalRows failed: {e.Message}\n" +
                                  "GoalRowPrefab se Missing Script component remove karo.");
            }

            // Show panel regardless of goal row errors
            if (startPanel == null) return;
            startPanel.SetActive(true);
            startPanel.transform.localScale = Vector3.zero;
            startPanel.transform
                .DOScale(Vector3.one, 0.35f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);

            Debug.Log($"[LevelResultManager] Start panel shown for Level {_currentLevelId}");
        }

        private void BuildGoalRows()
        {
            if (startGoalsContainer == null || goalRowPrefab == null) return;

            // Clear existing
            foreach (Transform child in startGoalsContainer)
                Destroy(child.gameObject);

            IReadOnlyList<GoalData> goals = goalTracker?.Goals;
            if (goals == null || goals.Count == 0)
            {
                Debug.LogWarning("[LevelResultManager] No goals to show in start panel.");
                return;
            }

            foreach (var goal in goals)
            {
                if (goal == null) continue;
                GameObject row = Instantiate(goalRowPrefab, startGoalsContainer);

                // FIX: GetComponentsInChildren<Image>()[0] used to grab whichever
                // Image came first in hierarchy order — if the row prefab's ROOT
                // GameObject also has an Image component (e.g. a background/frame),
                // that was returned instead of the "IconImage" child, so the goal
                // sprite ended up painted on the row background. Look up the
                // exact named children instead so it's unambiguous.
                Transform iconTf   = row.transform.Find("IconImage");
                Transform labelTf  = row.transform.Find("GoalLabelText");
                Transform amountTf = row.transform.Find("AmountText");

                Image    iconImage  = iconTf   != null ? iconTf.GetComponent<Image>()      : null;
                TMP_Text labelText  = labelTf  != null ? labelTf.GetComponent<TMP_Text>()  : null;
                TMP_Text amountText = amountTf != null ? amountTf.GetComponent<TMP_Text>() : null;

                // Label
                string label = !string.IsNullOrEmpty(goal.goalLabel)
                    ? goal.goalLabel
                    : goal.goalType switch
                    {
                        GoalType.CollectTile => goal.targetTile != null
                            ? $"Collect {goal.targetTile.color}"
                            : "Collect Tiles",
                        GoalType.ReachScore => "Reach Score",
                        GoalType.ClearJelly => "Clear Jellies",
                        _                   => goal.name
                    };

                if (labelText  != null) labelText.text  = label;
                if (amountText != null) amountText.text = $"x {goal.requiredAmount}";

                // Icon
                if (iconImage != null)
                {
                    if (goal.goalIcon != null)
                        iconImage.sprite = goal.goalIcon;
                    else if (goal.targetTile?.sprite != null)
                        iconImage.sprite = goal.targetTile.sprite;
                }
                else
                {
                    Debug.LogWarning("[LevelResultManager] GoalRowPrefab has no child named " +
                                      "'IconImage' — goal icon can't be assigned. Check the prefab hierarchy.");
                }
            }
        }

        // ─────────────────────────────────────────────────────
        // START BUTTON
        // ─────────────────────────────────────────────────────
        private void OnStartClicked()
        {
            if (startPanel == null) return;
            startButton?.transform.DOPunchScale(Vector3.one * 0.2f, 0.15f, 5, 0.5f);
            startPanel.transform
                .DOScale(Vector3.zero, 0.2f)
                .SetEase(Ease.InBack)
                .OnComplete(() =>
                {
                    startPanel.SetActive(false);
                    inputHandler?.SetInputEnabled(true);
                    Debug.Log("[LevelResultManager] Gameplay started!");
                });
        }

        // ─────────────────────────────────────────────────────
        // WIN
        // ─────────────────────────────────────────────────────
        private void OnWin()
        {
            if (_resultShown) return;
            _resultShown = true;
            StartCoroutine(ShowWinRoutine());
        }

        private IEnumerator ShowWinRoutine()
        {
            inputHandler?.SetInputEnabled(false);

            // FIX: GoalTracker.OnAllGoalsComplete fires the INSTANT the goal-completing
            // tile is processed inside BoardController.ClearTiles() — which is mid-batch,
            // before that batch's AddScore() runs and before any further cascade/rotation
            // steps in BoardController.ResolveBoard() finish. The old fixed 0.6s delay
            // wasn't always long enough for a big match/cascade/combo, so the panel could
            // show a score snapshot taken before the board had actually finished blasting.
            // Now we wait for BoardController to report it's fully idle (all cascades,
            // gravity, refill AND rotation done) before reading the final score at all.
            yield return StartCoroutine(WaitForBoardToSettle());
            yield return new WaitForSeconds(panelShowDelay);

            int score     = levelManager?.Score ?? LevelSession.CurrentScore;
            int stars     = moveCounter?.GetStarRating() ?? 1;
            int movesLeft = moveCounter?.MovesRemaining   ?? 0;
            int coins     = stars * coinsPerStar;

            LevelSession.CurrentScore = score;
            LevelSession.CheckUnlocks();

            // ── Save via ProfileManager ───────────────────────
            ProfileManager.Instance?.OnLevelCompleted(_currentLevelId, stars, score, coins);

            // Populate texts
            if (winLevelText     != null) winLevelText.text     = $"Level {_currentLevelId} Complete!";
            if (winScoreText     != null) winScoreText.text     = $"Score: {score:N0}";
            if (winCoinsText     != null) winCoinsText.text     = $"+{coins} Coins";
            if (winMovesLeftText != null) winMovesLeftText.text = $"Moves Left: {movesLeft}";

            // Show panel
            SafeShow(winPanel);
            winPanel.transform.localScale = Vector3.zero;
            winPanel.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack);

            // Stars
            yield return new WaitForSeconds(0.25f);
            yield return StartCoroutine(RevealStars(stars));

            // Unlock popup
            yield return new WaitForSeconds(0.4f);
            if (LevelSession.NewPetUnlocked || LevelSession.BossArenaUnlocked)
                ShowUnlockPopup();
        }

        /// <summary>
        /// Waits until BoardController is done cascading/settling (or a safety
        /// timeout elapses, so a stuck board can never softlock the result panel).
        /// </summary>
        private IEnumerator WaitForBoardToSettle()
        {
            if (boardController == null) yield break;

            float timeout = 5f;
            float elapsed = 0f;
            while (boardController.IsBusy && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (elapsed >= timeout)
                Debug.LogWarning("[LevelResultManager] Timed out waiting for BoardController to settle — " +
                                  "showing results anyway. Check for a stuck cascade.");
        }

        // ─────────────────────────────────────────────────────
        // STAR REVEAL
        // ─────────────────────────────────────────────────────
        private IEnumerator RevealStars(int earned)
        {
            if (starImages == null) yield break;
            for (int i = 0; i < starImages.Length; i++)
            {
                if (starImages[i] == null) continue;
                bool filled = i < earned;
                starImages[i].sprite = filled ? starFilled : starEmpty;

                if (filled)
                {
                    starImages[i].transform.localScale = Vector3.zero;
                    starImages[i].transform
                        .DOScale(Vector3.one, 0.3f)
                        .SetEase(Ease.OutBack);
                    starImages[i].DOColor(Color.yellow, 0.15f)
                        .SetLoops(2, LoopType.Yoyo);
                    yield return new WaitForSeconds(starRevealDelay);
                }
                else
                {
                    starImages[i].transform.localScale = Vector3.one;
                    starImages[i].color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                }
            }
        }

        // ─────────────────────────────────────────────────────
        // LOSE
        // ─────────────────────────────────────────────────────
        private void OnMovesExhausted()
        {
            if (_resultShown) return;
            StartCoroutine(CheckLoseDeferred());
        }

        private IEnumerator CheckLoseDeferred()
        {
            // FIX: purani 0.5s ki FIXED wait kaafi nahi thi. Ab yeh pehle
            // board ke MUKAMMAL settle hone ka wait karta hai (gravity +
            // refill + cascade + koi bhi chal raha combo/special-blast —
            // sab BoardController.IsBusy ke andar aata hai), phir goals
            // check karta hai. Isse "last move par goals complete ho gaye
            // lekin Loss dikh gaya" wala race condition fix ho jata hai.
            yield return StartCoroutine(WaitForBoardToSettle());
            yield return new WaitForSeconds(0.2f);   // chhota safety buffer

            if (_resultShown) yield break;

            if (goalTracker != null && goalTracker.AllGoalsComplete)
                OnWin();
            else
            {
                _resultShown = true;
                StartCoroutine(ShowLoseRoutine());
            }
        }

        private IEnumerator ShowLoseRoutine()
        {
            inputHandler?.SetInputEnabled(false);
            yield return StartCoroutine(WaitForBoardToSettle());
            yield return new WaitForSeconds(panelShowDelay);

            int score = levelManager?.Score ?? LevelSession.CurrentScore;
            if (loseTitleText != null) loseTitleText.text = "Out of Moves!";
            if (loseScoreText != null) loseScoreText.text = $"Score: {score:N0}";

            SafeShow(losePanel);
            losePanel.transform.localScale = Vector3.zero;
            losePanel.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack);
        }

        // ─────────────────────────────────────────────────────
        // UNLOCK POPUP
        // ─────────────────────────────────────────────────────
        private void ShowUnlockPopup()
        {
            if (unlockPopup == null) return;

            if (LevelSession.NewPetUnlocked)
            {
                if (unlockTitleText != null) unlockTitleText.text = "New Pet Unlocked!";
                if (unlockNameText  != null) unlockNameText.text  = $"Pet #{LevelSession.UnlockedPetIndex + 1}";
            }
            else if (LevelSession.BossArenaUnlocked)
            {
                if (unlockTitleText != null) unlockTitleText.text = "Boss Arena Unlocked!";
                if (unlockNameText  != null) unlockNameText.text  = $"Boss Arena {LevelSession.UnlockedBossId}";
            }

            SafeShow(unlockPopup);
            unlockPopup.transform.localScale = Vector3.zero;
            unlockPopup.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutElastic);
        }

        private void HideUnlockPopup()
        {
            if (unlockPopup == null) return;
            unlockPopup.transform
                .DOScale(Vector3.zero, 0.2f)
                .SetEase(Ease.InBack)
                .OnComplete(() => unlockPopup.SetActive(false));
        }

        // ─────────────────────────────────────────────────────
        // NAVIGATION
        // ─────────────────────────────────────────────────────
        private void GoToNextLevel()
        {
            DOTween.KillAll();
            LevelLoader.LoadLevel(_currentLevelId + 1);
        }

        private void GoToMap()
        {
            DOTween.KillAll();
            LevelSession.Clear();
            GameManager.Instance?.ChangeState(GameState.Map);
        }

        private void ReplayLevel()
        {
            DOTween.KillAll();
            LevelLoader.LoadLevel(_currentLevelId, LevelSession.ActiveBoosters);
        }

        // ─────────────────────────────────────────────────────
        // UTILITY
        // ─────────────────────────────────────────────────────
        private static void SafeHide(GameObject go) { if (go != null) go.SetActive(false); }
        private static void SafeShow(GameObject go) { if (go != null) go.SetActive(true);  }
    }
}