// ============================================================
//  JellyManager.cs  —  MonoBehaviour
//
//  Owns the jelly LAYER that sits underneath tiles — this is
//  separate from whatever tile currently occupies that cell. The
//  tile above still matches and clears completely normally; every
//  time a NORMAL tile clears while sitting on a jelly cell, one
//  layer of jelly peels off. When a cell's jelly count hits 0, the
//  cell is "clean" (goal is only about clearing the jelly, not the
//  tile — the tile itself keeps refilling normally forever).
//
//  Attach to: an empty "JellyManager" GameObject in the GameBoard
//  scene (sibling of BoardController / BoardGrid).
//  Wire up: boardGrid, jellyOverlayPrefab, jellyLayerSprites[].
//  Wire into: LevelManager.jellyManager AND BoardController.jellyManager
//  (same object, both fields point at it).
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace Match3
{
    public class JellyManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BoardGrid boardGrid;

        [Header("Visual")]
        [Tooltip("A simple prefab with just a SpriteRenderer, placed UNDER the tile at " +
                 "jelly cells (sorting order lower than the tile's own renderer).")]
        [SerializeField] private GameObject jellyOverlayPrefab;

        [Tooltip("One sprite per layer count. Index [0] = what a 1-layer jelly cell looks " +
                 "like, index [1] = 2-layer look, etc. Leave size 1 if you only use single-layer jelly.")]
        [SerializeField] private Sprite[] jellyLayerSprites;

        private int[,]        _jellyLevel;
        private GameObject[,] _overlays;
        private readonly HashSet<Vector2Int> _touchedThisTurn = new();

        // ── Setup ─────────────────────────────────────────────

        /// <summary>Sets up jelly cells for a fresh level. Call from LevelManager.InitializeLevel(), AFTER BoardGrid.InitializeBoard().</summary>
        public void Setup(LevelData levelData, BoardGrid grid)
        {
            boardGrid = grid;
            ClearExistingOverlays();

            _jellyLevel = new int[grid.Width, grid.Height];
            _overlays   = new GameObject[grid.Width, grid.Height];

            if (levelData.jellyPositions == null) return;

            foreach (Vector2Int pos in levelData.jellyPositions)
            {
                if (!grid.IsInBounds(pos.x, pos.y)) continue;
                _jellyLevel[pos.x, pos.y] = Mathf.Max(1, levelData.jellyLayers);
                SpawnOverlay(pos.x, pos.y);
            }
        }

        // ── Queries ───────────────────────────────────────────

        /// <summary>True if this cell currently has 1+ jelly layers remaining.</summary>
        public bool HasJelly(int x, int y) =>
            _jellyLevel != null && boardGrid != null && boardGrid.IsInBounds(x, y) && _jellyLevel[x, y] > 0;

        // ── Mutation ──────────────────────────────────────────

        /// <summary>
        /// Called by BoardController.ClearTiles() every time a NORMAL (non-special,
        /// non-obstacle) tile clears at (x,y). Peels one jelly layer off that
        /// cell, if any. Returns true if a layer was actually removed — the
        /// caller uses that to report progress to GoalTracker.OnJellyCleared().
        /// </summary>
        public bool DecrementAt(int x, int y)
        {
            if (!HasJelly(x, y)) return false;

            _touchedThisTurn.Add(new Vector2Int(x, y));

            _jellyLevel[x, y]--;

            if (_jellyLevel[x, y] <= 0)
                RemoveOverlay(x, y);
            else
                UpdateOverlaySprite(x, y);

            return true;
        }

        // ── Visuals ───────────────────────────────────────────

        private void SpawnOverlay(int x, int y)
        {
            if (jellyOverlayPrefab == null) return;

            Vector3 pos = boardGrid.GridToWorld(x, y);
            pos.z += 0.1f; // sit slightly behind the tile

            GameObject go = Instantiate(jellyOverlayPrefab, pos, Quaternion.identity, transform);
            _overlays[x, y] = go;
            UpdateOverlaySprite(x, y);
        }

        private void UpdateOverlaySprite(int x, int y)
        {
            if (_overlays[x, y] == null) return;
            var sr = _overlays[x, y].GetComponent<SpriteRenderer>();
            if (sr == null) return;

            int level = _jellyLevel[x, y];
            if (jellyLayerSprites != null && level > 0 && level <= jellyLayerSprites.Length)
                sr.sprite = jellyLayerSprites[level - 1];
        }

        private void RemoveOverlay(int x, int y)
        {
            GameObject go = _overlays[x, y];
            if (go == null) return;
            _overlays[x, y] = null;

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.DOFade(0f, 0.25f).OnComplete(() => Destroy(go));
            else
                Destroy(go);
        }

        private void ClearExistingOverlays()
        {
            if (_overlays == null) return;
            foreach (GameObject go in _overlays)
                if (go != null) Destroy(go);
        }

        // ── Wandering jelly (moves if not cleared this turn) ────

        /// <summary>Call at the START of every player turn, before any clearing happens.</summary>
        public void BeginTurn() => _touchedThisTurn.Clear();

        /// <summary>
        /// Call at the END of every player turn (after all matches/cascades/
        /// rotation have fully settled). Any jelly cell that did NOT get a
        /// layer peeled off this turn "creeps" onto a random adjacent cell
        /// that currently has a tile and no jelly of its own — so a jelly
        /// the player keeps ignoring doesn't just sit still forever.
        /// </summary>
        public void WanderUnclearedJelly()
        {
            if (_jellyLevel == null || boardGrid == null) return;

            var jellyCells = new List<Vector2Int>();
            for (int x = 0; x < boardGrid.Width; x++)
            for (int y = 0; y < boardGrid.Height; y++)
                if (_jellyLevel[x, y] > 0) jellyCells.Add(new Vector2Int(x, y));

            foreach (Vector2Int cell in jellyCells)
            {
                // FIX: a jelly cell whose tile just cleared THIS turn was always
                // skipped here (the "it was cleared this turn, leave it" case
                // below) — but if that cell sits below a hard tile/blocker in
                // its column, BoardRefiller intentionally never refills it
                // (trapped-cell design), so the tile above it is gone for good.
                // Without this check the jelly was left rendering over a blank
                // cell forever. If there's currently no tile here, the jelly
                // MUST relocate regardless of whether it was touched this turn.
                bool hasTileHere = boardGrid.GetTile(cell.x, cell.y) != null;
                if (hasTileHere && _touchedThisTurn.Contains(cell)) continue; // cleared normally this turn — leave it

                List<Vector2Int> candidates = GetAdjacentJellyFreeCells(cell.x, cell.y);
                if (candidates.Count == 0) continue; // nowhere to go — stays put (still stuck, but at least not silently ignored — see HasStrandedJelly below)

                Vector2Int dest = candidates[Random.Range(0, candidates.Count)];
                MoveJelly(cell, dest);
            }
        }

        private List<Vector2Int> GetAdjacentJellyFreeCells(int x, int y)
        {
            var result = new List<Vector2Int>();
            Vector2Int[] dirs = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

            foreach (Vector2Int d in dirs)
            {
                int nx = x + d.x, ny = y + d.y;
                if (!boardGrid.IsInBounds(nx, ny)) continue;
                if (_jellyLevel[nx, ny] > 0) continue;                 // already has jelly

                Tile t = boardGrid.GetTile(nx, ny);
                if (t == null) continue;                               // nothing to sit under
                if (t.Data != null && t.Data.isHardTile) continue;      // don't hide jelly under a hard tile

                result.Add(new Vector2Int(nx, ny));
            }
            return result;
        }

        private void MoveJelly(Vector2Int from, Vector2Int to)
        {
            int level = _jellyLevel[from.x, from.y];
            _jellyLevel[from.x, from.y] = 0;
            RemoveOverlaySnap(from.x, from.y);   // instant removal, no fade — it's relocating, not clearing

            _jellyLevel[to.x, to.y] = level;
            SpawnOverlay(to.x, to.y);
            if (_overlays[to.x, to.y] != null)
                _overlays[to.x, to.y].transform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 4, 0.6f);
        }

        private void RemoveOverlaySnap(int x, int y)
        {
            GameObject go = _overlays[x, y];
            if (go == null) return;
            _overlays[x, y] = null;
            Destroy(go);
        }

        // ── Board rotation support ──────────────────────────────

        /// <summary>
        /// Rotates the jelly layer 90° clockwise using the EXACT same transform
        /// BoardRotation.RotateGridClockwise() applies to the tile grid:
        /// new(x,y) = old(y, W-1-x). Called by BoardRotation right after it
        /// rotates the tiles, so a jelly layer "jumps" to stay under whichever
        /// tile now occupies its rotated cell — instead of staying pinned to
        /// its old world position while the tile above it rotates away.
        /// </summary>
        public void RotateClockwise(int width, int height)
        {
            if (_jellyLevel == null) return;

            var rotatedLevels   = new int[width, height];
            var rotatedOverlays = new GameObject[width, height];

            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                rotatedLevels[x, y]   = _jellyLevel[y, width - 1 - x];
                rotatedOverlays[x, y] = _overlays[y, width - 1 - x];
            }

            _jellyLevel = rotatedLevels;
            _overlays   = rotatedOverlays;

            // Snap every surviving overlay to its new world position.
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (_overlays[x, y] == null) continue;
                Vector3 pos = boardGrid.GridToWorld(x, y);
                pos.z += 0.1f;
                _overlays[x, y].transform.position = pos;
            }
        }
    }
}