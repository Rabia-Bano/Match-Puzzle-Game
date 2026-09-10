// ============================================================
//  TopBarAvatarIcon.cs  —  MonoBehaviour
//
//  Keeps the top-right Profile Icon (the button that opens
//  ProfilePanel) in sync with the player's chosen avatar
//  (preset avatarId OR uploaded avatarUrl photo). Without this,
//  the icon just stays on its static default sprite forever —
//  it was never wired to ProfileManager at all.
//
//  Attach to: the Profile Icon GameObject itself (the one with
//  the Button that already calls ProfilePanel.Show() from the
//  Inspector) in EVERY scene that shows this icon — Map, BossArena,
//  Store, Leaderboard, Setting, PetCompanion, etc. It's a
//  per-scene component just like TopBarPillThemeBinder /
//  NavBarThemeBinder.
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using Game.Firebase;

public class TopBarAvatarIcon : MonoBehaviour
{
    [Tooltip("The Image component that actually shows the avatar picture. " +
             "Usually the same GameObject this script is on, or a child Image.")]
    [SerializeField] private Image avatarIconImage;

    [Tooltip("Shown when the player has no avatarId/avatarUrl set yet.")]
    [SerializeField] private Sprite defaultAvatarSprite;

    private void OnEnable()
    {
        if (ProfileManager.Instance != null)
            ProfileManager.Instance.OnAvatarLoaded.AddListener(SetAvatar);

        ApplyCurrentAvatarImmediately();
    }

    private void OnDisable()
    {
        if (ProfileManager.Instance != null)
            ProfileManager.Instance.OnAvatarLoaded.RemoveListener(SetAvatar);
    }

    /// <summary>Covers the case where the avatar was already resolved
    /// BEFORE this icon's scene loaded (e.g. player already logged in and
    /// picked a preset back on the Map scene, then opened Store scene —
    /// this icon needs to show the right avatar immediately, not wait for
    /// the next OnAvatarLoaded fire).</summary>
    private void ApplyCurrentAvatarImmediately()
    {
        PlayerProfile profile = ProfileManager.Instance?.CurrentProfile;

        if (profile == null)
        {
            SetAvatar(defaultAvatarSprite);
            return;
        }

        if (!string.IsNullOrEmpty(profile.avatarId))
        {
            var preset = Resources.Load<Match3.AvatarPresetData>("Avatars/" + profile.avatarId);
            SetAvatar(preset != null ? preset.sprite : defaultAvatarSprite);
        }
        else if (string.IsNullOrEmpty(profile.avatarUrl))
        {
            // No preset, no uploaded photo — show default.
            SetAvatar(defaultAvatarSprite);
        }
        // else: avatarUrl is set but not yet downloaded in THIS scene —
        // OnAvatarLoaded will fire and update it once the download finishes.
    }

    private void SetAvatar(Sprite sprite)
    {
        if (avatarIconImage != null && sprite != null)
            avatarIconImage.sprite = sprite;
    }
}
