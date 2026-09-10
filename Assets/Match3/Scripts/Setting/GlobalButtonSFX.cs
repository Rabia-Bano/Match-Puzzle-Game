// ============================================================
//  GlobalButtonSFX.cs  —  MonoBehaviour
//
//  WHY THIS EXISTS:
//  Wiring "button_click" onto every single Button's OnClick() list by
//  hand, in every scene, is exactly the kind of thing that's easy to
//  forget on one scene and hard to notice until a player tells you —
//  which is precisely what happened (worked in GameBoardScene, silent
//  everywhere else, because that was the only scene it was manually
//  wired in).
//
//  This script removes the manual step entirely: attach it ONCE to
//  each scene's root Canvas (or any parent that contains all the
//  buttons you want sound on), and at Start() it finds every UI
//  Button underneath it — including ones nested inside panels/popups —
//  and subscribes "button_click" to each one automatically. No more
//  per-button Inspector wiring, so no more scenes silently missing it.
//
//  IMPORTANT — if you already manually wired PlaySFX("button_click")
//  on some buttons' OnClick() lists (e.g. in GameBoardScene, since
//  that's the one that already worked), REMOVE those manual entries
//  once this script is on that scene's Canvas too — otherwise those
//  specific buttons will play the click sound TWICE per tap.
//
//  LIMITATION: only wires buttons that already exist in the scene at
//  Start() time. Buttons instantiated LATER at runtime (e.g. a Store
//  item list built from Firestore data, or dynamically-spawned Map
//  level nodes) won't be auto-wired by this — for those, call
//  AudioManager.Instance?.PlaySFX("button_click") directly in the
//  code that handles that button's click (most of those already call
//  PlaySFX for other reasons, e.g. StoreManager's buy button, so it's
//  usually a one-line addition right next to that existing logic).
//
//  Attach to: the root Canvas (or a "UICanvas" GameObject) in EVERY
//  scene that has clickable buttons — MapScene, StoreScene,
//  SettingScene, LoginScene, ProfilePanel, BossArenaScene, etc.
// ============================================================

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
            // Capture safely per-button (avoids the classic C# closure bug
            // where every listener would end up referencing the SAME last
            // button if 'button' were used directly inside the lambda).
            Button captured = button;
            captured.onClick.AddListener(() => AudioManager.Instance?.PlaySFX(sfxKey));
        }

        Debug.Log($"[GlobalButtonSFX] Auto-wired '{sfxKey}' to {buttons.Length} button(s) in '{gameObject.scene.name}'.");
    }
}
