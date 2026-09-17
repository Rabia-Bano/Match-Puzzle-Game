// ============================================================
//  SpecialTileActivator.cs  —  Set to use BoardController's shared pipeline
//
//  ClearList()  -> decides WHICH tiles a blast pattern hits, hands the
//                  actual clearing to BoardController.ClearTiles().
//  Settle()     -> just calls BoardController.SettleAfterExternalClear().
//
//  REDESIGN NOTE (bug report ke baad — hard tile damage):
//    ClearList() ka code khud bilkul same hai — yeh already row/column/
//    3x3/5x5/colour-sweep ki har cell (hard tiles included) ko
//    "normals" list mein daal kar boardController.ClearTiles() ko de
//    deta tha. Sirf param ka naam change hua hai: damageAdjacentHardTiles
//    → canDamageHardTiles — kyunki ab ClearTiles() ke andar hard tile
//    ko "adjacency" se nahi, seedha DIRECT hit (is list mein khud
//    shamil hone) se damage milta hai. Yeh single special activation
//    (row/col/3x3/5x5/color-bomb — jab player ek special tile tap
//    kare ya match kare) ab bhi hard tile ko sahi tareeqe se hit
//    karta hai agar hard tile khud us blast path mein ho.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;

namespace Match3
{
    public class SpecialTileActivator : MonoBehaviour
    {
        [Header("References — ALL must be assigned")]
        [SerializeField] private BoardGrid       boardGrid;
        [SerializeField] private BoardController boardController;  // owns clearing/scoring/gravity/refill/cascade now
        [SerializeField] private LevelManager    levelManager; 
        [SerializeField] private JellyManager    jellyManager;

        [Header("Special TileData Assets")]
        [SerializeField] private TileData hStripedData;
        [SerializeField] private TileData vStripedData;
        [SerializeField] private TileData wrappedData;
        [SerializeField] private TileData colorBombData;

        [Header("Timings")]
        [SerializeField] private float blastDelay   = 0.02f;
        [SerializeField] private float wrappedPause = 0.12f;
        [SerializeField] private float postSettleWait = 0.05f;

        public bool IsRunning { get; private set; }

        /// <summary>
        /// Called by BoardController.ClearTiles() when it finds a special tile
        /// inside a list it was asked to clear (pet skill, booster, or a special
        /// caught inside a normal match) — activates that tile's own blast
        /// pattern instead of letting BoardController silently erase it.
        /// </summary>
        public IEnumerator ChainActivate(Tile tile) => FireSingle(tile);

        public void ActivateSingle(Tile tile)
        {
            if (!IsSpecial(tile)) return;
            StartCoroutine(SingleRoutine(tile));
        }

        public bool TryActivateSwap(Tile tA, Tile tB)
        {
            bool aS = IsSpecial(tA), bS = IsSpecial(tB);
            if (!aS && !bS) return false;
            StartCoroutine(SwapRoutine(tA, tB, aS, bS));
            return true;
        }

        private IEnumerator SingleRoutine(Tile tile)
        {
            IsRunning = true;
            yield return StartCoroutine(FireSingle(tile));
            yield return StartCoroutine(Settle());
            IsRunning = false;
        }

