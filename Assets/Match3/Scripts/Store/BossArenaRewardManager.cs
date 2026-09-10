// ============================================================
//  BossArenaRewardManager.cs  —  MonoBehaviour
//  Attach to: an empty "BossArenaRewardManager" GameObject in
//  BossArenaScene (the boss SELECTION list) AND/OR BossGameBoardScene
//  (the actual fight) — safe to have one in each, they're independent
//  and both just read LocalSaveManager's booster inventory.
//
//  PURPOSE (per request: "checks booster availability"):
//  This does NOT grant boss-fight rewards — BossResultManager.cs already
//  does that (coins + a weighted-random booster, via ProfileManager.AddBooster
//  using the BoosterType enum). This class is the READ side: it tells the UI
//  which STORE-bought boosters (BoosterManager.cs ids — hammer/row_clear/
//  color_bomb/extra_moves/shuffle) the player currently owns and can use
//  DURING a boss fight, and exposes a couple of small conveniences
//  (badge on the Boss Arena nav icon, gating BossArenaListManager entries).
//
//  NOTE ON THE TWO BOOSTER ID SETS — see BoosterManager.cs header for the
//  full explanation. BossResultManager's own reward drop still uses the
//  OLDER BoosterType enum (Hammer/Crystal/Beam/Refresh) and writes via
//  ProfileManager.AddBooster — that path is UNTOUCHED here. If you'd like
//  boss-fight rewards to grant the SAME store boosters this class checks,
//  that's a follow-up change to BossResultManager.cs / BossData.cs — happy
//  to do that separately so it doesn't get tangled with this feature.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Match3
{
    public class BossArenaRewardManager : MonoBehaviour
    {
        public static BossArenaRewardManager Instance { get; private set; }

        /// <summary>Fires whenever the owned-booster inventory changes
        /// (purchase, daily claim, or a booster used mid-fight).</summary>
        public event Action OnAvailabilityChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()  => LocalSaveManager.OnProfileChanged += HandleProfileChanged;
        private void OnDisable() => LocalSaveManager.OnProfileChanged -= HandleProfileChanged;

        private void HandleProfileChanged(PlayerProfile profile) => OnAvailabilityChanged?.Invoke();

        /// <summary>True if the player owns at least one of this store booster.
        /// Use boosterId constants from BoosterManager.cs (BoosterManager.Hammer, etc.).</summary>
        public bool IsBoosterAvailable(string boosterId)
        {
            var inventory = LocalSaveManager.LoadBoosterInventory();
            return inventory.TryGetValue(boosterId, out int count) && count > 0;
        }

        public int GetOwnedCount(string boosterId)
        {
            var inventory = LocalSaveManager.LoadBoosterInventory();
            return inventory.TryGetValue(boosterId, out int count) ? count : 0;
        }

        /// <summary>Full snapshot of every owned store booster — handy for
        /// building the Boss Arena's booster bar in one call instead of one
        /// LoadBoosterInventory() per slot.</summary>
        public Dictionary<string, int> GetAvailableBoosters()
        {
            var inventory = LocalSaveManager.LoadBoosterInventory();
            var result = new Dictionary<string, int>();
            foreach (var kv in inventory)
                if (kv.Value > 0) result[kv.Key] = kv.Value;
            return result;
        }

        /// <summary>True if the player owns ANY usable booster right now —
        /// wire this to a small badge/dot on the Boss Arena bottom-nav icon
        /// so players notice they have boosters ready before they even enter.</summary>
        public bool HasAnyBoosterAvailable()
        {
            var inventory = LocalSaveManager.LoadBoosterInventory();
            foreach (var kv in inventory)
                if (kv.Value > 0) return true;
            return false;
        }
    }
}