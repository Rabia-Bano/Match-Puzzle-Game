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

        public System.Action<TileColor, int> OnColorMatchResolved;

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

        public void CheckForMatchesAfterExternalChange()
        {
            StartCoroutine(ResolveAfterExternalChangeRoutine());
        }

        private IEnumerator ResolveAfterExternalChangeRoutine()
        {
            while (IsBusy) yield return null;

            _turnBusy = true;
            inputHandler.SetInputEnabled(false);

            yield return StartCoroutine(ResolveBoard());

            _turnBusy = false;
            inputHandler.SetInputEnabled(true);
        }

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

        public IEnumerator SettleAfterExternalClear()
        {
            yield return StartCoroutine(gravitySystem.ApplyGravity());
            yield return StartCoroutine(boardRefiller.RefillEmptyCells());
            yield return StartCoroutine(ResolveBoard());
        }

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
                        int matchSize = group.Tiles.Count;
                        TileColor matchColor = (group.Tiles.Count > 0 && group.Tiles[0].Data != null)
                            ? group.Tiles[0].Data.color
                            : TileColor.None;

                        TileData pivotClearedData = specialFactory.TryCreateSpecial(group, boardGrid, out int pivotX, out int pivotY);
                        if (pivotClearedData != null)
                        {
                            levelManager?.OnTileCleared(pivotClearedData);
                            if (jellyManager != null && jellyManager.DecrementAt(pivotX, pivotY))
                                levelManager?.OnJellyCleared();
                        }

                        OnMatchGroupResolved?.Invoke(matchSize);
                        OnColorMatchResolved?.Invoke(matchColor, matchSize);
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
                if (boardGrid.IsPlayable(x, y) && boardGrid.GetTile(x, y) == null) return true;
            return false;
        }

        private static readonly List<Tile> EmptyTileList = new();

        private IEnumerator ClearMatchGroups(List<MatchGroup> matches)
        {
            var toClear = new HashSet<Tile>();
            foreach (var group in matches)
                foreach (var tile in group.Tiles)
                    if (tile != null && tile.State != TileState.Inactive)
                        toClear.Add(tile);

            yield return StartCoroutine(ClearTiles(toClear));
        }

        public IEnumerator ClearTiles(IEnumerable<Tile> tiles, bool canDamageHardTiles = false, bool allowStoneCollection = false, bool isExternalClear = false)
        {
            int cleared = 0;
            var chainedSpecials = new List<Tile>();

            foreach (Tile tile in tiles)
            {
                if (tile == null) continue;
                if (boardGrid.GetTile(tile.GridX, tile.GridY) != tile) continue;

                TileData tileData = tile.Data;

                if (tileData != null && tileData.isSpecial)
                {
                    AudioManager.Instance?.PlaySFX("special_activate");
                    tile.GetComponent<TileVisualController>()?.PlaySpecialBurst();
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

                if (tileData != null && tileData.isHardTile)
                {
                    if (!canDamageHardTiles)
                    {
                        continue;
                    }

                    bool broke = tile.DamageObstacle();
                    if (!broke)
                    {
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

                if (tileData != null && tileData.isDropStone)
                {
                    if (!allowStoneCollection)
                        continue;

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

                levelManager?.OnTileCleared(tileData);
                AudioManager.Instance?.PlaySFX("tile_match");
                tile.GetComponent<TileVisualController>()?.PlayMatchBurst();

                if (jellyManager != null && jellyManager.DecrementAt(tile.GridX, tile.GridY))
                    levelManager?.OnJellyCleared();

                if (isExternalClear && tileData != null)
                    BossDamageEvents.OnSpecialTileCleared?.Invoke(tileData.color);

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

        private void OnEnable()
        {
            if (boardRotation != null) boardRotation.OnBeforeRotation += HandleBeforeRotation;
        }

        private void OnDisable()
        {
            if (boardRotation != null) boardRotation.OnBeforeRotation -= HandleBeforeRotation;
        }

        private void HandleBeforeRotation(int count)
        {
            AudioManager.Instance?.PlaySFX("board_rotate");
            JuiceManager.Instance?.Shake(0.3f, 0.1f);
        }

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