        private IEnumerator SwapRoutine(Tile tA, Tile tB, bool aS, bool bS)
        {
            IsRunning = true;

            SpecialType typeA = aS ? tA.Data.specialType : SpecialType.None;
            SpecialType typeB = bS ? tB.Data.specialType : SpecialType.None;

            if (aS && bS)
            {
                if (typeA == SpecialType.Rainbow && typeB == SpecialType.Rainbow)
                { RemoveBoth(tA, tB); yield return StartCoroutine(ClearEntireBoard()); }
                else if (typeA == SpecialType.Rainbow || typeB == SpecialType.Rainbow)
                {
                    SpecialType other = typeA == SpecialType.Rainbow ? typeB : typeA;
                    RemoveBoth(tA, tB);
                    yield return StartCoroutine(BombCombo(other));
                }
                else if (IsStriped(typeA) && IsStriped(typeB))
                {
                    int ax = tA.GridX, ay = tA.GridY, bx = tB.GridX, by = tB.GridY;
                    RemoveBoth(tA, tB);
                    yield return StartCoroutine(CrossBlast(ax, ay));
                    yield return StartCoroutine(CrossBlast(bx, by));
                }
                else if ((IsStriped(typeA) && typeB == SpecialType.Bomb)
                      || (typeA == SpecialType.Bomb && IsStriped(typeB)))
                {
                    int cx = tA.GridX, cy = tA.GridY;
                    RemoveBoth(tA, tB);
                    yield return StartCoroutine(Blast3x3(cx, cy));
                    yield return StartCoroutine(CrossBlast(cx, cy));
                }
                else if (typeA == SpecialType.Bomb && typeB == SpecialType.Bomb)
                {
                    int cx = tA.GridX, cy = tA.GridY;
                    RemoveBoth(tA, tB);
                    yield return StartCoroutine(Blast5x5(cx, cy));
                }
                else
                {
                    yield return StartCoroutine(FireSingle(tA));
                    yield return StartCoroutine(FireSingle(tB));
                }
            }
            else
            {
                Tile special    = aS ? tA : tB;
                Tile normalTile = aS ? tB : tA;
                SpecialType st  = aS ? typeA : typeB;

                if (st == SpecialType.Rainbow)
                {
                    TileColor target = (normalTile?.Data != null && normalTile.Data.color != TileColor.None)
                        ? normalTile.Data.color
                        : GetMostCommonColorOnBoard();

                    // FIX (bug report — "color bomb pe jelly thi, effect chala
                    // but jelly clear nahi hui"): this branch removed the bomb's
                    // own tile directly, without ever calling
                    // jellyManager.DecrementAt() on ITS OWN cell first — unlike
                    // FireSingle()/tap-to-activate, which always does. So a
                    // jelly layer sitting under the color bomb itself never got
                    // peeled when the bomb was fired via a SWAP. Mirror
                    // FireSingle()'s own-cell jelly handling here too.
                    if (jellyManager != null && jellyManager.DecrementAt(special.GridX, special.GridY))
                        levelManager?.OnJellyCleared();

                    boardGrid.RemoveTile(special.GridX, special.GridY);
                    if (target != TileColor.None)
                        yield return StartCoroutine(ClearAllOfColor(target));
                }
                else
                    yield return StartCoroutine(FireSingle(special));
            }

            yield return StartCoroutine(Settle());
            IsRunning = false;
        }

        /// <summary>Fallback target colour for a Rainbow (Color Bomb) swap when the swap partner has no colour of its own (e.g. a dropdown stone).</summary>
        private TileColor GetMostCommonColorOnBoard()
        {
            var counts = new System.Collections.Generic.Dictionary<TileColor, int>();
            for (int x = 0; x < boardGrid.Width; x++)
            for (int y = 0; y < boardGrid.Height; y++)
            {
                Tile t = boardGrid.GetTile(x, y);
                if (t == null || t.Data == null || t.Data.isSpecial || t.Data.color == TileColor.None) continue;
                counts.TryGetValue(t.Data.color, out int c);
                counts[t.Data.color] = c + 1;
            }

            TileColor best = TileColor.None;
            int bestCount = 0;
            foreach (var kv in counts)
                if (kv.Value > bestCount) { best = kv.Key; bestCount = kv.Value; }

            return best;
        }

        // ─────────────────────────────────────────────────────
        //  WHICH SPECIAL FIRES WHAT  (pattern selection — kept as-is)
        // ─────────────────────────────────────────────────────

