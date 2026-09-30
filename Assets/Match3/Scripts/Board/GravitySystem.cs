// ============================================================
//  GravitySystem.cs
//  UPDATED — Blank tiles: tiles fall THROUGH blank holes and land on
//  the next playable cell below (Candy-Crush style shaped boards).
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
                // writeY = lowest empty PLAYABLE row index in this column.
                // NEW — blank holes are skipped: tiles fall straight THROUGH a
                // blank cell and land on the next playable cell below it.
                int writeY = NextPlayable(x, 0);

                for (int y = 0; y < boardGrid.Height; y++)
                {
                    if (boardGrid.IsBlank(x, y)) continue;   // a hole is never a tile source

                    Tile tile = boardGrid.GetTile(x, y);
                    if (tile == null) continue;

                    // Hard tiles never move — they're a fixed obstacle. Jump the
                    // write cursor past them so tiles ABOVE stack directly on top
                    // instead of falling straight through. (Dropdown stones are
                    // NOT skipped here — they still fall like normal tiles; only
                    // isHardTile is treated as immovable.)
                    if (tile.Data != null && tile.Data.isHardTile)
                    {
                        writeY = NextPlayable(x, y + 1);
                        continue;
                    }

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

                    writeY = NextPlayable(x, writeY + 1);
                }

                // If there are still empty playable rows at the top, column needs refilling
                if (writeY < boardGrid.Height)
                    DirtyColumns.Add(x);
            }

            if (longestDuration > 0f)
                yield return new WaitForSeconds(longestDuration + settlePadding);
        }

        /// <summary>NEW — first row >= fromY in column x that is NOT a blank hole
        /// (returns Height if there is none).</summary>
        private int NextPlayable(int x, int fromY)
        {
            int y = fromY;
            while (y < boardGrid.Height && boardGrid.IsBlank(x, y)) y++;
            return y;
        }

        // ── Validation ────────────────────────────────────────

        private void Awake()
        {
            if (!boardGrid)
                Debug.LogError("[GravitySystem] boardGrid not assigned!", this);
        }
    }
}