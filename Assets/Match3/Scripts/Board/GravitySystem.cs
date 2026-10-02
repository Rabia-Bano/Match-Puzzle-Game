using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace Match3
{
    public class GravitySystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BoardGrid boardGrid;

        [Header("Animation")]
        [Tooltip("Seconds per unit of fall distance. 0.08 = snappy, 0.15 = floaty.")]
        [SerializeField] private float fallTimePerUnit = 0.08f;

        [Tooltip("Small pause after all tiles land before cascade continues.")]
        [SerializeField] private float settlePadding = 0.06f;

        public HashSet<int> DirtyColumns { get; private set; } = new();

        public IEnumerator ApplyGravity()
        {
            DirtyColumns.Clear();
            float longestDuration = 0f;

            for (int x = 0; x < boardGrid.Width; x++)
            {
                int writeY = NextPlayable(x, 0);

                for (int y = 0; y < boardGrid.Height; y++)
                {
                    if (boardGrid.IsBlank(x, y)) continue;

                    Tile tile = boardGrid.GetTile(x, y);
                    if (tile == null) continue;

                    if (tile.Data != null && tile.Data.isHardTile)
                    {
                        writeY = NextPlayable(x, y + 1);
                        continue;
                    }

                    if (y != writeY)
                    {
                        boardGrid.Grid[x, writeY] = tile;
                        boardGrid.Grid[x, y]      = null;
                        tile.SetGridPosition(x, writeY);
                        tile.SetState(TileState.Falling);

                        int   dist     = y - writeY;
                        float duration = dist * fallTimePerUnit;
                        longestDuration = Mathf.Max(longestDuration, duration);

                        Vector3 target   = boardGrid.GridToWorld(x, writeY);
                        Tile    captured = tile;

                        tile.transform.DOMove(target, duration)
                            .SetEase(Ease.InQuad)
                            .OnComplete(() => captured.SetState(TileState.Normal));
                    }

                    writeY = NextPlayable(x, writeY + 1);
                }

                if (writeY < boardGrid.Height)
                    DirtyColumns.Add(x);
            }

            if (longestDuration > 0f)
                yield return new WaitForSeconds(longestDuration + settlePadding);
        }

        private int NextPlayable(int x, int fromY)
        {
            int y = fromY;
            while (y < boardGrid.Height && boardGrid.IsBlank(x, y)) y++;
            return y;
        }

        private void Awake()
        {
            if (!boardGrid)
                Debug.LogError("[GravitySystem] boardGrid not assigned!", this);
        }
    }
}
