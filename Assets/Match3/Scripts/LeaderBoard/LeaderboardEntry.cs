// ============================================================
//  LeaderboardEntry.cs  —  Plain data model
//  Attach to: NOTHING — this is not a MonoBehaviour, just a POCO.
//  Put it anywhere under Scripts/Leaderboard/ (new folder).
// ============================================================

using System.Collections.Generic;
using Firebase.Database;

namespace Match3
{
    [System.Serializable]
    public class LeaderboardEntry
    {
        public string uid;
        public string displayName;
        public string avatarUrl;
        public long   totalScore;

        // Not stored in RTDB — computed client-side after sorting.
        public int rank;

        /// <summary>
        /// Builds a LeaderboardEntry from one child DataSnapshot under
        /// /leaderboard/{uid}. Returns null if the snapshot is
        /// malformed so the caller can safely skip it.
        /// </summary>
        public static LeaderboardEntry FromSnapshot(DataSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.Exists) return null;

            var raw = snapshot.Value as Dictionary<string, object>;
            if (raw == null) return null;

            var entry = new LeaderboardEntry
            {
                uid         = snapshot.Key,
                displayName = raw.TryGetValue("displayName", out var n) ? n?.ToString() : "Player",
                avatarUrl   = raw.TryGetValue("avatarUrl", out var a) ? a?.ToString() : "",
                totalScore  = raw.TryGetValue("totalScore", out var s) && long.TryParse(s.ToString(), out long v) ? v : 0
            };

            if (string.IsNullOrEmpty(entry.displayName)) entry.displayName = "Player";
            return entry;
        }
    }
}