// ============================================================
//  StripedTileEffect.cs  —  SpecialTileEffect Subclass
//
//  Orientation ke hisaab se:
//    • Horizontal (RowBlast)  → poori row clear
//    • Vertical   (ColBlast)  → poora column clear
//
//  Attach to: StripedTileEffect GameObject (child of SpecialEffectsManager)
//  Assign in: SpecialCombinations Inspector
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
        [Tooltip("Row blast: horizontal laser trail prefab (optional)")]
        [SerializeField] private GameObject hLaserTrailPrefab;

        [Tooltip("Column blast: vertical laser trail prefab (optional)")]
        [SerializeField] private GameObject vLaserTrailPrefab;

        [Tooltip("Laser trail kitna waqt dikhega")]
        [SerializeField] private float laserDuration = 0.4f;

        // ─────────────────────────────────────────────────────
        //  PUBLIC OVERRIDE
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// TileData.specialType se orientation decide hoti hai:
        ///   RowBlast → row clear
        ///   ColBlast → column clear
        /// </summary>
        public override IEnumerator Activate(Vector2Int position, List<Tile> clearedTiles)
        {
            // Tile ka data lo taake orientation pata chale
            Tile sourceTile = boardGrid.GetTile(position.x, position.y);
            bool isRow = true;   // default horizontal

            if (sourceTile != null && sourceTile.Data != null)
                isRow = sourceTile.Data.specialType == SpecialType.RowBlast;

            // Laser trail animation (optional visual)
            Vector3 worldOrigin = boardGrid.GridToWorld(position.x, position.y);
            PlayLaserTrail(worldOrigin, isRow);

            if (isRow)
                yield return StartCoroutine(BlastRow(position.y, clearedTiles));
            else
                yield return StartCoroutine(BlastColumn(position.x, clearedTiles));

            AddScoreForCleared(clearedTiles.Count);
        }

        // ─────────────────────────────────────────────────────
        //  BLAST HELPERS
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// Public — SpecialCombinations bhi call karta hai directly.
        /// </summary>
        public IEnumerator BlastRow(int row, List<Tile> clearedTiles)
        {
            // Left to right wave effect ke liye sorted tiles
            var rowTiles = new List<Tile>();
            for (int x = 0; x < boardGrid.Width; x++)
            {
                Tile t = boardGrid.GetTile(x, row);
                if (t != null) rowTiles.Add(t);
            }

            // Scale wave — tiles ek ek karke pop honge
            yield return StartCoroutine(WaveClearHorizontal(rowTiles, clearedTiles));
        }

        /// <summary>
        /// Public — SpecialCombinations bhi call karta hai directly.
        /// </summary>
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
            // Pehle sab tiles ko X ke hisaab se sort karo (left → right)
            tiles.Sort((a, b) => a.GridX.CompareTo(b.GridX));

            foreach (Tile t in tiles)
            {
                if (t == null || t.State == TileState.Inactive) continue;
                if (boardGrid.GetTile(t.GridX, t.GridY) != t)  continue;

                // Stretch animation — tile horizontally squeeze hogi
                Sequence stretchSeq = DOTween.Sequence();
                stretchSeq.Append(t.transform.DOScaleX(1.4f, 0.06f).SetEase(Ease.OutQuad));
                stretchSeq.Append(t.transform.DOScaleX(0f,   0.08f).SetEase(Ease.InQuad));

                if (t.Data != null && !t.Data.isSpecial)
                    levelManager?.OnTileCleared(t.Data);

                cleared.Add(t);
                PlayParticleAt(t.transform.position);
                boardGrid.RemoveTile(t.GridX, t.GridY);
                t.SetState(TileState.Matched);

                yield return new WaitForSeconds(tileBlastDelay);
            }

            // Scale reset
            foreach (Tile t in tiles)
                if (t != null) t.transform.localScale = Vector3.one;
        }

        private IEnumerator WaveClearVertical(List<Tile> tiles, List<Tile> cleared)
        {
            // Bottom to top
            tiles.Sort((a, b) => a.GridY.CompareTo(b.GridY));

            foreach (Tile t in tiles)
            {
                if (t == null || t.State == TileState.Inactive) continue;
                if (boardGrid.GetTile(t.GridX, t.GridY) != t)  continue;

                Sequence stretchSeq = DOTween.Sequence();
                stretchSeq.Append(t.transform.DOScaleY(1.4f, 0.06f).SetEase(Ease.OutQuad));
                stretchSeq.Append(t.transform.DOScaleY(0f,   0.08f).SetEase(Ease.InQuad));

                if (t.Data != null && !t.Data.isSpecial)
                    levelManager?.OnTileCleared(t.Data);

                cleared.Add(t);
                PlayParticleAt(t.transform.position);
                boardGrid.RemoveTile(t.GridX, t.GridY);
                t.SetState(TileState.Matched);

                yield return new WaitForSeconds(tileBlastDelay);
            }

            foreach (Tile t in tiles)
                if (t != null) t.transform.localScale = Vector3.one;
        }

        // ─────────────────────────────────────────────────────
        //  LASER TRAIL VISUAL
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
