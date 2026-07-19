// ============================================================
//  StripedTileEffect.cs  —  SpecialTileEffect Subclass
//
//  Orientation ke hisaab se:
//    • Horizontal (RowBlast)  → poori row clear
//    • Vertical   (ColBlast)  → poora column clear
//
//  REDESIGN (bug report ke baad — hard tile damage):
//    Ab jab hard tile khud is row/column ke path mein aata hai, wahi
//    ek DIRECT HIT hai — turant 1 damage lagta hai (DamageHardTileDirect).
//    Poori row/column ki adjacency-based damage (jo door wali hardtiles
//    ko bhi galat tareeqe se destroy kar deti thi) bilkul hata di gayi
//    hai — ab hardtile SIRF apni khud ki cell blast hone par damage
//    leta hai, kabhi bhi paas wali cell clear hone se nahi.
//
//    (Pehle wale fix mein jelly-decrement + hardtile-skip add kiya
//    gaya tha — wo dono still yahan hain, sirf hardtile handling
//    "skip + adjacency" se "direct damage" mein badal gayi hai.)
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace Match3
{
    public class StripedTileEffect : SpecialTileEffect
    {
        // ── Inspector ─────────────────────────────────────────

        [Header("Stripe Settings")]
        [SerializeField] private GameObject hLaserTrailPrefab;
        [SerializeField] private GameObject vLaserTrailPrefab;
        [SerializeField] private float laserDuration = 0.4f;

        // ─────────────────────────────────────────────────────
        //  PUBLIC OVERRIDE
        // ─────────────────────────────────────────────────────

        public override IEnumerator Activate(Vector2Int position, List<Tile> clearedTiles)
        {
            Tile sourceTile = boardGrid.GetTile(position.x, position.y);
            bool isRow = true;

            if (sourceTile != null && sourceTile.Data != null)
                isRow = sourceTile.Data.specialType == SpecialType.RowBlast;

            Vector3 worldOrigin = boardGrid.GridToWorld(position.x, position.y);
            PlayLaserTrail(worldOrigin, isRow);

            if (isRow)
                yield return StartCoroutine(BlastRow(position.y, clearedTiles));
            else
                yield return StartCoroutine(BlastColumn(position.x, clearedTiles));

            AddScoreForCleared(clearedTiles.Count);
        }

        // ─────────────────────────────────────────────────────
        //  BLAST HELPERS  (public — SpecialCombinations calls these directly)
        // ─────────────────────────────────────────────────────

        public IEnumerator BlastRow(int row, List<Tile> clearedTiles)
        {
            var rowTiles = new List<Tile>();
            for (int x = 0; x < boardGrid.Width; x++)
            {
                Tile t = boardGrid.GetTile(x, row);
                if (t != null) rowTiles.Add(t);
            }

            yield return StartCoroutine(WaveClearHorizontal(rowTiles, clearedTiles));
        }

        public IEnumerator BlastColumn(int col, List<Tile> clearedTiles)
        {
            var colTiles = new List<Tile>();
            for (int y = 0; y < boardGrid.Height; y++)
            {
                Tile t = boardGrid.GetTile(col, y);
                if (t != null) colTiles.Add(t);
            }

            yield return StartCoroutine(WaveClearVertical(colTiles, clearedTiles));
        }

        // ─────────────────────────────────────────────────────
        //  WAVE CLEAR ANIMATIONS
        // ─────────────────────────────────────────────────────

        private IEnumerator WaveClearHorizontal(List<Tile> tiles, List<Tile> cleared)
        {
            tiles.Sort((a, b) => a.GridX.CompareTo(b.GridX));

            foreach (Tile t in tiles)
            {
                if (t == null || t.State == TileState.Inactive) continue;
                if (boardGrid.GetTile(t.GridX, t.GridY) != t)  continue;

                // Special tile caught inside this row blast — chain-fire it.
                if (t.Data != null && t.Data.isSpecial && specialActivator != null)
                {
                    cleared.Add(t);
                    yield return StartCoroutine(specialActivator.ChainActivate(t));
                    continue;
                }

                // FIX: hard tile whose OWN cell is inside this row is a DIRECT
                // hit — damage it right here (1 point), don't skip it and
                // don't treat it as a normal colour tile.
                if (t.Data != null && t.Data.isHardTile)
                {
                    DamageHardTileDirect(t, cleared);
                    continue;
                }

                // Dropdown stone — IMMUNE to this clear source.
                if (t.Data != null && t.Data.isDropStone) continue;

                Sequence stretchSeq = DOTween.Sequence();
                stretchSeq.Append(t.transform.DOScaleX(1.4f, 0.06f).SetEase(Ease.OutQuad));
                stretchSeq.Append(t.transform.DOScaleX(0f,   0.08f).SetEase(Ease.InQuad));

                ClearNormalTileTracked(t, cleared);

                PlayParticleAt(t.transform.position);
                boardGrid.RemoveTile(t.GridX, t.GridY);
                t.SetState(TileState.Matched);

                yield return new WaitForSeconds(tileBlastDelay);
            }

            foreach (Tile t in tiles)
                if (t != null) t.transform.localScale = Vector3.one;
        }

        private IEnumerator WaveClearVertical(List<Tile> tiles, List<Tile> cleared)
        {
            tiles.Sort((a, b) => a.GridY.CompareTo(b.GridY));

            foreach (Tile t in tiles)
            {
                if (t == null || t.State == TileState.Inactive) continue;
                if (boardGrid.GetTile(t.GridX, t.GridY) != t)  continue;

                if (t.Data != null && t.Data.isSpecial && specialActivator != null)
                {
                    cleared.Add(t);
                    yield return StartCoroutine(specialActivator.ChainActivate(t));
                    continue;
                }

                // FIX: same direct-hit hard-tile damage as WaveClearHorizontal.
                if (t.Data != null && t.Data.isHardTile)
                {
                    DamageHardTileDirect(t, cleared);
                    continue;
                }

                if (t.Data != null && t.Data.isDropStone) continue;

                Sequence stretchSeq = DOTween.Sequence();
                stretchSeq.Append(t.transform.DOScaleY(1.4f, 0.06f).SetEase(Ease.OutQuad));
                stretchSeq.Append(t.transform.DOScaleY(0f,   0.08f).SetEase(Ease.InQuad));

                ClearNormalTileTracked(t, cleared);

                PlayParticleAt(t.transform.position);
                boardGrid.RemoveTile(t.GridX, t.GridY);
                t.SetState(TileState.Matched);

                yield return new WaitForSeconds(tileBlastDelay);
            }

            foreach (Tile t in tiles)
                if (t != null) t.transform.localScale = Vector3.one;
        }

        // ─────────────────────────────────────────────────────

        private void PlayLaserTrail(Vector3 origin, bool horizontal)
        {
            GameObject prefab = horizontal ? hLaserTrailPrefab : vLaserTrailPrefab;
            if (prefab == null) return;

            GameObject trail = Instantiate(prefab, origin, Quaternion.identity);
            Destroy(trail, laserDuration);
        }
    }
}