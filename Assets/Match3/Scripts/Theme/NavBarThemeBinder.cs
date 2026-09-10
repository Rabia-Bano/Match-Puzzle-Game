using UnityEngine;
using UnityEngine.UI;

namespace Match3.Theme
{
    /// <summary>
    /// Attach to BottomBarPanel in every scene that has it (Map, BossArena, PetCompanion,
    /// Store, LeaderBoard, Setting). Same 6 icons everywhere, so one reusable component
    /// instead of repeating this in every scene applier.
    /// </summary>
    public class NavBarThemeBinder : MonoBehaviour, IThemeApplier
    {
        [SerializeField] private Image mapIcon;
        [SerializeField] private Image bossArenaIcon;
        [SerializeField] private Image petsIcon;
        [SerializeField] private Image storeIcon;
        [SerializeField] private Image leaderBoardIcon;
        [SerializeField] private Image settingIcon;

        public void Apply(ThemeData theme)
        {
            if (theme == null) return;
            if (mapIcon && theme.mapIcon != null) mapIcon.sprite = theme.mapIcon;
            if (bossArenaIcon && theme.bossArenaIcon != null) bossArenaIcon.sprite = theme.bossArenaIcon;
            if (petsIcon && theme.petsIcon != null) petsIcon.sprite = theme.petsIcon;
            if (storeIcon && theme.storeIcon != null) storeIcon.sprite = theme.storeIcon;
            if (leaderBoardIcon && theme.leaderBoardIcon != null) leaderBoardIcon.sprite = theme.leaderBoardIcon;
            if (settingIcon && theme.settingIcon != null) settingIcon.sprite = theme.settingIcon;
        }
    }
}