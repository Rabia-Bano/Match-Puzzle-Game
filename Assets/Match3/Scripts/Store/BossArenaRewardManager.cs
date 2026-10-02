using System;
using System.Collections.Generic;
using UnityEngine;

namespace Match3
{
    public class BossArenaRewardManager : MonoBehaviour
    {
        public static BossArenaRewardManager Instance { get; private set; }

        public event Action OnAvailabilityChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()  => LocalSaveManager.OnProfileChanged += HandleProfileChanged;
        private void OnDisable() => LocalSaveManager.OnProfileChanged -= HandleProfileChanged;

        private void HandleProfileChanged(PlayerProfile profile) => OnAvailabilityChanged?.Invoke();

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

        public Dictionary<string, int> GetAvailableBoosters()
        {
            var inventory = LocalSaveManager.LoadBoosterInventory();
            var result = new Dictionary<string, int>();
            foreach (var kv in inventory)
                if (kv.Value > 0) result[kv.Key] = kv.Value;
            return result;
        }

        public bool HasAnyBoosterAvailable()
        {
            var inventory = LocalSaveManager.LoadBoosterInventory();
            foreach (var kv in inventory)
                if (kv.Value > 0) return true;
            return false;
        }
    }
}
