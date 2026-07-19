// ============================================================
//  BoardRefiller.cs  —  Pure "fill the board" mechanic
//
//  Only job: find empty cells left after gravity, ask TileSpawner
//  to create a new tile in each one, and animate it falling in
//  from above the board.
//
//  It used to ALSO run its own match-finding + clearing + scoring
//  cascade loop, which duplicated (and slowly drifted out of sync
//  with) the same logic in BoardController. That responsibility now
//  lives only in BoardController.ResolveBoard(), which calls
//  RefillEmptyCells() below as one step of its loop.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace Match3
{
    public class BoardRefiller : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BoardGrid   boardGrid;
        [SerializeField] private TileSpawner tileSpawner;

        [Header("Animation")]
        [SerializeField] private float spawnHeightOffset = 2f;
        [SerializeField] private float refillFallTime    = 0.25f;
        [SerializeField] private float columnStagger     = 0.03f;

        /// <summary>
        /// Spawns a new tile in every empty cell (column by column, bottom-most
        /// empty row first) and animates it dropping in from above the board.
        /// Waits for the longest fall before returning.
        /// </summary>
        public IEnumerator RefillEmptyCells()
        {
            float longestFall = 0f;
            bool  anySpawned  = false;

            for (int col = 0; col < boardGrid.Width; col++)
            {
                // Only the CONTIGUOUS run of empty cells starting from the very
                // top of the column is refillable. The moment we hit an occupied
                // cell scanning downward, we STOP — anything empty further down
                // is "trapped" below that tile (most commonly a hard tile, which
                // GravitySystem deliberately never moves) and must stay empty
                // until whatever's blocking it is cleared. Filling those cells
                // anyway is what caused a tile to seemingly "appear out of
                // nowhere" underneath a hard tile.
                var emptyRows = new List<int>();
                for (int y = boardGrid.Height - 1; y >= 0; y--)
                {
                    if (boardGrid.GetTile(col, y) != null) break;
                    emptyRows.Add(y);
                }

                if (emptyRows.Count == 0) continue;
                anySpawned = true;

                float aboveY = boardGrid.GridToWorld(col, boardGrid.Height - 1).y + spawnHeightOffset;

                foreach (int targetRow in emptyRows)
                {
                    tileSpawner.RefillSingleCell(col, targetRow);
                    Tile tile = boardGrid.GetTile(col, targetRow);
                    if (tile == null) continue;

                    Vector3 startPos = tile.transform.position;
                    startPos.y = aboveY;
                    tile.transform.position = startPos;
                    tile.SetState(TileState.Falling);

                    Vector3 targetPos = boardGrid.GridToWorld(col, targetRow);
                    float   dist      = Mathf.Abs(startPos.y - targetPos.y);
                    float   duration  = refillFallTime + dist * 0.018f;
                    longestFall       = Mathf.Max(longestFall, duration);

                    Tile captured = tile;
                    tile.transform.DOMove(targetPos, duration)
                        .SetEase(Ease.InQuad)
                        .OnComplete(() => captured.SetState(TileState.Normal));
                }

                yield return new WaitForSeconds(columnStagger);
            }

            if (anySpawned && longestFall > 0f)
                yield return new WaitForSeconds(longestFall);
        }

        private void Awake()
        {
            if (!boardGrid)   Debug.LogError("[BoardRefiller] boardGrid missing!",   this);
            if (!tileSpawner) Debug.LogError("[BoardRefiller] tileSpawner missing!", this);
        }
    }
}