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
            SetAvatar(defaultAvatarSprite);
        }
    }

    private void SetAvatar(Sprite sprite)
    {
        if (avatarIconImage != null && sprite != null)
            avatarIconImage.sprite = sprite;
    }
}
