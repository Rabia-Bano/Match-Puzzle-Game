// ============================================================
//  PetData.cs  —  ScriptableObject
//  Create via: Assets > Create > Match3 > Pet Data
//
//  One PetData asset = one collectible pet companion.
//  Save these under: Assets/Resources/Pets/  (PetManager and
//  PetCollection both load pets from that exact folder via
//  Resources.Load / Resources.LoadAll — see setup notes).
//
//  Unlock rule (per current design):
//    • The very first pet (index 0) is unlocked from Level 1,
//      before the player has played anything.
//    • Every pet after that unlocks after completing every
//      5th level (after Level 5, Level 10, Level 15 ...).
//    • unlockAfterLevel = 0 means "always unlocked".
// ============================================================

using UnityEngine;

namespace Match3
{
    // ── Enums ────────────────────────────────────────────────

    /// <summary>Collection rarity — purely cosmetic (border colour, sort order in UI).</summary>
    public enum PetRarity
    {
        Common = 0,
        Rare   = 1,
        Epic   = 2
    }

    /// <summary>
    /// Which PetSkill implementation this pet uses. PetManager maps this
    /// enum to a concrete PetSkill instance (see PetManager.CreateSkillInstance).
    /// </summary>
    public enum PetSkillType
    {
        Icera  = 0,   // clears 3 random rows
        Luna   = 1,   // shuffles all non-special tiles on board, twice
        Sparky = 2,   // adds bonus moves
        Ripple = 3    // converts a few random tiles to a goal-relevant colour
    }

    // ── ScriptableObject ──────────────────────────────────────

    [CreateAssetMenu(fileName = "PetData_New", menuName = "Match3/Pet Data", order = 6)]
    public class PetData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique string id. Also used as the Resources file name: Resources/Pets/<id>.asset")]
        public string id;

        [Tooltip("Display name shown in HUD / Collection / notifications.")]
        public string petName;

        public PetRarity rarity = PetRarity.Common;

        [Tooltip("Pet portrait — used in HUD, Collection grid, and unlock popup.")]
        public Sprite sprite;

        [TextArea] public string description;

        [Header("Skill")]
        public PetSkillType skillType;

        [Tooltip("Short skill name shown next to the skill button, e.g. 'Row Blast'.")]
        public string skillName;

        [TextArea] public string skillDescription;

        [Header("Charge")]
        [Tooltip("Total charge points needed to fill the battery to 100% and unlock the skill button. " +
                 "Matches award charge as: 3-match = +10, 4-match = +20, 5+-match = +30 (see PetManager). " +
                 "Default 100 means a pure run of 3-matches needs 10 matches to charge; raise this for " +
                 "Rare/Epic pets to make their (usually stronger) skill take longer to charge.")]
        public int chargeRequired = 100;

        [Header("Unlock")]
        [Tooltip("How many levels must be completed before this pet unlocks. 0 = unlocked from the start " +
                 "(use this for the very first / starter pet). Subsequent pets: 5, 10, 15 ...")]
        public int unlockAfterLevel = 0;
    }
}