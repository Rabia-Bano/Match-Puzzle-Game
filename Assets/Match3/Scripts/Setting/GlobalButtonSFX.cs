using UnityEngine;
using UnityEngine.UI;

public class GlobalButtonSFX : MonoBehaviour
{
    [Tooltip("The SFX key to play on every button click. Matches the key you " +
             "set up in AudioManager's SFX Library.")]
    [SerializeField] private string sfxKey = "button_click";

    [Tooltip("Include inactive/disabled buttons too (e.g. ones inside a popup " +
             "that starts hidden). Usually leave this ON.")]
    [SerializeField] private bool includeInactive = true;

    private void Start()
    {
        Button[] buttons = GetComponentsInChildren<Button>(includeInactive);

        foreach (Button button in buttons)
        {
            Button captured = button;
            captured.onClick.AddListener(() => AudioManager.Instance?.PlaySFX(sfxKey));
        }

        Debug.Log($"[GlobalButtonSFX] Auto-wired '{sfxKey}' to {buttons.Length} button(s) in '{gameObject.scene.name}'.");
    }
}
