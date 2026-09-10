// ============================================================
//  AudioManager.cs  —  MonoBehaviour, DontDestroyOnLoad singleton
//
//  Central sound system for the whole game. Same pattern as
//  GameManager / SceneLoader — lives only in PreloaderScene on the
//  persistent "FirebaseManagers" GameObject (or its own persistent
//  object), Awake() does the singleton + DontDestroyOnLoad dance,
//  and every other scene just calls AudioManager.Instance.
//
//  Two AudioSources:
//    musicSource — loops background music, one clip at a time
//    sfxSource   — one-shots for match/swap/win/etc (PlayOneShot,
//                  so overlapping SFX don't cut each other off)
//
//  Clips are wired in the Inspector as (key, AudioClip) pairs and
//  converted into Dictionaries at Awake() for fast lookup by string
//  key — e.g. AudioManager.Instance.PlaySFX("tile_match").
//
//  Volume + mute state persist to PlayerPrefs using the same flat
//  lower_snake_case key style as LocalSaveManager.cs:
//    "audio_music_volume", "audio_sfx_volume",
//    "audio_music_on",     "audio_sfx_on"
//
//  Attach to: an empty GameObject named "AudioManager" that lives
//  inside PreloaderScene (sibling of FirebaseManagers, or as a
//  child of it — either is fine, it does its own DontDestroyOnLoad).
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    // ── Inspector: Audio Sources ────────────────────────────

    [Header("Audio Sources")]
    [Tooltip("Loops background music. Auto-created at Awake() if left empty.")]
    [SerializeField] private AudioSource musicSource;

    [Tooltip("Plays one-shot SFX. Auto-created at Awake() if left empty.")]
    [SerializeField] private AudioSource sfxSource;

    [Header("Optional — Audio Mixer")]
    [Tooltip("Assign GameMixer here to route Music/SFX through mixer groups " +
             "(see the SKILL guide section on setting up the mixer). Leave " +
             "blank and the manager will just control AudioSource.volume directly.")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private string musicMixerParam = "MusicVolume";
    [SerializeField] private string sfxMixerParam    = "SFXVolume";

    // ── Inspector: Clip Libraries ───────────────────────────

    [Serializable]
    public class SoundEntry
    {
        [Tooltip("Lookup key used in code, e.g. \"tile_match\".")]
        public string key;
        public AudioClip clip;
    }

    [Header("SFX Library")]
    [Tooltip("Recommended keys: tile_swap, tile_match, special_activate, " +
             "board_rotate, level_win, level_fail, button_click, pet_skill, boss_attack")]
    [SerializeField] private List<SoundEntry> sfxClips = new();

    [Header("Music Library")]
    [Tooltip("Keys are up to you — e.g. \"map_theme\", \"ice_world_theme\", \"boss_theme\".")]
    [SerializeField] private List<SoundEntry> musicClips = new();

    // ── Runtime lookup ───────────────────────────────────────

    private Dictionary<string, AudioClip> _sfxDict;
    private Dictionary<string, AudioClip> _musicDict;
    private string _currentMusicKey;

    // ── PlayerPrefs keys (match LocalSaveManager's naming style) ──

    private const string PREF_MUSIC_VOL = "audio_music_volume";
    private const string PREF_SFX_VOL   = "audio_sfx_volume";
    private const string PREF_MUSIC_ON  = "audio_music_on";
    private const string PREF_SFX_ON    = "audio_sfx_on";

    // ── Public volume / mute state ───────────────────────────

    private float _musicVolume = 1f;
    private float _sfxVolume   = 1f;
    private bool  _musicOn     = true;
    private bool  _sfxOn       = true;

    /// <summary>0..1. Setting this updates the source/mixer AND saves to PlayerPrefs.</summary>
    public float MusicVolume
    {
        get => _musicVolume;
        set
        {
            _musicVolume = Mathf.Clamp01(value);
            ApplyMusicVolume();
            PlayerPrefs.SetFloat(PREF_MUSIC_VOL, _musicVolume);
            PlayerPrefs.Save();
        }
    }

    /// <summary>0..1. Setting this updates the source/mixer AND saves to PlayerPrefs.</summary>
    public float SFXVolume
    {
        get => _sfxVolume;
        set
        {
            _sfxVolume = Mathf.Clamp01(value);
            ApplySFXVolume();
            PlayerPrefs.SetFloat(PREF_SFX_VOL, _sfxVolume);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Master on/off toggle for music (Settings screen "Music" checkbox).</summary>
    public bool MusicOn
    {
        get => _musicOn;
        set
        {
            _musicOn = value;
            ApplyMusicVolume();
            PlayerPrefs.SetInt(PREF_MUSIC_ON, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Master on/off toggle for SFX (Settings screen "Sound" checkbox).</summary>
    public bool SFXOn
    {
        get => _sfxOn;
        set
        {
            _sfxOn = value;
            PlayerPrefs.SetInt(PREF_SFX_ON, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    // ── Lifecycle ─────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
        }

        BuildDictionaries();
        LoadSettings();

        Debug.Log("[AudioManager] Initialized — persists across all scenes.");
    }

    private void BuildDictionaries()
    {
        _sfxDict = new Dictionary<string, AudioClip>();
        foreach (var entry in sfxClips)
        {
            if (entry == null || string.IsNullOrEmpty(entry.key) || entry.clip == null) continue;
            if (!_sfxDict.ContainsKey(entry.key)) _sfxDict.Add(entry.key, entry.clip);
            else Debug.LogWarning($"[AudioManager] Duplicate SFX key '{entry.key}' — first entry kept.");
        }

        _musicDict = new Dictionary<string, AudioClip>();
        foreach (var entry in musicClips)
        {
            if (entry == null || string.IsNullOrEmpty(entry.key) || entry.clip == null) continue;
            if (!_musicDict.ContainsKey(entry.key)) _musicDict.Add(entry.key, entry.clip);
            else Debug.LogWarning($"[AudioManager] Duplicate Music key '{entry.key}' — first entry kept.");
        }
    }

    /// <summary>Reads saved volume/mute state from PlayerPrefs (defaults: full volume, both on).</summary>
    private void LoadSettings()
    {
        _musicVolume = PlayerPrefs.GetFloat(PREF_MUSIC_VOL, 1f);
        _sfxVolume   = PlayerPrefs.GetFloat(PREF_SFX_VOL, 1f);
        _musicOn     = PlayerPrefs.GetInt(PREF_MUSIC_ON, 1) == 1;
        _sfxOn       = PlayerPrefs.GetInt(PREF_SFX_ON, 1) == 1;

        ApplyMusicVolume();
        ApplySFXVolume();
    }

    // ── Public API ────────────────────────────────────────────

    /// <summary>Plays a one-shot SFX by key. Does nothing (with a warning) if the key isn't in the library.</summary>
    public void PlaySFX(string key)
    {
        if (!_sfxOn) return;
        if (string.IsNullOrEmpty(key)) return;

        if (_sfxDict != null && _sfxDict.TryGetValue(key, out AudioClip clip) && clip != null)
        {
            sfxSource.PlayOneShot(clip, _sfxVolume);
        }
        else
        {
            Debug.LogWarning($"[AudioManager] PlaySFX: no clip found for key '{key}'.");
        }
    }

    /// <summary>Starts looping (by default) background music by key. Ignores the call if the same track is already playing.</summary>
    public void PlayMusic(string key, bool loop = true)
    {
        if (string.IsNullOrEmpty(key)) return;

        if (_musicDict == null || !_musicDict.TryGetValue(key, out AudioClip clip) || clip == null)
        {
            Debug.LogWarning($"[AudioManager] PlayMusic: no clip found for key '{key}'.");
            return;
        }

        if (_currentMusicKey == key && musicSource.isPlaying) return;

        _currentMusicKey = key;
        musicSource.clip = clip;
        musicSource.loop = loop;
        ApplyMusicVolume();
        if (_musicOn) musicSource.Play();
    }

    /// <summary>Stops whatever music is currently playing.</summary>
    public void StopMusic()
    {
        _currentMusicKey = null;
        musicSource.Stop();
    }

    // ── Internal helpers ──────────────────────────────────────

    private void ApplyMusicVolume()
    {
        float vol = _musicOn ? _musicVolume : 0f;

        if (audioMixer != null && !string.IsNullOrEmpty(musicMixerParam))
        {
            // Mixer expects decibels; -80dB is effectively silent.
            float db = vol > 0.0001f ? Mathf.Log10(vol) * 20f : -80f;
            audioMixer.SetFloat(musicMixerParam, db);
        }
        else
        {
            musicSource.volume = vol;
        }

        if (!_musicOn && musicSource.isPlaying) musicSource.Pause();
        else if (_musicOn && !musicSource.isPlaying && musicSource.clip != null) musicSource.Play();
        // FIX: was UnPause() before — that only resumes a source that was
        // previously paused via Pause(). If MusicOn was false right from the
        // very first PlayMusic() call (e.g. loaded from a saved "off"
        // setting), musicSource.Play() was never called at all, so there was
        // nothing to "un-pause" — the source was stuck in a stopped state
        // forever, until the whole app restarted and PlayMusic() ran again
        // with MusicOn already true. Play() correctly handles BOTH cases:
        // it resumes from a paused position if the source was paused, and
        // starts fresh if the source was never started.
    }

    private void ApplySFXVolume()
    {
        if (audioMixer != null && !string.IsNullOrEmpty(sfxMixerParam))
        {
            float vol = _sfxVolume;
            float db = vol > 0.0001f ? Mathf.Log10(vol) * 20f : -80f;
            audioMixer.SetFloat(sfxMixerParam, db);
        }
        // sfxSource itself doesn't need a volume set — PlayOneShot takes the
        // volume per-call (see PlaySFX above), which is what lets multiple
        // overlapping SFX each carry the right volume.
    }
}