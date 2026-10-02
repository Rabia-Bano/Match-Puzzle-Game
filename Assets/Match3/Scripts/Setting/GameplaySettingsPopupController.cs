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

            if (_isOpen) Time.timeScale = 1f;
        }

        private void OpenPopup()
        {
            if (popupRoot == null) return;
            AudioManager.Instance?.PlaySFX("button_click");
            popupRoot.SetActive(true);
            _isOpen = true;
            Time.timeScale = 0f;
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
            Time.timeScale = 1f;

            if (isBossArena)
            {
                GameManager.Instance?.ChangeState(GameState.BossArena);
            }
            else
            {
                Game.Firebase.PlayerActivityTracker.Instance?.RecordLevelQuit(LevelSession.CurrentLevelId, "exitButton");

                LivesManager.Instance?.ClearLevelInProgress();

                LivesManager.Instance?.LoseLife();
                GameManager.Instance?.ChangeState(GameState.Map);
            }
        }
    }
}
