using System;
using System.Collections.Generic;
using System.Linq;

public static class ConflictResolver
{
    public static PlayerProfile Resolve(PlayerProfile local, PlayerProfile cloud)
    {
        if (local == null && cloud == null) return new PlayerProfile();
        if (local == null) return cloud;
        if (cloud == null) return local;

        DateTime localTime = ParseTime(local.lastUpdated);
        DateTime cloudTime = ParseTime(cloud.lastUpdated);

        PlayerProfile newer = cloudTime >= localTime ? cloud : local;
        PlayerProfile older = ReferenceEquals(newer, cloud) ? local : cloud;

        PlayerProfile merged = new PlayerProfile
        {
            saveVersion = LocalSaveManager.CURRENT_SAVE_VERSION,

            uid         = string.IsNullOrEmpty(newer.uid)         ? older.uid         : newer.uid,
            displayName = string.IsNullOrEmpty(newer.displayName) ? older.displayName : newer.displayName,
            email       = string.IsNullOrEmpty(newer.email)       ? older.email       : newer.email,
            avatarUrl   = string.IsNullOrEmpty(newer.avatarUrl)   ? older.avatarUrl   : newer.avatarUrl,
            avatarId    = string.IsNullOrEmpty(newer.avatarId)    ? older.avatarId    : newer.avatarId,
            joinDate    = string.IsNullOrEmpty(newer.joinDate)    ? older.joinDate    : newer.joinDate,

            soundEnabled     = newer.soundEnabled,
            musicEnabled     = newer.musicEnabled,
            vibrationEnabled = newer.vibrationEnabled,

            isBanned = local.isBanned || cloud.isBanned,
        };

        merged.gems             = Math.Max(local.gems, cloud.gems);
        merged.coins            = Math.Max(local.coins, cloud.coins);
        merged.totalScore       = Math.Max(local.totalScore, cloud.totalScore);
        merged.levelsCompleted  = Math.Max(local.levelsCompleted, cloud.levelsCompleted);
        merged.level            = merged.levelsCompleted + 1;
        merged.currentThemeIndex    = Math.Max(local.currentThemeIndex, cloud.currentThemeIndex);
        merged.highestBossDefeated  = Math.Max(local.highestBossDefeated, cloud.highestBossDefeated);

        merged.levelStars = new Dictionary<string, int>(local.levelStars ?? new Dictionary<string, int>());
        if (cloud.levelStars != null)
        {
            foreach (var kv in cloud.levelStars)
            {
                if (merged.levelStars.TryGetValue(kv.Key, out int existing))
                    merged.levelStars[kv.Key] = Math.Max(existing, kv.Value);
                else
                    merged.levelStars[kv.Key] = kv.Value;
            }
        }

        merged.pets         = UnionDistinct(local.pets, cloud.pets);
        merged.unlockedPets = UnionDistinct(local.unlockedPets, cloud.unlockedPets);

        merged.boosters = MergeBoosterCounts(local.boosters, cloud.boosters);

        merged.ownedAvatars = UnionDistinct(local.ownedAvatars, cloud.ownedAvatars);

        merged.lastUpdated = DateTime.UtcNow.ToString("o");
        return merged;
    }

    private static List<string> UnionDistinct(List<string> a, List<string> b)
    {
        var seta = a ?? new List<string>();
        var setb = b ?? new List<string>();
        return seta.Union(setb).Distinct().ToList();
    }

    private static List<string> MergeBoosterCounts(List<string> a, List<string> b)
    {
        Dictionary<string, int> countsA = CountById(a);
        Dictionary<string, int> countsB = CountById(b);

        var allIds = new HashSet<string>(countsA.Keys);
        allIds.UnionWith(countsB.Keys);

        var result = new List<string>();
        foreach (string id in allIds)
        {
            int max = Math.Max(
                countsA.TryGetValue(id, out int ca) ? ca : 0,
                countsB.TryGetValue(id, out int cb) ? cb : 0);

            for (int i = 0; i < max; i++)
                result.Add(id);
        }
        return result;
    }

    private static Dictionary<string, int> CountById(List<string> list)
    {
        var d = new Dictionary<string, int>();
        if (list == null) return d;
        foreach (string id in list)
            d[id] = d.TryGetValue(id, out int c) ? c + 1 : 1;
        return d;
    }

    private static DateTime ParseTime(string iso)
    {
        return DateTime.TryParse(
            iso, null,
            System.Globalization.DateTimeStyles.RoundtripKind,
            out DateTime t) ? t : DateTime.MinValue;
    }
}
