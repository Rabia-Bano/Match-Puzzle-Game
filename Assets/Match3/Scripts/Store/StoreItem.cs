using System;
using System.Collections.Generic;

namespace Match3
{
    public enum StoreItemType
    {
        Booster  = 0,
        GemPack  = 1,
        CoinPack = 2,
        Bundle   = 3
    }

    [Serializable]
    public class StoreItem
    {
        public string id          = "";
        public string displayName = "";
        public string description = "";

        public StoreItemType itemType = StoreItemType.Booster;
        public int quantity = 1;

        public string iapProductId = "";
        public string priceDisplay = "";
        public int    gemPrice     = 0;
        public int    coinPrice    = 0;

        public string spriteKey = "";

        public bool isActive = true;
        public bool comingSoon = false;

        public int sortOrder = 0;

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
