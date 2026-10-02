using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace Match3
{
    public enum BoardRotationStyle
    {
        Clockwise90        = 0,
        CounterClockwise90 = 1,
        Rotate180          = 2,
        RandomEachTime     = 3
    }

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
                case 1:  return new Vector2Int(y, oldW - 1 - x);
                case 2:  return new Vector2Int(oldW - 1 - x, oldH - 1 - y);
                case 3:  return new Vector2Int(oldH - 1 - y, x);
                default: return new Vector2Int(x, y);
            }
        }

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

        public System.Action<int> OnBeforeRotation;

        public System.Action<int> OnAfterRotation;

        private int _moveCount;
        private int _rotationCount;

        public void RegisterMove() => _moveCount++;

        public bool ShouldRotateThisTurn() =>
            movesPerRotation > 0 && _moveCount > 0 && _moveCount % movesPerRotation == 0;

        public int MoveCount => _moveCount;

        public int MovesUntilRotation =>
            movesPerRotation - (_moveCount % movesPerRotation);

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

            Tile[,] rotatedTiles = BoardRotationMath.Rotate(boardGrid.Grid, turns);
            bool[,] rotatedBlank = BoardRotationMath.Rotate(boardGrid.BlankMask, turns);
            boardGrid.ApplyRotatedLayout(rotatedTiles, rotatedBlank);
            jellyManager?.RotateLayout(turns);

            SnapTilesToGrid();

            OnAfterRotation?.Invoke(_rotationCount);
            Debug.Log($"[BoardRotation] Rotation #{_rotationCount} complete.");
        }

        private int PickQuarterTurns()
        {
            bool square = boardGrid.Width == boardGrid.Height;
            if (!square && nonSquareUse180Only) return 2;

            switch (rotationStyle)
            {
                case BoardRotationStyle.CounterClockwise90: return 3;
                case BoardRotationStyle.Rotate180:          return 2;
                case BoardRotationStyle.RandomEachTime:     return Random.Range(1, 4);
                default:                                    return 1;
            }
        }

        private IEnumerator AnimatePerTile(int turns, int oldW, int oldH, int newW, int newH)
        {
            Vector3 centre     = boardGrid.transform.position;
            float   totalAngle = -90f * turns;
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

        private void SnapTilesToGrid()
        {
            for (int x = 0; x < boardGrid.Width; x++)
            for (int y = 0; y < boardGrid.Height; y++)
            {
                Tile tile = boardGrid.GetTile(x, y);
                if (tile == null) continue;
                tile.transform.position = boardGrid.GridToWorld(x, y);

                bool isHardTile = tile.Data != null && tile.Data.isHardTile;
                bool isStone    = tile.Data != null && tile.Data.isDropStone;
                if (!isHardTile && !isStone && tile.State != TileState.Locked)
                    tile.SetState(TileState.Normal);
            }
        }

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
