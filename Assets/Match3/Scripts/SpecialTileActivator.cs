// ============================================================
//  SpecialTileActivator.cs  —  Set to use BoardController's shared pipeline
//
//  Before: this file had its OWN gravity implementation and its OWN
//  match-cascade loop (Settle()/ApplyGravity()), separate from — and
//  subtly different from — the one in BoardController/GravitySystem.
//  It also duplicated the "clear tile + notify LevelManager + add score"
//  code that already lives in BoardController.ClearTiles().
//
//  Now:
//    • ClearList()  -> still decides WHICH tiles a blast pattern hits,
//                      but hands the actual clearing to BoardController.ClearTiles().
//    • Settle()     -> just calls BoardController.SettleAfterExternalClear(),
//                      so gravity, refill AND any resulting cascades (including
//                      new special tiles) go through the exact same code path
//                      as a normal swap-match.
//
//  Unity wiring: this component now needs a BoardController reference
//  assigned in the Inspector (tileSpawner / matchFinder are no longer
//  needed here — BoardController already has them).
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

        [Header("Special TileData Assets")]
        [SerializeField] private TileData hStripedData;
        [SerializeField] private TileData vStripedData;
        [SerializeField] private TileData wrappedData;
        [SerializeField] private TileData colorBombData;

        [Header("Timings")]
        [SerializeField] private float blastDelay   = 0.04f;
        [SerializeField] private float wrappedPause = 0.25f;
        [SerializeField] private float postSettleWait = 0.08f;

        public bool IsRunning { get; private set; }

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
                    TileColor target = normalTile?.Data != null
                        ? normalTile.Data.color : TileColor.None;
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

        // ─────────────────────────────────────────────────────
        //  WHICH SPECIAL FIRES WHAT  (pattern selection — kept as-is)
        // ─────────────────────────────────────────────────────

        private IEnumerator FireSingle(Tile tile)
        {
            if (tile == null || tile.Data == null)                 yield break;
            if (tile.State == TileState.Inactive)                  yield break;
            if (boardGrid.GetTile(tile.GridX, tile.GridY) != tile)  yield break;

            SpecialType st = tile.Data.specialType;
            int x = tile.GridX, y = tile.GridY;
            boardGrid.RemoveTile(x, y);

            switch (st)
            {
                case SpecialType.RowBlast: yield return StartCoroutine(BlastRow(y));    break;
                case SpecialType.ColBlast: yield return StartCoroutine(BlastColumn(x)); break;
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
            yield return new WaitForSeconds(0.15f);

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
        //  CLEAR — now delegates the actual clear+score+goal work
        //  to BoardController.ClearTiles(). This method's only job
        //  is separating "tiles hit by the blast" into normals
        //  (clear them) vs specials (chain-fire them).
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

            yield return StartCoroutine(boardController.ClearTiles(normals));

            foreach (Tile special in specials)
                yield return StartCoroutine(FireSingle(special));

            yield return new WaitForSeconds(blastDelay);
        }

        // ─────────────────────────────────────────────────────
        //  SETTLE — now delegates gravity + refill + cascade to
        //  BoardController instead of keeping a second copy of it.
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
        }
    }
}