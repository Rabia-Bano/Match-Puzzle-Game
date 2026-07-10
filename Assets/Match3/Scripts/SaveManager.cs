// ============================================================
//  SaveManager.cs  —  MonoBehaviour (Singleton)
//
//  Saves/loads player progress using PlayerPrefs.
//  Stores: level stars, high scores, total score, coins.
//
//  Attach to: SaveManager (empty GameObject, DontDestroyOnLoad)
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace Match3
{
    [System.Serializable]
    public class PlayerProfile
    {
        public int   totalScore;
        public int   coins;
        public int   highestLevelReached;

        // Per-level data: key = "level_N_stars", "level_N_score"
        public Dictionary<string, int> levelData = new();

        public int  GetLevelStars(int levelIndex) =>
            levelData.TryGetValue($"level_{levelIndex}_stars", out int v) ? v : 0;

        public int  GetLevelScore(int levelIndex) =>
            levelData.TryGetValue($"level_{levelIndex}_score", out int v) ? v : 0;

        public void SetLevelStars(int levelIndex, int stars)
        {
            string key = $"level_{levelIndex}_stars";
            // Never overwrite with a lower star count
            levelData.TryGetValue(key, out int existing);
            levelData[key] = Mathf.Max(existing, stars);
        }

        public void SetLevelScore(int levelIndex, int score)
        {
            string key = $"level_{levelIndex}_score";
            levelData.TryGetValue(key, out int existing);
            levelData[key] = Mathf.Max(existing, score);
        }
    }

    public class SaveManager : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────

        public static SaveManager Instance { get; private set; }

        private const string PROFILE_KEY = "PlayerProfile_v1";

        // ── Public state ──────────────────────────────────────

        public PlayerProfile Profile { get; private set; }

        // ── Lifecycle ─────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            { Destroy(gameObject); return; }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadProfile();
        }

        // ── Save / Load ───────────────────────────────────────

        public void SaveProfile()
        {
            string json = JsonUtility.ToJson(Profile);
            PlayerPrefs.SetString(PROFILE_KEY, json);
            PlayerPrefs.Save();
            Debug.Log("[SaveManager] Profile saved.");
        }

        private void LoadProfile()
        {
            if (PlayerPrefs.HasKey(PROFILE_KEY))
            {
                string json = PlayerPrefs.GetString(PROFILE_KEY);
                Profile = JsonUtility.FromJson<PlayerProfile>(json) ?? new PlayerProfile();

                // Dictionary is not JSON-serialized by Unity — rebuild from flat keys
                RebuildDictionaryFromPrefs();
            }
            else
            {
                Profile = new PlayerProfile();
            }
            Debug.Log($"[SaveManager] Profile loaded. TotalScore={Profile.totalScore}");
        }

        /// <summary>
        /// Saves level result after win.
        /// Updates stars, high score, total score, coins.
        /// </summary>
        public void SaveLevelResult(int levelIndex, int stars, int score, int coinsEarned)
        {
            Profile.SetLevelStars(levelIndex, stars);
            Profile.SetLevelScore(levelIndex, score);
            Profile.totalScore += score;
            Profile.coins      += coinsEarned;

            if (levelIndex + 1 > Profile.highestLevelReached)
                Profile.highestLevelReached = levelIndex + 1;

            // Persist each level entry to PlayerPrefs too (flat key approach)
            PlayerPrefs.SetInt($"level_{levelIndex}_stars", Profile.GetLevelStars(levelIndex));
            PlayerPrefs.SetInt($"level_{levelIndex}_score", Profile.GetLevelScore(levelIndex));
            PlayerPrefs.SetInt("totalScore", Profile.totalScore);
            PlayerPrefs.SetInt("coins",      Profile.coins);
            PlayerPrefs.SetInt("highestLevel", Profile.highestLevelReached);
            PlayerPrefs.Save();

            Debug.Log($"[SaveManager] Level {levelIndex} saved — Stars:{stars} Score:{score}");
        }

        /// <summary>Rebuild in-memory dictionary from flat PlayerPrefs keys.</summary>
        private void RebuildDictionaryFromPrefs()
        {
            Profile.levelData   = new Dictionary<string, int>();
            Profile.totalScore  = PlayerPrefs.GetInt("totalScore",   0);
            Profile.coins       = PlayerPrefs.GetInt("coins",        0);
            Profile.highestLevelReached = PlayerPrefs.GetInt("highestLevel", 0);

            // Scan all levels up to 200
            for (int i = 0; i < 200; i++)
            {
                string sk = $"level_{i}_stars";
                string sc = $"level_{i}_score";
                if (PlayerPrefs.HasKey(sk))
                    Profile.levelData[sk] = PlayerPrefs.GetInt(sk);
                if (PlayerPrefs.HasKey(sc))
                    Profile.levelData[sc] = PlayerPrefs.GetInt(sc);
            }
        }

        /// <summary>Wipes all save data (for testing).</summary>
        public void ClearAllData()
        {
            PlayerPrefs.DeleteAll();
            Profile = new PlayerProfile();
            Debug.Log("[SaveManager] All data cleared.");
        }
    }
}
