using UnityEngine;
using UnityEngine.UI;

public class SettingsUIController : MonoBehaviour
{
    [Header("Toggles (assign the 3 checkbox Toggles)")]
    [SerializeField] private Toggle soundToggle;
    [SerializeField] private Toggle musicToggle;
    [SerializeField] private Toggle vibrationToggle;

    private const string PREF_VIBRATION_ON = "vibration_on";

    public static bool VibrationEnabled { get; private set; } = true;

    private void OnEnable()
    {
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

    public static void Vibrate()
    {
        if (!VibrationEnabled) return;
#if UNITY_ANDROID && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }
}
