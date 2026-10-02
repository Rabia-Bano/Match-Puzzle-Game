using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Match3
{
    public class PetCollection : MonoBehaviour
    {
        private const string PETS_RESOURCE_FOLDER = "Pets";
        private const string SEEN_UNLOCKS_KEY = "PetsSeenUnlocked";

        [Header("References")]
        [Tooltip("Left blank on purpose — always resolved from PetManager.Instance in Start(). Do not wire manually.")]
        private PetManager petManager;

        [Header("Grid")]
        [SerializeField] private Transform    gridContainer;
        [SerializeField] private GameObject   slotPrefab;

        [Header("Unlock Notification (optional)")]
        [SerializeField] private GameObject      unlockPopup;
        [SerializeField] private UnityEngine.UI.Image petIconOnPopup;
        [SerializeField] private TMPro.TextMeshProUGUI unlockNameText;

        private List<PetData> _allPets = new();
        private readonly List<PetCollectionSlot> _slots = new();
        private bool _started;

        private void Start()
        {
            petManager = PetManager.GetOrCreateInstance();

            _started = true;
            RefreshScreen();
        }

        private void OnEnable()
        {
            if (_started) RefreshScreen();
        }

        private void RefreshScreen()
        {
            LoadAllPets();
            BuildGrid();
            CheckForNewlyUnlockedPets();
        }

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
            BuildGrid();
        }

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
                Game.Firebase.ProfileManager.Instance?.OnPetUnlocked(pet.id);
                break;
            }

            SaveSeenUnlocks(seen);

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
