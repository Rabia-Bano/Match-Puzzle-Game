using System.Collections.Generic;
using UnityEngine;

namespace Match3
{
    public enum MatchShape
    {
        Line3,
        HLine4,
        VLine4,
        Line5,
        TShape,
        LShape
    }

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

    public class MatchFinder : MonoBehaviour
    {
        [SerializeField] private BoardGrid boardGrid;

        public List<MatchGroup> FindAllMatches()
        {
            var hRuns = FindRuns(horizontal: true);
            var vRuns = FindRuns(horizontal: false);

            List<MatchGroup> groups = MergeRuns(hRuns, vRuns);

            return groups;
        }

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
                        FlushRun(currentRun, runs);
                        currentRun = new List<Tile> { tile };
                        runColor   = tile.Data.color;
                    }
                }

                FlushRun(currentRun, runs);
            }

            return runs;
        }

        private static void FlushRun(List<Tile> run, List<List<Tile>> output)
        {
            if (run.Count >= 3)
                output.Add(new List<Tile>(run));
            run.Clear();
        }

        private static List<MatchGroup> MergeRuns(
            List<List<Tile>> hRuns,
            List<List<Tile>> vRuns)
        {
            var allRuns = new List<(List<Tile> tiles, bool isH)>();
            foreach (var r in hRuns) allRuns.Add((r, true));
            foreach (var r in vRuns) allRuns.Add((r, false));

            int n = allRuns.Count;
            int[] parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;

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

            foreach (var (_, runIndices) in tileToRuns)
            {
                for (int a = 1; a < runIndices.Count; a++)
                    Union(parent, runIndices[0], runIndices[a]);
            }

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

        private static MatchShape DetermineShape(bool hasH, bool hasV, int maxH, int maxV)
        {
            if (maxH >= 5 || maxV >= 5) return MatchShape.Line5;

            if (hasH && hasV)
            {
                bool bothExactly3 = (maxH == 3 && maxV == 3);
                return bothExactly3 ? MatchShape.TShape : MatchShape.LShape;
            }

            if (hasH) return maxH >= 4 ? MatchShape.HLine4 : MatchShape.Line3;

            return maxV >= 4 ? MatchShape.VLine4 : MatchShape.Line3;
        }

        private static int Find(int[] parent, int i)
        {
            if (parent[i] != i) parent[i] = Find(parent, parent[i]);
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
