// ============================================================
//  PetManager.cs  —  MonoBehaviour
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Match3
{
    public class PetManager : MonoBehaviour
    {
        public static PetManager Instance { get; private set; }

        public static PetManager GetOrCreateInstance()
        {
            if (Instance != null) return Instance;

            GameObject go = new GameObject("PetManager (Auto-Created)");
            PetManager pm = go.AddComponent<PetManager>();
            pm.LoadEquippedPet();
            Debug.Log("[PetManager] Auto-created (no level had been played yet).");

            return Instance;
        }

        private BoardGrid       boardGrid;
        private BoardController boardController;
        private GoalTracker     goalTracker;
        private MoveCounter     moveCounter;

        private const string EQUIPPED_PET_KEY = "EquippedPetId";
        private const string PETS_RESOURCE_FOLDER = "Pets";
        private const string DEFAULT_PET_ID = "pet_icera";

        private const int MaxCharge = 100;

        public string  EquippedPetId { get; private set; }
        public PetData EquippedPet   { get; private set; }

        public int  ChargeProgress { get; private set; }
        public bool IsCharged      => EquippedPet != null && ChargeProgress >= EquippedPet.chargeRequired;
        public bool IsBusy         { get; private set; }

        public event Action<int, int> OnChargeChanged;
        public event Action           OnPetReady;
        public event Action<PetData>  OnSkillUsed;
        public event Action           OnPetChanged;

        private PetSkill _skillInstance;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (Instance != this) return;
            LoadEquippedPet();
        }

        public void BindToLevel(BoardGrid grid, BoardController controller, GoalTracker goals, MoveCounter moves)
        {
            if (boardController != null)
                boardController.OnMatchGroupResolved -= HandleMatchGroupResolved;

            boardGrid       = grid;
            boardController = controller;
            goalTracker     = goals;
            moveCounter     = moves;

            if (boardController != null)
                boardController.OnMatchGroupResolved += HandleMatchGroupResolved;
            else
                Debug.LogError("[PetManager] BindToLevel() got a null BoardController — pet charging won't work this level.");

            ChargeProgress = 0;
            OnChargeChanged?.Invoke(ChargeProgress, EquippedPet != null ? EquippedPet.chargeRequired : MaxCharge);
        }

        public void LoadEquippedPet()
        {
            EquippedPetId = PlayerPrefs.GetString(EQUIPPED_PET_KEY, DEFAULT_PET_ID);
            ApplyEquippedPet();
        }

        public void EquipPet(string petId)
        {
            if (string.IsNullOrEmpty(petId)) return;

            PetData candidate = Resources.Load<PetData>($"{PETS_RESOURCE_FOLDER}/{petId}");
            if (candidate == null)
            {
                Debug.LogError($"[PetManager] EquipPet() — no PetData found for id '{petId}'.");
                return;
            }
            if (!IsPetUnlocked(candidate))
            {
                Debug.LogWarning($"[PetManager] EquipPet() — '{petId}' isn't unlocked yet " +
                                  $"(needs Level {candidate.unlockAfterLevel}). Ignoring.");
                return;
            }

            EquippedPetId = petId;
            PlayerPrefs.SetString(EQUIPPED_PET_KEY, petId);
            PlayerPrefs.Save();

            ApplyEquippedPet();
        }

        private void ApplyEquippedPet()
        {
            EquippedPet = Resources.Load<PetData>($"{PETS_RESOURCE_FOLDER}/{EquippedPetId}");

            if (EquippedPet == null)
            {
                Debug.LogError($"[PetManager] Could not load PetData '{EquippedPetId}' from " +
                                $"Resources/{PETS_RESOURCE_FOLDER}/. Falling back to default pet.");
                EquippedPetId = DEFAULT_PET_ID;
                EquippedPet    = Resources.Load<PetData>($"{PETS_RESOURCE_FOLDER}/{DEFAULT_PET_ID}");
            }
            else if (!IsPetUnlocked(EquippedPet))
            {
                Debug.LogWarning($"[PetManager] Saved equipped pet '{EquippedPetId}' is not unlocked " +
                                  $"(needs Level {EquippedPet.unlockAfterLevel}) — reverting to default pet.");
                EquippedPetId = DEFAULT_PET_ID;
                PlayerPrefs.SetString(EQUIPPED_PET_KEY, DEFAULT_PET_ID);
                PlayerPrefs.Save();
                EquippedPet = Resources.Load<PetData>($"{PETS_RESOURCE_FOLDER}/{DEFAULT_PET_ID}");
            }

            _skillInstance  = CreateSkillInstance(EquippedPet != null ? EquippedPet.skillType : PetSkillType.Icera);
            ChargeProgress  = 0;

            int max = EquippedPet != null ? EquippedPet.chargeRequired : MaxCharge;
            OnChargeChanged?.Invoke(ChargeProgress, max);
            OnPetChanged?.Invoke();
        }

        // ── CHANGED: accepts an optional override so callers with FRESH data
        //    (like ProfileManager right after updating levelsCompleted) don't
        //    have to rely on the possibly-stale LocalSaveManager cache. ──
        private bool IsPetUnlocked(PetData pet, int? levelsCompletedOverride = null)
        {
            if (pet == null) return false;
            int levelsCompleted = levelsCompletedOverride
                                   ?? (LocalSaveManager.GetOrLoadProfile()?.levelsCompleted ?? 0);
            return levelsCompleted >= pet.unlockAfterLevel;
        }

        // ── CHANGED: same override pattern. Pass Profile.levelsCompleted
        //    directly from ProfileManager to avoid the stale-cache bug. ──
        public List<string> GetUnlockedPetIds(int? levelsCompletedOverride = null)
        {
            var result = new List<string>();
            PetData[] allPets = Resources.LoadAll<PetData>(PETS_RESOURCE_FOLDER);

            if (allPets.Length == 0)
                Debug.LogWarning($"[PetManager] Resources.LoadAll<PetData>(\"{PETS_RESOURCE_FOLDER}\") returned 0 assets. " +
                                  $"Check that PetData .asset files exist at Assets/Resources/{PETS_RESOURCE_FOLDER}/.");

            int levelsCompleted = levelsCompletedOverride
                                   ?? (LocalSaveManager.GetOrLoadProfile()?.levelsCompleted ?? 0);

            foreach (PetData pet in allPets)
                if (levelsCompleted >= pet.unlockAfterLevel)
                    result.Add(pet.id);

            return result;
        }

        private PetSkill CreateSkillInstance(PetSkillType type)
        {
            return type switch
            {
                PetSkillType.Icera  => new IceraSkill(),
                PetSkillType.Luna   => new LunaSkill(),
                PetSkillType.Sparky => new SparkySkill(),
                PetSkillType.Ripple => new RippleSkill(),
                _                   => null
            };
        }

        private void HandleMatchGroupResolved(int matchSize)
        {
            if (EquippedPet == null || IsBusy) return;

            int gain = matchSize switch
            {
                3 => 5,
                4 => 10,
                >= 5 => 15,
                _ => 0
            };
            if (gain <= 0) return;

            int max = EquippedPet.chargeRequired;
            ChargeProgress = Mathf.Min(max, ChargeProgress + gain);
            OnChargeChanged?.Invoke(ChargeProgress, max);

            if (IsCharged)
                OnPetReady?.Invoke();
        }

        public void UseSkill()
        {
            if (!IsCharged || IsBusy || _skillInstance == null || EquippedPet == null) return;
            StartCoroutine(UseSkillRoutine());
        }

        private IEnumerator UseSkillRoutine()
        {
            IsBusy = true;
            OnSkillUsed?.Invoke(EquippedPet);

            Debug.Log($"[PetManager] Using skill: {EquippedPet.skillName} ({EquippedPet.skillType})");

            yield return _skillInstance.UseSkill(boardGrid, boardController, goalTracker, moveCounter);

            ChargeProgress = 0;
            OnChargeChanged?.Invoke(ChargeProgress, EquippedPet.chargeRequired);
            IsBusy = false;
        }
    }
}