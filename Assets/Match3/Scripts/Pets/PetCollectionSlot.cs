// ============================================================
//  PetCollectionSlot.cs  —  MonoBehaviour
//  One grid cell in the Pet Companion Collection screen
//  (matches the "Pet Companion" mockup you shared — portrait,
//  name, Use/equip button, locked padlock overlay).
//
//  Attach to: the PetSlot prefab used by PetCollection's grid.
// ============================================================

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    public class PetCollectionSlot : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Image           portrait;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI skillNameText;     // NEW — shows PetData.skillName
        [SerializeField] private GameObject      lockedOverlay;     // padlock icon + dim tint
        [SerializeField] private TextMeshProUGUI lockedLabel;       // "Unlocks at Lvl 6"
        [SerializeField] private GameObject      equippedBadge;     // "Equipped" ribbon/checkmark
        [SerializeField] private Button          selectButton;      // tap to equip (only works if unlocked)

        public PetData Data { get; private set; }

        public void Setup(PetData data, bool isUnlocked, bool isEquipped, System.Action<PetData> onSelected)
        {
            Data = data;

            if (portrait != null) portrait.sprite = data.sprite;
            if (nameText != null) nameText.text   = data.petName;

            if (skillNameText != null)
                skillNameText.text = isUnlocked ? data.skillName : "???";   // hide skill name while locked

            if (lockedOverlay != null) lockedOverlay.SetActive(!isUnlocked);
            if (lockedLabel   != null) lockedLabel.text = $"Unlocks after Level {data.unlockAfterLevel}";

            if (equippedBadge != null) equippedBadge.SetActive(isUnlocked && isEquipped);

            if (portrait != null)
                portrait.color = isUnlocked ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f);

            selectButton?.onClick.RemoveAllListeners();
            if (selectButton != null)
            {
                selectButton.interactable = isUnlocked;
                selectButton.onClick.AddListener(() => onSelected?.Invoke(data));
            }
        }
    }
}