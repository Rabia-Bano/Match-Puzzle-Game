// ============================================================
//  AvatarPresetData.cs  —  ScriptableObject
//  Create via: Assets > Create > Match3 > Avatar Preset
//
//  One asset = one selectable preset avatar. Save these under:
//  Assets/Resources/Avatars/  with the FILE NAME matching `id`
//  exactly (e.g. id = "avatar_1" -> Resources/Avatars/avatar_1.asset).
//  ProfileManager resolves the active avatar via
//  Resources.Load<AvatarPresetData>("Avatars/" + avatarId), the
//  same Resources.Load convention used by PetData.
// ============================================================

using UnityEngine;

namespace Match3
{
    [CreateAssetMenu(fileName = "AvatarPreset_New", menuName = "Match3/Avatar Preset", order = 7)]
    public class AvatarPresetData : ScriptableObject
    {
        [Tooltip("Unique string id. MUST match this asset's file name exactly, " +
                 "e.g. id \"avatar_1\" -> Resources/Avatars/avatar_1.asset")]
        public string id;

        [Tooltip("Shown in the avatar picker grid and applied to Profile Panel's Avatar Image once selected.")]
        public Sprite sprite;

        // ── NEW — Avatar Shop ─────────────────────────────────
        [Header("Shop (NEW)")]
        [Tooltip("Name shown on the shop card, e.g. \"Ice Princess\".")]
        public string displayName;

        [Tooltip("Price in coins. 0 = FREE (every player owns it from the start).")]
        [Min(0)] public int price = 0;

        [Tooltip("Lower numbers appear first in the shop / picker.")]
        public int sortOrder = 0;

        [Tooltip("Optional small label on the card, e.g. \"NEW\", \"RARE\", \"LEGENDARY\".")]
        public string badgeText;

        public bool IsFree => price <= 0;
        public string NameOrId => string.IsNullOrEmpty(displayName) ? id : displayName;
    }
}
