// ============================================================
//  PetCollection.cs  —  MonoBehaviour
//
//  Builds the Pet Companion Collection screen: every PetData found
//  in Resources/Pets/, shown as owned/locked, with an Equip button.
//  Also raises the "New Pet Unlocked" notification the first time
//  a pet's unlock threshold is crossed.
//
//  Attach to: "PetCollection" panel/screen GameObject (Pets tab from
//  the bottom nav bar). Wire up gridContainer + slotPrefab (which
//  needs a PetCollectionSlot component) in the Inspector.
//
//  UPDATED: unlock check source is now LocalSaveManager (Newtonsoft-based
//  local save, wraps the single canonical global PlayerProfile) instead
//  of the deprecated Match3.SaveManager / Match3.PlayerProfile. If you
//  want the cloud copy to win when online, swap ResolveHighestLevelReached()
//  to read Game.Firebase.ProfileManager.Instance.Profile.levelsCompleted
//  first, falling back to LocalSaveManager when offline.
// ============================================================

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Match3
{
    public class PetCollection : MonoBehaviour
    {
        private const string PETS_RESOURCE_FOLDER = "Pets";
        private const string SEEN_UNLOCKS_KEY = "PetsSeenUnlocked"; // comma-separated ids already notified

        [Header("References")]
        [Tooltip("Left blank on purpose — always resolved from PetManager.Instance in Start(). Do not wire manually.")]
        private PetManager petManager;

        [Header("Grid")]
        [SerializeField] private Transform    gridContainer;
        [SerializeField] private GameObject   slotPrefab;   // needs PetCollectionSlot component

        [Header("Unlock Notification (optional)")]
        [SerializeField] private GameObject      unlockPopup;
        [SerializeField] private UnityEngine.UI.Image petIconOnPopup;
        [SerializeField] private TMPro.TextMeshProUGUI unlockNameText;

        private List<PetData> _allPets = new();
        private readonly List<PetCollectionSlot> _slots = new();
        private bool _started;

        private void Start()
        {
            // Resolved here (not OnEnable) because Unity guarantees every object's
            // Awake() — including PetManager's, which sets Instance — has already run
            // by the time ANY object's Start() runs, regardless of hierarchy order.
            // OnEnable() carries no such guarantee and could fire before PetManager
            // exists, leaving this screen permanently unable to equip pets.
            petManager = PetManager.GetOrCreateInstance();

            _started = true;
            RefreshScreen();
        }

        private void OnEnable()
        {
            // If the panel gets re-shown later without a scene reload (Start already
            // ran once), refresh the grid so newly-unlocked pets show up immediately.
            if (_started) RefreshScreen();
        }

        private void RefreshScreen()
        {
            LoadAllPets();
            BuildGrid();
            CheckForNewlyUnlockedPets();
        }

        // ── Loading ───────────────────────────────────────────

        private void LoadAllPets()
        {
            _allPets = Resources.LoadAll<PetData>(PETS_RESOURCE_FOLDER)
                                 .OrderBy(p => p.unlockAfterLevel)
                                 .ToList();

            if (_allPets.Count == 0)
                Debug.LogWarning($"[PetCollection] No PetData assets found in Resources/{PETS_RESOURCE_FOLDER}/.");
        }

        private void BuildGrid()
        {
            foreach (Transform child in gridContainer)
                Destroy(child.gameObject);
            _slots.Clear();

            int highestLevelReached = ResolveHighestLevelReached();
            string equippedId = petManager != null ? petManager.EquippedPetId : null;

            foreach (PetData pet in _allPets)
            {
                bool isUnlocked = highestLevelReached >= pet.unlockAfterLevel;
                bool isEquipped = pet.id == equippedId;

                GameObject go = Instantiate(slotPrefab, gridContainer);
                var slot = go.GetComponent<PetCollectionSlot>();
                if (slot == null)
                {
                    Debug.LogError("[PetCollection] slotPrefab is missing a PetCollectionSlot component.", go);
                    continue;
                }

                slot.Setup(pet, isUnlocked, isEquipped, OnPetSelected);
                _slots.Add(slot);
            }
        }

        private void OnPetSelected(PetData pet)
        {
            petManager?.EquipPet(pet.id);
            BuildGrid();   // refresh "Equipped" badges
        }

        // ── Unlock detection / notification ───────────────────

        /// <summary>
        /// Call this too from LevelResultManager right after a win (in addition
        /// to OnEnable here) so the popup can fire even if the player doesn't
        /// open the Collection screen immediately.
        /// </summary>
        public void CheckForNewlyUnlockedPets()
        {
            int highestLevelReached = ResolveHighestLevelReached();
            HashSet<string> seen = LoadSeenUnlocks();
            bool anyNewUnlock = false;

            foreach (PetData pet in _allPets)
            {
                if (highestLevelReached < pet.unlockAfterLevel) continue;
                if (seen.Contains(pet.id)) continue;

                seen.Add(pet.id);
                anyNewUnlock = true;
                ShowUnlockPopup(pet);
                // Sync to Firebase profile if available/online — safe no-op otherwise.
                Game.Firebase.ProfileManager.Instance?.OnPetUnlocked(pet.id);
                break; // show one popup at a time; remaining new pets will show next OnEnable
            }

            SaveSeenUnlocks(seen);

            // Persist the up-to-date unlocked-pet list into the local save
            // whenever a new pet crosses its unlock threshold.
            if (anyNewUnlock)
            {
                var unlockedIds = petManager != null
                    ? petManager.GetUnlockedPetIds()
                    : _allPets.Where(p => highestLevelReached >= p.unlockAfterLevel).Select(p => p.id).ToList();
                LocalSaveManager.SavePetCollection(unlockedIds);
            }
        }

        private void ShowUnlockPopup(PetData pet)
        {
            if (unlockPopup == null) return;

            if (petIconOnPopup != null)  petIconOnPopup.sprite = pet.sprite;
            if (unlockNameText != null)  unlockNameText.text   = pet.petName;

            unlockPopup.SetActive(true);
        }

        // ── Helpers ───────────────────────────────────────────

        /// <summary>Reads levels-completed from the canonical local save
        /// (LocalSaveManager), which mirrors the same PlayerProfile used
        /// by ProfileManager/Firestore — no more separate Match3.SaveManager copy.</summary>
        private int ResolveHighestLevelReached()
        {
            return LocalSaveManager.GetOrLoadProfile()?.levelsCompleted ?? 0;
        }

        private HashSet<string> LoadSeenUnlocks()
        {
            string raw = PlayerPrefs.GetString(SEEN_UNLOCKS_KEY, "");
            return string.IsNullOrEmpty(raw)
                ? new HashSet<string>()
                : new HashSet<string>(raw.Split(','));
        }

        private void SaveSeenUnlocks(HashSet<string> seen)
        {
            PlayerPrefs.SetString(SEEN_UNLOCKS_KEY, string.Join(",", seen));
            PlayerPrefs.Save();
        }
    }
}