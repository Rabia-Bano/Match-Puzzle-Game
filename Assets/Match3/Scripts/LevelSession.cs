// ============================================================
//  LevelSession.cs  —  Static Class (no MonoBehaviour)
//
//  Purpose: Scene-to-scene data bridge
//    MapScene  → sets CurrentLevel, SelectedBoosters
//    GameBoard → reads them in LevelManager
//
//  NO attach needed — pure static, lives in memory.
//  Place in: Assets/Scripts/
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace Match3
{
    public static class LevelSession
    {
        // ─── Data set before entering GameBoardScene ──────────
        public static LevelData         CurrentLevel    { get; set; }
        public static int               CurrentLevelId  { get; set; }
        public static List<BoosterType> ActiveBoosters  { get; private set; }
            = new List<BoosterType>();

        // ─── Score updated during gameplay ────────────────────
        public static int CurrentScore { get; set; }

        // ─── Unlock flags set after win ───────────────────────
        public static bool NewPetUnlocked    { get; set; }
        public static int  UnlockedPetIndex  { get; set; } = -1;
        public static bool BossArenaUnlocked { get; set; }
        public static int  UnlockedBossId    { get; set; } = -1;

        // ─────────────────────────────────────────────────────
        // PUBLIC API
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// Called by LevelLoader just before loading GameBoardScene.
        /// Stores everything the board needs.
        /// </summary>
        public static void Begin(LevelData data, int levelId,
                                  List<BoosterType> boosters)
        {
            CurrentLevel   = data;
            CurrentLevelId = levelId;
            CurrentScore   = 0;
            ActiveBoosters = boosters ?? new List<BoosterType>();

            // Clear previous unlock flags
            NewPetUnlocked    = false;
            UnlockedPetIndex  = -1;
            BossArenaUnlocked = false;
            UnlockedBossId    = -1;

            Debug.Log($"[LevelSession] Begin — Level {levelId}, " +
                      $"Boosters: {ActiveBoosters.Count}");
        }

        /// <summary>Reset after returning to map.</summary>
        public static void Clear()
        {
            CurrentLevel      = null;
            CurrentLevelId    = 0;
            CurrentScore      = 0;
            ActiveBoosters    = new List<BoosterType>();
            NewPetUnlocked    = false;
            UnlockedPetIndex  = -1;
            BossArenaUnlocked = false;
            UnlockedBossId    = -1;
        }

        /// <summary>
        /// Called after win — checks if level triggers pet or boss unlock.
        /// Pet every 3 levels (3,6,9...), Boss every 5 levels (5,10,15...).
        /// </summary>
        public static void CheckUnlocks()
        {
            int id = CurrentLevelId;

            if (id > 0 && id % 3 == 0)
            {
                NewPetUnlocked   = true;
                UnlockedPetIndex = (id / 3) - 1;
                Debug.Log($"[LevelSession] Pet unlock! Index={UnlockedPetIndex}");
            }

            if (id > 0 && id % 5 == 0)
            {
                BossArenaUnlocked = true;
                UnlockedBossId    = id / 5;
                Debug.Log($"[LevelSession] Boss unlock! BossId={UnlockedBossId}");
            }
        }
    }
}
