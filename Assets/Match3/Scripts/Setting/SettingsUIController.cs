// ============================================================
//  SettingsUIController.cs  —  MonoBehaviour
//
//  Lives in SettingScene. Wires the three checkboxes (Sound / Music /
//  Vibration — see Image 1 in the brief) to AudioManager's persisted
//  settings, plus a local "vibration_on" PlayerPrefs flag (there's no
//  vibration system elsewhere in the project yet, so this owns it).
//
//  Attach to: SettingsPanel (the ice-card GameObject that holds the
//  three rows — see the "Setting Scene UI" build guide below).
//  Wire each Toggle in the Inspector.
// ============================================================

using UnityEngine;
using UnityEngine.UI;

public class SettingsUIController : MonoBehaviour
{
    [Header("Toggles (assign the 3 checkbox Toggles)")]
    [SerializeField] private Toggle soundToggle;      // maps to AudioManager.SFXOn
    [SerializeField] private Toggle musicToggle;      // maps to AudioManager.MusicOn
    [SerializeField] private Toggle vibrationToggle;   // local — no AudioManager equivalent

    private const string PREF_VIBRATION_ON = "vibration_on";

    public static bool VibrationEnabled { get; private set; } = true;

    private void OnEnable()
    {
        // Reflect saved state on the checkboxes WITHOUT re-firing
        // onValueChanged (SetIsOnWithoutNotify) — otherwise opening this
        // screen would immediately re-save the same value it just loaded,
        // and worse, could trigger a stray button_click SFX on open.
        if (AudioManager.Instance != null)
        {
            soundToggle?.SetIsOnWithoutNotify(AudioManager.Instance.SFXOn);
            musicToggle?.SetIsOnWithoutNotify(AudioManager.Instance.MusicOn);
        }

        VibrationEnabled = PlayerPrefs.GetInt(PREF_VIBRATION_ON, 1) == 1;
        vibrationToggle?.SetIsOnWithoutNotify(VibrationEnabled);

        soundToggle?.onValueChanged.AddListener(OnSoundChanged);
        musicToggle?.onValueChanged.AddListener(OnMusicChanged);
        vibrationToggle?.onValueChanged.AddListener(OnVibrationChanged);
    }

    private void OnDisable()
    {
        soundToggle?.onValueChanged.RemoveListener(OnSoundChanged);
        musicToggle?.onValueChanged.RemoveListener(OnMusicChanged);
        vibrationToggle?.onValueChanged.RemoveListener(OnVibrationChanged);
    }

    private void OnSoundChanged(bool isOn)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.SFXOn = isOn;
    }

    private void OnMusicChanged(bool isOn)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.MusicOn = isOn;
    }

    private void OnVibrationChanged(bool isOn)
    {
        VibrationEnabled = isOn;
        PlayerPrefs.SetInt(PREF_VIBRATION_ON, isOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Call this from anywhere (e.g. InputHandler on a successful swipe) to
    /// vibrate the device, respecting the player's toggle.
    /// Example: SettingsUIController.Vibrate();
    /// </summary>
    public static void Vibrate()
    {
        if (!VibrationEnabled) return;
#if UNITY_ANDROID && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }
}
