// ============================================================
//  LevelTimer.cs  —  MonoBehaviour  (NEW)
//
//  Optional countdown timer for REGULAR levels (never Boss Arena —
//  the Boss scene has no LevelData / LevelManager, so this simply
//  never exists there).
//
//  Rules:
//    • Only active when the level's LevelData.useTimer is ON.
//    • Starts counting when the player taps the goal panel's Start
//      button (LevelResultManager calls StartTimer()).
//    • Uses scaled time — the in-game Settings popup (timeScale = 0)
//      and app-in-background automatically pause it.
//    • Also pauses while a tutorial card is on screen.
//    • Reaching 0 fires OnTimeUp → LevelResultManager waits for the
//      board to settle, then: goals complete → WIN, otherwise → LOSE
//      ("Time's Up!") — even if moves are still left.
//    • Moves running out still loses as before; whichever happens
//      first ends the level.
//
//  Attach to: an empty "LevelTimer" GameObject in GameBoardScene
//  (or directly on the timer's UI object in GameHUD).
//  Wire: timerText (TMP), optional fillImage / timerRoot / clock icon.
//  Then drag it into LevelManager.levelTimer AND LevelResultManager.levelTimer.
// ============================================================

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace Match3
{
    public class LevelTimer : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("The whole timer widget (icon + text + bar). Hidden automatically on levels without a timer.")]
        [SerializeField] private GameObject timerRoot;
        [Tooltip("Shows the remaining time as m:ss")]
        [SerializeField] private TMP_Text timerText;
        [Tooltip("Optional — Image with Image Type = Filled. Drains from 1 to 0.")]
        [SerializeField] private Image fillImage;
        [Tooltip("Optional — the clock icon; it shakes during the warning phase.")]
        [SerializeField] private RectTransform clockIcon;

        [Header("Colours")]
        [SerializeField] private Color normalColor  = Color.white;
        [SerializeField] private Color warningColor = new Color(1f, 0.3f, 0.3f);

        [Header("Events")]
        public UnityEvent       OnTimeUp;
        public UnityEvent<int>  OnSecondTick;     // passes whole seconds remaining
        public UnityEvent       OnWarningStarted;

        // ── State ─────────────────────────────────────────────
        public bool  IsEnabledForLevel { get; private set; }
        public bool  IsRunning         { get; private set; }
        public float TotalSeconds      { get; private set; }
        public float RemainingSeconds  { get; private set; }
        public bool  IsTimeUp          => IsEnabledForLevel && RemainingSeconds <= 0f;

        private int   _warningSeconds = 10;
        private int   _lastWholeSecond = -1;
        private bool  _warningActive;
        private float _freezeTimeLeft;   // for a future "Time Freeze" booster
        private Tween _pulseTween;

        // ─────────────────────────────────────────────────────
        //  PUBLIC API
        // ─────────────────────────────────────────────────────

        /// <summary>Called by LevelManager.InitializeLevel(). Hides the widget if this level has no timer.</summary>
        public void Initialize(LevelData data)
        {
            StopPulse();
            IsRunning        = false;
            _warningActive   = false;
            _freezeTimeLeft  = 0f;
            _lastWholeSecond = -1;

            IsEnabledForLevel = data != null && data.useTimer && data.timeLimitSeconds > 0;
            if (timerRoot != null) timerRoot.SetActive(IsEnabledForLevel);
            if (!IsEnabledForLevel) return;

            TotalSeconds     = data.timeLimitSeconds;
            RemainingSeconds = TotalSeconds;
            _warningSeconds  = Mathf.Max(1, data.timerWarningSeconds);
            RefreshUI();
        }

        /// <summary>Called by LevelResultManager when the player taps Start.</summary>
        public void StartTimer()
        {
            if (!IsEnabledForLevel) return;
            IsRunning = true;
        }

        /// <summary>Stops counting for good (win / lose / exit).</summary>
        public void StopTimer()
        {
            IsRunning = false;
            StopPulse();
        }

        /// <summary>Adds bonus seconds (e.g. a future "+10s" booster or pet power).</summary>
        public void AddTime(float seconds)
        {
            if (!IsEnabledForLevel || seconds <= 0f) return;
            RemainingSeconds += seconds;
            TotalSeconds      = Mathf.Max(TotalSeconds, RemainingSeconds);
            if (RemainingSeconds > _warningSeconds) { _warningActive = false; StopPulse(); }
            timerText?.transform.DOPunchScale(Vector3.one * 0.3f, 0.3f, 6, 0.6f);
            RefreshUI();
        }

        /// <summary>Freezes the clock for N seconds (ready for the "Time Freeze" store booster).</summary>
        public void FreezeFor(float seconds)
        {
            if (!IsEnabledForLevel || seconds <= 0f) return;
            _freezeTimeLeft = Mathf.Max(_freezeTimeLeft, seconds);
        }

        public string FormattedRemaining => Format(RemainingSeconds);

        // ─────────────────────────────────────────────────────

        private void Update()
        {
            if (!IsEnabledForLevel || !IsRunning) return;

            // Pause while a tutorial card is covering the board.
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsShowing) return;

            if (_freezeTimeLeft > 0f)
            {
                _freezeTimeLeft -= Time.deltaTime;
                return;
            }

            RemainingSeconds -= Time.deltaTime;   // scaled → pauses with timeScale 0

            if (RemainingSeconds <= 0f)
            {
                RemainingSeconds = 0f;
                IsRunning = false;
                RefreshUI();
                StopPulse();
                Debug.Log("[LevelTimer] Time's up!");
                OnTimeUp?.Invoke();
                return;
            }

            int whole = Mathf.CeilToInt(RemainingSeconds);
            if (whole != _lastWholeSecond)
            {
                _lastWholeSecond = whole;
                OnSecondTick?.Invoke(whole);

                if (whole <= _warningSeconds)
                {
                    if (!_warningActive)
                    {
                        _warningActive = true;
                        StartPulse();
                        OnWarningStarted?.Invoke();
                    }
                    AudioManager.Instance?.PlaySFX("timer_tick");
                }
            }

            RefreshUI();
        }

        private void RefreshUI()
        {
            if (timerText != null)
            {
                timerText.text  = Format(RemainingSeconds);
                timerText.color = _warningActive ? warningColor : normalColor;
            }
            if (fillImage != null)
            {
                fillImage.fillAmount = TotalSeconds > 0f ? RemainingSeconds / TotalSeconds : 0f;
                fillImage.color      = _warningActive ? warningColor : normalColor;
            }
        }

        private void StartPulse()
        {
            StopPulse();
            Transform t = timerText != null ? timerText.transform : (timerRoot != null ? timerRoot.transform : null);
            if (t != null)
                _pulseTween = t.DOScale(1.18f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
            clockIcon?.DOShakeRotation(0.5f, new Vector3(0, 0, 25f), 12, 90f).SetLoops(-1);
        }

        private void StopPulse()
        {
            _pulseTween?.Kill();
            _pulseTween = null;
            if (timerText != null) timerText.transform.localScale = Vector3.one;
            if (clockIcon != null) { clockIcon.DOKill(); clockIcon.localRotation = Quaternion.identity; }
        }

        private static string Format(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }

        private void OnDestroy() => StopPulse();
    }
}
