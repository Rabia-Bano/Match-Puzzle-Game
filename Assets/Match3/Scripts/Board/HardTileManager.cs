// ============================================================
//  HardTileManager.cs  —  MonoBehaviour
//
//  Spawns hard-tile (blocker/crate) obstacles at level start.
//
//  REDESIGN (bug report ke baad):
//    Pehle yeh class "adjacency damage" karta tha — jab bhi koi NORMAL
//    tile hard tile ke paas (up/down/left/right) clear hoti, hard tile
//    ko 1 damage lagta tha, chahe wo blast/combo/pet-power ka target
//    khud hard tile ki cell na bhi ho.
//
//    Ab hard tile SIRF tab damage leta hai jab koi special/combo/pet
//    power ki apni target list (jo row/column/3x3/5x5/color-sweep clear
//    kar rahi hai) mein hard tile ka apna cell KHUD shamil ho — yani
//    DIRECT hit. Yeh damage ab seedha Tile.DamageObstacle() call kar ke
//    lagaya jata hai (BoardController.ClearTiles, SpecialCombinations,
//    StripedTileEffect, WrappedTileEffect, ColorBombEffect — sab jagah),
//    is manager class ki zaroorat nahi rahi damage ke liye — sirf
//    LEVEL SETUP (spawning) ke liye reh gayi hai.
//
//  Attach to: an empty "HardTileManager" GameObject in the GameBoard
//  scene (sibling of BoardController / BoardGrid).
//  Wire up: boardGrid.
//  Wire into: LevelManager.hardTileManager (Setup() call only now —
//  BoardController/SpecialCombinations no longer need a reference to
//  this class at all).
// ============================================================

using UnityEngine;

namespace Match3
{
    public class HardTileManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BoardGrid boardGrid;

        // ── Setup ─────────────────────────────────────────────

        /// <summary>Spawns hard tiles for a fresh level. Call from LevelManager.InitializeLevel(), AFTER TileSpawner.FillBoard().</summary>
        public void Setup(LevelData levelData, BoardGrid grid)
        {
            boardGrid = grid;
            if (levelData.hardTileData == null || levelData.hardTilePositions == null) return;

            if (!levelData.hardTileData.isHardTile)
                Debug.LogWarning($"[HardTileManager] '{levelData.hardTileData.name}' is assigned as " +
                                  "hardTileData but its isHardTile checkbox is OFF — fix the TileData asset.", this);

            foreach (Vector2Int pos in levelData.hardTilePositions)
            {
                if (!grid.IsInBounds(pos.x, pos.y)) continue;
                grid.RemoveTile(pos.x, pos.y);                       // clear whatever FillBoard put there
                grid.SpawnTile(pos.x, pos.y, levelData.hardTileData); // Tile.Initialize() sets HP + Locked
            }
        }
    }
}