        private IEnumerator FireSingle(Tile tile)
        {
            if (tile == null || tile.Data == null)
            {
                Debug.LogWarning("[SpecialTileActivator] FireSingle() got a null tile or null Data — aborting.");
                yield break;
            }

            int x = tile.GridX, y = tile.GridY;
            SpecialType type = tile.Data.specialType;

            if (jellyManager != null && jellyManager.DecrementAt(x, y))
                levelManager?.OnJellyCleared();

            boardGrid.RemoveTile(x, y);

            switch (type)
            {
                case SpecialType.RowBlast:
                    yield return StartCoroutine(BlastRow(y));
                    break;
                case SpecialType.ColBlast:
                    yield return StartCoroutine(BlastColumn(x));
                    break;
                case SpecialType.Bomb:
                    yield return StartCoroutine(Blast3x3(x, y));
                    yield return new WaitForSeconds(wrappedPause);
                    yield return StartCoroutine(Blast3x3(x, y));
                    break;
                case SpecialType.Rainbow:
                    TileColor most = GetMostCommonColor();
                    if (most != TileColor.None)
                        yield return StartCoroutine(ClearAllOfColor(most));
                    break;
            }
        }

        private IEnumerator BlastRow(int row)
        {
            var list = new List<Tile>();
            for (int x = 0; x < boardGrid.Width; x++)
            { Tile t = boardGrid.GetTile(x, row); if (t != null) list.Add(t); }
            yield return StartCoroutine(ClearList(list));
        }

        private IEnumerator BlastColumn(int col)
        {
            var list = new List<Tile>();
            for (int y = 0; y < boardGrid.Height; y++)
            { Tile t = boardGrid.GetTile(col, y); if (t != null) list.Add(t); }
            yield return StartCoroutine(ClearList(list));
        }

        private IEnumerator Blast3x3(int cx, int cy)
        {
            var list = new List<Tile>();
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            { Tile t = boardGrid.GetTile(cx+dx, cy+dy); if (t != null) list.Add(t); }
            yield return StartCoroutine(ClearList(list));
        }

        private IEnumerator Blast5x5(int cx, int cy)
        {
            var list = new List<Tile>();
            for (int dx = -2; dx <= 2; dx++)
            for (int dy = -2; dy <= 2; dy++)
            { Tile t = boardGrid.GetTile(cx+dx, cy+dy); if (t != null) list.Add(t); }
            yield return StartCoroutine(ClearList(list));
        }

        private IEnumerator CrossBlast(int cx, int cy)
        {
            yield return StartCoroutine(BlastRow(cy));
            yield return StartCoroutine(BlastColumn(cx));
        }

        private IEnumerator ClearAllOfColor(TileColor color)
        {
            var list = new List<Tile>();
            for (int x = 0; x < boardGrid.Width;  x++)
            for (int y = 0; y < boardGrid.Height; y++)
            {
                Tile t = boardGrid.GetTile(x, y);
                if (t != null && t.Data != null && t.Data.color == color) list.Add(t);
            }
            yield return StartCoroutine(ClearList(list));
        }

        private IEnumerator ClearEntireBoard()
        {
            var list = new List<Tile>();
            for (int x = 0; x < boardGrid.Width;  x++)
            for (int y = 0; y < boardGrid.Height; y++)
            { Tile t = boardGrid.GetTile(x, y); if (t != null) list.Add(t); }
            yield return StartCoroutine(ClearList(list));
        }

        private IEnumerator BombCombo(SpecialType targetType)
        {
            TileColor most = GetMostCommonColor();
            if (most == TileColor.None) yield break;
            TileData replacement = GetDataForType(targetType);
            if (replacement == null) yield break;

            var positions = new List<Vector2Int>();
            for (int x = 0; x < boardGrid.Width;  x++)
            for (int y = 0; y < boardGrid.Height; y++)
            {
                Tile t = boardGrid.GetTile(x, y);
                if (t != null && t.Data != null && t.Data.color == most)
                    positions.Add(new Vector2Int(x, y));
            }

            foreach (var pos in positions)
            {
                boardGrid.RemoveTile(pos.x, pos.y);
                Tile s = boardGrid.SpawnTile(pos.x, pos.y, replacement);
                if (s != null) { s.RefreshVisuals(); s.transform.DOPunchScale(Vector3.one * 0.4f, 0.2f, 4, 0.5f); }
                yield return new WaitForSeconds(blastDelay * 2f);
            }
            yield return new WaitForSeconds(0.08f);

            var toFire = new List<Tile>();
            for (int x = 0; x < boardGrid.Width;  x++)
            for (int y = 0; y < boardGrid.Height; y++)
            {
                Tile t = boardGrid.GetTile(x, y);
                if (t != null && t.Data != null && t.Data.isSpecial && t.Data.specialType == targetType)
                    toFire.Add(t);
            }
            foreach (var t in toFire)
                yield return StartCoroutine(FireSingle(t));
        }

