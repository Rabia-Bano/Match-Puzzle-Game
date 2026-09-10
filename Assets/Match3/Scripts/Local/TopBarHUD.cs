// ============================================================
//  TopBarHUD.cs  —  MonoBehaviour
//
//  Drives the Map screen's TopBarPanel (hearts + coins pill).
//
//  UPDATED — lives now come from LivesManager.cs (a real backend that
//  didn't exist before — see that file for the full regen + Firestore
//  sync design), not from a static placeholder. Also shows a small
//  countdown next to the heart icon while lives are regenerating
//  (e.g. "4 min"), which hides itself automatically once lives are full.
//
//  Attach to: TopBarPanel GameObject (Hierarchy: MapScene > UICanvas
//  > TopBarPanel). Drag CoinsPill/CoinsCountText (TMP) into coinsText,
//  LivesPill's count text into livesText, and (optional) a small TMP
//  text near the heart icon into livesTimerText.
// ============================================================

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

    // Countdown text needs to tick every second even when lives AREN'T
    // changing (that's the whole point of a countdown) — so it's polled
    // here in Update() rather than driven by OnLivesChanged, which only
    // fires when the life COUNT itself changes.
    private void Update()
    {
        if (livesTimerText == null || LivesManager.Instance == null) return;

        string countdown = LivesManager.Instance.NextLifeCountdownText;
        bool shouldShow = !string.IsNullOrEmpty(countdown);

        // Toggle the whole TimerImage (icon + text) together — not just the
        // text — so the clock icon doesn't sit there empty once lives are full.
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

    /// <summary>Call manually if you ever need to force an immediate refresh
    /// (e.g. right when the Map scene finishes loading).</summary>
    public void Refresh() => Refresh(LocalSaveManager.GetOrLoadProfile());

    private void Refresh(PlayerProfile profile)
    {
        if (coinsText != null)
            coinsText.text = (profile?.coins ?? 0).ToString("N0");

        if (livesText != null)
            livesText.text = (LivesManager.Instance?.CurrentLives ?? 0).ToString();
    }
}