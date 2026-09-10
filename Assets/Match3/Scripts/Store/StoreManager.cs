// ============================================================
//  StoreManager.cs  —  Singleton MonoBehaviour (DontDestroyOnLoad)
//  Attach to: "FirebaseManagers" GameObject in PreloaderScene — the
//             SAME object as FirebaseInitializer, AuthManager, ProfileManager,
//             CloudSyncManager, LeaderboardManager, NetworkChecker.
//  Call: Initialize() from FirebaseInitializer.OnFirebaseReady (Inspector
//        UnityEvent), same as the other managers.
//  Access: Match3.StoreManager.Instance
//
//  CURRENT BOOSTER SET (coins only — no gems, no "coming soon" items):
//    hammer, row_bomb, column_bomb, shuffle_2tiles, shuffle_board
//  All five are active/purchasable from day one. See BoosterManager.cs
//  (Board folder) for the gameplay implementation of each.
//
//  Responsibilities:
//    1) Loads the store catalog from Firestore `store_catalog/` so prices
//       and items can change without an app update (falls back to a
//       built-in default catalog if offline or Firestore is empty/blocked).
//    2) Initializes Unity IAP (IStoreListener) — kept in place for future
//       real-money items even though the current catalog is coins-only.
//    3) BuyWithCoins() is the active purchase path right now. BuyWithGems()
//       and PurchaseBooster() (IAP) stay implemented and ready to use the
//       moment you add a gem-priced or IAP item to the catalog — they're
//       simply unused while every catalog item is coin-priced.
//    4) Grants boosters through LocalSaveManager's existing quantity-based
//       booster inventory (SaveBoosterInventory/LoadBoosterInventory) —
//       the SAME storage BoosterManager.cs (gameplay) reads from.
// ============================================================

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Purchasing;

using Firebase.Firestore;
using Game.Firebase;

namespace Match3
{
    public class StoreManager : MonoBehaviour, IStoreListener
    {
        public static StoreManager Instance { get; private set; }

        private const string CATALOG_COLLECTION = "store_catalog";

        [Tooltip("Set true for verbose debug logs while wiring this up.")]
        [SerializeField] private bool logVerbose = true;

        // ── Events (StorePanel / UI subscribe to these) ─────────────
        public event Action<List<StoreItem>> OnCatalogLoaded;
        public event Action<string>          OnCatalogLoadFailed;

        /// <summary>Fires after ANY successful grant (IAP, gems, or coins path).
        /// Passes the item and the player's new owned-count for boosters.</summary>
        public event Action<StoreItem, int> OnItemGranted;

        /// <summary>Fires on any failed purchase attempt (IAP failure, insufficient
        /// coins, etc.) with a short human-readable reason for a toast/popup.</summary>
        public event Action<string> OnPurchaseFailedFeedback;

        public bool IsIapInitialized { get; private set; }
        public IReadOnlyList<StoreItem> Catalog => _catalog;

        private FirebaseFirestore _db;
        private bool _initialized;

        private IStoreController   _storeController;
        private IExtensionProvider _extensionProvider;

        private readonly List<StoreItem> _catalog = new();
        private readonly HashSet<string> _purchaseInFlight = new(); // guards double-taps

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>Call this from FirebaseInitializer.OnFirebaseReady (Inspector).</summary>
        public void Initialize()
        {
            if (!FirebaseInitializer.IsReady)
            {
                Debug.LogError("[StoreManager] Firebase not ready — cannot initialize.");
                return;
            }
            _db = FirebaseFirestore.DefaultInstance;
            _initialized = true;
            if (logVerbose) Debug.Log("[StoreManager] Ready.");
        }

        // ============================================================
        //  CATALOG — Firestore fetch with offline-safe fallback
        // ============================================================

