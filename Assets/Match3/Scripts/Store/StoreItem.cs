// ============================================================
//  StoreItem.cs  —  plain C# data class (NOT a MonoBehaviour)
//
//  One StoreItem = one row in Firestore's `store_catalog/` collection
//  (see StoreManager.cs LoadCatalogFromFirestoreAsync()). Mirrors the
//  same "ToFirestoreDict / FromFirestoreDict" pattern already used by
//  PlayerProfile.cs so it stays consistent with the rest of the codebase.
//
//  PRICING MODEL (an item uses exactly ONE of these — StoreManager
//  looks at iapProductId first, then gemPrice, then coinPrice):
//    • iapProductId set  -> real-money purchase via Unity IAP (PurchaseBooster)
//    • gemPrice > 0      -> soft-currency purchase using gems (BuyWithGems)
//    • coinPrice > 0     -> soft-currency purchase using coins (BuyWithCoins) —
//                           this is what the mockup image shows (Coins: 10/15/30...)
//    • all zero/empty    -> not purchasable this way (e.g. the daily free booster,
//                           which StorePanel grants directly via a claim button)
// ============================================================

using System;
using System.Collections.Generic;

namespace Match3
{
    /// <summary>What granting this item actually does to the player's profile.</summary>
    public enum StoreItemType
    {
        Booster  = 0,   // grants `quantity` copies of a booster (id used = LocalSaveManager booster inventory key)
        GemPack  = 1,   // grants `quantity` gems
        CoinPack = 2,   // grants `quantity` coins
        Bundle   = 3    // reserved for future multi-item bundles (e.g. "starter pack")
    }

    [Serializable]
    public class StoreItem
    {
        // ── Identity ──────────────────────────────────────────────
        public string id          = "";   // Firestore doc id AND booster inventory key (e.g. "hammer")
        public string displayName = "";
        public string description = "";

        // ── Type & payout ─────────────────────────────────────────
        public StoreItemType itemType = StoreItemType.Booster;
        public int quantity = 1;          // how many boosters/gems/coins ONE purchase grants

        // ── Pricing (see class header — only one of these should be set) ──
        public string iapProductId = "";  // Unity IAP SKU, empty if this isn't a real-money item
        public string priceDisplay = "";  // e.g. "$1.99" — shown on the card for IAP items (from IAP metadata ideally)
        public int    gemPrice     = 0;
        public int    coinPrice    = 0;

        // ── Visuals ───────────────────────────────────────────────
        public string spriteKey = "";     // looked up via Resources.Load<Sprite>($"StoreIcons/{spriteKey}")

        // ── Availability ─────────────────────────────────────────
        public bool isActive = true;      // set false in Firestore to hide/disable an item without an app update
        public bool comingSoon = false;   // shown greyed-out with "Coming Soon" instead of a Buy button

        public int sortOrder = 0;

        // ── Firestore serialization (same pattern as PlayerProfile.cs) ──

        public Dictionary<string, object> ToFirestoreDict()
        {
            return new Dictionary<string, object>
            {
                { "id",            id },
                { "displayName",   displayName },
                { "description",   description },
                { "itemType",      itemType.ToString() },
                { "quantity",      quantity },
                { "iapProductId",  iapProductId },
                { "priceDisplay",  priceDisplay },
                { "gemPrice",      gemPrice },
                { "coinPrice",     coinPrice },
                { "spriteKey",     spriteKey },
                { "isActive",      isActive },
                { "comingSoon",    comingSoon },
                { "sortOrder",     sortOrder }
            };
        }

        public static StoreItem FromFirestoreDict(string docId, Dictionary<string, object> data)
        {
            var item = new StoreItem { id = docId };

            item.displayName  = Get(data, "displayName");
            item.description  = Get(data, "description");
            item.iapProductId = Get(data, "iapProductId");
            item.priceDisplay = Get(data, "priceDisplay");
            item.spriteKey    = Get(data, "spriteKey");
            item.quantity     = GetInt(data, "quantity", 1);
            item.gemPrice     = GetInt(data, "gemPrice", 0);
            item.coinPrice    = GetInt(data, "coinPrice", 0);
            item.sortOrder    = GetInt(data, "sortOrder", 0);
            item.isActive     = GetBool(data, "isActive", true);
            item.comingSoon   = GetBool(data, "comingSoon", false);

            string typeStr = Get(data, "itemType");
            item.itemType = Enum.TryParse(typeStr, true, out StoreItemType parsed)
                ? parsed
                : StoreItemType.Booster;

            // If id wasn't stored as a field, fall back to the document id itself.
            if (string.IsNullOrEmpty(item.id)) item.id = docId;

            return item;
        }

        private static string Get(Dictionary<string, object> d, string key)
            => d.ContainsKey(key) ? d[key]?.ToString() ?? "" : "";

        private static int GetInt(Dictionary<string, object> d, string key, int def)
            => d.ContainsKey(key) && int.TryParse(d[key]?.ToString(), out int v) ? v : def;

        private static bool GetBool(Dictionary<string, object> d, string key, bool def)
            => d.ContainsKey(key) && bool.TryParse(d[key]?.ToString(), out bool v) ? v : def;
    }
}