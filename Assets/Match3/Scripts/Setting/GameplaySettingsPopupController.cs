// ============================================================
//  GameplaySettingsPopupController.cs  —  MonoBehaviour
//
//  The in-gameplay Settings popup (opened from the gear icon that
//  already exists on GameHUD.cs and needs adding to BossArenaHUD.cs).
//  Shows Music / Sound / Vibration toggles (reusing SettingsUIController
//  — put that script on the SAME popup panel, it doesn't care what
//  scene it's in) PLUS an Exit button whose behaviour depends on which
//  scene this popup lives in:
//
//    GameBoardScene (regular level)   → isBossArena = false (Inspector)
//        Exit tapped → LivesManager.LoseLife() → GameManager.ChangeState(Map)
//
//    BossGameBoardScene (boss fight)  → isBossArena = true (Inspector)
//        Exit tapped → NO life lost → GameManager.ChangeState(BossArena)
//        (BossArena = the boss SELECTION list scene, per GameState.cs)
//
//  Attach to: the popup panel's PARENT (or the popup panel itself —
//  either works, see setup guide). One instance per scene; only the
//  isBossArena checkbox differs between the two scenes' copies.
// ============================================================

using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    public class GameplaySettingsPopupController : MonoBehaviour
    {
        [Header("Popup Panel")]
        [Tooltip("The whole popup panel (background + toggles + buttons). Hidden by default.")]
        [SerializeField] private GameObject popupRoot;

        [Header("Buttons")]
        [Tooltip("The small gear/settings icon on the gameplay HUD — opens the popup.")]
        [SerializeField] private Button openButton;

        [Tooltip("Resume/close (X) button INSIDE the popup — closes it without leaving the scene.")]
        [SerializeField] private Button closeButton;

        [Tooltip("Exit button INSIDE the popup — behaviour depends on isBossArena below.")]
        [SerializeField] private Button exitButton;

        [Header("Exit Behaviour")]
        [Tooltip("OFF (unchecked) for GameBoardScene — exiting costs a life, returns to Map.\n" +
                 "ON (checked) for BossGameBoardScene — exiting costs NOTHING, returns to Boss Arena list.")]
        [SerializeField] private bool isBossArena = false;

        private bool _isOpen;

        private void Start()
        {
            if (popupRoot != null) popupRoot.SetActive(false);

            openButton?.onClick.AddListener(OpenPopup);
            closeButton?.onClick.AddListener(ClosePopup);
            exitButton?.onClick.AddListener(HandleExit);
        }

        private void OnDestroy()
        {
            openButton?.onClick.RemoveListener(OpenPopup);
            closeButton?.onClick.RemoveListener(ClosePopup);
            exitButton?.onClick.RemoveListener(HandleExit);

            // Safety: if this popup is destroyed (scene unload) while open,
            // make sure we don't leave the game permanently frozen.
            if (_isOpen) Time.timeScale = 1f;
        }

        private void OpenPopup()
        {
            if (popupRoot == null) return;
            AudioManager.Instance?.PlaySFX("button_click");
            popupRoot.SetActive(true);
            _isOpen = true;
            Time.timeScale = 0f;   // freeze gameplay (board tweens use DOTween's
                                    // own unscaled-time option by default in most
                                    // setups, but stopping player input/board
                                    // logic via timeScale is the simplest, safest
                                    // freeze for a pause popup)
        }

        private void ClosePopup()
        {
            if (popupRoot == null) return;
            AudioManager.Instance?.PlaySFX("button_click");
            popupRoot.SetActive(false);
            _isOpen = false;
            Time.timeScale = 1f;
        }

        private void HandleExit()
        {
            AudioManager.Instance?.PlaySFX("button_click");
            Time.timeScale = 1f;   // always restore before leaving the scene

            if (isBossArena)
            {
                // Boss fight exit — no life lost, back to the boss SELECTION list.
                GameManager.Instance?.ChangeState(GameState.BossArena);
            }
            else
            {
                // Regular level exit — costs a life, back to the Map.
                LivesManager.Instance?.LoseLife();
                GameManager.Instance?.ChangeState(GameState.Map);
            }
        }
    }
}
