using UnityEngine;

namespace Match3
{
    public class HardTileManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BoardGrid boardGrid;

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
                grid.RemoveTile(pos.x, pos.y);
                grid.SpawnTile(pos.x, pos.y, levelData.hardTileData);
            }
        }
    }
}
