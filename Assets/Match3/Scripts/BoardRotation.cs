// ============================================================
//  BoardRotation.cs
//
//  Fires every N player moves (default 5).
//  Rotates the LOGICAL grid array 90 degrees clockwise.
//  Rotates all tile GameObjects in world space via DOTween.
//  Updates every Tile's stored GridPosition after rotation.
//
//  This component only performs the rotation itself — it does NOT
//  check for new matches afterwards. BoardController.TurnRoutine()
//  calls RotateBoard90() and then re-runs its ResolveBoard() cascade
//  loop, so any match the rotation creates is found and cleared the
//  same way a normal swap-match would be.
//
//  Clockwise 90-degree matrix transform:
//    newGrid[x][y] = oldGrid[y][W-1-x]
//
//  Attach to: BoardRotation (empty GameObject)
//  Wire:      boardGrid
// ============================================================

using System.Collections;
using UnityEngine;
using DG.Tweening;

namespace Match3
{
    public class BoardRotation : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────

        [Header("References")]
        [SerializeField] private BoardGrid boardGrid;

        [Header("Rotation Settings")]
        [Tooltip("How many player moves between each board rotation.")]
        [SerializeField] private int movesPerRotation = 5;

        [Tooltip("DOTween duration for the full 90-degree rotation animation.")]
        [SerializeField] private float rotationDuration = 0.4f;

        [Tooltip("Ease curve for the rotation animation.")]
        [SerializeField] private Ease rotationEase = Ease.InOutQuad;

        [Tooltip("If true, the board pivot GameObject rotates visually.\n" +
                 "If false, each tile tweens to its new world position individually.")]
        [SerializeField] private bool usePivotRotation = true;

        [Tooltip("The parent Transform that tiles are children of (for pivot rotation).\n" +
                 "If null, falls back to per-tile position tween.")]
        [SerializeField] private Transform boardPivot;

        // ── Events ────────────────────────────────────────────

        /// <summary>Fires before the rotation animation starts. Int = rotation count.</summary>
        public System.Action<int> OnBeforeRotation;

        /// <summary>Fires after rotation + grid update complete (matches have NOT been resolved yet).</summary>
        public System.Action<int> OnAfterRotation;

        // ── Private state ─────────────────────────────────────

        private int _moveCount;
        private int _rotationCount;

        // ─────────────────────────────────────────────────────
        //  PUBLIC API
        // ─────────────────────────────────────────────────────

        /// <summary>Call this once per successful player move (SwapController does this).</summary>
        public void RegisterMove() => _moveCount++;

        /// <summary>True if this move should trigger a rotation. BoardController checks this after each turn.</summary>
        public bool ShouldRotateThisTurn() =>
            _moveCount > 0 && _moveCount % movesPerRotation == 0;

        /// <summary>How many moves have been made so far.</summary>
        public int MoveCount => _moveCount;

        /// <summary>Moves remaining until the next rotation (handy for a UI hint).</summary>
        public int MovesUntilRotation =>
            movesPerRotation - (_moveCount % movesPerRotation);

        // ─────────────────────────────────────────────────────
        //  ROTATE BOARD 90 DEGREES CLOCKWISE
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// Full rotation coroutine:
        ///   1. Fire OnBeforeRotation
        ///   2. Animate tiles (pivot or per-tile)
        ///   3. Rotate logical grid array clockwise
        ///   4. Snap tiles to exact new world positions
        ///   5. Fire OnAfterRotation
        /// </summary>
        public IEnumerator RotateBoard90()
        {
            _rotationCount++;
            OnBeforeRotation?.Invoke(_rotationCount);
            Debug.Log($"[BoardRotation] Rotation #{_rotationCount} starting...");

            if (usePivotRotation && boardPivot != null)
            {
                boardPivot.DORotate(
                    new Vector3(0f, 0f, boardPivot.eulerAngles.z - 90f),
                    rotationDuration,
                    RotateMode.Fast)
                    .SetEase(rotationEase);

                yield return new WaitForSeconds(rotationDuration);

                // Reset pivot rotation so future rotations don't compound angle offsets.
                boardPivot.rotation = Quaternion.identity;
            }
            else
            {
                yield return StartCoroutine(AnimatePerTile());
            }

            RotateGridClockwise();
            SnapTilesToGrid();

            OnAfterRotation?.Invoke(_rotationCount);
            Debug.Log($"[BoardRotation] Rotation #{_rotationCount} complete.");
        }

        // ─────────────────────────────────────────────────────
        //  LOGICAL GRID ROTATION  (clockwise 90 degrees)
        // ─────────────────────────────────────────────────────

        private void RotateGridClockwise()
        {
            int W = boardGrid.Width;
            int H = boardGrid.Height;

            Tile[,] rotated = new Tile[W, H];

            for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
                rotated[x, y] = boardGrid.Grid[y, W - 1 - x];   // clockwise: new(x,y) = old(y, W-1-x)

            for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
                boardGrid.SetTile(x, y, rotated[x, y]);         // also syncs each Tile's GridX/GridY
        }

        // ─────────────────────────────────────────────────────
        //  ANIMATION METHODS
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// Per-tile animation: each tile tweens to its new world position
        /// AFTER the logical rotation. Used when no pivot object is available.
        /// </summary>
        private IEnumerator AnimatePerTile()
        {
            int W = boardGrid.Width;

            var moves = new System.Collections.Generic.List<(Tile tile, Vector3 target)>();

            for (int x = 0; x < W; x++)
            for (int y = 0; y < boardGrid.Height; y++)
            {
                Tile tile = boardGrid.Grid[y, W - 1 - x];   // same mapping as RotateGridClockwise
                if (tile == null) continue;
                moves.Add((tile, boardGrid.GridToWorld(x, y)));
            }

            foreach (var (tile, target) in moves)
                tile.transform.DOMove(target, rotationDuration).SetEase(rotationEase);

            yield return new WaitForSeconds(rotationDuration + 0.05f);
        }

        /// <summary>After logical rotation, snap every tile's transform to its exact world position.</summary>
        private void SnapTilesToGrid()
        {
            for (int x = 0; x < boardGrid.Width; x++)
            for (int y = 0; y < boardGrid.Height; y++)
            {
                Tile tile = boardGrid.GetTile(x, y);
                if (tile == null) continue;
                tile.transform.position = boardGrid.GridToWorld(x, y);
                tile.SetState(TileState.Normal);
            }
        }

        // ── Validation ────────────────────────────────────────

        private void Awake()
        {
            if (!boardGrid)
                Debug.LogError("[BoardRotation] boardGrid not assigned!", this);

            if (usePivotRotation && boardPivot == null)
            {
                Debug.LogWarning("[BoardRotation] usePivotRotation=true but boardPivot is null. " +
                                 "Falling back to per-tile animation.", this);
                usePivotRotation = false;
            }
        }
    }
}