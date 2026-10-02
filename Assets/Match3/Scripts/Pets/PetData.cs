using UnityEngine;

namespace Match3
{
    public enum PetRarity
    {
        Common = 0,
        Rare   = 1,
        Epic   = 2
    }

    public enum PetSkillType
    {
        Icera  = 0,
        Luna   = 1,
        Sparky = 2,
        Ripple = 3
    }

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
