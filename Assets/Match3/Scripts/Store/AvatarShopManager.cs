using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Firebase;

namespace Match3
{
    public static class AvatarShopManager
    {
        public enum PurchaseResult { Success, AlreadyOwned, NotEnoughCoins, NoProfile, InvalidItem }

        public static event Action OnAvatarsChanged;

        private static PlayerProfile Profile =>
            ProfileManager.Instance?.Profile ?? LocalSaveManager.GetOrLoadProfile();

        public static List<AvatarPresetData> GetAll()
        {
            return Resources.LoadAll<AvatarPresetData>("Avatars")
                .Where(a => a != null && a.sprite != null && !string.IsNullOrEmpty(a.id))
                .OrderBy(a => a.sortOrder).ThenBy(a => a.price).ThenBy(a => a.id)
                .ToList();
        }

        public static bool IsOwned(AvatarPresetData preset)
        {
            if (preset == null) return false;
            if (preset.IsFree) return true;
            PlayerProfile p = Profile;
            if (p == null) return false;
            if (p.avatarId == preset.id) return true;
            return p.ownedAvatars != null && p.ownedAvatars.Contains(preset.id);
        }

        public static bool IsEquipped(AvatarPresetData preset) =>
            preset != null && Profile != null && Profile.avatarId == preset.id;

        public static int CurrentCoins => Profile?.coins ?? 0;

        public static PurchaseResult TryPurchase(AvatarPresetData preset)
        {
            if (preset == null || string.IsNullOrEmpty(preset.id)) return PurchaseResult.InvalidItem;

            PlayerProfile p = Profile;
            if (p == null) return PurchaseResult.NoProfile;
            if (IsOwned(preset)) return PurchaseResult.AlreadyOwned;
            if (p.coins < preset.price) return PurchaseResult.NotEnoughCoins;

            p.coins -= preset.price;
            p.ownedAvatars ??= new List<string>();
            if (!p.ownedAvatars.Contains(preset.id)) p.ownedAvatars.Add(preset.id);

            LocalSaveManager.SaveProfile(p);

            GameEvents.OnCoinsChanged?.Invoke(p.coins);

            ProfileManager.Instance?.SaveProfile();
            _ = CloudSyncManager.Instance?.SyncAfterLevelAsync();

            AudioManager.Instance?.PlaySFX("purchase");
            Debug.Log($"[AvatarShop] Bought '{preset.id}' for {preset.price} coins. Coins left: {p.coins}");
            OnAvatarsChanged?.Invoke();
            return PurchaseResult.Success;
        }

        public static bool Equip(AvatarPresetData preset)
        {
            if (preset == null || !IsOwned(preset)) return false;
            ProfileManager.Instance?.SetPresetAvatar(preset.id);
            OnAvatarsChanged?.Invoke();
            return true;
        }

        public static string MessageFor(PurchaseResult r, AvatarPresetData preset) => r switch
        {
            PurchaseResult.Success        => $"{preset?.NameOrId} unlocked!",
            PurchaseResult.AlreadyOwned   => "You already own this avatar.",
            PurchaseResult.NotEnoughCoins => $"Not enough coins — you need {preset?.price} coins.",
            PurchaseResult.NoProfile      => "Profile not loaded yet. Try again.",
            _                             => "This avatar is not available."
        };
    }
}
