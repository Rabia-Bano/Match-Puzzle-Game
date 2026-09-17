// ============================================================
//  BossDamageEvents.cs  —  Static Bridge (not a MonoBehaviour)
//
//  WHY THIS EXISTS:
//  BoardController.OnColorMatchResolved only fires from its OWN
//  match-group resolution loop (a normal 3+ same-colour match).
//  Special tiles (Striped/Wrapped/ColorBomb) and their combos
//  (SpecialCombinations.cs) clear tiles through a COMPLETELY
//  separate direct-clear path (SpecialTileEffect.ClearNormalTileTracked
//  / SpecialCombinations.ClearOneObstacleAwareTile) that never goes
//  back through BoardController's loop — so those clears never fired
//  OnColorMatchResolved, and the boss never took damage from them.
//
//  This static class is a lightweight pub/sub bridge: the two clear
//  paths above call ReportTileCleared() once per NORMAL tile they
//  clear (same as they already do for levelManager?.OnTileCleared),
//  and BossController subscribes to react to it — with zero coupling
//  between SpecialTileEffect/SpecialCombinations and the Boss system.
//  Regular level-play is 100% unaffected: nothing here touches goals,
//  score, or jelly — it's purely an extra notification.
// ============================================================

namespace Match3
{
    public static class BossDamageEvents
    {
        /// <summary>
        /// Fired once per NORMAL (non-special, non-hard, non-drop-stone) tile
        /// cleared by a special-tile blast or combo. Passes that tile's colour.
        /// BossController subscribes and applies weakness-colour damage per
        /// tile — see BossController.HandleSpecialTileCleared().
        /// </summary>
        public static System.Action<TileColor> OnSpecialTileCleared;

        /// <summary>
        /// NEW — fired exactly once per Color Bomb blast (single activation,
        /// NOT the Rainbow+Rainbow combo — that clears the whole board through
        /// a different path). Rabia's request: Color Bomb should ALWAYS damage
        /// the boss at the same tier as a "5+ weakness tiles" hit, regardless
        /// of which colour it actually targeted — every other special tile's
        /// single blast (Striped, Wrapped) does zero boss damage now. See
        /// BossController.HandleColorBombBlast().
        /// </summary>
        public static System.Action OnColorBombBlast;
    }
}