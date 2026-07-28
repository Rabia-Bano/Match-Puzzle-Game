using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PlayerProfile
{
    // ── Save schema version (used by LocalSaveManager / SaveMigration) ──
    public int saveVersion = 2;

    // ── Identity ──────────────────────────────────────────────
    public string uid          = "";
    public string displayName  = "";
    public string email        = "";
    public string avatarUrl    = "";

    // ── Progression ───────────────────────────────────────────
    public int  level           = 1;
    public int  totalScore      = 0;
    public int  gems            = 0;
    public int  coins           = 0;
    public int  levelsCompleted = 0;
    public bool isBanned        = false;

    // ── Theme & Boss ──────────────────────────────────────────
    public int currentThemeIndex  = 0;
    public int highestBossDefeated = 0;

    // ── Settings ──────────────────────────────────────────────
    public bool soundEnabled     = true;
    public bool musicEnabled     = true;
    public bool vibrationEnabled = true;

    // ── Per-level stars:  "level_1" -> 3 ─────────────────────
    public Dictionary<string, int> levelStars = new Dictionary<string, int>();

    // ── Collections ───────────────────────────────────────────
    public List<string> pets        = new List<string>();
    public List<string> unlockedPets = new List<string>();   // alias kept for compatibility
    public List<string> boosters    = new List<string>();

    // ── Timestamps (stored as string for Firestore) ───────────
    public string joinDate    = DateTime.UtcNow.ToString("o");
    public string lastUpdated = DateTime.UtcNow.ToString("o");

    // ── Helpers ───────────────────────────────────────────────

    public int GetStars(int levelIndex)
    {
        string key = $"level_{levelIndex}";
        return levelStars.TryGetValue(key, out int s) ? s : 0;
    }

    public void SetStars(int levelIndex, int stars)
    {
        string key = $"level_{levelIndex}";
        levelStars[key] = Math.Max(GetStars(levelIndex), stars);
    }

    public bool IsLevelUnlocked(int levelIndex)
    {
        if (levelIndex <= 1) return true;
        return GetStars(levelIndex - 1) > 0;
    }

    public void AddPet(string petId)
    {
        if (!pets.Contains(petId))        pets.Add(petId);
        if (!unlockedPets.Contains(petId)) unlockedPets.Add(petId);
    }

    public void AddBooster(string boosterId) => boosters.Add(boosterId);

    public bool UseBooster(string boosterId) => boosters.Remove(boosterId);

    public bool HasBooster(string boosterId) => boosters.Contains(boosterId);

    // ── Firestore serialization ───────────────────────────────

    public Dictionary<string, object> ToFirestoreDict()
    {
        return new Dictionary<string, object>
        {
            { "uid",              uid              },
            { "displayName",      displayName      },
            { "email",            email            },
            { "avatarUrl",        avatarUrl        },
            { "level",            level            },
            { "totalScore",       totalScore       },
            { "gems",             gems             },
            { "coins",            coins            },
            { "levelsCompleted",  levelsCompleted  },
            { "isBanned",         isBanned         },
            { "currentThemeIndex",currentThemeIndex},
            { "highestBossDefeated", highestBossDefeated },
            { "soundEnabled",     soundEnabled     },
            { "musicEnabled",     musicEnabled     },
            { "vibrationEnabled", vibrationEnabled },
            { "levelStars",       levelStars       },
            { "pets",             pets             },
            { "unlockedPets",     unlockedPets     },
            { "boosters",         boosters         },
            { "joinDate",         joinDate         },
            { "lastUpdated",      DateTime.UtcNow.ToString("o") }
        };
    }

    public static PlayerProfile FromFirestoreDict(Dictionary<string, object> data)
    {
        PlayerProfile p = new PlayerProfile();
        p.uid              = Get(data, "uid");
        p.displayName      = Get(data, "displayName");
        // Purane accounts mein sirf "username" field tha — fallback
        if (string.IsNullOrEmpty(p.displayName))
            p.displayName = Get(data, "username");
        p.email            = Get(data, "email");
        p.avatarUrl        = Get(data, "avatarUrl");
        p.level            = GetInt(data,  "level",            1);
        p.totalScore       = GetInt(data,  "totalScore",       0);
        p.gems             = GetInt(data,  "gems",             0);
        p.coins            = GetInt(data,  "coins",            0);
        p.levelsCompleted  = GetInt(data,  "levelsCompleted",  0);
        p.isBanned         = GetBool(data, "isBanned",         false);
        p.currentThemeIndex   = GetInt(data, "currentThemeIndex",   0);
        p.highestBossDefeated = GetInt(data, "highestBossDefeated", 0);
        p.soundEnabled     = GetBool(data, "soundEnabled",     true);
        p.musicEnabled     = GetBool(data, "musicEnabled",     true);
        p.vibrationEnabled = GetBool(data, "vibrationEnabled", true);
        p.pets             = GetList(data, "pets");
        p.unlockedPets     = GetList(data, "unlockedPets");
        p.boosters         = GetList(data, "boosters");
        p.joinDate         = Get(data, "joinDate");
        p.lastUpdated      = Get(data, "lastUpdated");

        if (data.ContainsKey("levelStars") &&
            data["levelStars"] is Dictionary<string, object> raw)
            foreach (var kv in raw)
                if (int.TryParse(kv.Value?.ToString(), out int s))
                    p.levelStars[kv.Key] = s;

        return p;
    }

    // ── Parse helpers ─────────────────────────────────────────

    private static string Get(Dictionary<string, object> d, string key)
        => d.ContainsKey(key) ? d[key]?.ToString() ?? "" : "";

    private static int GetInt(Dictionary<string, object> d, string key, int def = 0)
        => d.ContainsKey(key) && int.TryParse(d[key]?.ToString(), out int v) ? v : def;

    private static bool GetBool(Dictionary<string, object> d, string key, bool def = false)
        => d.ContainsKey(key) && bool.TryParse(d[key]?.ToString(), out bool v) ? v : def;

    private static List<string> GetList(Dictionary<string, object> d, string key)
    {
        var list = new List<string>();
        if (!d.ContainsKey(key)) return list;
        if (d[key] is List<object> raw)
            foreach (var item in raw)
                if (item != null) list.Add(item.ToString());
        return list;
    }
}