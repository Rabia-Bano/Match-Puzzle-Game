// ============================================================
//  StoneManager.cs  —  MonoBehaviour
//
//  Spawns "dropdown stone" (ingredient) obstacles at level start.
//  A stone falls with gravity exactly like a normal tile (GravitySystem
//  doesn't care about TileState), but can't be matched or swapped —
//  it spawns TileState.Locked, same mechanism hard tiles use. Once a
//  stone lands on the BOTTOM row (y == 0) it's ready to be collected;
//  BoardController.ResolveBoard() asks this manager after every gravity
//  + refill pass whether any stone has reached the bottom, and clears
//  it through the normal ClearTiles() pipeline.
//
//  Attach to: an empty "StoneManager" GameObject in the GameBoard scene
//  (sibling of BoardController / BoardGrid).
//  Wire up: boardGrid.
//  Wire into: LevelManager.stoneManager AND BoardController.stoneManager
//  (same object, both fields point at it).
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace Match3
{
    public class StoneManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BoardGrid boardGrid;

        // ── Setup ─────────────────────────────────────────────

        /// <summary>Spawns stones for a fresh level. Call from LevelManager.InitializeLevel(), AFTER TileSpawner.FillBoard().</summary>
        public void Setup(LevelData levelData, BoardGrid grid)
        {
            boardGrid = grid;
            if (levelData.dropStoneData == null || levelData.stonePositions == null) return;

            if (!levelData.dropStoneData.isDropStone)
                Debug.LogWarning($"[StoneManager] '{levelData.dropStoneData.name}' is assigned as " +
                                  "dropStoneData but its isDropStone checkbox is OFF — fix the TileData asset.", this);

            foreach (Vector2Int pos in levelData.stonePositions)
            {
                if (!grid.IsInBounds(pos.x, pos.y)) continue;
                grid.RemoveTile(pos.x, pos.y);
                grid.SpawnTile(pos.x, pos.y, levelData.dropStoneData);
            }
        }

        // ── Query ─────────────────────────────────────────────

        /// <summary>
        /// Returns every stone tile currently sitting on the bottom row
        /// (y == 0) — these are ready to be collected. Called by
        /// BoardController.ResolveBoard() after each gravity+refill pass.
        /// </summary>
        public List<Tile> GetStonesAtBottomRow()
        {
            var result = new List<Tile>();
            if (boardGrid == null) return result;

            for (int x = 0; x < boardGrid.Width; x++)
            {
                Tile t = boardGrid.GetTile(x, 0);
                if (t != null && t.Data != null && t.Data.isDropStone)
                    result.Add(t);
            }
            return result;
        }
    }
}
