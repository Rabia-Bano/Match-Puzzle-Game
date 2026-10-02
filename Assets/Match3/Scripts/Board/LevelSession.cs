using System.Collections.Generic;
using UnityEngine;

namespace Match3
{
    public static class LevelSession
    {
        public static LevelData         CurrentLevel    { get; set; }
        public static int               CurrentLevelId  { get; set; }
        public static List<BoosterType> ActiveBoosters  { get; private set; }
            = new List<BoosterType>();

        public static int CurrentScore { get; set; }

        public static bool NewPetUnlocked    { get; set; }
        public static int  UnlockedPetIndex  { get; set; } = -1;
        public static bool BossArenaUnlocked { get; set; }
        public static int  UnlockedBossId    { get; set; } = -1;

        public static void Begin(LevelData data, int levelId,
                                  List<BoosterType> boosters)
        {
            CurrentLevel   = data;
            CurrentLevelId = levelId;
            CurrentScore   = 0;
            ActiveBoosters = boosters ?? new List<BoosterType>();

            NewPetUnlocked    = false;
            UnlockedPetIndex  = -1;
            BossArenaUnlocked = false;
            UnlockedBossId    = -1;

            Debug.Log($"[LevelSession] Begin — Level {levelId}, " +
                      $"Boosters: {ActiveBoosters.Count}");
        }

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

        public static void CheckUnlocks(int previousLevelsCompleted)
        {
            int id = CurrentLevelId;
            bool isFirstTimePastThisLevel = previousLevelsCompleted < id;

            if (isFirstTimePastThisLevel && id > 0 && id % 5 == 0)
            {
                NewPetUnlocked   = true;
                UnlockedPetIndex = id / 5;
                Debug.Log($"[LevelSession] Pet unlock! Index={UnlockedPetIndex}");
            }

            if (isFirstTimePastThisLevel && id > 0 && id % 6 == 0)
            {
                BossArenaUnlocked = true;
                UnlockedBossId    = id / 6;
                Debug.Log($"[LevelSession] Boss unlock! BossId={UnlockedBossId}");
            }
        }
    }
}
