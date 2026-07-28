// ============================================================
//  ConflictResolver.cs  —  static utility (NO GameObject needed)
//
//  Purpose:
//    Jab local save aur cloud (Firestore) profile dono hi
//    "last sync" ke baad change ho chuke hon (e.g. player ne
//    offline khela aur dusre device se bhi login kiya), sirf
//    "jo newer hai wo poora le lo" karna galat hai — kyunki
//    dono side kuch cheezein khoyi ja sakti hain (coins, boosters,
//    stars waghera). Ye class field-by-field SAFE merge karti hai:
//
//      - gems / coins / totalScore  -> MAX(local, cloud)   [kabhi bhi player ka nuksan nahi]
//      - boosters                   -> per-id MAX count    [duplication na ho, na hi loss ho]
//      - levelStars                 -> per-level MAX stars
//      - pets / unlockedPets        -> UNION (unlock permanent hai, kabhi remove nahi hota)
//      - levelsCompleted / bosses   -> MAX
//      - identity/settings fields   -> jo profile NEWER (lastUpdated) hai uska value
//
//  Attach to: NOTHING. Static class — call directly:
//      PlayerProfile merged = ConflictResolver.Resolve(local, cloud);
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;

public static class ConflictResolver
{
    /// <summary>
    /// Merges local aur cloud PlayerProfile ko safely combine karta hai.
    /// Null-safe: agar ek side null ho to doosri side seedha return ho jati hai.
    /// </summary>
    public static PlayerProfile Resolve(PlayerProfile local, PlayerProfile cloud)
    {
        if (local == null && cloud == null) return new PlayerProfile();
        if (local == null) return cloud;
        if (cloud == null) return local;

        DateTime localTime = ParseTime(local.lastUpdated);
        DateTime cloudTime = ParseTime(cloud.lastUpdated);

        // "newer" sirf identity/settings jaise non-additive fields ke liye base ban raha hai.
        // Additive/progress fields (coins, gems, stars, boosters, pets) hamesha MAX/UNION lete hain
        // chahe kisi bhi taraf se newer ho — is se "restore" ki wajah se progress kabhi nahi girta.
        PlayerProfile newer = cloudTime >= localTime ? cloud : local;
        PlayerProfile older = ReferenceEquals(newer, cloud) ? local : cloud;

        PlayerProfile merged = new PlayerProfile
        {
            saveVersion = LocalSaveManager.CURRENT_SAVE_VERSION,

            // ── Identity / settings: newer wins, older ka fallback agar newer mein empty ho ──
            uid         = string.IsNullOrEmpty(newer.uid)         ? older.uid         : newer.uid,
            displayName = string.IsNullOrEmpty(newer.displayName) ? older.displayName : newer.displayName,
            email       = string.IsNullOrEmpty(newer.email)       ? older.email       : newer.email,
            avatarUrl   = string.IsNullOrEmpty(newer.avatarUrl)   ? older.avatarUrl   : newer.avatarUrl,
            joinDate    = string.IsNullOrEmpty(newer.joinDate)    ? older.joinDate    : newer.joinDate,

            soundEnabled     = newer.soundEnabled,
            musicEnabled     = newer.musicEnabled,
            vibrationEnabled = newer.vibrationEnabled,

            // Ban flag: agar kisi bhi copy (local ya cloud) mein banned true hai to banned rakho.
            // Admin panel ka ban kabhi bhi ek "stale" local save se accidentally overwrite nahi hona chahiye.
            isBanned = local.isBanned || cloud.isBanned,
        };

        // ── Progress / currency: kabhi bhi player ko punish mat karo — MAX lo ──
        merged.gems             = Math.Max(local.gems, cloud.gems);
        merged.coins            = Math.Max(local.coins, cloud.coins);
        merged.totalScore       = Math.Max(local.totalScore, cloud.totalScore);
        merged.levelsCompleted  = Math.Max(local.levelsCompleted, cloud.levelsCompleted);
        merged.level            = merged.levelsCompleted + 1;
        merged.currentThemeIndex    = Math.Max(local.currentThemeIndex, cloud.currentThemeIndex);
        merged.highestBossDefeated  = Math.Max(local.highestBossDefeated, cloud.highestBossDefeated);

        // ── Stars: per-level MAX ──
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

        // ── Pets: unlock permanent hai -> UNION, kabhi remove nahi ──
        merged.pets         = UnionDistinct(local.pets, cloud.pets);
        merged.unlockedPets = UnionDistinct(local.unlockedPets, cloud.unlockedPets);

        // ── Boosters: quantity-based list -> per-id MAX count ──
        // (List<string> mein har booster id utni dafa repeat hoti hai jitni uski quantity hai,
        //  jaise ["hammer","hammer","shuffle"] = 2 hammer + 1 shuffle)
        merged.boosters = MergeBoosterCounts(local.boosters, cloud.boosters);

        merged.lastUpdated = DateTime.UtcNow.ToString("o");
        return merged;
    }

    // ────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────

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
