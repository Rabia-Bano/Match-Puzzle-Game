using System.Collections.Generic;
using UnityEngine;

namespace Match3
{
    public class StoneManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BoardGrid boardGrid;

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

        public List<Tile> GetStonesAtBottomRow()
        {
            var result = new List<Tile>();
            if (boardGrid == null) return result;

            for (int x = 0; x < boardGrid.Width; x++)
            {
                int bottomY = 0;
                while (bottomY < boardGrid.Height && boardGrid.IsBlank(x, bottomY)) bottomY++;
                if (bottomY >= boardGrid.Height) continue;

                Tile t = boardGrid.GetTile(x, bottomY);
                if (t != null && t.Data != null && t.Data.isDropStone)
                    result.Add(t);
            }
            return result;
        }
    }
}
