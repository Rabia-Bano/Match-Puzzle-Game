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
        /// (after Level 5, 10, 15 ...).
        /// PetData.unlockAfterLevel should be set to match: starter pet = 0,
        /// second pet = 5, third pet = 10, etc.
        ///
        /// NOTE: Boss Arena now unlocks on its OWN cadence — every 6th level
        /// (after Level 6, 12, 18 ...) — intentionally decoupled from the pet
        /// cadence above (they used to share the same "id % 5" check). Only
        /// the boss block below changed; the pet block above is untouched, so
        /// regular level-play / pet unlocking behaves exactly as before.
        ///
        /// FIX (Rabia's report — "pet unlock popup shows every time, even
        /// though the pet was already unlocked"): this used to flag
        /// NewPetUnlocked/BossArenaUnlocked purely from "id % 5/6 == 0" with
        /// no check for whether the unlock had already happened before — so
        /// REPLAYING Level 5 (or 10, 15, 6, 12...) showed the "new pet" /
        /// "new boss" popup every single time. `previousLevelsCompleted` is
        /// the player's saved progress from BEFORE this completion (the
        /// caller passes ProfileManager.Instance.Profile.levelsCompleted —
        /// read before ProfileManager.OnLevelCompleted() bumps it) — an
        /// unlock only fires when this is genuinely the FIRST time the
        /// player has ever reached/passed that milestone level.
        /// </summary>
        public static void CheckUnlocks(int previousLevelsCompleted)
        {
            int id = CurrentLevelId;
            bool isFirstTimePastThisLevel = previousLevelsCompleted < id;

            if (isFirstTimePastThisLevel && id > 0 && id % 5 == 0)
            {
                NewPetUnlocked   = true;
                UnlockedPetIndex = id / 5;   // 1 = pet unlocked after Level 5, 2 = after Level 10, ...
                Debug.Log($"[LevelSession] Pet unlock! Index={UnlockedPetIndex}");
            }

            if (isFirstTimePastThisLevel && id > 0 && id % 6 == 0)
            {
                BossArenaUnlocked = true;
                UnlockedBossId    = id / 6;   // 1 = boss unlocked after Level 6, 2 = after Level 12, ...
                Debug.Log($"[LevelSession] Boss unlock! BossId={UnlockedBossId}");
            }
        }
    }
}