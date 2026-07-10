// ============================================================
//  WrappedTileEffect.cs  —  SpecialTileEffect Subclass
//
//  Double Pulse Mechanic (Candy Crush Wrapped style):
//    Pulse 1: Match hone par → 3×3 area clear
//    Pulse 2: Jab cleared tile dobara match hoti → 3×3 phir clear
//
//  Agar directly activate (single click) ho to dono pulses
//  ek ke baad ek fire hote hain.
//
//  Attach to: WrappedTileEffect GameObject
//  Assign in: SpecialCombinations Inspector
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace Match3
{
    public class WrappedTileEffect : SpecialTileEffect
    {
        // ── Inspector ─────────────────────────────────────────

        [Header("Wrapped Settings")]
        [Tooltip("Dono pulses ke beech ruk jaane ka waqt")]
        [SerializeField] private float pulsePause = 0.3f;

        [Tooltip("Expand ring prefab — pulse ke waqt play hogi (optional)")]
        [SerializeField] private GameObject expandRingPrefab;

        [Tooltip("Ring kitni dair mein expand ho")]
        [SerializeField] private float ringExpandDuration = 0.25f;

        // ── Internal state — pulse tracking ──────────────────

        // Wrapped tile ka world position store karte hain taake
        // dono pulses same jagah se fire hon
        private Vector3 _epicenter;

        // ─────────────────────────────────────────────────────
        //  PUBLIC OVERRIDE
        // ─────────────────────────────────────────────────────

        public override IEnumerator Activate(Vector2Int position, List<Tile> clearedTiles)
        {
            _epicenter = boardGrid.GridToWorld(position.x, position.y);

            // ── PULSE 1 ───────────────────────────────────────
            yield return StartCoroutine(Pulse3x3(position.x, position.y, clearedTiles));

            yield return new WaitForSeconds(pulsePause);

            // ── PULSE 2 — same center ─────────────────────────
            yield return StartCoroutine(Pulse3x3(position.x, position.y, clearedTiles));

            AddScoreForCleared(clearedTiles.Count);
        }

        // ─────────────────────────────────────────────────────
        //  PUBLIC — SpecialCombinations call karta hai directly
        //  when doing Wrapped + Striped combo (5-row sweep)
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// Single 3×3 pulse. SpecialCombinations bhi use karta hai.
        /// </summary>
        public IEnumerator Pulse3x3(int cx, int cy, List<Tile> clearedTiles)
        {
            // Expanding ring visual
            PlayExpandRing(_epicenter == Vector3.zero
                ? boardGrid.GridToWorld(cx, cy)
                : _epicenter);

            // Sab 3x3 tiles collect karo
            var area = new List<Tile>();
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                Tile t = boardGrid.GetTile(cx + dx, cy + dy);
                if (t != null) area.Add(t);
            }

            // Circular clear: center se bahar ki taraf
            yield return StartCoroutine(CircularClear(cx, cy, area, clearedTiles));
        }

        // ─────────────────────────────────────────────────────
        //  CIRCULAR CLEAR ANIMATION
        // ─────────────────────────────────────────────────────

        private IEnumerator CircularClear(int cx, int cy, List<Tile> area, List<Tile> cleared)
        {
            // Center pehle, phir ring mein
            Tile centerTile = boardGrid.GetTile(cx, cy);
            if (centerTile != null && centerTile.State != TileState.Inactive)
            {
                yield return StartCoroutine(BurstClear(centerTile, cleared));
            }

            // 8 surrounding tiles
            var ring = new List<Tile>();
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                Tile t = boardGrid.GetTile(cx + dx, cy + dy);
                if (t != null && t.State != TileState.Inactive)
                    ring.Add(t);
            }

            foreach (Tile t in ring)
                yield return StartCoroutine(BurstClear(t, cleared));
        }

        private IEnumerator BurstClear(Tile tile, List<Tile> cleared)
        {
            if (tile == null)                                       yield break;
            if (tile.State == TileState.Inactive)                   yield break;
            if (boardGrid.GetTile(tile.GridX, tile.GridY) != tile)  yield break;

            if (tile.Data != null && !tile.Data.isSpecial)
                levelManager?.OnTileCleared(tile.Data);

            cleared.Add(tile);
            Vector3 pos = tile.transform.position;
            boardGrid.RemoveTile(tile.GridX, tile.GridY);
            tile.SetState(TileState.Matched);

            PlayParticleAt(pos);

            // Burst animation — scale up phir zero
            Sequence burst = DOTween.Sequence();
            burst.Append(tile.transform.DOScale(1.3f, 0.07f).SetEase(Ease.OutQuad));
            burst.Append(tile.transform.DOScale(0f,   0.1f).SetEase(Ease.InBack));
            burst.OnComplete(() => tile.transform.localScale = Vector3.one);

            yield return new WaitForSeconds(tileBlastDelay * 0.5f);
        }

        // ─────────────────────────────────────────────────────
        //  EXPAND RING VISUAL
        // ─────────────────────────────────────────────────────

        private void PlayExpandRing(Vector3 pos)
        {
            if (expandRingPrefab == null) return;

            GameObject ring = Instantiate(expandRingPrefab, pos, Quaternion.identity);

            // DOTween se ring expand karein
            ring.transform.localScale = Vector3.zero;
            ring.transform.DOScale(3.5f, ringExpandDuration)
                .SetEase(Ease.OutCubic);

            SpriteRenderer sr = ring.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.DOFade(0f, ringExpandDuration)
                  .SetDelay(ringExpandDuration * 0.5f);
            }

            Destroy(ring, ringExpandDuration + 0.1f);
        }
    }
}
