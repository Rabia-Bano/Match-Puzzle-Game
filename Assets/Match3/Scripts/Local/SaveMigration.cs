// ============================================================
//  SaveMigration.cs  —  static utility
//
//  Converts an OLD-schema save JSON string into the CURRENT
//  PlayerProfile shape. Called internally by LocalSaveManager
//  whenever a save's "saveVersion" is older than
//  LocalSaveManager.CURRENT_SAVE_VERSION.
//
//  v1 -> v2 example below models a real, relevant case for this
//  project: an old save written with different field names
//  (e.g. "score" instead of "totalScore", "highestLevel" instead
//  of "levelsCompleted", boosters as a comma-separated string,
//  per-level stars as flat "level_0_stars" keys instead of a
//  levelStars dictionary) — exactly the shape the old
//  Match3.SaveManager / JsonUtility-based cache used to produce.
//
//  Attach to: NOTHING. Static class, no GameObject.
// ============================================================

using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class SaveMigration
{
    /// <summary>
    /// Migrates a raw JSON save string from fromVersion up to toVersion,
    /// running each version-step migration in sequence, and returns a
    /// ready-to-use PlayerProfile. Returns null if the JSON is unreadable.
    /// </summary>
    public static PlayerProfile Migrate(string json, int fromVersion, int toVersion)
    {
        JObject obj;
        try
        {
            obj = JObject.Parse(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveMigration] Save JSON is corrupt, cannot migrate: {e.Message}");
            return null;
        }

        int version = fromVersion;

        if (version == 1 && toVersion >= 2)
        {
            obj = Migrate_v1_to_v2(obj);
            version = 2;
        }

        // Future migrations chain here, e.g.:
        // if (version == 2 && toVersion >= 3) { obj = Migrate_v2_to_v3(obj); version = 3; }

        obj["saveVersion"] = version;

        try
        {
            return obj.ToObject<PlayerProfile>();
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveMigration] Failed to build PlayerProfile after migration: {e.Message}");
            return null;
        }
    }

    // ────────────────────────────────────────────────────────
    // v1 -> v2
    // ────────────────────────────────────────────────────────
    private static JObject Migrate_v1_to_v2(JObject old)
    {
        var migrated = new JObject();

        migrated["uid"]         = old["uid"] ?? "";
        migrated["displayName"] = old["displayName"] ?? old["username"] ?? "";
        migrated["email"]       = old["email"] ?? "";
        migrated["avatarUrl"]   = old["avatarUrl"] ?? "";

        // v1 field "score" -> v2 field "totalScore"
        migrated["totalScore"] = old["score"] ?? old["totalScore"] ?? 0;

        // v1 field "highestLevel" -> v2 field "levelsCompleted"
        int levelsCompleted = (old["highestLevel"] ?? old["levelsCompleted"] ?? 0).Value<int>();
        migrated["levelsCompleted"] = levelsCompleted;
        migrated["level"] = levelsCompleted + 1;

        migrated["coins"] = old["coins"] ?? 0;
        migrated["gems"]  = old["gems"]  ?? 0;
        migrated["isBanned"] = old["isBanned"] ?? false;

        migrated["currentThemeIndex"]   = old["currentThemeIndex"]   ?? 0;
        migrated["highestBossDefeated"] = old["highestBossDefeated"] ?? 0;

        migrated["soundEnabled"]     = old["soundEnabled"]     ?? true;
        migrated["musicEnabled"]     = old["musicEnabled"]     ?? true;
        migrated["vibrationEnabled"] = old["vibrationEnabled"] ?? true;

        // v1 boosters: comma-separated string, e.g. "hammer,hammer,shuffle"
        // v2 boosters: JSON array of strings
        var boostersArr = new JArray();
        if (old["boosters"] != null && old["boosters"].Type == JTokenType.String)
        {
            string raw = old["boosters"].Value<string>();
            if (!string.IsNullOrEmpty(raw))
                foreach (string id in raw.Split(','))
                {
                    string trimmed = id.Trim();
                    if (!string.IsNullOrEmpty(trimmed)) boostersArr.Add(trimmed);
                }
        }
        else if (old["boosters"] is JArray existingArr)
        {
            boostersArr = existingArr;
        }
        migrated["boosters"] = boostersArr;

        // v1 per-level stars: flat keys "level_0_stars", "level_1_stars", ...
        // v2: single "levelStars" dictionary { "level_0": 3, "level_1": 2 }
        var levelStars = new JObject();
        foreach (var prop in old.Properties())
        {
            const string prefix = "level_";
            const string suffix = "_stars";
            if (prop.Name.StartsWith(prefix) && prop.Name.EndsWith(suffix) &&
                prop.Name.Length > prefix.Length + suffix.Length)
            {
                string idx = prop.Name.Substring(prefix.Length,
                    prop.Name.Length - prefix.Length - suffix.Length);
                levelStars[$"level_{idx}"] = prop.Value;
            }
        }
        // If v1 already had a levelStars dictionary (some builds did), keep it.
        if (old["levelStars"] is JObject existingStars)
            foreach (var kv in existingStars)
                levelStars[kv.Key] = kv.Value;

        migrated["levelStars"] = levelStars;

        migrated["pets"]         = old["pets"] ?? new JArray();
        migrated["unlockedPets"] = old["unlockedPets"] ?? migrated["pets"];

        migrated["joinDate"]    = old["joinDate"] ?? DateTime.UtcNow.ToString("o");
        migrated["lastUpdated"] = DateTime.UtcNow.ToString("o");

        Debug.Log("[SaveMigration] Migrated local save v1 -> v2.");
        return migrated;
    }
}
