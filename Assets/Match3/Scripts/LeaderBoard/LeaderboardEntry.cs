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
        public string avatarId;
        public long   totalScore;

        public int rank;

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
                avatarId    = raw.TryGetValue("avatarId", out var id) ? id?.ToString() : "",
                totalScore  = raw.TryGetValue("totalScore", out var s) && long.TryParse(s.ToString(), out long v) ? v : 0
            };

            if (string.IsNullOrEmpty(entry.displayName)) entry.displayName = "Player";
            return entry;
        }
    }
}
