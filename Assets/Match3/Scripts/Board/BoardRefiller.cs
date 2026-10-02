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

        public IEnumerator RefillEmptyCells()
        {
            float longestFall = 0f;
            bool  anySpawned  = false;

            for (int col = 0; col < boardGrid.Width; col++)
            {
                var emptyRows = new List<int>();
                for (int y = boardGrid.Height - 1; y >= 0; y--)
                {
                    if (boardGrid.IsBlank(col, y)) continue;
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
