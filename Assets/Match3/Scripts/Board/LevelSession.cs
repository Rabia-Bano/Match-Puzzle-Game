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
        ///
        /// UPDATED cadence: the starter pet (index 0) is unlocked from Level 1,
        /// before the player has completed anything, so it is NOT granted here.
        /// Every pet after that unlocks after every 5th level completed
        /// (after Level 5, 10, 15 ...) — same cadence as Boss Arena, so both
        /// checks below share the same "id % 5 == 0" condition.
        /// PetData.unlockAfterLevel should be set to match: starter pet = 0,
        /// second pet = 5, third pet = 10, etc.
        /// </summary>
        public static void CheckUnlocks()
        {
            int id = CurrentLevelId;

            if (id > 0 && id % 5 == 0)
            {
                NewPetUnlocked   = true;
                UnlockedPetIndex = id / 5;   // 1 = pet unlocked after Level 5, 2 = after Level 10, ...
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
