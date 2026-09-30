// ============================================================
//  LivesManager.cs  —  MonoBehaviour, DontDestroyOnLoad singleton
//
//  V2 — now with:
//    1. Auto-regen: +1 life every `regenIntervalSeconds` (default 300s
//       = 5 min) whenever CurrentLives < MaxLives. Survives app close —
//       the "next life due" timestamp is a saved UTC time, not a
//       running Unity timer, so re-opening the app after being away
//       for a while correctly grants however many lives were earned
//       while it was closed (catch-up, not just "start counting from 0
//       again").
//    2. Countdown for UI: SecondsUntilNextLife / NextLifeCountdownText
//       (e.g. "4 min") — TopBarHUD.cs reads this every frame to show
//       next to the heart icon, and hides it once lives are full.
//    3. Backend: local PlayerPrefs (works fully offline, always
//       authoritative for THIS device) + Firestore via
//       Game.Firebase.ProfileManager (same debounced SaveProfile()/
//       UpdateField() pattern every other profile field already uses)
//       — so lives now travel with the player's account across devices,
//       same as coins/score/pets.
//
//  Attach to: an empty GameObject named "LivesManager" in
//  PreloaderScene, sibling of GameManager / AudioManager / ProfileManager.
// ============================================================

using System;
using UnityEngine;

public class LivesManager : MonoBehaviour
{
    public static LivesManager Instance { get; private set; }

    [Header("Config")]
    [SerializeField] private int   maxLives             = 5;
    [SerializeField] private float regenIntervalSeconds = 300f;  // 5 minutes

    public int  MaxLives      => maxLives;
    public int  CurrentLives  { get; private set; }
    public bool HasLives      => CurrentLives > 0;

    /// <summary>Fired whenever the life COUNT changes. Passes (current, max).</summary>
    public event Action<int, int> OnLivesChanged;

    /// <summary>
    /// Seconds remaining until the next auto-granted life. 0 when lives
    /// are already full (nothing regenerating). TopBarHUD.cs polls this
    /// every frame — cheap (just a DateTime subtraction), no event needed.
    /// </summary>
    public float SecondsUntilNextLife
    {
        get
        {
            if (CurrentLives >= maxLives || !_hasNextLifeTime) return 0f;
            double remaining = (_nextLifeUtc - DateTime.UtcNow).TotalSeconds;
            return (float)Math.Max(0, remaining);
        }
    }

    /// <summary>Convenience for UI: "4 min" / "1 min" / "" when full. Rounds UP so it never shows "0 min" right before the tick.</summary>
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
    private const string PREF_NEXT_LIFE_UTC = "player_next_life_utc";   // ISO-8601 string, empty = not regenerating

    // NEW (Rabia's report — "app got killed from the recent-apps list while
    // mid-level, but no life was lost"): a regular level's LOSE screen already
    // costs a life (see LevelResultManager.ShowLoseRoutine()) — but that code
    // only runs if the app is still ALIVE to run it. If the player backgrounds
    // the app mid-level and then kills it from the OS's task switcher, Unity
    // never gets a chance to run any of that — the process is just gone. This
    // flag is how we catch that case anyway, without needing OnApplicationPause
    // at all: LevelResultManager.OnStartClicked() sets it true the moment a
    // level truly starts, and clears it the moment that level ends ANY way
    // (win, lose, or the player deliberately exits to map). Both of those
    // writes call PlayerPrefs.Save() immediately (not the usual batched write)
    // specifically so they survive being killed a split-second later. Because
    // this DontDestroyOnLoad singleton's Awake() only ever runs once per actual
    // COLD START of the app (never again just from backgrounding/foregrounding
    // within the same session), finding this flag still TRUE the next time
    // Awake() runs can only mean one thing: last session ended with a level
    // in progress that never got a chance to finish or be exited properly —
    // i.e. exactly the kill-while-playing case.
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
        CatchUpRegen();   // grants any lives earned while the app was closed
        CheckForAbandonedLevelPenalty();   // NEW — see PREF_LEVEL_IN_PROGRESS comment above

