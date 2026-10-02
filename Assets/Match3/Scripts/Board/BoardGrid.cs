using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace Match3
{
    public class BoardGrid : MonoBehaviour
    {
        [Header("Layout")]
        [Tooltip("World-space size of one tile (sprite width/height at 1 unit = 100 px).")]
        [SerializeField] private float cellSize = 1.0f;

        [Tooltip("Gap between cell centres (0 = tiles touch each other).")]
        [SerializeField] private float cellSpacing = 0.05f;

        [Header("Tile Size")]
        [Tooltip("Scales every tile's visual sprite relative to its cell. 1 = full size (fills the cell, current look). Lower it (e.g. 0.8) to make gems visually smaller and add a small gap around each one — this does NOT change cell spacing/positions, only how big the sprite is drawn inside its cell.")]
        [Range(0.3f, 1f)]
        [SerializeField] private float tileVisualScale = 1f;

        public static float TileVisualScale { get; private set; } = 1f;

        [Header("References")]
        [Tooltip("The ObjectPool that hands out Tile GameObjects.")]
        [SerializeField] private ObjectPool tilePool;

        [Header("Camera Fit")]
        [Tooltip("Board is fit into this camera's view. Leave blank to use Camera.main.")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("Extra world-space margin kept around the board so tiles don't touch screen edges.")]
        [SerializeField] private float viewportPadding = 0.5f;

        [Tooltip("NEW — seconds the camera takes to zoom when a non-square board rotates (e.g. 6x8 -> 8x6). 0 = instant.")]
        [SerializeField] private float cameraRefitDuration = 0.35f;

        [Header("Cell Backgrounds (optional)")]
        [Tooltip("NEW — optional sprite prefab (just a SpriteRenderer) drawn UNDER every playable cell. " +
                 "Blank cells get no background, so the board's custom shape is clearly visible. " +
                 "Leave empty if your board already has its own background art.")]
        [SerializeField] private GameObject cellBackgroundPrefab;

        public int Width  { get; private set; }
        public int Height { get; private set; }

        public Tile[,] Grid { get; private set; }

        public bool[,] BlankMask { get; private set; }

        public System.Action OnLayoutChanged;

        private Vector3 _originOffset;
        private readonly List<GameObject> _cellBackgrounds = new();
        private Tween _camTween;

        public void InitializeBoard(int width, int height)
        {
            Width     = width;
            Height    = height;
            Grid      = new Tile[width, height];
            BlankMask = new bool[width, height];

            TileVisualScale = tileVisualScale;

            RecalculateOrigin();

            Debug.Log($"[BoardGrid] Initialized {width}x{height} board. " +
                      $"Origin={_originOffset}  CellStep={cellSize + cellSpacing:F3}");

            FitCameraToBoard(width, height, 0f);
            RebuildCellBackgrounds();
        }

        private void RecalculateOrigin()
        {
            float step = cellSize + cellSpacing;
            _originOffset = transform.position
                - new Vector3((Width  - 1) * step * 0.5f,
                              (Height - 1) * step * 0.5f,
                              0f);
        }

        public void SetBlankCells(IEnumerable<Vector2Int> cells)
        {
            if (BlankMask == null) return;

            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                BlankMask[x, y] = false;

            int count = 0;
            if (cells != null)
            {
                foreach (Vector2Int c in cells)
                {
                    if (!IsInBounds(c.x, c.y))
                    {
                        Debug.LogWarning($"[BoardGrid] Blank cell ({c.x},{c.y}) is outside the {Width}x{Height} board — ignored.");
                        continue;
                    }
                    BlankMask[c.x, c.y] = true;
                    RemoveTile(c.x, c.y);
                    count++;
                }
            }

            if (count > 0) Debug.Log($"[BoardGrid] {count} blank cell(s) applied.");
            RebuildCellBackgrounds();
        }

        public bool IsBlank(int x, int y) =>
            IsInBounds(x, y) && BlankMask != null && BlankMask[x, y];

        public bool IsPlayable(int x, int y) =>
            IsInBounds(x, y) && (BlankMask == null || !BlankMask[x, y]);

        public int PlayableCellCount
        {
            get
            {
                int n = 0;
                for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    if (IsPlayable(x, y)) n++;
                return n;
            }
        }

        public void ApplyRotatedLayout(Tile[,] newGrid, bool[,] newBlank)
        {
            int w = newGrid.GetLength(0);
            int h = newGrid.GetLength(1);
            bool sizeChanged = w != Width || h != Height;

            Width     = w;
            Height    = h;
            Grid      = newGrid;
            BlankMask = newBlank ?? new bool[w, h];

            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                Grid[x, y]?.SetGridPosition(x, y);

            RecalculateOrigin();
            if (sizeChanged) FitCameraToBoard(w, h, cameraRefitDuration);
            RebuildCellBackgrounds();
            OnLayoutChanged?.Invoke();
        }

        public Vector3 GridToWorldForSize(int x, int y, int width, int height)
        {
            float step = cellSize + cellSpacing;
            Vector3 origin = transform.position
                - new Vector3((width - 1) * step * 0.5f, (height - 1) * step * 0.5f, 0f);
            return origin + new Vector3(x * step, y * step, 0f);
        }

        private void RebuildCellBackgrounds()
        {
            foreach (GameObject go in _cellBackgrounds)
                if (go != null) Destroy(go);
            _cellBackgrounds.Clear();

            if (cellBackgroundPrefab == null || Grid == null) return;

            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                if (!IsPlayable(x, y)) continue;
                Vector3 pos = GridToWorld(x, y);
                pos.z += 0.2f;
                GameObject bg = Instantiate(cellBackgroundPrefab, pos, Quaternion.identity, transform);
                bg.name = $"CellBG_{x}_{y}";
                _cellBackgrounds.Add(bg);
            }
        }

        private void FitCameraToBoard(int width, int height, float duration)
        {
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null || !cam.orthographic)
            {
                Debug.LogWarning("[BoardGrid] Camera fit skipped — no orthographic camera assigned.");
                return;
            }

            float step = cellSize + cellSpacing;
            float boardWorldWidth  = width  * step;
            float boardWorldHeight = height * step;
            float screenAspect     = (float)Screen.width / Screen.height;

            float sizeForHeight = (boardWorldHeight * 0.5f) + viewportPadding;
            float sizeForWidth  = ((boardWorldWidth * 0.5f) + viewportPadding) / screenAspect;

            float target = Mathf.Max(sizeForHeight, sizeForWidth);

            if (duration > 0f)
            {
                _camTween?.Kill();
                _camTween = cam.DOOrthoSize(target, duration).SetEase(Ease.InOutQuad);
            }
            else
            {
                cam.orthographicSize = target;
            }
        }

        public Vector3 GridToWorld(int x, int y)
        {
            float step = cellSize + cellSpacing;
            return _originOffset + new Vector3(x * step, y * step, 0f);
        }

        public bool WorldToGrid(Vector3 worldPos, out int x, out int y)
        {
            float step  = cellSize + cellSpacing;
            Vector3 rel = worldPos - _originOffset;

            x = Mathf.RoundToInt(rel.x / step);
            y = Mathf.RoundToInt(rel.y / step);

            return IsInBounds(x, y);
        }

        public Tile SpawnTile(int x, int y, TileData data)
        {
            if (!IsInBounds(x, y))
            {
                Debug.LogWarning($"[BoardGrid] SpawnTile called out of bounds ({x},{y}).");
                return null;
            }

            if (IsBlank(x, y))
            {
                Debug.LogWarning($"[BoardGrid] SpawnTile refused at ({x},{y}) — that cell is BLANK. " +
                                  "Check LevelData: an obstacle position overlaps a blank position.");
                return null;
            }

            Tile tile = tilePool.Get();
            if (tile == null)
            {
                Debug.LogError("[BoardGrid] ObjectPool returned null — consider increasing pool size.");
                return null;
            }

            tile.transform.position = GridToWorld(x, y);
            tile.Initialize(data, x, y);
            Grid[x, y] = tile;

            return tile;
        }

        public void RemoveTile(int x, int y)
        {
            if (!IsInBounds(x, y)) return;

            Tile tile = Grid[x, y];
            if (tile == null) return;

            tile.ResetForPool();
            tilePool.Return(tile);
            Grid[x, y] = null;
        }

        public void SetTile(int x, int y, Tile tile)
        {
            if (!IsInBounds(x, y)) return;
            if (tile != null && IsBlank(x, y))
            {
                Debug.LogWarning($"[BoardGrid] SetTile refused at blank cell ({x},{y}).");
                return;
            }
            Grid[x, y] = tile;
            tile?.SetGridPosition(x, y);
        }

        public void SwapTiles(int ax, int ay, int bx, int by)
        {
            if (!IsInBounds(ax, ay) || !IsInBounds(bx, by)) return;

            Tile tA = Grid[ax, ay];
            Tile tB = Grid[bx, by];

            Grid[ax, ay] = tB;
            Grid[bx, by] = tA;

            tA?.SetGridPosition(bx, by);
            tB?.SetGridPosition(ax, ay);
        }

        public bool IsInBounds(int x, int y) =>
            x >= 0 && x < Width && y >= 0 && y < Height;

        public Tile GetTile(int x, int y) =>
            IsInBounds(x, y) ? Grid[x, y] : null;

        public int GetNeighbours(int x, int y, Tile[] results)
        {
            int count = 0;
            Tile t;

            if ((t = GetTile(x + 1, y)) != null) results[count++] = t;
            if ((t = GetTile(x - 1, y)) != null) results[count++] = t;
            if ((t = GetTile(x, y + 1)) != null) results[count++] = t;
            if ((t = GetTile(x, y - 1)) != null) results[count++] = t;

            return count;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (Grid == null) return;

            Gizmos.color = new Color(0.2f, 0.8f, 0.4f, 0.35f);
            float step = cellSize + cellSpacing;

            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
            {
                Vector3 centre = GridToWorld(x, y);
                if (IsBlank(x, y))
                {
                    Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.35f);
                    Gizmos.DrawLine(centre - new Vector3(cellSize, cellSize) * 0.4f, centre + new Vector3(cellSize, cellSize) * 0.4f);
                    Gizmos.DrawLine(centre - new Vector3(cellSize, -cellSize) * 0.4f, centre + new Vector3(cellSize, -cellSize) * 0.4f);
                    Gizmos.color = new Color(0.2f, 0.8f, 0.4f, 0.35f);
                    continue;
                }
                Gizmos.DrawWireCube(centre, new Vector3(cellSize, cellSize, 0.01f));

                if (Grid[x, y] != null)
                {
                    Gizmos.color = new Color(0.2f, 0.8f, 0.4f, 0.12f);
                    Gizmos.DrawCube(centre, new Vector3(cellSize, cellSize, 0.01f));
                    Gizmos.color = new Color(0.2f, 0.8f, 0.4f, 0.35f);
                }
            }
        }
#endif
    }
}
