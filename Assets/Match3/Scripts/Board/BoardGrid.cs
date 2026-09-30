// ============================================================
//  BoardGrid.cs  —  MonoBehaviour
//
//  Owns the 2-D array of Tile references and handles:
//    • Board initialisation (size, world-space layout)
//    • Grid <-> World coordinate conversion
//    • Spawning / removing individual tile GameObjects via the ObjectPool
//
//  This is the single source of truth for "what tile is at (x,y)".
//  Every other board script (MatchFinder, GravitySystem, BoardRefiller,
//  BoardRotation, SwapController, InputHandler) reads/writes through
//  this class instead of keeping its own copy of the grid state.
//
//  Place on a dedicated "BoardGrid" GameObject in your game scene.
//  Wire up: tilePool reference in the Inspector.
//
//  UPDATE (Blank Tiles + Rectangular Rotation):
//    • Blank cells — LevelData.blankPositions. A blank cell is a HOLE
//      in the board: no gem, no obstacle, no jelly can ever sit there,
//      the player can't swap into it, and falling tiles pass THROUGH
//      it. Query with IsBlank(x,y) / IsPlayable(x,y).
//    • ApplyRotatedLayout() — lets BoardRotation swap Width/Height
//      (e.g. a 6x8 board becomes 8x6 after a 90° turn). Recomputes the
//      world-space origin and re-fits the camera.
//    • Optional cellBackgroundPrefab — drawn only under PLAYABLE cells,
//      so blank cells visibly look empty (shaped boards).
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace Match3
{
    public class BoardGrid : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────

        [Header("Layout")]
        [Tooltip("World-space size of one tile (sprite width/height at 1 unit = 100 px).")]
        [SerializeField] private float cellSize = 1.0f;

        [Tooltip("Gap between cell centres (0 = tiles touch each other).")]
        [SerializeField] private float cellSpacing = 0.05f;

        [Header("Tile Size")]
        [Tooltip("Scales every tile's visual sprite relative to its cell. 1 = full size (fills the cell, current look). Lower it (e.g. 0.8) to make gems visually smaller and add a small gap around each one — this does NOT change cell spacing/positions, only how big the sprite is drawn inside its cell.")]
        [Range(0.3f, 1f)]
        [SerializeField] private float tileVisualScale = 1f;

        /// <summary>Current board's tile visual scale — read by Tile.cs and
        /// SpecialTileFactory.cs so every tile (regular or special) stays visually
        /// consistent without threading this value through every spawn call.</summary>
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

        // ── Public read-only state ────────────────────────────

        public int Width  { get; private set; }
        public int Height { get; private set; }

        /// <summary>
        /// The authoritative grid. [x, y] where x = column, y = row.
        /// Null means the cell is empty (pending a new tile).
        /// Exposed so tightly-coupled systems (gravity, rotation) can
        /// batch-update it directly during heavy operations — but prefer
        /// SetTile / RemoveTile / SwapTiles below wherever possible.
        /// </summary>
        public Tile[,] Grid { get; private set; }

        /// <summary>NEW — true = this cell is a hole (LevelData.blankPositions).</summary>
        public bool[,] BlankMask { get; private set; }

        /// <summary>NEW — fired after ApplyRotatedLayout() changes the board layout.</summary>
        public System.Action OnLayoutChanged;

        // ── World-space origin ────────────────────────────────

        private Vector3 _originOffset;
        private readonly List<GameObject> _cellBackgrounds = new();
        private Tween _camTween;

        // ── Initialisation ────────────────────────────────────

        /// <summary>
        /// Creates the empty Grid array and calculates the world-space
        /// origin so the board is centred on this transform.
        /// Call this from LevelManager after reading the LevelData asset.
        /// </summary>
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

        // ── Blank cells (NEW) ─────────────────────────────────

        /// <summary>
        /// NEW — marks the given cells as blank holes. Call right after
        /// InitializeBoard() and BEFORE TileSpawner.FillBoard() (LevelManager
        /// does this). Out-of-bounds entries are ignored with a warning.
        /// </summary>
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
                    RemoveTile(c.x, c.y);   // safety: nothing may live in a hole
                    count++;
                }
            }

            if (count > 0) Debug.Log($"[BoardGrid] {count} blank cell(s) applied.");
            RebuildCellBackgrounds();
        }

        /// <summary>True if (x,y) is inside the board AND is a blank hole.</summary>
        public bool IsBlank(int x, int y) =>
            IsInBounds(x, y) && BlankMask != null && BlankMask[x, y];

        /// <summary>True if (x,y) is inside the board and is NOT blank — i.e. a tile may live here.</summary>
        public bool IsPlayable(int x, int y) =>
            IsInBounds(x, y) && (BlankMask == null || !BlankMask[x, y]);

        /// <summary>Number of playable (non-blank) cells on the board.</summary>
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

        // ── Rotation support (NEW) ────────────────────────────

        /// <summary>
        /// NEW — used ONLY by BoardRotation. Replaces the grid + blank mask with
        /// already-rotated arrays whose dimensions may be swapped (W x H -> H x W),
        /// recomputes the world origin and re-fits the camera for the new shape.
        /// Tile transforms are NOT moved here — BoardRotation snaps them afterwards.
        /// </summary>
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

        /// <summary>World position a cell WOULD have on a board of the given size
        /// (used by BoardRotation's per-tile animation before the layout is applied).</summary>
        public Vector3 GridToWorldForSize(int x, int y, int width, int height)
        {
            float step = cellSize + cellSpacing;
            Vector3 origin = transform.position
                - new Vector3((width - 1) * step * 0.5f, (height - 1) * step * 0.5f, 0f);
            return origin + new Vector3(x * step, y * step, 0f);
        }

        // ── Cell backgrounds (NEW, optional) ──────────────────

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
                pos.z += 0.2f;   // behind jelly (0.1) and tiles (0)
                GameObject bg = Instantiate(cellBackgroundPrefab, pos, Quaternion.identity, transform);
                bg.name = $"CellBG_{x}_{y}";
                _cellBackgrounds.Add(bg);
            }
        }

        /// <summary>
        /// NEW — zooms the orthographic camera out just enough so the FULL board width
        /// (not just height) always fits the current device's screen, regardless of its
        /// aspect ratio. Without this, cellSize/cellSpacing produce a fixed world-space
        /// board size while the camera's orthographicSize was only tuned for one aspect
        /// ratio in the editor — on narrower/taller phones the board overflows and gets
        /// cut off at the sides. Runs every time a level (re)initializes the board.
        /// </summary>
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
                _camTween?.Kill();   // only OUR zoom tween — never a camera shake tween
                _camTween = cam.DOOrthoSize(target, duration).SetEase(Ease.InOutQuad);
            }
            else
            {
                cam.orthographicSize = target;
            }
        }

        // ── Coordinate conversion ─────────────────────────────

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

        // ── Tile management ───────────────────────────────────

        /// <summary>
        /// Retrieves a Tile from the ObjectPool, positions it at grid (x, y),
        /// initialises it with <paramref name="data"/>, and registers it in
        /// the Grid array. Returns null if the pool is exhausted or (x,y) is invalid.
        /// </summary>
        public Tile SpawnTile(int x, int y, TileData data)
        {
            if (!IsInBounds(x, y))
            {
                Debug.LogWarning($"[BoardGrid] SpawnTile called out of bounds ({x},{y}).");
                return null;
            }

            // NEW — a blank hole can never hold a tile (gem OR obstacle).
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

        /// <summary>Returns the tile at (x, y) to the pool and clears the grid slot. Safe on an empty cell.</summary>
        public void RemoveTile(int x, int y)
        {
            if (!IsInBounds(x, y)) return;

            Tile tile = Grid[x, y];
            if (tile == null) return;

            tile.ResetForPool();
            tilePool.Return(tile);
            Grid[x, y] = null;
        }

        /// <summary>
        /// Directly places an already-existing Tile reference into a grid slot and
        /// syncs its GridX/GridY. Used by GravitySystem / BoardRotation when they move
        /// tiles around without spawning/removing them.
        /// </summary>
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

        /// <summary>Swaps the two grid slots (and each tile's GridX/GridY) without touching transforms.</summary>
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

        // ── Bounds check ──────────────────────────────────────

        public bool IsInBounds(int x, int y) =>
            x >= 0 && x < Width && y >= 0 && y < Height;

        public Tile GetTile(int x, int y) =>
            IsInBounds(x, y) ? Grid[x, y] : null;

        // ── Neighbour helpers (used by match-detection) ───────

        /// <summary>
        /// Fills <paramref name="results"/> (pre-allocated, length >= 4) with the
        /// horizontal and vertical neighbours of (x, y). Returns how many were found.
        /// </summary>
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

        // ── Gizmos (editor visualisation) ─────────────────────

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