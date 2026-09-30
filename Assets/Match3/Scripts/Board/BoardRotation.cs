// ============================================================
//  BoardRotation.cs  —  UPDATED (square + NON-SQUARE boards)
//
//  Fires every N player moves (default 5).
//
//  WHAT CHANGED:
//    Pehle yeh sirf SQUARE board (width == height) par chalta tha —
//    non-square board par rotation skip ho jati thi. Ab:
//
//    • 90° rotation on a W x H board produces an H x W board.
//      (e.g. 6 columns x 8 rows  ->  8 columns x 6 rows)
//      BoardGrid.ApplyRotatedLayout() swaps Width/Height, recomputes
//      the world origin and smoothly re-fits the camera.
//    • 180° rotation keeps the same dimensions.
//    • Blank cells (holes), jelly, hard tiles, stones, frozen tiles
//      all rotate WITH the board — one shared mapping function
//      (BoardRotationMath) is used for tiles, blank mask and jelly,
//      so they can never go out of sync.
//    • FIX: the old logical mapping  new(x,y)=old(y,W-1-x)  was actually
//      COUNTER-clockwise while the pivot animation turned CLOCKWISE, so
//      with usePivotRotation=true tiles visibly "jumped" at the end.
//      Logic and animation now always turn the same direction.
//    • FIX: any Locked tile (hard tile, dropdown stone, boss-frozen tile)
//      keeps its Locked state after rotation — previously only hard tiles
//      were protected, so stones silently became swappable.
//
//  This component only performs the rotation itself — BoardController
//  .TurnRoutine() calls RotateBoard90() and then re-runs ResolveBoard(),
//  so any match the rotation creates is resolved normally.
//
//  Attach to: BoardRotation (empty GameObject)
//  Wire:      boardGrid, (optional) jellyManager
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace Match3
{
    /// <summary>How the board turns each time a rotation triggers.</summary>
    public enum BoardRotationStyle
    {
        Clockwise90        = 0,
        CounterClockwise90 = 1,
        Rotate180          = 2,
        RandomEachTime     = 3    // picks 90° CW / 90° CCW / 180° at random
    }

    /// <summary>
    /// Pure math shared by BoardRotation and JellyManager so every layer of the
    /// board (tiles, blank mask, jelly) rotates with the EXACT same mapping.
    /// quarterTurnsCW: 1 = 90° clockwise, 2 = 180°, 3 = 90° counter-clockwise.
    /// Coordinates: x = column (left→right), y = row (bottom→top).
    /// </summary>
    public static class BoardRotationMath
    {
        public static void NewSize(int oldW, int oldH, int quarterTurnsCW, out int newW, out int newH)
        {
            bool swap = (quarterTurnsCW & 1) == 1;
            newW = swap ? oldH : oldW;
            newH = swap ? oldW : oldH;
        }

        public static Vector2Int Map(int x, int y, int oldW, int oldH, int quarterTurnsCW)
        {
            switch (((quarterTurnsCW % 4) + 4) % 4)
            {
                case 1:  return new Vector2Int(y, oldW - 1 - x);              // 90° CW
                case 2:  return new Vector2Int(oldW - 1 - x, oldH - 1 - y);   // 180°
                case 3:  return new Vector2Int(oldH - 1 - y, x);              // 90° CCW
                default: return new Vector2Int(x, y);
            }
        }

        /// <summary>Rotates any 2-D array with the shared mapping.</summary>
        public static T[,] Rotate<T>(T[,] source, int quarterTurnsCW)
        {
            if (source == null) return null;
            int w = source.GetLength(0), h = source.GetLength(1);
            NewSize(w, h, quarterTurnsCW, out int nw, out int nh);

            var result = new T[nw, nh];
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                Vector2Int n = Map(x, y, w, h, quarterTurnsCW);
                result[n.x, n.y] = source[x, y];
            }
            return result;
        }
    }

    public class BoardRotation : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────

        [Header("References")]
        [SerializeField] private BoardGrid boardGrid;
        [Tooltip("Optional — if a level uses jelly, wire this so the jelly layer rotates WITH the board.")]
        [SerializeField] private JellyManager jellyManager;

        [Header("Rotation Settings")]
        [Tooltip("How many player moves between each board rotation.")]
        [SerializeField] private int movesPerRotation = 5;

        [Tooltip("Direction/amount of each rotation. Works for square AND non-square boards.")]
        [SerializeField] private BoardRotationStyle rotationStyle = BoardRotationStyle.Clockwise90;

        [Tooltip("If true, a NON-square board only ever rotates 180° (keeps its shape, camera never zooms). " +
                 "If false (default), non-square boards rotate 90° too and the board changes shape (6x8 -> 8x6).")]
        [SerializeField] private bool nonSquareUse180Only = false;

        [Tooltip("Duration of the rotation animation (seconds).")]
        [SerializeField] private float rotationDuration = 0.45f;

        [Tooltip("Ease curve for the rotation animation.")]
        [SerializeField] private Ease rotationEase = Ease.InOutQuad;

        [Tooltip("If true, the board pivot GameObject rotates visually (tiles must be its children).\n" +
                 "If false (recommended), each tile swings along an arc to its new world position.")]
        [SerializeField] private bool usePivotRotation = false;

        [Tooltip("Parent Transform that tiles are children of (only used when usePivotRotation = true). " +
                 "Must sit exactly at the board centre (same position as BoardGrid).")]
        [SerializeField] private Transform boardPivot;

        // ── Events ────────────────────────────────────────────

        /// <summary>Fires before the rotation animation starts. Int = rotation count.</summary>
        public System.Action<int> OnBeforeRotation;

        /// <summary>Fires after rotation + grid update complete (matches NOT resolved yet).</summary>
        public System.Action<int> OnAfterRotation;

        // ── Private state ─────────────────────────────────────

        private int _moveCount;
        private int _rotationCount;

        // ─────────────────────────────────────────────────────
        //  PUBLIC API (same names as before — SwapController / LevelHUD / BoardController use these)
        // ─────────────────────────────────────────────────────

        public void RegisterMove() => _moveCount++;

        public bool ShouldRotateThisTurn() =>
            movesPerRotation > 0 && _moveCount > 0 && _moveCount % movesPerRotation == 0;

        public int MoveCount => _moveCount;

        public int MovesUntilRotation =>
            movesPerRotation - (_moveCount % movesPerRotation);

        /// <summary>
        /// Old method name kept so BoardController needs no change. It now performs
        /// whatever rotationStyle says (90° CW / 90° CCW / 180°), on square AND
        /// non-square boards.
        /// </summary>
        public IEnumerator RotateBoard90()
        {
            if (boardGrid == null || boardGrid.Grid == null) yield break;

            int turns = PickQuarterTurns();
            int oldW = boardGrid.Width;
            int oldH = boardGrid.Height;
            BoardRotationMath.NewSize(oldW, oldH, turns, out int newW, out int newH);

            _rotationCount++;
            OnBeforeRotation?.Invoke(_rotationCount);
            Debug.Log($"[BoardRotation] Rotation #{_rotationCount}: {turns * 90}° clockwise " +
                      $"({oldW}x{oldH} -> {newW}x{newH})");

            // 1. Animate
            if (usePivotRotation && boardPivot != null)
            {
                boardPivot.DORotate(new Vector3(0f, 0f, boardPivot.eulerAngles.z - 90f * turns),
                                    rotationDuration, RotateMode.FastBeyond360)
                          .SetEase(rotationEase);
                yield return new WaitForSeconds(rotationDuration);
                boardPivot.rotation = Quaternion.identity;
            }
            else
            {
                yield return StartCoroutine(AnimatePerTile(turns, oldW, oldH, newW, newH));
            }

            // 2. Rotate every logical layer with the SAME mapping
            Tile[,] rotatedTiles = BoardRotationMath.Rotate(boardGrid.Grid, turns);
            bool[,] rotatedBlank = BoardRotationMath.Rotate(boardGrid.BlankMask, turns);
            boardGrid.ApplyRotatedLayout(rotatedTiles, rotatedBlank);
            jellyManager?.RotateLayout(turns);

            // 3. Snap tiles exactly onto the new layout
            SnapTilesToGrid();

            OnAfterRotation?.Invoke(_rotationCount);
            Debug.Log($"[BoardRotation] Rotation #{_rotationCount} complete.");
        }

        // ─────────────────────────────────────────────────────

        private int PickQuarterTurns()
        {
            bool square = boardGrid.Width == boardGrid.Height;
            if (!square && nonSquareUse180Only) return 2;

            switch (rotationStyle)
            {
                case BoardRotationStyle.CounterClockwise90: return 3;
                case BoardRotationStyle.Rotate180:          return 2;
                case BoardRotationStyle.RandomEachTime:     return Random.Range(1, 4);   // 1, 2 or 3
                default:                                    return 1;
            }
        }

        /// <summary>
        /// Each tile swings around the board centre along a circular arc and lands on
        /// its cell in the NEW layout (which may have swapped width/height).
        /// </summary>
        private IEnumerator AnimatePerTile(int turns, int oldW, int oldH, int newW, int newH)
        {
            Vector3 centre     = boardGrid.transform.position;
            float   totalAngle = -90f * turns;           // negative z = clockwise in Unity
            Quaternion fullRot = Quaternion.Euler(0f, 0f, totalAngle);

            var moves = new List<(Tile tile, Vector3 start, Vector3 correction)>();

            for (int x = 0; x < oldW; x++)
            for (int y = 0; y < oldH; y++)
            {
                Tile tile = boardGrid.Grid[x, y];
                if (tile == null) continue;

                Vector2Int n     = BoardRotationMath.Map(x, y, oldW, oldH, turns);
                Vector3    start = tile.transform.position;
                Vector3    end   = boardGrid.GridToWorldForSize(n.x, n.y, newW, newH);
                Vector3    arcEnd = centre + fullRot * (start - centre);
                // correction is ~zero for a centred board; kept so the tile ALWAYS
                // lands exactly on its target even if the pivot isn't perfectly centred.
                moves.Add((tile, start, end - arcEnd));
            }

            float elapsed = 0f;
            while (elapsed < rotationDuration)
            {
                elapsed += Time.deltaTime;
                float t = DOVirtual.EasedValue(0f, 1f, Mathf.Clamp01(elapsed / rotationDuration), rotationEase);
                Quaternion rot = Quaternion.Euler(0f, 0f, totalAngle * t);

                foreach (var (tile, start, correction) in moves)
                {
                    if (tile == null) continue;
                    tile.transform.position = centre + rot * (start - centre) + correction * t;
                }
                yield return null;
            }
        }

        /// <summary>After the logical rotation, snap every tile to its exact world position.</summary>
        private void SnapTilesToGrid()
        {
            for (int x = 0; x < boardGrid.Width; x++)
            for (int y = 0; y < boardGrid.Height; y++)
            {
                Tile tile = boardGrid.GetTile(x, y);
                if (tile == null) continue;
                tile.transform.position = boardGrid.GridToWorld(x, y);

                // Keep ANY locked tile locked (hard tile, dropdown stone, boss-frozen
                // tile). Only tiles that were mid fall/swap go back to Normal.
                bool isHardTile = tile.Data != null && tile.Data.isHardTile;
                bool isStone    = tile.Data != null && tile.Data.isDropStone;
                if (!isHardTile && !isStone && tile.State != TileState.Locked)
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
