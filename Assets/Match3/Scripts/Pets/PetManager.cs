// ============================================================
//  PetManager.cs  —  MonoBehaviour
//
//  Owns the currently EQUIPPED pet across the whole app session
//  (DontDestroyOnLoad):
//    • Loads its PetData from Resources/Pets/<id>
//    • Listens to the CURRENT level's BoardController.OnMatchGroupResolved
//      for charge — re-bound every level via BindToLevel()
//    • Runs the pet's PetSkill when the player taps the skill button
//
//  Attach to: an empty "PetManager" GameObject, spawned once (e.g. in a
//  Preloader/Bootstrap scene, same pattern as SaveManager/ProfileManager).
//  Do NOT wire boardGrid/boardController/goalTracker/moveCounter in the
//  Inspector — those are per-level objects, re-wired at runtime by
//  LevelManager.Start() calling PetManager.Instance.BindToLevel(...).
//
//  Charge table (per match cleared in ONE resolve pass):
//    3-match   -> +5
//    4-match   -> +10
//    5+-match  -> +15
//  Battery fills 0 -> 100. At 100 the skill button becomes usable;
//  using it resets the battery back to 0.
// ============================================================

using System;
using System.Collections;
using UnityEngine;

namespace Match3
{
    public class PetManager : MonoBehaviour
    {
        public static PetManager Instance { get; private set; }

        /// <summary>
        /// Returns the existing PetManager, or creates one on the spot if none
        /// exists yet. PetManager normally gets created the first time GameBoard
        /// scene loads — but the player can open the Pet Collection screen from
        /// Map *before ever playing a level*, in which case nothing has created
        /// it yet. Screens that need a pet (PetHUD, PetCollection) should call
        /// this instead of reading .Instance directly.
        /// </summary>
        public static PetManager GetOrCreateInstance()
        {
            if (Instance != null) return Instance;

            GameObject go = new GameObject("PetManager (Auto-Created)");
            PetManager pm = go.AddComponent<PetManager>();   // Awake() runs synchronously here → sets Instance + DontDestroyOnLoad
            pm.LoadEquippedPet();                            // don't wait for Start() — make it usable immediately
            Debug.Log("[PetManager] Auto-created (no level had been played yet).");

            return Instance;
        }

        [Header("Core References — DO NOT wire these in the Inspector.")]
        [Header("PetManager is DontDestroyOnLoad; these belong to whichever")]
        [Header("GameBoard scene/level is currently loaded. LevelManager calls")]
        [Header("BindToLevel() every time a level starts, which re-wires these")]
        [Header("and re-subscribes to that level's BoardController.")]
        private BoardGrid       boardGrid;
        private BoardController boardController;
        private GoalTracker     goalTracker;
        private MoveCounter     moveCounter;

        private const string EQUIPPED_PET_KEY = "EquippedPetId";
        private const string PETS_RESOURCE_FOLDER = "Pets";
        private const string DEFAULT_PET_ID = "pet_icera"; // your Level-1 starter pet's id

        private const int MaxCharge = 100;

        // ── Public state ──────────────────────────────────────

        public string  EquippedPetId { get; private set; }
        public PetData EquippedPet   { get; private set; }

        public int  ChargeProgress { get; private set; }
        public bool IsCharged      => EquippedPet != null && ChargeProgress >= EquippedPet.chargeRequired;
        public bool IsBusy         { get; private set; }

        // ── Events ────────────────────────────────────────────

        /// <summary>(currentCharge, chargeRequired)</summary>
        public event Action<int, int> OnChargeChanged;
        public event Action           OnPetReady;
        public event Action<PetData>  OnSkillUsed;
        public event Action           OnPetChanged;   // fired after LoadEquippedPet()/EquipPet()

        private PetSkill _skillInstance;

        // ── Lifecycle ─────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // Guard against the 1-frame window where a duplicate PetManager
            // (destroyed in Awake above) can still run Start() before Unity
            // actually removes it at end of frame.
            if (Instance != this) return;
            LoadEquippedPet();
        }

        // NOTE: subscription to BoardController.OnMatchGroupResolved now happens
        // inside BindToLevel(), NOT in OnEnable/OnDisable — OnEnable only runs
        // once for a DontDestroyOnLoad object (the first time it's created), so
        // it would only ever bind to Level 1's BoardController and silently stop
        // working from Level 2 onwards. BindToLevel() is called fresh by
        // LevelManager every time a level scene finishes initializing.

        /// <summary>
        /// Call this once per level, after the level's BoardGrid/BoardController/
        /// GoalTracker/MoveCounter exist (LevelManager.Start() does this — see
        /// the LevelManager.cs patch). Re-points PetManager at the new level's
        /// objects and re-subscribes to the new BoardController's match events.
        /// </summary>
        public void BindToLevel(BoardGrid grid, BoardController controller, GoalTracker goals, MoveCounter moves)
        {
            // Unsubscribe from whatever level we were bound to before (safe if null/destroyed).
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

            // Fresh battery for the new level.
            ChargeProgress = 0;
            OnChargeChanged?.Invoke(ChargeProgress, EquippedPet != null ? EquippedPet.chargeRequired : MaxCharge);
        }

        // ── Loading / equipping ────────────────────────────────

        /// <summary>Loads whichever pet is saved as equipped (or the starter pet on first run).</summary>
        public void LoadEquippedPet()
        {
            EquippedPetId = PlayerPrefs.GetString(EQUIPPED_PET_KEY, DEFAULT_PET_ID);
            ApplyEquippedPet();
        }

        /// <summary>Called from PetCollection when the player picks a different unlocked pet.</summary>
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
                // Guards against stale PlayerPrefs data (e.g. equipped during earlier
                // testing) pointing at a pet the player hasn't actually unlocked yet.
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

        /// <summary>True if the player has completed enough levels to have unlocked this pet.</summary>
        private bool IsPetUnlocked(PetData pet)
        {
            if (pet == null) return false;
            int highestLevelReached = (SaveManager.Instance != null && SaveManager.Instance.Profile != null)
                ? SaveManager.Instance.Profile.highestLevelReached
                : 0;
            return highestLevelReached >= pet.unlockAfterLevel;
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

        // ── Charging ──────────────────────────────────────────

        /// <summary>Subscribed to BoardController.OnMatchGroupResolved(int matchSize).</summary>
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

        // ── Using the skill ───────────────────────────────────

        /// <summary>Call this from the pet skill button's OnClick.</summary>
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