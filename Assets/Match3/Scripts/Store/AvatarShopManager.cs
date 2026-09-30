// ============================================================
//  AvatarShopManager.cs  —  static  (NEW)
//
//  Coins se avatar kharidne ka poora logic — bilkul Store ke boosters
//  jaisa: price check → coins cut → avatar player ki collection mein
//  → local save + cloud sync.
//
//  Ownership rules:
//    • price == 0            → FREE, sab ke paas (kabhi save nahi hota)
//    • profile.ownedAvatars  → kharide hue avatars (Firestore mein sync)
//    • profile.avatarId      → jo avatar player ne update se PEHLE lagaya
//                              hua tha, wo hamesha owned rehta hai
//                              (koi purana player apna avatar na khoye)
//
//  Avatar assets: Assets/Resources/Avatars/<id>.asset  (AvatarPresetData)
// ============================================================

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

        /// <summary>Fires after a successful purchase OR equip — UI refreshes on this.</summary>
        public static event Action OnAvatarsChanged;

        private static PlayerProfile Profile =>
            ProfileManager.Instance?.Profile ?? LocalSaveManager.GetOrLoadProfile();

        /// <summary>All avatar presets, sorted: sortOrder, then price, then id.</summary>
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
            if (p.avatarId == preset.id) return true;   // grandfathered current avatar
            return p.ownedAvatars != null && p.ownedAvatars.Contains(preset.id);
        }

        public static bool IsEquipped(AvatarPresetData preset) =>
            preset != null && Profile != null && Profile.avatarId == preset.id;

        public static int CurrentCoins => Profile?.coins ?? 0;

        /// <summary>Buys the avatar with coins. Does NOT auto-equip (call Equip after, if wanted).</summary>
        public static PurchaseResult TryPurchase(AvatarPresetData preset)
        {
            if (preset == null || string.IsNullOrEmpty(preset.id)) return PurchaseResult.InvalidItem;

            PlayerProfile p = Profile;
            if (p == null) return PurchaseResult.NoProfile;
            if (IsOwned(preset)) return PurchaseResult.AlreadyOwned;
            if (p.coins < preset.price) return PurchaseResult.NotEnoughCoins;

            // 1. pay
            p.coins -= preset.price;
            // 2. own
            p.ownedAvatars ??= new List<string>();
            if (!p.ownedAvatars.Contains(preset.id)) p.ownedAvatars.Add(preset.id);

            // 3. save locally (fires LocalSaveManager.OnProfileChanged → coin labels refresh)
            LocalSaveManager.SaveProfile(p);

            // 4. keep GameManager's coin counter in sync (ProfileManager copies it back on save)
            GameEvents.OnCoinsChanged?.Invoke(p.coins);

            // 5. cloud (debounced merge-write; offline → CloudSync pushes later)
            ProfileManager.Instance?.SaveProfile();
            _ = CloudSyncManager.Instance?.SyncAfterLevelAsync();

            AudioManager.Instance?.PlaySFX("purchase");
            Debug.Log($"[AvatarShop] Bought '{preset.id}' for {preset.price} coins. Coins left: {p.coins}");
            OnAvatarsChanged?.Invoke();
            return PurchaseResult.Success;
        }

        /// <summary>Equips an OWNED avatar (updates profile, leaderboard, top-bar icon).</summary>
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
