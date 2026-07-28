// ============================================================
//  TopBarHUD.cs  —  MonoBehaviour
//
//  Drives the Map screen's TopBarPanel (hearts + coins pill).
//  This did NOT exist before — CoinsCountText (TMP) was a static
//  placeholder with no script wired to it at all, which is why
//  the coin balance never updated even though PlayerProfile.coins
//  was correct.
//
//  Attach to: TopBarPanel GameObject (Hierarchy: MapScene > UICanvas
//  > TopBarPanel). Drag CoinsPill/CoinsCountText (TMP) into
//  coinsText, and LivesPill's count text into livesText, in the
//  Inspector.
// ============================================================

using UnityEngine;
using TMPro;

public class TopBarHUD : MonoBehaviour
{
    [Header("Wire these from Hierarchy")]
    [SerializeField] private TMP_Text coinsText;
    [SerializeField] private TMP_Text livesText; // optional — leave empty if lives aren't wired yet

    private void OnEnable()
    {
        Refresh();
        LocalSaveManager.OnProfileChanged += HandleProfileChanged;
    }

    private void OnDisable()
    {
        LocalSaveManager.OnProfileChanged -= HandleProfileChanged;
    }

    private void HandleProfileChanged(PlayerProfile profile) => Refresh(profile);

    /// <summary>Call manually if you ever need to force an immediate refresh
    /// (e.g. right when the Map scene finishes loading).</summary>
    public void Refresh() => Refresh(LocalSaveManager.GetOrLoadProfile());

    private void Refresh(PlayerProfile profile)
    {
        if (coinsText != null)
            coinsText.text = (profile?.coins ?? 0).ToString("N0");

        // Lives aren't part of PlayerProfile yet in this codebase — wire this
        // up once a lives/energy system exists. Left as a safe no-op for now.
        if (livesText != null && profile == null)
            livesText.text = "0";
    }
}