        Debug.Log($"[LivesManager] Initialized — {CurrentLives}/{maxLives} lives. " +
                   (_hasNextLifeTime ? $"Next life in {SecondsUntilNextLife:0}s." : "Full — not regenerating."));
    }

    private void Start()
    {
        // Adopt the cloud value once the player's profile finishes loading
        // (login flow) — cloud is authoritative for cross-device play,
        // same "cloud wins on conflict" rule the rest of the save system
        // already follows (see ConflictResolver.cs).
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

    // Every frame: cheap tick — only does real work once a second, and
    // only touches PlayerPrefs/Firestore when a life actually regenerates.
    private void Update()
    {
        if (CurrentLives >= maxLives) return;

        _tickAccumulator += Time.unscaledDeltaTime;
        if (_tickAccumulator < 1f) return;
        _tickAccumulator = 0f;

        CatchUpRegen();
    }

    // ─────────────────────────────────────────────────────────
    // PUBLIC API
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Removes one life (clamped at 0). Starts the regen timer if this is
    /// the FIRST life lost since being full (if already regenerating, the
    /// existing timer for the next life is left untouched — only one life
    /// regenerates at a time, standard match-3 behaviour).
    /// </summary>
    public void LoseLife()
    {
        if (CurrentLives <= 0) return;

        bool wasFull = CurrentLives == maxLives;
        CurrentLives--;

        if (wasFull) StartRegenTimer();

        PersistAndBroadcast();
        Debug.Log($"[LivesManager] Life lost — {CurrentLives}/{maxLives} remaining.");
    }

    /// <summary>Adds lives directly (rewarded ad, IAP, admin grant) — clamped at MaxLives.</summary>
    public void AddLife(int amount = 1)
    {
        if (amount <= 0) return;
        int before = CurrentLives;
        CurrentLives = Mathf.Min(maxLives, CurrentLives + amount);
        if (CurrentLives == maxLives) _hasNextLifeTime = false;

        if (CurrentLives != before) PersistAndBroadcast();
    }

    /// <summary>Force-refreshes any newly-enabled listener (e.g. TopBarHUD.OnEnable()).</summary>
    public void RaiseCurrentState() => OnLivesChanged?.Invoke(CurrentLives, maxLives);

    // ─────────────────────────────────────────────────────────
    // ABANDONED-LEVEL PENALTY (app killed mid-level — see PREF_LEVEL_IN_PROGRESS)
    // ─────────────────────────────────────────────────────────

    /// <summary>Call the moment a REGULAR level's gameplay actually begins
    /// (LevelResultManager.OnStartClicked() — NOT Boss Arena, which never
    /// touches lives at all). Persisted immediately so it survives a kill.</summary>
    public void MarkLevelInProgress()
    {
        PlayerPrefs.SetInt(PREF_LEVEL_IN_PROGRESS, 1);
        PlayerPrefs.Save();
    }

    /// <summary>Call the moment a level ends ANY way — win, lose, or the
    /// player deliberately exits to the map. Persisted immediately for the
    /// same reason as MarkLevelInProgress().</summary>
    public void ClearLevelInProgress()
    {
        PlayerPrefs.SetInt(PREF_LEVEL_IN_PROGRESS, 0);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Runs once, at app cold-start. If a level was left "in progress" from
    /// the PREVIOUS run of the app — never cleared by a win, a lose, or a
    /// deliberate exit to map — the only way that could happen is the app got
    /// killed while that level was still being played. Costs exactly one life,
    /// same as running out of moves normally would have.
    /// </summary>
    private void CheckForAbandonedLevelPenalty()
    {
        if (PlayerPrefs.GetInt(PREF_LEVEL_IN_PROGRESS, 0) != 1) return;

        PlayerPrefs.SetInt(PREF_LEVEL_IN_PROGRESS, 0);
        PlayerPrefs.Save();

        Debug.Log("[LivesManager] A level was still in progress when the app last closed " +
                   "(killed mid-level) — deducting 1 life, same as a normal loss.");
        LoseLife();
    }

    // ─────────────────────────────────────────────────────────
    // REGEN LOGIC
    // ─────────────────────────────────────────────────────────

    private void StartRegenTimer()
    {
        _nextLifeUtc     = DateTime.UtcNow.AddSeconds(regenIntervalSeconds);
        _hasNextLifeTime = true;
    }

    /// <summary>
    /// Grants every life that's "come due" since _nextLifeUtc — handles
    /// both the normal 1-second tick AND large gaps (app was closed for
    /// an hour = several lives granted at once, up to MaxLives).
    /// </summary>
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
                // Still not full — schedule the following life from THIS
                // grant's due time (not from "now"), so regen speed stays
                // exactly 1 per interval even after a catch-up burst.
                _nextLifeUtc = _nextLifeUtc.AddSeconds(regenIntervalSeconds);
            }
        }

        if (changed)
        {
            PersistAndBroadcast();
            Debug.Log($"[LivesManager] Regen — now {CurrentLives}/{maxLives}.");
        }
    }

    // ─────────────────────────────────────────────────────────
    // PERSISTENCE — local (always) + cloud (best-effort, debounced)
    // ─────────────────────────────────────────────────────────

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
            // Lives are missing but no timer was saved (e.g. first run after
            // this update, or manual PlayerPrefs edit) — start a fresh timer
            // rather than leaving the player stuck with no regen at all.
            StartRegenTimer();
        }
    }

    private void PersistAndBroadcast()
    {
        // 1) Local — always, instantly, works fully offline.
        PlayerPrefs.SetInt(PREF_LIVES, CurrentLives);
        PlayerPrefs.SetString(PREF_NEXT_LIFE_UTC, _hasNextLifeTime ? _nextLifeUtc.ToString("o") : "");
        PlayerPrefs.Save();

        // 2) Cloud — best-effort, debounced (same pattern coins/score use).
        //    Safe to call even if the player is offline or not logged in
        //    yet — ProfileManager no-ops when Profile is null.
        var profileManager = Game.Firebase.ProfileManager.Instance;
        if (profileManager != null && profileManager.Profile != null)
        {
            profileManager.Profile.lives       = CurrentLives;
            profileManager.Profile.nextLifeUtc = _hasNextLifeTime ? _nextLifeUtc.ToString("o") : "";
            profileManager.SaveProfile();   // debounced full save, same as OnLevelCompleted() etc.
        }

        OnLivesChanged?.Invoke(CurrentLives, maxLives);
    }

    /// <summary>
    /// Runs once, right after the player's cloud profile finishes loading
    /// (login). Cloud is treated as authoritative — adopts Profile.lives /
    /// nextLifeUtc, overwriting whatever the local PlayerPrefs guess was.
    /// </summary>
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

        CatchUpRegen();       // in case cloud's nextLifeUtc is already in the past
        PersistAndBroadcast(); // sync this device's local cache to match cloud

        Debug.Log($"[LivesManager] Adopted cloud lives — {CurrentLives}/{maxLives}.");
    }
}