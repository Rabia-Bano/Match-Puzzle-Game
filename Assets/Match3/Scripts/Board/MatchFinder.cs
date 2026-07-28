// ============================================================
//  MatchFinder.cs
//  Reviewed — no duplication or bugs found, kept as-is (included
//  here only so the whole Board/ folder is a complete drop-in set).
//  Scans the board for all horizontal and vertical 3+ runs.
//  Merges overlapping runs into T/L shapes (single group).
//  Tags each group with its MatchShape for SpecialTileFactory.
//
//  Attach to: MatchFinder (empty GameObject in scene)
//  Requires:  BoardGrid reference
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace Match3
{
    // ── Shape enum ────────────────────────────────────────────

    /// <summary>Describes the shape of a matched group (used by SpecialTileFactory).</summary>
    public enum MatchShape
    {
        Line3,          // exactly 3 in a row / column
        HLine4,         // 4 in a horizontal row
        VLine4,         // 4 in a vertical column
        Line5,          // 5+ in a row or column
        TShape,         // T-shape (merged horizontal + vertical)
        LShape          // L-shape (merged horizontal + vertical)
    }

    // ── Match group ───────────────────────────────────────────

    /// <summary>One connected group of matched tiles and the shape they form.</summary>
    public class MatchGroup
    {
        public readonly List<Tile> Tiles = new();
        public MatchShape Shape;

        public MatchGroup(IEnumerable<Tile> tiles, MatchShape shape)
        {
            Tiles.AddRange(tiles);
            Shape = shape;
        }
    }

    // ── Component ─────────────────────────────────────────────

    public class MatchFinder : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────

        [SerializeField] private BoardGrid boardGrid;

        // ── Public API ────────────────────────────────────────

        /// <summary>
        /// Scans the whole board and returns every matched group.
        /// Returns an empty list when there are no matches.
        ///
        /// Algorithm:
        ///   1. Collect all horizontal runs of 3+.
        ///   2. Collect all vertical runs of 3+.
        ///   3. Merge runs that share ≥ 1 tile → T / L shapes.
        ///   4. Tag each merged group with the correct MatchShape.
        /// </summary>
        public List<MatchGroup> FindAllMatches()
        {
            var hRuns = FindRuns(horizontal: true);   // List of raw tile-lists
            var vRuns = FindRuns(horizontal: false);

            // Merge overlapping horizontal and vertical runs
            List<MatchGroup> groups = MergeRuns(hRuns, vRuns);

            return groups;
        }

        // ── Run detection ─────────────────────────────────────

        /// <summary>
        /// Scans the board in one direction and returns every run of 3+ same-colour tiles.
        /// A run is represented as a List&lt;Tile&gt;.
        /// </summary>
        private List<List<Tile>> FindRuns(bool horizontal)
        {
            var runs = new List<List<Tile>>();

            int outer = horizontal ? boardGrid.Height : boardGrid.Width;
            int inner = horizontal ? boardGrid.Width  : boardGrid.Height;

            for (int o = 0; o < outer; o++)
            {
                var currentRun = new List<Tile>();
                TileColor? runColor = null;

                for (int i = 0; i < inner; i++)
                {
                    int x = horizontal ? i : o;
                    int y = horizontal ? o : i;

                    Tile tile = boardGrid.GetTile(x, y);

                    if (tile == null || tile.Data == null || tile.Data.color == TileColor.None
                        || tile.State == TileState.Locked || tile.State == TileState.Inactive)
                    {
                        FlushRun(currentRun, runs);
                        currentRun = new List<Tile>();
                        runColor   = null;
                        continue;
                    }

                    if (runColor == null || tile.Data.color == runColor)
                    {
                        currentRun.Add(tile);
                        runColor = tile.Data.color;
                    }
                    else
                    {
                        // Colour changed — flush and start a new run with this tile
                        FlushRun(currentRun, runs);
                        currentRun = new List<Tile> { tile };
                        runColor   = tile.Data.color;
                    }
                }

                // End of row/column — flush any open run
                FlushRun(currentRun, runs);
            }

            return runs;
        }

        /// <summary>Adds the run to the list only if it has 3 or more tiles.</summary>
        private static void FlushRun(List<Tile> run, List<List<Tile>> output)
        {
            if (run.Count >= 3)
                output.Add(new List<Tile>(run));
            run.Clear();
        }

        // ── Merge & shape tagging ─────────────────────────────

        /// <summary>
        /// Merges horizontal and vertical runs that share at least one tile.
        /// Assigns the correct MatchShape to each resulting group.
        ///
        /// Each unmerged run becomes its own group.
        /// Two runs that intersect become a single T or L group.
        /// </summary>
        private static List<MatchGroup> MergeRuns(
            List<List<Tile>> hRuns,
            List<List<Tile>> vRuns)
        {
            // Assign every raw run a temporary group id
            // Groups start as individual runs; we union-find them if they share a tile.

            // Build a master list of all raw runs
            var allRuns = new List<(List<Tile> tiles, bool isH)>();
            foreach (var r in hRuns) allRuns.Add((r, true));
            foreach (var r in vRuns) allRuns.Add((r, false));

            int n = allRuns.Count;
            int[] parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;

            // For fast lookup: tile → which run indices contain it
            var tileToRuns = new Dictionary<Tile, List<int>>();
            for (int i = 0; i < n; i++)
            {
                foreach (var tile in allRuns[i].tiles)
                {
                    if (!tileToRuns.TryGetValue(tile, out var list))
                    { list = new List<int>(); tileToRuns[tile] = list; }
                    list.Add(i);
                }
            }

            // Union any two runs that share a tile
            foreach (var (_, runIndices) in tileToRuns)
            {
                for (int a = 1; a < runIndices.Count; a++)
                    Union(parent, runIndices[0], runIndices[a]);
            }

            // Collect groups by root
            var rootToTiles = new Dictionary<int, HashSet<Tile>>();
            var rootHasH    = new Dictionary<int, bool>();
            var rootHasV    = new Dictionary<int, bool>();
            var rootMaxH    = new Dictionary<int, int>();
            var rootMaxV    = new Dictionary<int, int>();

            for (int i = 0; i < n; i++)
            {
                int root = Find(parent, i);
                if (!rootToTiles.ContainsKey(root))
                {
                    rootToTiles[root] = new HashSet<Tile>();
                    rootHasH[root]    = false;
                    rootHasV[root]    = false;
                    rootMaxH[root]    = 0;
                    rootMaxV[root]    = 0;
                }

                bool isH = allRuns[i].isH;
                int  len  = allRuns[i].tiles.Count;

                foreach (var t in allRuns[i].tiles)
                    rootToTiles[root].Add(t);

                if (isH) { rootHasH[root] = true; rootMaxH[root] = Mathf.Max(rootMaxH[root], len); }
                else      { rootHasV[root] = true; rootMaxV[root] = Mathf.Max(rootMaxV[root], len); }
            }

            // Build MatchGroup list
            var result = new List<MatchGroup>();

            foreach (var (root, tiles) in rootToTiles)
            {
                bool hasH  = rootHasH[root];
                bool hasV  = rootHasV[root];
                int  maxH  = rootMaxH[root];
                int  maxV  = rootMaxV[root];

                MatchShape shape = DetermineShape(hasH, hasV, maxH, maxV);
                result.Add(new MatchGroup(tiles, shape));
            }

            return result;
        }

        // ── Shape determination ───────────────────────────────

        /// <summary>
        /// Rules:
        ///   5+ (either axis)        → Line5
        ///   H + V present           → T or L  (T when one axis is 3 and other is 3+, L otherwise)
        ///   H only, len == 4        → HLine4
        ///   V only, len == 4        → VLine4
        ///   anything else           → Line3
        /// </summary>
        private static MatchShape DetermineShape(bool hasH, bool hasV, int maxH, int maxV)
        {
            // 5-in-a-row trumps everything
            if (maxH >= 5 || maxV >= 5) return MatchShape.Line5;

            // Cross shape — both axes present
            if (hasH && hasV)
            {
                // T-shape: one arm is length 3 and the other is also 3 (shares the pivot)
                // L-shape: one arm is 3, other starts/ends at an endpoint
                // For simplicity we classify T when both arms are exactly 3,
                // and L otherwise.
                bool bothExactly3 = (maxH == 3 && maxV == 3);
                return bothExactly3 ? MatchShape.TShape : MatchShape.LShape;
            }

            // Horizontal line only
            if (hasH) return maxH >= 4 ? MatchShape.HLine4 : MatchShape.Line3;

            // Vertical line only
            return maxV >= 4 ? MatchShape.VLine4 : MatchShape.Line3;
        }

        // ── Union-Find helpers ────────────────────────────────

        private static int Find(int[] parent, int i)
        {
            if (parent[i] != i) parent[i] = Find(parent, parent[i]);  // path compression
            return parent[i];
        }

        private static void Union(int[] parent, int a, int b)
        {
            int ra = Find(parent, a);
            int rb = Find(parent, b);
            if (ra != rb) parent[ra] = rb;
        }
    }
}