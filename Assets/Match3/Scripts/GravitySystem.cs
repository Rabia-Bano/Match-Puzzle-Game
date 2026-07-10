// ============================================================
//  GravitySystem.cs
//  Reviewed — no duplication or bugs found, kept as-is (included
//  here only so the whole Board/ folder is a complete drop-in set).
//  This is now the ONLY gravity implementation BoardController's
//  cascade loop uses.
//
//  Moves tiles downward to fill empty cells after matches.
//  Animates each fall with DOTween.
//  Tracks which columns have gaps so BoardRefiller knows
//  exactly which columns need new tiles from the top.
//
//  Attach to: GravitySystem (empty GameObject)
//  Wire:      boardGrid
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace Match3
{
    public class GravitySystem : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────

        [Header("References")]
        [SerializeField] private BoardGrid boardGrid;

        [Header("Animation")]
        [Tooltip("Seconds per unit of fall distance. 0.08 = snappy, 0.15 = floaty.")]
        [SerializeField] private float fallTimePerUnit = 0.08f;

        [Tooltip("Small pause after all tiles land before cascade continues.")]
        [SerializeField] private float settlePadding = 0.06f;

        // ── Public state ──────────────────────────────────────

        /// <summary>
        /// Columns that still have empty cells at the top after gravity.
        /// BoardRefiller uses this to know which columns need new tiles.
        /// </summary>
        public HashSet<int> DirtyColumns { get; private set; } = new();

        // ─────────────────────────────────────────────────────
        //  PUBLIC COROUTINE
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// Scan every column bottom-to-top.
        /// Any tile above an empty cell falls down to fill it.
        /// All falls animate in parallel via DOTween.
        /// Awaits the slowest fall, then yields settlePadding.
        /// </summary>
        public IEnumerator ApplyGravity()
        {
            DirtyColumns.Clear();
            float longestDuration = 0f;

            for (int x = 0; x < boardGrid.Width; x++)
            {
                // writeY = lowest empty row index in this column
                int writeY = 0;

                for (int y = 0; y < boardGrid.Height; y++)
                {
                    Tile tile = boardGrid.GetTile(x, y);
                    if (tile == null) continue;

                    if (y != writeY)
                    {
                        // Update logical grid
                        boardGrid.Grid[x, writeY] = tile;
                        boardGrid.Grid[x, y]      = null;
                        tile.SetGridPosition(x, writeY);
                        tile.SetState(TileState.Falling);

                        // Duration scales with fall distance
                        int   dist     = y - writeY;
                        float duration = dist * fallTimePerUnit;
                        longestDuration = Mathf.Max(longestDuration, duration);

                        Vector3 target   = boardGrid.GridToWorld(x, writeY);
                        Tile    captured = tile;

                        tile.transform.DOMove(target, duration)
                            .SetEase(Ease.InQuad)
                            .OnComplete(() => captured.SetState(TileState.Normal));
                    }

                    writeY++;
                }

                // If there are still empty rows at the top, column needs refilling
                if (writeY < boardGrid.Height)
                    DirtyColumns.Add(x);
            }

            if (longestDuration > 0f)
                yield return new WaitForSeconds(longestDuration + settlePadding);
        }

        // ── Validation ────────────────────────────────────────

        private void Awake()
        {
            if (!boardGrid)
                Debug.LogError("[GravitySystem] boardGrid not assigned!", this);
        }
    }
}