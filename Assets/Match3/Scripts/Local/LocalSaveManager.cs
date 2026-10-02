using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class LocalSaveManager
{
    private const string PROFILE_KEY   = "player_profile";
    private const string BOOSTER_KEY   = "local_booster_inventory";
    private const string SYNC_TIME_KEY = "last_sync_time";

    public const int CURRENT_SAVE_VERSION = 2;

    private static PlayerProfile _cachedProfile;

    public static event Action<PlayerProfile> OnProfileChanged;

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
            PlayerPrefs.Save();
            _cachedProfile = profile;
            OnProfileChanged?.Invoke(profile);
        }
        catch (Exception e)
        {
            Debug.LogError($"[LocalSaveManager] SaveProfile failed: {e.Message}");
        }
    }

    public static PlayerProfile LoadProfile()
    {
        if (!PlayerPrefs.HasKey(PROFILE_KEY))
            return null;

        string json = PlayerPrefs.GetString(PROFILE_KEY);
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            int fromVersion = 1;
            try
            {
                JObject probe = JObject.Parse(json);
                if (probe["saveVersion"] != null)
                    fromVersion = probe["saveVersion"].Value<int>();
            }
            catch
            {
            }

            PlayerProfile profile;
            if (fromVersion < CURRENT_SAVE_VERSION)
            {
                profile = SaveMigration.Migrate(json, fromVersion, CURRENT_SAVE_VERSION);
                if (profile != null)
                    SaveProfile(profile);
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

    public static PlayerProfile GetOrLoadProfile()
    {
        return _cachedProfile ?? LoadProfile();
    }

    public static void SaveLevelResult(int levelId, int stars, int score)
    {
        PlayerProfile profile = GetOrLoadProfile() ?? new PlayerProfile();

        profile.SetStars(levelId, stars);
        profile.totalScore += score;
        profile.levelsCompleted = Mathf.Max(profile.levelsCompleted, levelId);
        profile.level = profile.levelsCompleted + 1;

        SaveProfile(profile);
    }

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

    public static void SavePetCollection(List<string> petIds)
    {
        PlayerProfile profile = GetOrLoadProfile() ?? new PlayerProfile();
        profile.pets = new List<string>(petIds ?? new List<string>());
        profile.unlockedPets = new List<string>(profile.pets);
        SaveProfile(profile);
    }

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
