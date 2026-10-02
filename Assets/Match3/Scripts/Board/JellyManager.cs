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
                if (grid.IsBlank(pos.x, pos.y))
                {
                    Debug.LogWarning($"[JellyManager] Jelly at ({pos.x},{pos.y}) skipped — that cell is BLANK.");
                    continue;
                }
                _jellyLevel[pos.x, pos.y] = Mathf.Max(1, levelData.jellyLayers);
                SpawnOverlay(pos.x, pos.y);
            }
        }

        public void SetupEmpty(BoardGrid grid)
        {
            boardGrid = grid;
            ClearExistingOverlays();
            _jellyLevel = new int[grid.Width, grid.Height];
            _overlays   = new GameObject[grid.Width, grid.Height];
        }

        public void AddJellyAt(int x, int y, int layers)
        {
            if (boardGrid == null) return;

            if (_jellyLevel == null ||
                _jellyLevel.GetLength(0) != boardGrid.Width ||
                _jellyLevel.GetLength(1) != boardGrid.Height)
            {
                SetupEmpty(boardGrid);
            }

            if (!boardGrid.IsInBounds(x, y)) return;
            if (boardGrid.IsBlank(x, y)) return;
            if (_jellyLevel[x, y] > 0) return;

            _jellyLevel[x, y] = Mathf.Max(1, layers);
            SpawnOverlay(x, y);
            if (_overlays[x, y] != null)
                _overlays[x, y].transform.DOPunchScale(Vector3.one * 0.25f, 0.3f, 4, 0.6f);
        }

        public bool HasJelly(int x, int y) =>
            _jellyLevel != null && boardGrid != null && boardGrid.IsInBounds(x, y) && _jellyLevel[x, y] > 0;

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

        private void SpawnOverlay(int x, int y)
        {
            if (jellyOverlayPrefab == null) return;

            Vector3 pos = boardGrid.GridToWorld(x, y);
            pos.z += 0.1f;

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

        public void BeginTurn() => _touchedThisTurn.Clear();

        public void WanderUnclearedJelly()
        {
            if (_jellyLevel == null || boardGrid == null) return;

            var jellyCells = new List<Vector2Int>();
            for (int x = 0; x < boardGrid.Width; x++)
            for (int y = 0; y < boardGrid.Height; y++)
                if (_jellyLevel[x, y] > 0) jellyCells.Add(new Vector2Int(x, y));

            foreach (Vector2Int cell in jellyCells)
            {
                bool hasTileHere = boardGrid.GetTile(cell.x, cell.y) != null;
                if (hasTileHere && _touchedThisTurn.Contains(cell)) continue;

                List<Vector2Int> candidates = GetAdjacentJellyFreeCells(cell.x, cell.y);
                if (candidates.Count == 0) continue;

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
                if (boardGrid.IsBlank(nx, ny)) continue;
                if (_jellyLevel[nx, ny] > 0) continue;

                Tile t = boardGrid.GetTile(nx, ny);
                if (t == null) continue;
                if (t.Data != null && t.Data.isHardTile) continue;

                result.Add(new Vector2Int(nx, ny));
            }
            return result;
        }

        private void MoveJelly(Vector2Int from, Vector2Int to)
        {
            int level = _jellyLevel[from.x, from.y];
            _jellyLevel[from.x, from.y] = 0;
            RemoveOverlaySnap(from.x, from.y);

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

        public void RotateLayout(int quarterTurnsCW)
        {
            if (_jellyLevel == null || boardGrid == null) return;

            _jellyLevel = BoardRotationMath.Rotate(_jellyLevel, quarterTurnsCW);
            _overlays   = BoardRotationMath.Rotate(_overlays,   quarterTurnsCW);

            int w = _jellyLevel.GetLength(0), h = _jellyLevel.GetLength(1);
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                if (_overlays[x, y] == null) continue;
                Vector3 pos = boardGrid.GridToWorld(x, y);
                pos.z += 0.1f;
                _overlays[x, y].transform.position = pos;
            }
        }

        public void RotateClockwise(int width, int height) => RotateLayout(1);
    }
}