        /// <summary>Fetches store_catalog/ from Firestore. On failure or empty
        /// result, falls back to a built-in default catalog so the Store screen
        /// is never blank. Call from StorePanel.OnEnable() —
        /// `_ = StoreManager.Instance.LoadCatalogFromFirestoreAsync();`</summary>
        public async Task LoadCatalogFromFirestoreAsync()
        {
            _catalog.Clear();

            bool online = NetworkChecker.Instance == null || await NetworkChecker.Instance.CheckConnectivityAsync();
            if (!_initialized || _db == null || !online)
            {
                if (logVerbose) Debug.Log("[StoreManager] Offline/not initialized — using default catalog.");
                _catalog.AddRange(BuildDefaultCatalog());
                RegisterIapProductsFromCatalog();
                OnCatalogLoaded?.Invoke(new List<StoreItem>(_catalog));
                return;
            }

            try
            {
                QuerySnapshot snap = await _db.Collection(CATALOG_COLLECTION).GetSnapshotAsync();

                foreach (DocumentSnapshot doc in snap.Documents)
                {
                    StoreItem item = StoreItem.FromFirestoreDict(doc.Id, doc.ToDictionary());
                    if (item.isActive) _catalog.Add(item);
                }

                if (_catalog.Count == 0)
                {
                    if (logVerbose) Debug.LogWarning("[StoreManager] store_catalog is empty in Firestore — using default catalog.");
                    _catalog.AddRange(BuildDefaultCatalog());
                }

                _catalog.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));
                RegisterIapProductsFromCatalog();
                OnCatalogLoaded?.Invoke(new List<StoreItem>(_catalog));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StoreManager] LoadCatalogFromFirestoreAsync failed: {ex.Message}. Using default catalog.");
                _catalog.Clear();
                _catalog.AddRange(BuildDefaultCatalog());
                RegisterIapProductsFromCatalog();
                OnCatalogLoadFailed?.Invoke(ex.Message);
                OnCatalogLoaded?.Invoke(new List<StoreItem>(_catalog));
            }
        }

        /// <summary>Hardcoded fallback catalog — the 5 active, coin-priced
        /// boosters. Edit freely, or add matching docs to Firestore's
        /// store_catalog/ collection so prices can change without an app update.</summary>
        private List<StoreItem> BuildDefaultCatalog()
        {
            return new List<StoreItem>
            {
                new StoreItem { id="hammer",         displayName="Hammer",          description="Destroy any one tile.",           itemType=StoreItemType.Booster, quantity=1, coinPrice=10, spriteKey="hammer",         sortOrder=0 },
                new StoreItem { id="row_bomb",        displayName="Row Bomb",        description="Clear one full row.",             itemType=StoreItemType.Booster, quantity=1, coinPrice=15, spriteKey="row_bomb",       sortOrder=1 },
                new StoreItem { id="column_bomb",     displayName="Column Bomb",     description="Clear one full column.",          itemType=StoreItemType.Booster, quantity=1, coinPrice=15, spriteKey="column_bomb",    sortOrder=2 },
                new StoreItem { id="shuffle_2tiles",  displayName="Shuffle 2 Tiles", description="Swap any two tiles you pick.",    itemType=StoreItemType.Booster, quantity=1, coinPrice=20, spriteKey="shuffle_2tiles", sortOrder=3 },
                new StoreItem { id="shuffle_board",   displayName="Shuffle Board",   description="Shuffle the whole board.",        itemType=StoreItemType.Booster, quantity=1, coinPrice=20, spriteKey="shuffle_board",  sortOrder=4 },
            };
        }

        // ============================================================
        //  UNITY IAP — IStoreListener
        //  (kept in place for future real-money items; current catalog
        //  has no iapProductId set on any item, so this simply won't
        //  initialize Unity IAP until you add one — see RegisterIapProductsFromCatalog)
        // ============================================================

        private void RegisterIapProductsFromCatalog()
        {
            if (IsIapInitialized) return; // Unity IAP only initializes once per session

            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            bool anyIapProduct = false;

            foreach (StoreItem item in _catalog)
            {
                if (string.IsNullOrEmpty(item.iapProductId)) continue;
                builder.AddProduct(item.iapProductId, ProductType.Consumable);
                anyIapProduct = true;
            }

            if (!anyIapProduct)
            {
                if (logVerbose) Debug.Log("[StoreManager] No IAP products in catalog — skipping Unity IAP init.");
                return;
            }

            UnityPurchasing.Initialize(this, builder);
        }

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            _storeController   = controller;
            _extensionProvider = extensions;
            IsIapInitialized    = true;
            if (logVerbose) Debug.Log("[StoreManager] Unity IAP initialized.");
        }

        public void OnInitializeFailed(InitializationFailureReason error)
        {
            Debug.LogError($"[StoreManager] Unity IAP init failed: {error}");
            OnPurchaseFailedFeedback?.Invoke("Store is unavailable right now.");
        }

        // Newer Unity IAP versions call this overload with an extra message —
        // implement both so this compiles regardless of your installed IAP version.
        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            Debug.LogError($"[StoreManager] Unity IAP init failed: {error} — {message}");
            OnPurchaseFailedFeedback?.Invoke("Store is unavailable right now.");
        }

        /// <summary>Real-money purchase entry point — unused while the catalog
        /// is coins-only, kept ready for when you add an IAP item.</summary>
        public Task PurchaseBooster(string sku)
        {
            if (!IsIapInitialized || _storeController == null)
            {
                Debug.LogWarning("[StoreManager] PurchaseBooster called before IAP initialized.");
                OnPurchaseFailedFeedback?.Invoke("Store not ready yet — try again in a moment.");
                return Task.CompletedTask;
            }

            Product product = _storeController.products.WithID(sku);
            if (product == null || !product.availableToPurchase)
            {
                Debug.LogWarning($"[StoreManager] SKU '{sku}' not found/available.");
                OnPurchaseFailedFeedback?.Invoke("This item isn't available right now.");
                return Task.CompletedTask;
            }

            _storeController.InitiatePurchase(product);
            return Task.CompletedTask; // result arrives async via ProcessPurchase()/OnPurchaseFailed()
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            Product product = args.purchasedProduct;
            StoreItem item = _catalog.Find(i => i.iapProductId == product.definition.id);

            if (item == null)
            {
                Debug.LogError($"[StoreManager] ProcessPurchase: no catalog item matches IAP sku '{product.definition.id}'.");
                return PurchaseProcessingResult.Complete;
            }

            GrantItem(item);
            if (logVerbose) Debug.Log($"[StoreManager] IAP purchase complete: {item.displayName}");
            return PurchaseProcessingResult.Complete;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason reason)
        {
            Debug.LogWarning($"[StoreManager] IAP purchase failed for '{product?.definition.id}': {reason}");
            OnPurchaseFailedFeedback?.Invoke(FriendlyIapFailureMessage(reason));
        }

        private string FriendlyIapFailureMessage(PurchaseFailureReason reason) => reason switch
        {
            PurchaseFailureReason.UserCancelled       => "Purchase cancelled.",
            PurchaseFailureReason.PaymentDeclined     => "Payment was declined.",
            PurchaseFailureReason.DuplicateTransaction => "This purchase was already processed.",
            _ => "Purchase failed — please try again."
        };

        // ============================================================
        //  GEM / COIN PURCHASE PATHS
        //  BuyWithCoins() is the active path for the current catalog.
        //  BuyWithGems() stays implemented for when a gem-priced item exists.
        // ============================================================

        public async Task<bool> BuyWithGems(string itemId, int gemCost)
        {
            return await BuySoftCurrency(itemId, gemCost, useGems: true);
        }

        /// <summary>Buys a store item using coins — the active purchase path
        /// for all 5 current boosters.</summary>
        public async Task<bool> BuyWithCoins(string itemId, int coinCost)
        {
            return await BuySoftCurrency(itemId, coinCost, useGems: false);
        }

        private Task<bool> BuySoftCurrency(string itemId, int cost, bool useGems)
        {
            if (_purchaseInFlight.Contains(itemId))
                return Task.FromResult(false); // ignore double-tap while a purchase is mid-flight

            StoreItem item = _catalog.Find(i => i.id == itemId);
            if (item == null)
            {
                Debug.LogWarning($"[StoreManager] BuySoftCurrency: unknown item id '{itemId}'.");
                OnPurchaseFailedFeedback?.Invoke("This item is no longer available.");
                return Task.FromResult(false);
            }

            PlayerProfile profile = LocalSaveManager.GetOrLoadProfile();
            if (profile == null)
            {
                OnPurchaseFailedFeedback?.Invoke("Could not load your profile — try again.");
                return Task.FromResult(false);
            }

            int balance = useGems ? profile.gems : profile.coins;
            if (balance < cost)
            {
                OnPurchaseFailedFeedback?.Invoke(useGems ? "Not enough gems." : "Not enough coins.");
                return Task.FromResult(false);
            }

            _purchaseInFlight.Add(itemId);
            try
            {
                if (useGems) profile.gems  -= cost;
                else         profile.coins -= cost;

                LocalSaveManager.SaveProfile(profile);
                GrantItem(item);
                return Task.FromResult(true);
            }
            finally
            {
                _purchaseInFlight.Remove(itemId);
            }
        }

        // ============================================================
        //  GRANT — single path every purchase method funnels into
        // ============================================================

        /// <summary>Applies the item's payout to the player's profile/inventory
        /// and triggers a background cloud sync.</summary>
        public void GrantItem(StoreItem item)
        {
            PlayerProfile profile = LocalSaveManager.GetOrLoadProfile() ?? new PlayerProfile();
            int newBoosterCount = 0;

            switch (item.itemType)
            {
                case StoreItemType.Booster:
                {
                    Dictionary<string, int> inventory = LocalSaveManager.LoadBoosterInventory();
                    inventory.TryGetValue(item.id, out int current);
                    newBoosterCount = current + item.quantity;
                    inventory[item.id] = newBoosterCount;
                    LocalSaveManager.SaveBoosterInventory(inventory); // also mirrors into profile.boosters + saves profile
                    break;
                }
                case StoreItemType.GemPack:
                    profile.gems += item.quantity;
                    LocalSaveManager.SaveProfile(profile);
                    break;

                case StoreItemType.CoinPack:
                    profile.coins += item.quantity;
                    LocalSaveManager.SaveProfile(profile);
                    if (GameManager.Instance != null) GameManager.Instance.AddCoins(item.quantity);
                    break;

                case StoreItemType.Bundle:
                    Debug.LogWarning($"[StoreManager] Bundle grant not implemented yet for '{item.id}'.");
                    break;
            }

            // Fire-and-forget cloud push — reuses CloudSyncManager's existing
            // "push local profile" pipeline (same one LevelResultManager uses
            // after a level win). UI never blocks on this.
            _ = CloudSyncManager.Instance?.SyncAfterLevelAsync();

            OnItemGranted?.Invoke(item, newBoosterCount);
            if (logVerbose) Debug.Log($"[StoreManager] Granted: {item.displayName} (x{item.quantity}).");
        }
    }
}