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
// ============================================================

using UnityEngine;

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

        // ── World-space origin ────────────────────────────────

        private Vector3 _originOffset;

        // ── Initialisation ────────────────────────────────────

        /// <summary>
        /// Creates the empty Grid array and calculates the world-space
        /// origin so the board is centred on this transform.
        /// Call this from LevelManager after reading the LevelData asset.
        /// </summary>
        public void InitializeBoard(int width, int height)
        {
            Width  = width;
            Height = height;
            Grid   = new Tile[width, height];

            TileVisualScale = tileVisualScale;

            float step = cellSize + cellSpacing;
            _originOffset = transform.position
                - new Vector3((width  - 1) * step * 0.5f,
                              (height - 1) * step * 0.5f,
                              0f);

            Debug.Log($"[BoardGrid] Initialized {width}x{height} board. " +
                      $"Origin={_originOffset}  CellStep={step:F3}");

            FitCameraToBoard(width, height);
        }

        /// <summary>
        /// NEW — zooms the orthographic camera out just enough so the FULL board width
        /// (not just height) always fits the current device's screen, regardless of its
        /// aspect ratio. Without this, cellSize/cellSpacing produce a fixed world-space
        /// board size while the camera's orthographicSize was only tuned for one aspect
        /// ratio in the editor — on narrower/taller phones the board overflows and gets
        /// cut off at the sides. Runs every time a level (re)initializes the board.
        /// </summary>
        private void FitCameraToBoard(int width, int height)
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

            cam.orthographicSize = Mathf.Max(sizeForHeight, sizeForWidth);
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