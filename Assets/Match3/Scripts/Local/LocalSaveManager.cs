// ============================================================
//  LocalSaveManager.cs  —  static utility (NO GameObject needed)
//
//  Purpose:
//    Single source of truth for OFFLINE persistence. Wraps the
//    canonical global `PlayerProfile` (Firebase/PlayerProfile.cs)
//    using PlayerPrefs + Newtonsoft.Json.
//
//    Replaces:
//      - ProfileManager.CacheLocally() / LoadFromCache()
//        (those used JsonUtility, which silently drops
//        Dictionary fields like levelStars — see notes below)
//      - Match3.SaveManager's separate/duplicate PlayerProfile
//        class and its flat-key PlayerPrefs scheme.
//
//  Attach to: NOTHING. This is a static class — do not put it on
//  a GameObject. Call its methods directly from anywhere, e.g.
//  LocalSaveManager.SaveProfile(profile);
//
//  Requires: com.unity.nuget.newtonsoft-json package
//  (Window > Package Manager > Add package by name
//   "com.unity.nuget.newtonsoft-json")
// ============================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class LocalSaveManager
{
    // ── PlayerPrefs keys ─────────────────────────────────────
    private const string PROFILE_KEY   = "player_profile";
    private const string BOOSTER_KEY   = "local_booster_inventory";
    private const string SYNC_TIME_KEY = "last_sync_time";

    /// <summary>Bump this whenever PlayerProfile's schema changes,
    /// and add a matching step inside SaveMigration.</summary>
    public const int CURRENT_SAVE_VERSION = 2;

    // In-memory cache so repeated calls in one session don't keep
    // hitting PlayerPrefs/JSON parsing.
    private static PlayerProfile _cachedProfile;

    /// <summary>Fired every time SaveProfile() successfully persists a profile —
    /// e.g. coins changed, level completed, pet unlocked. UI (TopBarHUD,
    /// ProfilePanel, etc.) can subscribe to this instead of polling every frame.</summary>
    public static event Action<PlayerProfile> OnProfileChanged;

    // ────────────────────────────────────────────────────────
    // PROFILE
    // ────────────────────────────────────────────────────────

    /// <summary>Serializes profile to JSON and writes it synchronously
    /// to PlayerPrefs. Call this after every meaningful game event
    /// (level win, coins change, pet unlock, settings change, etc.)</summary>
    public static void SaveProfile(PlayerProfile profile)
    {
        if (profile == null)
        {
            Debug.LogWarning("[LocalSaveManager] SaveProfile called with null profile — ignored.");
            return;
        }

        profile.saveVersion = CURRENT_SAVE_VERSION;
        profile.lastUpdated = DateTime.UtcNow.ToString("o");

        try
        {
            string json = JsonConvert.SerializeObject(profile);
            PlayerPrefs.SetString(PROFILE_KEY, json);
            PlayerPrefs.Save(); // PlayerPrefs is synchronous — flush immediately
            _cachedProfile = profile;
            OnProfileChanged?.Invoke(profile);
        }
        catch (Exception e)
        {
            Debug.LogError($"[LocalSaveManager] SaveProfile failed: {e.Message}");
        }
    }

    /// <summary>Reads PlayerPrefs and deserializes. Returns null if no
    /// local save exists yet (first launch) or if it's unreadable.
    /// Automatically migrates old-schema saves via SaveMigration.</summary>
    public static PlayerProfile LoadProfile()
    {
        if (!PlayerPrefs.HasKey(PROFILE_KEY))
            return null;

        string json = PlayerPrefs.GetString(PROFILE_KEY);
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            int fromVersion = 1; // assume oldest schema if the field is missing entirely
            try
            {
                JObject probe = JObject.Parse(json);
                if (probe["saveVersion"] != null)
                    fromVersion = probe["saveVersion"].Value<int>();
            }
            catch
            {
                // Not even valid JSON — SaveMigration will handle/reject it below.
            }

            PlayerProfile profile;
            if (fromVersion < CURRENT_SAVE_VERSION)
            {
                profile = SaveMigration.Migrate(json, fromVersion, CURRENT_SAVE_VERSION);
                if (profile != null)
                    SaveProfile(profile); // persist the migrated copy so we don't re-migrate every launch
            }
            else
            {
                profile = JsonConvert.DeserializeObject<PlayerProfile>(json);
            }

            _cachedProfile = profile;
            return profile;
        }
        catch (Exception e)
        {
            Debug.LogError($"[LocalSaveManager] LoadProfile failed — save may be corrupt: {e.Message}");
            return null;
        }
    }

    /// <summary>Returns the in-memory cached profile if this session
    /// already loaded/saved one, otherwise loads from disk.</summary>
    public static PlayerProfile GetOrLoadProfile()
    {
        return _cachedProfile ?? LoadProfile();
    }

    // ────────────────────────────────────────────────────────
    // LEVEL RESULT
    // ────────────────────────────────────────────────────────

    /// <summary>Call right after a level is won (offline-safe). Updates
    /// stars (never downgrades), adds score, bumps levelsCompleted.</summary>
    public static void SaveLevelResult(int levelId, int stars, int score)
    {
        PlayerProfile profile = GetOrLoadProfile() ?? new PlayerProfile();

        profile.SetStars(levelId, stars);              // PlayerProfile.SetStars already keeps the max
        profile.totalScore += score;
        profile.levelsCompleted = Mathf.Max(profile.levelsCompleted, levelId);
        profile.level = profile.levelsCompleted + 1;

        SaveProfile(profile);
    }

    // ────────────────────────────────────────────────────────
    // BOOSTERS
    // ────────────────────────────────────────────────────────

    /// <summary>Saves a quantity-based booster inventory (id -> owned count),
    /// e.g. { "hammer": 3, "shuffle": 1 }. Also mirrors it into
    /// PlayerProfile.boosters (flat List&lt;string&gt;) so ProfileManager /
    /// Firestore sync keeps working unchanged.</summary>
    public static void SaveBoosterInventory(Dictionary<string, int> boosters)
    {
        boosters ??= new Dictionary<string, int>();

        try
        {
            string json = JsonConvert.SerializeObject(boosters);
            PlayerPrefs.SetString(BOOSTER_KEY, json);
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogError($"[LocalSaveManager] SaveBoosterInventory failed: {e.Message}");
            return;
        }

        PlayerProfile profile = GetOrLoadProfile() ?? new PlayerProfile();
        profile.boosters = new List<string>();
        foreach (var kv in boosters)
            for (int i = 0; i < kv.Value; i++)
                profile.boosters.Add(kv.Key);

        SaveProfile(profile);
    }

    /// <summary>Loads the quantity-based booster inventory saved above.</summary>
    public static Dictionary<string, int> LoadBoosterInventory()
    {
        if (!PlayerPrefs.HasKey(BOOSTER_KEY))
            return new Dictionary<string, int>();

        try
        {
            string json = PlayerPrefs.GetString(BOOSTER_KEY);
            return JsonConvert.DeserializeObject<Dictionary<string, int>>(json)
                   ?? new Dictionary<string, int>();
        }
        catch (Exception e)
        {
            Debug.LogError($"[LocalSaveManager] LoadBoosterInventory failed: {e.Message}");
            return new Dictionary<string, int>();
        }
    }

    // ────────────────────────────────────────────────────────
    // PETS
    // ────────────────────────────────────────────────────────

    /// <summary>Overwrites the unlocked-pet collection (list of pet ids,
    /// e.g. "Icera", "Sparky") and persists it.</summary>
    public static void SavePetCollection(List<string> petIds)
    {
        PlayerProfile profile = GetOrLoadProfile() ?? new PlayerProfile();
        profile.pets = new List<string>(petIds ?? new List<string>());
        profile.unlockedPets = new List<string>(profile.pets);
        SaveProfile(profile);
    }

    // ────────────────────────────────────────────────────────
    // LAST SYNC TIME (for cloud sync conflict resolution)
    // ────────────────────────────────────────────────────────

    public static DateTime GetLastSyncTime()
    {
        string raw = PlayerPrefs.GetString(SYNC_TIME_KEY, "");
        if (string.IsNullOrEmpty(raw)) return DateTime.MinValue;

        return DateTime.TryParse(
            raw, null,
            System.Globalization.DateTimeStyles.RoundtripKind,
            out DateTime result) ? result : DateTime.MinValue;
    }

    public static void SetLastSyncTime(DateTime t)
    {
        PlayerPrefs.SetString(SYNC_TIME_KEY, t.ToUniversalTime().ToString("o"));
        PlayerPrefs.Save();
    }

    // ────────────────────────────────────────────────────────
    // CLEAR
    // ────────────────────────────────────────────────────────

    /// <summary>Wipes ONLY this manager's keys (profile, boosters, sync time).
    /// Use for "Delete Account" or a genuine local wipe.
    /// Do NOT call this on ordinary logout — offline players rely on this
    /// cache to keep playing without internet.</summary>
    public static void ClearAll()
    {
        PlayerPrefs.DeleteKey(PROFILE_KEY);
        PlayerPrefs.DeleteKey(BOOSTER_KEY);
        PlayerPrefs.DeleteKey(SYNC_TIME_KEY);
        PlayerPrefs.Save();
        _cachedProfile = null;
        Debug.Log("[LocalSaveManager] Local save cleared.");
    }
}