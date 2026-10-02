using System;
using UnityEngine;

public class LivesManager : MonoBehaviour
{
    public static LivesManager Instance { get; private set; }

    [Header("Config")]
    [SerializeField] private int   maxLives             = 5;
    [SerializeField] private float regenIntervalSeconds = 300f;

    public int  MaxLives      => maxLives;
    public int  CurrentLives  { get; private set; }
    public bool HasLives      => CurrentLives > 0;

    public event Action<int, int> OnLivesChanged;

    public float SecondsUntilNextLife
    {
        get
        {
            if (CurrentLives >= maxLives || !_hasNextLifeTime) return 0f;
            double remaining = (_nextLifeUtc - DateTime.UtcNow).TotalSeconds;
            return (float)Math.Max(0, remaining);
        }
    }

    public string NextLifeCountdownText
    {
        get
        {
            float secs = SecondsUntilNextLife;
            if (secs <= 0f) return "";
            int mins = Mathf.CeilToInt(secs / 60f);
            return $"{mins} min";
        }
    }

    private const string PREF_LIVES         = "player_lives";
    private const string PREF_NEXT_LIFE_UTC = "player_next_life_utc";

    private const string PREF_LEVEL_IN_PROGRESS = "level_in_progress_flag";

    private DateTime _nextLifeUtc;
    private bool     _hasNextLifeTime;
    private float    _tickAccumulator;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadLocal();
        CatchUpRegen();
        CheckForAbandonedLevelPenalty();

        Debug.Log($"[LivesManager] Initialized — {CurrentLives}/{maxLives} lives. " +
                   (_hasNextLifeTime ? $"Next life in {SecondsUntilNextLife:0}s." : "Full — not regenerating."));
    }

    private void Start()
    {
        var profileManager = Game.Firebase.ProfileManager.Instance;
        if (profileManager != null)
            profileManager.OnProfileLoaded.AddListener(HandleProfileLoaded);
    }

    private void OnDestroy()
    {
        var profileManager = Game.Firebase.ProfileManager.Instance;
        if (profileManager != null)
            profileManager.OnProfileLoaded.RemoveListener(HandleProfileLoaded);
    }

    private void Update()
    {
        if (CurrentLives >= maxLives) return;

        _tickAccumulator += Time.unscaledDeltaTime;
        if (_tickAccumulator < 1f) return;
        _tickAccumulator = 0f;

        CatchUpRegen();
    }

    public void LoseLife()
    {
        if (CurrentLives <= 0) return;

        bool wasFull = CurrentLives == maxLives;
        CurrentLives--;

        if (wasFull) StartRegenTimer();

        PersistAndBroadcast();
        Debug.Log($"[LivesManager] Life lost — {CurrentLives}/{maxLives} remaining.");
    }

    public void AddLife(int amount = 1)
    {
        if (amount <= 0) return;
        int before = CurrentLives;
        CurrentLives = Mathf.Min(maxLives, CurrentLives + amount);
        if (CurrentLives == maxLives) _hasNextLifeTime = false;

        if (CurrentLives != before) PersistAndBroadcast();
    }

    public void RaiseCurrentState() => OnLivesChanged?.Invoke(CurrentLives, maxLives);

    public void MarkLevelInProgress()
    {
        PlayerPrefs.SetInt(PREF_LEVEL_IN_PROGRESS, 1);
        PlayerPrefs.Save();
    }

    public void ClearLevelInProgress()
    {
        PlayerPrefs.SetInt(PREF_LEVEL_IN_PROGRESS, 0);
        PlayerPrefs.Save();
    }

    private void CheckForAbandonedLevelPenalty()
    {
        if (PlayerPrefs.GetInt(PREF_LEVEL_IN_PROGRESS, 0) != 1) return;

        PlayerPrefs.SetInt(PREF_LEVEL_IN_PROGRESS, 0);
        PlayerPrefs.Save();

        Debug.Log("[LivesManager] A level was still in progress when the app last closed " +
                   "(killed mid-level) — deducting 1 life, same as a normal loss.");
        LoseLife();
    }

    private void StartRegenTimer()
    {
        _nextLifeUtc     = DateTime.UtcNow.AddSeconds(regenIntervalSeconds);
        _hasNextLifeTime = true;
    }

    private void CatchUpRegen()
    {
        if (!_hasNextLifeTime || CurrentLives >= maxLives) return;

        bool changed = false;
        while (_hasNextLifeTime && CurrentLives < maxLives && DateTime.UtcNow >= _nextLifeUtc)
        {
            CurrentLives++;
            changed = true;

            if (CurrentLives >= maxLives)
            {
                _hasNextLifeTime = false;
            }
            else
            {
                _nextLifeUtc = _nextLifeUtc.AddSeconds(regenIntervalSeconds);
            }
        }

        if (changed)
        {
            PersistAndBroadcast();
            Debug.Log($"[LivesManager] Regen — now {CurrentLives}/{maxLives}.");
        }
    }

    private void LoadLocal()
    {
        CurrentLives = Mathf.Clamp(PlayerPrefs.GetInt(PREF_LIVES, maxLives), 0, maxLives);

        string savedUtc = PlayerPrefs.GetString(PREF_NEXT_LIFE_UTC, "");
        if (!string.IsNullOrEmpty(savedUtc) &&
            DateTime.TryParse(savedUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsed))
        {
            _nextLifeUtc     = parsed;
            _hasNextLifeTime = CurrentLives < maxLives;
        }
        else if (CurrentLives < maxLives)
        {
            StartRegenTimer();
        }
    }

    private void PersistAndBroadcast()
    {
        PlayerPrefs.SetInt(PREF_LIVES, CurrentLives);
        PlayerPrefs.SetString(PREF_NEXT_LIFE_UTC, _hasNextLifeTime ? _nextLifeUtc.ToString("o") : "");
        PlayerPrefs.Save();

        var profileManager = Game.Firebase.ProfileManager.Instance;
        if (profileManager != null && profileManager.Profile != null)
        {
            profileManager.Profile.lives       = CurrentLives;
            profileManager.Profile.nextLifeUtc = _hasNextLifeTime ? _nextLifeUtc.ToString("o") : "";
            profileManager.SaveProfile();
        }

        OnLivesChanged?.Invoke(CurrentLives, maxLives);
    }

    private void HandleProfileLoaded()
    {
        var profile = Game.Firebase.ProfileManager.Instance?.Profile;
        if (profile == null) return;

        CurrentLives = Mathf.Clamp(profile.lives, 0, maxLives);

        if (!string.IsNullOrEmpty(profile.nextLifeUtc) &&
            DateTime.TryParse(profile.nextLifeUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsed))
        {
            _nextLifeUtc     = parsed;
            _hasNextLifeTime = CurrentLives < maxLives;
        }
        else if (CurrentLives < maxLives)
        {
            StartRegenTimer();
        }
        else
        {
            _hasNextLifeTime = false;
        }

        CatchUpRegen();
        PersistAndBroadcast();

        Debug.Log($"[LivesManager] Adopted cloud lives — {CurrentLives}/{maxLives}.");
    }
}
