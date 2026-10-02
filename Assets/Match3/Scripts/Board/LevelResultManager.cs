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
        [Header("— Dependencies —")]
        [SerializeField] private GoalTracker     goalTracker;
        [SerializeField] private MoveCounter     moveCounter;
        [SerializeField] private LevelManager    levelManager;
        [SerializeField] private InputHandler    inputHandler;
        [SerializeField] private BoardController boardController;
        [Tooltip("NEW — optional. Drag the LevelTimer here (same object you dragged into LevelManager.levelTimer).")]
        [SerializeField] private LevelTimer      levelTimer;

        [Header("Config")]
        [SerializeField] private int coinsPerStar = 25;

        [Header("— Start Panel —")]
        [SerializeField] private GameObject startPanel;
        [SerializeField] private TMP_Text   startLevelTitle;
        [SerializeField] private TMP_Text   startMovesText;
        [Tooltip("NEW — optional. Shows \"Time: 1:30\" on the start panel for timed levels; hidden otherwise.")]
        [SerializeField] private TMP_Text   startTimeText;
        [SerializeField] private Transform  startGoalsContainer;
        [SerializeField] private GameObject goalRowPrefab;
        [SerializeField] private Button     startButton;
        [SerializeField] private Button     closeButton;

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

        [Header("— Lose Panel —")]
        [SerializeField] private GameObject losePanel;
        [SerializeField] private TMP_Text   loseTitleText;
        [SerializeField] private TMP_Text   loseScoreText;
        [SerializeField] private Button     loseReplayButton;
        [SerializeField] private Button     loseMapButton;

        [Header("— Unlock Popup (optional) —")]
        [SerializeField] private GameObject unlockPopup;
        [SerializeField] private TMP_Text   unlockTitleText;
        [SerializeField] private TMP_Text   unlockNameText;
        [Tooltip("Optional — shows the theme's bg3 thumbnail / the pet's portrait / the boss's " +
                 "portrait for whichever notice is currently on screen. Leave unassigned if you " +
                 "don't want an icon on this popup.")]
        [SerializeField] private Image      unlockIconImage;
        [SerializeField] private Button     unlockOkButton;

        private struct UnlockNotice
        {
            public string Title;
            public string Message;
            public Sprite Icon;
        }

        private readonly Queue<UnlockNotice> _unlockQueue = new Queue<UnlockNotice>();

        [Header("Animation")]
        [SerializeField] private float panelShowDelay  = 0.6f;
        [SerializeField] private float starRevealDelay = 0.35f;

        [Header("— Win Celebration Effect —")]
        [SerializeField] private ParticleSystem winCelebrationPrefab;

        private bool _resultShown = false;
        private int  _currentLevelId;
        private bool _loseByTimer = false;

        private void Awake()
        {
            SafeHide(startPanel);
            SafeHide(winPanel);
            SafeHide(losePanel);
            SafeHide(unlockPopup);

            inputHandler?.SetInputEnabled(false);

            _currentLevelId = LevelSession.CurrentLevelId > 0
                ? LevelSession.CurrentLevelId
                : (GameManager.Instance != null ? GameManager.Instance.CurrentLevel : 1);

            ValidateButtonWiring();
        }

        private void ValidateButtonWiring()
        {
            if (startButton      == null) Debug.LogError("[LevelResultManager] startButton is not assigned!", this);
            if (nextLevelButton  == null) Debug.LogError("[LevelResultManager] nextLevelButton is not assigned!", this);
            if (winReplayButton  == null) Debug.LogError("[LevelResultManager] winReplayButton is not assigned!", this);
            if (winMapButton     == null) Debug.LogError("[LevelResultManager] winMapButton is not assigned!", this);
            if (loseReplayButton == null) Debug.LogError("[LevelResultManager] loseReplayButton is not assigned!", this);
            if (loseMapButton    == null) Debug.LogError("[LevelResultManager] loseMapButton is not assigned!", this);
        }

        private void OnEnable()
        {
            LevelManager.OnLevelInitialized += OnLevelReady;

            goalTracker?.OnAllGoalsComplete.AddListener(OnWin);
            moveCounter?.OnMovesExhausted .AddListener(OnMovesExhausted);
            levelTimer?.OnTimeUp          .AddListener(OnTimeUp);

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

            startButton?     .onClick.AddListener(() => AudioManager.Instance?.PlaySFX("button_click"));
            closeButton?     .onClick.AddListener(() => AudioManager.Instance?.PlaySFX("button_click"));
            nextLevelButton? .onClick.AddListener(() => AudioManager.Instance?.PlaySFX("button_click"));
            winReplayButton? .onClick.AddListener(() => AudioManager.Instance?.PlaySFX("button_click"));
            winMapButton?    .onClick.AddListener(() => AudioManager.Instance?.PlaySFX("button_click"));
            loseReplayButton?.onClick.AddListener(() => AudioManager.Instance?.PlaySFX("button_click"));
            loseMapButton?   .onClick.AddListener(() => AudioManager.Instance?.PlaySFX("button_click"));
            unlockOkButton?  .onClick.AddListener(() => AudioManager.Instance?.PlaySFX("button_click"));
        }

        private void OnDisable()
        {
            LevelManager.OnLevelInitialized -= OnLevelReady;
            goalTracker?.OnAllGoalsComplete.RemoveListener(OnWin);
            moveCounter?.OnMovesExhausted .RemoveListener(OnMovesExhausted);
            levelTimer?.OnTimeUp          .RemoveListener(OnTimeUp);
        }

        private void OnLevelReady()
        {
            if (LevelSession.CurrentLevelId > 0)
                _currentLevelId = LevelSession.CurrentLevelId;

            StartCoroutine(ShowStartPanelDeferred());
        }

        private IEnumerator ShowStartPanelDeferred()
        {
            yield return null;
            BuildAndShowStartPanel();
        }

        private void BuildAndShowStartPanel()
        {
            if (startLevelTitle != null)
                startLevelTitle.text = $"Level {_currentLevelId}";

            if (startMovesText != null)
            {
                int moves = moveCounter?.MovesRemaining
                    ?? LevelSession.CurrentLevel?.moveLimit
                    ?? 30;
                startMovesText.text = $"Moves: {moves}";
            }

            if (startTimeText != null)
            {
                bool timed = levelTimer != null && levelTimer.IsEnabledForLevel;
                startTimeText.gameObject.SetActive(timed);
                if (timed) startTimeText.text = $"Time: {levelTimer.FormattedRemaining}";
            }

            try { BuildGoalRows(); }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[LevelResultManager] BuildGoalRows failed: {e.Message}\n" +
                                  "GoalRowPrefab se Missing Script component remove karo.");
            }

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

                Transform iconTf   = row.transform.Find("IconImage");
                Transform labelTf  = row.transform.Find("GoalLabelText");
                Transform amountTf = row.transform.Find("AmountText");

                Image    iconImage  = iconTf   != null ? iconTf.GetComponent<Image>()      : null;
                TMP_Text labelText  = labelTf  != null ? labelTf.GetComponent<TMP_Text>()  : null;
                TMP_Text amountText = amountTf != null ? amountTf.GetComponent<TMP_Text>() : null;

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

                    LivesManager.Instance?.MarkLevelInProgress();

                    levelTimer?.StartTimer();

                    PlayerActivityTracker.Instance?.RecordLevelStart(_currentLevelId);

                    LevelManager.Instance?.QueueObstacleTutorials();
                });
        }

        private void OnWin()
        {
            if (_resultShown) return;
            _resultShown = true;

            levelTimer?.StopTimer();
            PlayerActivityTracker.Instance?.RecordLevelResult(_currentLevelId, PlayerActivityTracker.Result.Win);

            LivesManager.Instance?.ClearLevelInProgress();

            AudioManager.Instance?.PlaySFX("level_win");
            JuiceManager.Instance?.FlashScreen(Color.white, 0.2f);
            TileVisualController.PlayEffect(winCelebrationPrefab, TileVisualController.ScreenCenterWorldPoint(), Color.white);
            StartCoroutine(ShowWinRoutine());
        }

        private IEnumerator ShowWinRoutine()
        {
            inputHandler?.SetInputEnabled(false);

            yield return StartCoroutine(WaitForBoardToSettle());
            yield return new WaitForSeconds(panelShowDelay);

            int score     = levelManager?.Score ?? LevelSession.CurrentScore;
            int stars     = moveCounter?.GetStarRating() ?? 1;
            int movesLeft = moveCounter?.MovesRemaining   ?? 0;
            int coins     = stars * coinsPerStar;

            LevelSession.CurrentScore = score;

            int previousLevelsCompleted = ProfileManager.Instance?.Profile?.levelsCompleted ?? 0;
            LevelSession.CheckUnlocks(previousLevelsCompleted);

            bool themeJustChanged = false;
            Match3.Theme.ThemeData newTheme = null;
            System.Action<Match3.Theme.ThemeData> onThemeChanged = t =>
            {
                themeJustChanged = true;
                newTheme = t;
            };
            if (Match3.Theme.ThemeManager.Instance != null)
                Match3.Theme.ThemeManager.Instance.OnThemeChanged += onThemeChanged;

            ProfileManager.Instance?.OnLevelCompleted(_currentLevelId, stars, score, coins);

            if (Match3.Theme.ThemeManager.Instance != null)
                Match3.Theme.ThemeManager.Instance.OnThemeChanged -= onThemeChanged;

            BuildUnlockQueue(themeJustChanged, newTheme);

            bool hasUnlocksToShow = unlockPopup != null && _unlockQueue.Count > 0;
            SetWinButtonsInteractable(!hasUnlocksToShow);

            _ = CloudSyncManager.Instance?.SyncAfterLevelAsync();
            _ = Game.Firebase.LeaderboardManager.Instance?.SubmitScore(score, _currentLevelId.ToString());

            if (winLevelText     != null) winLevelText.text     = $"Level {_currentLevelId} Complete!";
            if (winScoreText     != null) winScoreText.text     = $"Score: {score:N0}";
            if (winCoinsText     != null) winCoinsText.text     = $"+{coins} Coins";
            if (winMovesLeftText != null) winMovesLeftText.text = $"Moves Left: {movesLeft}";

            SafeShow(winPanel);
            winPanel.transform.localScale = Vector3.zero;
            winPanel.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack);

            yield return new WaitForSeconds(0.25f);
            yield return StartCoroutine(RevealStars(stars));

            yield return new WaitForSeconds(0.4f);
            if (_unlockQueue.Count > 0)
                ShowNextUnlockNotice();
        }

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

        private void OnMovesExhausted()
        {
            if (_resultShown) return;
            levelTimer?.StopTimer();
            _loseByTimer = false;
            StartCoroutine(CheckLoseDeferred());
        }

        private void OnTimeUp()
        {
            if (_resultShown) return;
            inputHandler?.SetInputEnabled(false);
            _loseByTimer = true;
            AudioManager.Instance?.PlaySFX("time_up");
            StartCoroutine(CheckLoseDeferred());
        }

        private IEnumerator CheckLoseDeferred()
        {
            yield return StartCoroutine(WaitForBoardToSettle());
            yield return new WaitForSeconds(0.2f);

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

            LivesManager.Instance?.ClearLevelInProgress();

            PlayerActivityTracker.Instance?.RecordLevelResult(_currentLevelId,
                _loseByTimer ? PlayerActivityTracker.Result.LoseTime : PlayerActivityTracker.Result.LoseMoves);

            LivesManager.Instance?.LoseLife();

            AudioManager.Instance?.PlaySFX("level_fail");
            yield return StartCoroutine(WaitForBoardToSettle());
            yield return new WaitForSeconds(panelShowDelay);

            int score = levelManager?.Score ?? LevelSession.CurrentScore;
            if (loseTitleText != null) loseTitleText.text = _loseByTimer ? "Time's Up!" : "Out of Moves!";
            if (loseScoreText != null) loseScoreText.text = $"Score: {score:N0}";

            SafeShow(losePanel);
            losePanel.transform.localScale = Vector3.zero;
            losePanel.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack);
        }

        private void BuildUnlockQueue(bool themeJustChanged, Match3.Theme.ThemeData newTheme)
        {
            _unlockQueue.Clear();

            if (themeJustChanged && newTheme != null)
            {
                _unlockQueue.Enqueue(new UnlockNotice
                {
                    Title   = "A New Theme is Unlocked!",
                    Message = newTheme.themeName,
                    Icon    = newTheme.bg3
                });
            }

            if (LevelSession.NewPetUnlocked)
            {
                PetData unlockedPet = null;
                foreach (PetData p in Resources.LoadAll<PetData>("Pets"))
                {
                    if (p != null && p.unlockAfterLevel == _currentLevelId) { unlockedPet = p; break; }
                }

                _unlockQueue.Enqueue(new UnlockNotice
                {
                    Title   = "A New Pet is Unlocked!",
                    Message = unlockedPet != null ? unlockedPet.petName : $"Pet #{LevelSession.UnlockedPetIndex + 1}",
                    Icon    = unlockedPet != null ? unlockedPet.sprite : null
                });
            }

            if (LevelSession.BossArenaUnlocked)
            {
                BossData unlockedBoss = Resources.Load<BossData>($"Bosses/boss_{LevelSession.UnlockedBossId}");

                _unlockQueue.Enqueue(new UnlockNotice
                {
                    Title   = "A New Boss Arena is Unlocked!",
                    Message = unlockedBoss != null ? unlockedBoss.bossName : $"Boss Arena {LevelSession.UnlockedBossId}",
                    Icon    = unlockedBoss != null ? unlockedBoss.portrait : null
                });
            }
        }

        private void ShowNextUnlockNotice()
        {
            if (unlockPopup == null || _unlockQueue.Count == 0) return;

            UnlockNotice notice = _unlockQueue.Dequeue();

            if (unlockTitleText != null) unlockTitleText.text = notice.Title;
            if (unlockNameText  != null) unlockNameText.text  = notice.Message;
            if (unlockIconImage != null)
            {
                unlockIconImage.sprite  = notice.Icon;
                unlockIconImage.enabled = notice.Icon != null;
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
                .OnComplete(() =>
                {
                    unlockPopup.SetActive(false);
                    if (_unlockQueue.Count > 0)
                        ShowNextUnlockNotice();
                    else
                        SetWinButtonsInteractable(true);
                });
        }

        private void SetWinButtonsInteractable(bool interactable)
        {
            if (nextLevelButton != null) nextLevelButton.interactable = interactable;
            if (winReplayButton != null) winReplayButton.interactable = interactable;
            if (winMapButton    != null) winMapButton.interactable    = interactable;
        }

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

        private static void SafeHide(GameObject go) { if (go != null) go.SetActive(false); }
        private static void SafeShow(GameObject go) { if (go != null) go.SetActive(true);  }
    }
}