        // ─────────────────────────────────────────────────────
        //  CLEAR — delegates the actual clear+score+goal work to
        //  BoardController.ClearTiles(). This method's only job is
        //  separating "tiles hit by the blast" into normals (which
        //  ClearTiles() further splits into colour/hard/stone) vs
        //  specials (chain-fire them).
        //
        //  canDamageHardTiles: true because every list built above
        //  (row/column/3x3/5x5/colour-sweep) is this special's own
        //  direct target area — any hard tile caught inside it is a
        //  genuine direct hit, never just a neighbour.
        // ─────────────────────────────────────────────────────

        private IEnumerator ClearList(List<Tile> tiles)
        {
            var normals  = new List<Tile>();
            var specials = new List<Tile>();

            foreach (Tile tile in tiles)
            {
                if (tile == null || tile.State == TileState.Inactive) continue;
                if (boardGrid.GetTile(tile.GridX, tile.GridY) != tile) continue;

                if (tile.Data != null && tile.Data.isSpecial) specials.Add(tile);
                else normals.Add(tile);
            }

            yield return StartCoroutine(boardController.ClearTiles(normals, canDamageHardTiles: true));

            foreach (Tile special in specials)
                yield return StartCoroutine(FireSingle(special));

            yield return new WaitForSeconds(blastDelay);
        }

        // ─────────────────────────────────────────────────────
        //  SETTLE
        // ─────────────────────────────────────────────────────

        private IEnumerator Settle()
        {
            yield return new WaitForSeconds(postSettleWait);
            yield return StartCoroutine(boardController.SettleAfterExternalClear());
        }

        // ─────────────────────────────────────────────────────

        private static bool IsSpecial(Tile t) => t != null && t.Data != null && t.Data.isSpecial;
        private static bool IsStriped(SpecialType t) => t == SpecialType.RowBlast || t == SpecialType.ColBlast;
        private void RemoveBoth(Tile a, Tile b)
        { boardGrid.RemoveTile(a.GridX, a.GridY); boardGrid.RemoveTile(b.GridX, b.GridY); }

        private TileColor GetMostCommonColor()
        {
            var counts = new Dictionary<TileColor, int>();
            for (int x = 0; x < boardGrid.Width;  x++)
            for (int y = 0; y < boardGrid.Height; y++)
            {
                Tile t = boardGrid.GetTile(x, y);
                if (t == null || t.Data == null || t.Data.color == TileColor.None || t.Data.isSpecial) continue;
                counts.TryGetValue(t.Data.color, out int c);
                counts[t.Data.color] = c + 1;
            }
            return counts.Count == 0 ? TileColor.None : counts.OrderByDescending(kv => kv.Value).First().Key;
        }

        private TileData GetDataForType(SpecialType t) => t switch
        {
            SpecialType.RowBlast => hStripedData,
            SpecialType.ColBlast => vStripedData,
            SpecialType.Bomb     => wrappedData,
            SpecialType.Rainbow  => colorBombData,
            _                    => null
        };

        private void Awake()
        {
            if (!boardGrid)       Debug.LogError("[SpecialTileActivator] boardGrid missing!", this);
            if (!boardController) Debug.LogError("[SpecialTileActivator] boardController missing! Clearing/scoring/gravity/refill/cascade will NOT run.", this);
            if (!levelManager)    Debug.LogWarning("[SpecialTileActivator] levelManager not assigned — jelly-goal callbacks won't fire for a special tile's own cell.", this);
            if (!jellyManager)    Debug.Log("[SpecialTileActivator] jellyManager not assigned — fine if this level doesn't use jelly.", this);
        }
    }
}