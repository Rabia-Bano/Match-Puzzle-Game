// ============================================================
//  BoardController.cs  —  Consolidated turn / cascade orchestrator
//
//  This is now the ONLY place on the board that:
//    • clears matched tiles (with the pop animation)
//    • reports cleared tiles to LevelManager (goals + score)
//    • loops the match -> special -> clear -> gravity -> refill
//      cascade until the board is stable
//
//  Previously this logic was written TWICE (once here, once in
//  BoardRefiller) and had drifted apart:
//    - only the first-level match created a Special tile;
//      cascade matches inside BoardRefiller never did.
//    - board rotation was never re-checked for the new matches
//      it can create, so a rotation could silently leave a valid
//      match sitting on the board.
//  Both are fixed below: ResolveBoard() is the single loop used
//  both for the initial swap-match AND for anything a rotation
//  produces afterwards, and it creates specials on every pass.
//
//  BoardRefiller and GravitySystem are now "dumb" mechanics
//  components — they don't know about scoring or goals at all.
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

        [Header("Clear Animation")]
        [SerializeField] private float clearPopDuration = 0.13f;
        [SerializeField] private float clearStagger     = 0.02f;
        [SerializeField] private int   scorePerTile      = 50;

        [Header("Cascade Safety")]
        [Tooltip("Hard cap on chain-reaction passes so a bad level config can't loop forever.")]
        [SerializeField] private int maxCascadeIterations = 15;

        private bool _turnBusy;

        /// <summary>
        /// True while ANYTHING is still happening on the board — a normal
        /// turn's cascade (swap -> match -> gravity -> refill -> rotation),
        /// a single special-tile activation, or a special+special combo.
        ///
        /// This used to be just the normal-turn flag, which meant it went
        /// false the instant a plain match settled — even while a special
        /// blast or combo triggered from the SAME move was still clearing
        /// tiles and adding score on its own separate coroutine. Anything
        /// that needs to know "is the board 100% done reacting to this move"
        /// (e.g. LevelResultManager deciding when it's safe to read the
        /// final score) should check THIS, not just a normal-turn flag.
        /// </summary>
        public bool IsBusy =>
            _turnBusy
            || (specialActivator    != null && specialActivator.IsRunning)
            || (specialCombinations != null && specialCombinations.IsRunning);

        /// <summary>Fires once per cleared batch (initial match or a cascade step) with the tile count.</summary>
        public System.Action<int> OnTilesCleared;

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

            // Resolve whatever match the swap just created (+ any cascades).
            yield return StartCoroutine(ResolveBoard());

            // Every-5-moves board rotation, then resolve anything IT created.
            if (boardRotation != null && boardRotation.ShouldRotateThisTurn())
            {
                yield return StartCoroutine(boardRotation.RotateBoard90());
                yield return StartCoroutine(ResolveBoard());
            }

            _turnBusy = false;
            inputHandler.SetInputEnabled(true);

            Debug.Log("[BoardController] Turn complete.");
        }

        /// <summary>
        /// Call this after tiles were removed by something OTHER than a normal
        /// match — a special-tile blast (SpecialTileActivator) or a special+special
        /// combo (SpecialCombinations). Those systems decide WHICH tiles to clear
        /// (row/column/3x3/color/etc.) and call ClearTiles() themselves; once done,
        /// they call this to apply gravity, refill the empty cells, and resolve
        /// any matches the new tiles create — using the exact same gravity /
        /// refill / cascade code as a normal swap, instead of each keeping its
        /// own copy.
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
        /// Public so other systems that alter the board directly (a pet power,
        /// a booster, a boss-arena obstacle) can settle the board afterwards
        /// through this exact same path instead of re-implementing it.
        /// </summary>
        public IEnumerator ResolveBoard()
        {
            for (int i = 0; i < maxCascadeIterations; i++)
            {
                List<MatchGroup> matches = matchFinder.FindAllMatches();
                if (matches.Count == 0) yield break;

                foreach (var group in matches)
                    specialFactory.TryCreateSpecial(group, boardGrid);

                yield return StartCoroutine(ClearMatchGroups(matches));
                yield return StartCoroutine(gravitySystem.ApplyGravity());
                yield return StartCoroutine(boardRefiller.RefillEmptyCells());
            }

            Debug.LogWarning("[BoardController] Cascade safety cap hit — " +
                              "check the level for a tile configuration that can never settle.");
        }

        // ─────────────────────────────────────────────────────
        //  SHARED CLEAR LOGIC  (was duplicated in BoardRefiller)
        // ─────────────────────────────────────────────────────

        private IEnumerator ClearMatchGroups(List<MatchGroup> matches)
        {
            var toClear = new HashSet<Tile>();
            foreach (var group in matches)
                foreach (var tile in group.Tiles)
                    if (tile != null && tile.State != TileState.Inactive)
                        toClear.Add(tile);

            yield return StartCoroutine(ClearTiles(toClear));
        }

        /// <summary>
        /// Clears the given tiles with the pop animation, reports every
        /// non-special tile to LevelManager for goal tracking, and adds
        /// score once for the whole batch. This is the ONLY method in the
        /// project that should ever do this — anything that needs to clear
        /// tiles (specials, boosters, pet powers) should call this instead
        /// of writing its own clear + score logic.
        /// </summary>
        public IEnumerator ClearTiles(IEnumerable<Tile> tiles)
        {
            int cleared = 0;

            foreach (Tile tile in tiles)
            {
                if (tile == null) continue;
                if (boardGrid.GetTile(tile.GridX, tile.GridY) != tile) continue;

                TileData tileData = tile.Data;
                if (tileData != null && !tileData.isSpecial)
                    levelManager?.OnTileCleared(tileData);

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
            if (!specialActivator)    Debug.LogWarning("[BoardController] specialActivator not assigned — IsBusy won't account for single special-tile blasts. Results panel may show a score snapshot taken too early.", this);
            if (!specialCombinations) Debug.LogWarning("[BoardController] specialCombinations not assigned — IsBusy won't account for special+special combos. Results panel may show a score snapshot taken too early.", this);
        }
    }
}