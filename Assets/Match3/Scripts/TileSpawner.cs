// ============================================================
//  TileSpawner.cs — Phase 4 update
//
//  NEW: RefillSingleCell(int x, int y) — public method so
//  BoardRefiller can spawn one tile at a specific cell.
//  All existing logic preserved from Phase 3.
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace Match3
{
    public class TileSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BoardGrid  boardGrid;
        [SerializeField] private TileData[] defaultTiles;

        [Header("Spawn Settings")]
        [SerializeField] private int maxAttempts = 200;

        private TileData[] _activeTiles;

        // ── Public API ────────────────────────────────────────

        public void SetLevel(LevelData levelData)
        {
            _activeTiles = (levelData?.allowedTiles != null && levelData.allowedTiles.Length > 0)
                ? levelData.allowedTiles
                : defaultTiles;

            if (_activeTiles == null || _activeTiles.Length == 0)
                Debug.LogError("[TileSpawner] No tiles configured!");
        }

        /// <summary>Fill entire board bottom-to-top (initial setup).</summary>
        public void FillBoard()
        {
            if (!ValidateState()) return;
            for (int y = 0; y < boardGrid.Height; y++)
            for (int x = 0; x < boardGrid.Width;  x++)
                SpawnAt(x, y);
        }

        /// <summary>Fill all null cells (called after cascade).</summary>
        public void RefillEmpty()
        {
            if (!ValidateState()) return;
            for (int y = 0; y < boardGrid.Height; y++)
            for (int x = 0; x < boardGrid.Width;  x++)
                if (boardGrid.Grid[x, y] == null)
                    SpawnAt(x, y);
        }

        /// <summary>
        /// NEW — Spawn exactly one tile at (x, y).
        /// Called by BoardRefiller for animated per-cell refill.
        /// The tile is placed in the grid; BoardRefiller moves its
        /// transform from above the board to the target position.
        /// </summary>
        public void RefillSingleCell(int x, int y)
        {
            if (!ValidateState()) return;
            if (!boardGrid.IsInBounds(x, y)) return;
            SpawnAt(x, y);
        }

        // ── Core spawn ────────────────────────────────────────

        private void SpawnAt(int x, int y)
        {
            TileData chosen = PickSafeTile(x, y);
            if (chosen != null)
                boardGrid.SpawnTile(x, y, chosen);
        }

        private TileData PickSafeTile(int x, int y)
        {
            List<int> order = ShuffledIndices(_activeTiles.Length);

            foreach (int idx in order)
            {
                TileData candidate = _activeTiles[idx];
                if (candidate == null || candidate.isSpecial) continue;
                if (!WouldFormMatch(x, y, candidate.color))
                    return candidate;
            }

            // Fallback
            foreach (int idx in order)
                if (_activeTiles[idx] != null && !_activeTiles[idx].isSpecial)
                    return _activeTiles[idx];

            return _activeTiles[0];
        }

        // ── Match prediction ──────────────────────────────────

        private bool WouldFormMatch(int x, int y, TileColor color)
        {
            bool l1 = IsNormalColor(x-1, y,   color);
            bool l2 = IsNormalColor(x-2, y,   color);
            bool r1 = IsNormalColor(x+1, y,   color);
            bool r2 = IsNormalColor(x+2, y,   color);

            if (l2 && l1) return true;
            if (l1 && r1) return true;
            if (r1 && r2) return true;

            bool d1 = IsNormalColor(x, y-1, color);
            bool d2 = IsNormalColor(x, y-2, color);
            bool u1 = IsNormalColor(x, y+1, color);
            bool u2 = IsNormalColor(x, y+2, color);

            if (d2 && d1) return true;
            if (d1 && u1) return true;
            if (u1 && u2) return true;

            return false;
        }

        private bool IsNormalColor(int x, int y, TileColor color)
        {
            if (!boardGrid.IsInBounds(x, y)) return false;
            Tile tile = boardGrid.GetTile(x, y);
            if (tile == null || tile.Data == null) return false;
            if (tile.Data.isSpecial) return false;
            if (tile.Data.color == TileColor.None) return false;
            return tile.Data.color == color;
        }

        private static List<int> ShuffledIndices(int n)
        {
            var list = new List<int>(n);
            for (int i = 0; i < n; i++) list.Add(i);
            for (int i = n - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            return list;
        }

        private bool ValidateState()
        {
            if (boardGrid == null)
            { Debug.LogError("[TileSpawner] boardGrid not assigned!"); return false; }
            if (_activeTiles == null || _activeTiles.Length == 0)
            { Debug.LogError("[TileSpawner] No active tiles. Call SetLevel() first."); return false; }
            return true;
        }
    }
}