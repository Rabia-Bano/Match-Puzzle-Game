// ============================================================
//  BoardController.cs  —  Consolidated turn / cascade orchestrator
//
//  This is now the ONLY place on the board that:
//    • clears matched tiles (with the pop animation)
//    • reports cleared tiles to LevelManager (goals + score)
//    • loops the match -> special -> clear -> gravity -> refill
//      cascade until the board is stable
//
//  REDESIGN (bug report ke baad — hard tile damage):
//    Pehle ClearTiles() ke andar NORMAL tile clear hone par
//    hardTileManager.DamageAdjacent() call hoti thi — jo us cleared
//    tile ke UP/DOWN/LEFT/RIGHT wali hard tile ko damage deti thi,
//    chahe blast/combo ka target khud hard tile ki cell na ho.
//    Ab yeh "adjacency damage" mechanic bilkul hata di gayi hai.
//    Hard tile ab sirf tab damage leta hai jab caller (SpecialTileActivator,
//    SpecialCombinations, ya ek pet skill) ne apni target list mein
//    hard tile ka apna cell seedha shamil kiya ho — DIRECT hit — aur
//    canDamageHardTiles:true pass kiya ho. Isliye hardTileManager
//    field aur brokenHardTiles queue ab yahan zaroorat nahi rahi.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace Match3
{
    public class BoardController : MonoBehaviour
    {
        [Header("Core References")]
        [SerializeField] private BoardGrid          boardGrid;
        [SerializeField] private InputHandler       inputHandler;
        [SerializeField] private MatchFinder        matchFinder;
        [SerializeField] private GravitySystem      gravitySystem;
        [SerializeField] private BoardRefiller      boardRefiller;
        [SerializeField] private BoardRotation      boardRotation;
        [SerializeField] private SpecialTileFactory specialFactory;
        [SerializeField] private LevelManager       levelManager;

        [Header("Related Systems (used only to make IsBusy accurate)")]
        [Tooltip("Optional, but assign it — without this, IsBusy goes false while a single " +
                 "special-tile blast is still animating, so the win/lose panel can appear " +
                 "with a score that doesn't include that blast yet.")]
        [SerializeField] private SpecialTileActivator specialActivator;
        [Tooltip("Optional, but assign it — same reason as specialActivator above, for special+special combos.")]
        [SerializeField] private SpecialCombinations  specialCombinations;

        [Header("Obstacle Systems (optional — leave blank if a level uses none of these)")]
        [Tooltip("Hard tiles no longer need a manager reference here — damage is applied " +
                 "directly on the Tile instance passed into ClearTiles().")]
        [SerializeField] private JellyManager    jellyManager;
        [SerializeField] private StoneManager    stoneManager;

        [Header("Clear Animation")]
        [SerializeField] private float clearPopDuration = 0.13f;
        [SerializeField] private float clearStagger     = 0.02f;
        [SerializeField] private int   scorePerTile      = 50;

        [Header("Cascade Safety")]
        [Tooltip("Hard cap on chain-reaction passes so a bad level config can't loop forever.")]
        [SerializeField] private int maxCascadeIterations = 15;

        private bool _turnBusy;

        public bool IsBusy =>
            _turnBusy
            || (specialActivator    != null && specialActivator.IsRunning)
            || (specialCombinations != null && specialCombinations.IsRunning);

        public System.Action<int> OnTilesCleared;
        public System.Action<int> OnMatchGroupResolved;

        // ─────────────────────────────────────────────────────
        //  PUBLIC API  (unchanged — SwapController depends on this)
        // ─────────────────────────────────────────────────────

        public void ProcessTurn()
        {
            if (IsBusy) return;
            StartCoroutine(TurnRoutine());
        }

        public void OnSwapFailed()
        {
            _turnBusy = false;
            inputHandler.SetInputEnabled(true);
        }

        public bool HasMatches() => matchFinder.FindAllMatches().Count > 0;

        // ─────────────────────────────────────────────────────
        //  MAIN TURN FLOW
        // ─────────────────────────────────────────────────────

        private IEnumerator TurnRoutine()
        {
            _turnBusy = true;
            inputHandler.SetInputEnabled(false);

            jellyManager?.BeginTurn();

            yield return StartCoroutine(ResolveBoard());

            if (boardRotation != null && boardRotation.ShouldRotateThisTurn())
            {
                yield return StartCoroutine(boardRotation.RotateBoard90());
                yield return StartCoroutine(ResolveBoard());
            }

            jellyManager?.WanderUnclearedJelly();

            _turnBusy = false;
            inputHandler.SetInputEnabled(true);

            Debug.Log("[BoardController] Turn complete.");
        }

        /// <summary>
        /// Call this after tiles were removed by something OTHER than a normal
        /// match — a special-tile blast (SpecialTileActivator) or a special+special
        /// combo (SpecialCombinations). Applies gravity, refills, and resolves
        /// any matches the new tiles create.
        /// </summary>
        public IEnumerator SettleAfterExternalClear()
        {
            yield return StartCoroutine(gravitySystem.ApplyGravity());
            yield return StartCoroutine(boardRefiller.RefillEmptyCells());
            yield return StartCoroutine(ResolveBoard());
        }

        /// <summary>
        /// The single cascade loop: find matches -> turn qualifying groups into
        /// special tiles -> clear -> gravity -> refill -> repeat until stable.
        /// </summary>
        public IEnumerator ResolveBoard()
        {
            for (int i = 0; i < maxCascadeIterations; i++)
            {
                List<MatchGroup> matches = matchFinder.FindAllMatches();
                List<Tile> stonesAtBottom = stoneManager != null
                    ? stoneManager.GetStonesAtBottomRow()
                    : EmptyTileList;
                bool hasEmptyCells = BoardHasEmptyCells();

                if (matches.Count == 0 && stonesAtBottom.Count == 0 && !hasEmptyCells) yield break;

                if (matches.Count > 0)
                {
                    foreach (var group in matches)
                    {
                        // FIX: SpecialTileFactory.TryCreateSpecial() removes the
                        // pivot tile from group.Tiles when it turns this match
                        // into a special (4-line/5-line/T/L). Reading
                        // group.Tiles.Count AFTER that call under-reports the
                        // real match size by 1 for every match that spawns a
                        // special — which silently under-charged PetManager's
                        // battery (e.g. a 4-match reported as 3 → only +5%
                        // instead of +10%). Capture the true size first.
                        int matchSize = group.Tiles.Count;
                        specialFactory.TryCreateSpecial(group, boardGrid);
                        OnMatchGroupResolved?.Invoke(matchSize);
                    }
                    yield return StartCoroutine(ClearMatchGroups(matches));
                }

                if (stonesAtBottom.Count > 0)
                    yield return StartCoroutine(ClearTiles(stonesAtBottom, allowStoneCollection: true));

                yield return StartCoroutine(gravitySystem.ApplyGravity());
                yield return StartCoroutine(boardRefiller.RefillEmptyCells());
            }

            Debug.LogWarning("[BoardController] Cascade safety cap hit — " +
                              "check the level for a tile configuration that can never settle.");
        }

        private bool BoardHasEmptyCells()
        {
            for (int x = 0; x < boardGrid.Width; x++)
            for (int y = 0; y < boardGrid.Height; y++)
                if (boardGrid.GetTile(x, y) == null) return true;
            return false;
        }

        private static readonly List<Tile> EmptyTileList = new();

        // ─────────────────────────────────────────────────────

        private IEnumerator ClearMatchGroups(List<MatchGroup> matches)
        {
            var toClear = new HashSet<Tile>();
            foreach (var group in matches)
                foreach (var tile in group.Tiles)
                    if (tile != null && tile.State != TileState.Inactive)
                        toClear.Add(tile);

            // Plain colour matches NEVER damage hard tiles — MatchFinder already
            // excludes Locked hard tiles from match groups, so canDamageHardTiles
            // stays false (the default) here.
            yield return StartCoroutine(ClearTiles(toClear));
        }

        /// <summary>
        /// Clears the given tiles with the pop animation, reports every
        /// non-special tile to LevelManager for goal tracking, and adds
        /// score once for the whole batch. This is the ONLY method in the
        /// project that should ever do this — anything that needs to clear
        /// tiles (specials, boosters, pet powers) should call this instead
        /// of writing its own clear + score logic.
        ///
        /// Handles FOUR kinds of tile it might find in the list:
        ///   • Special tile      → chain-fires its blast
        ///   • Hard tile         → this cell was DIRECTLY inside the caller's
        ///                         own target area (a special blast's row/
        ///                         column/3x3/5x5/colour-sweep, or a pet skill's
        ///                         tile list) — takes exactly 1 point of damage
        ///                         right here via Tile.DamageObstacle(). If that
        ///                         breaks it, reports GoalTracker.OnHardTileCleared()
        ///                         and clears it. If it survives, it's simply
        ///                         left in place (already showed its own crack-
        ///                         sprite + punch-scale feedback). It is NEVER
        ///                         damaged just for being next to something else
        ///                         that cleared.
        ///   • Dropdown stone    → only ever arrives here from ResolveBoard()'s
        ///                         "reached the bottom row" check
        ///   • Normal colour tile → reports OnTileCleared() + jelly decrement
        /// </summary>
        /// <param name="canDamageHardTiles">
        /// Pass true ONLY when this list comes from a special-tile blast, a
        /// special+special combo, or a pet skill's own target area — i.e.
        /// whenever a hard tile appearing IN this list means its cell was
        /// deliberately, directly targeted. Plain colour matches (the
        /// default, false) never include a hard tile in their list at all
        /// (MatchFinder excludes Locked tiles) — this is just a safety guard.
        /// </param>
        /// <param name="allowStoneCollection">
        /// Pass true ONLY from ResolveBoard()'s "stone reached the bottom
        /// row" check. Dropdown stones are IMMUNE to every other clear source.
        /// </param>
        public IEnumerator ClearTiles(IEnumerable<Tile> tiles, bool canDamageHardTiles = false, bool allowStoneCollection = false)
        {
            int cleared = 0;
            var chainedSpecials = new List<Tile>();

            foreach (Tile tile in tiles)
            {
                if (tile == null) continue;
                if (boardGrid.GetTile(tile.GridX, tile.GridY) != tile) continue;

                TileData tileData = tile.Data;

                // ── Special tile → chain-fire its own blast ──
                if (tileData != null && tileData.isSpecial)
                {
                    if (specialActivator != null)
                    {
                        chainedSpecials.Add(tile);
                    }
                    else
                    {
                        tile.SetState(TileState.Matched);
                        boardGrid.RemoveTile(tile.GridX, tile.GridY);
                        cleared++;
                        tile.transform.DOScale(Vector3.zero, clearPopDuration)
                            .SetEase(Ease.InBack)
                            .OnComplete(() => tile.transform.localScale = Vector3.one);
                        yield return new WaitForSeconds(clearStagger);
                    }
                    continue;
                }

                // ── Hard tile → this cell was a DIRECT hit ──
                if (tileData != null && tileData.isHardTile)
                {
                    if (!canDamageHardTiles)
                    {
                        // Shouldn't normally happen (MatchFinder excludes Locked
                        // tiles from plain matches) — stay safe, leave it untouched.
                        continue;
                    }

                    bool broke = tile.DamageObstacle();
                    if (!broke)
                    {
                        // Took 1 damage, still standing — DamageObstacle() already
                        // updated its crack sprite + punch-scale feedback. Leave it.
                        continue;
                    }

                    levelManager?.OnHardTileCleared();
                    tile.SetState(TileState.Matched);
                    boardGrid.RemoveTile(tile.GridX, tile.GridY);
                    cleared++;
                    tile.transform.DOScale(Vector3.zero, clearPopDuration)
                        .SetEase(Ease.InBack)
                        .OnComplete(() => tile.transform.localScale = Vector3.one);
                    yield return new WaitForSeconds(clearStagger);
                    continue;
                }

                // ── Dropdown stone ──
                if (tileData != null && tileData.isDropStone)
                {
                    if (!allowStoneCollection)
                        continue; // immune to this clear source, only gravity collects it

                    Debug.Log($"[BoardController] Stone collected at ({tile.GridX},{tile.GridY}).");
                    levelManager?.OnStoneCollected();
                    tile.SetState(TileState.Matched);
                    boardGrid.RemoveTile(tile.GridX, tile.GridY);
                    cleared++;
                    tile.transform.DOScale(Vector3.zero, clearPopDuration)
                        .SetEase(Ease.InBack)
                        .OnComplete(() => tile.transform.localScale = Vector3.one);
                    yield return new WaitForSeconds(clearStagger);
                    continue;
                }

                // ── Normal colour tile ──
                levelManager?.OnTileCleared(tileData);

                if (jellyManager != null && jellyManager.DecrementAt(tile.GridX, tile.GridY))
                    levelManager?.OnJellyCleared();

                tile.SetState(TileState.Matched);
                boardGrid.RemoveTile(tile.GridX, tile.GridY);
                cleared++;

                tile.transform.DOScale(Vector3.zero, clearPopDuration)
                    .SetEase(Ease.InBack)
                    .OnComplete(() => tile.transform.localScale = Vector3.one);

                yield return new WaitForSeconds(clearStagger);
            }

            yield return new WaitForSeconds(clearPopDuration + 0.05f);

            if (cleared > 0)
            {
                OnTilesCleared?.Invoke(cleared);
                levelManager?.AddScore(cleared * scorePerTile);
            }

            foreach (Tile special in chainedSpecials)
            {
                if (boardGrid.GetTile(special.GridX, special.GridY) != special) continue;
                yield return StartCoroutine(specialActivator.ChainActivate(special));
            }
        }

        // ─────────────────────────────────────────────────────

        private void Awake()
        {
            if (!boardGrid)      Debug.LogError("[BoardController] boardGrid missing!",      this);
            if (!inputHandler)   Debug.LogError("[BoardController] inputHandler missing!",   this);
            if (!matchFinder)    Debug.LogError("[BoardController] matchFinder missing!",    this);
            if (!gravitySystem)  Debug.LogError("[BoardController] gravitySystem missing!",  this);
            if (!boardRefiller)  Debug.LogError("[BoardController] boardRefiller missing!",  this);
            if (!specialFactory) Debug.LogError("[BoardController] specialFactory missing!", this);
            if (!levelManager)   Debug.LogError("[BoardController] levelManager missing!",   this);
            if (!boardRotation)  Debug.LogWarning("[BoardController] boardRotation not assigned — rotation feature disabled.", this);
            if (!specialActivator)    Debug.LogWarning("[BoardController] specialActivator not assigned — IsBusy won't account for single special-tile blasts.", this);
            if (!specialCombinations) Debug.LogWarning("[BoardController] specialCombinations not assigned — IsBusy won't account for special+special combos.", this);
            if (!jellyManager)    Debug.Log("[BoardController] jellyManager not assigned — jelly obstacles disabled (fine if this level doesn't use them).", this);
            if (!stoneManager)    Debug.Log("[BoardController] stoneManager not assigned — dropdown stone obstacles disabled (fine if this level doesn't use them).", this);
        }
    }
}