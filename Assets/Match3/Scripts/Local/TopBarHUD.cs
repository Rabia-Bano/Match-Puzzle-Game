using UnityEngine;
using TMPro;

public class TopBarHUD : MonoBehaviour
{
    [Header("Wire these from Hierarchy")]
    [SerializeField] private TMP_Text coinsText;
    [SerializeField] private TMP_Text livesText;

    [Header("Lives Regen Countdown (optional)")]
    [Tooltip("Small text near the heart icon showing time until the next life " +
             "(e.g. \"4 min\"). Leave empty if you don't want this shown. " +
             "Automatically hides itself when lives are full.")]
    [SerializeField] private TMP_Text livesTimerText;

    [Tooltip("The TimerImage GameObject (clock icon) that TimerText sits inside. " +
             "Whole thing shows/hides together with the countdown — leave empty " +
             "to just use livesTimerText's own parent automatically.")]
    [SerializeField] private GameObject livesTimerContainer;

    private void OnEnable()
    {
        Refresh();
        LocalSaveManager.OnProfileChanged += HandleProfileChanged;

        if (LivesManager.Instance != null)
        {
            LivesManager.Instance.OnLivesChanged += HandleLivesChanged;
            LivesManager.Instance.RaiseCurrentState();
        }
    }

    private void OnDisable()
    {
        LocalSaveManager.OnProfileChanged -= HandleProfileChanged;

        if (LivesManager.Instance != null)
            LivesManager.Instance.OnLivesChanged -= HandleLivesChanged;
    }

    private void Update()
    {
        if (livesTimerText == null || LivesManager.Instance == null) return;

        string countdown = LivesManager.Instance.NextLifeCountdownText;
        bool shouldShow = !string.IsNullOrEmpty(countdown);

        GameObject target = livesTimerContainer != null
            ? livesTimerContainer
            : livesTimerText.transform.parent != null
                ? livesTimerText.transform.parent.gameObject
                : livesTimerText.gameObject;

        if (target.activeSelf != shouldShow)
            target.SetActive(shouldShow);

        if (shouldShow) livesTimerText.text = countdown;
    }

    private void HandleLivesChanged(int current, int max)
    {
        if (livesText != null) livesText.text = current.ToString();
    }

    private void HandleProfileChanged(PlayerProfile profile) => Refresh(profile);

    public void Refresh() => Refresh(LocalSaveManager.GetOrLoadProfile());

    private void Refresh(PlayerProfile profile)
    {
        if (coinsText != null)
            coinsText.text = (profile?.coins ?? 0).ToString("N0");

        if (livesText != null)
            livesText.text = (LivesManager.Instance?.CurrentLives ?? 0).ToString();
    }
}
