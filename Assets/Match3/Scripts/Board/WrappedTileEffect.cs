using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace Match3
{
    public class WrappedTileEffect : SpecialTileEffect
    {
        [Header("Wrapped Settings")]
        [SerializeField] private float pulsePause = 0.12f;
        [SerializeField] private GameObject expandRingPrefab;
        [SerializeField] private float ringExpandDuration = 0.25f;

        public override IEnumerator Activate(Vector2Int position, List<Tile> clearedTiles)
        {
            yield return StartCoroutine(Pulse3x3(position.x, position.y, clearedTiles));
            yield return new WaitForSeconds(pulsePause);
            yield return StartCoroutine(Pulse3x3(position.x, position.y, clearedTiles));

            AddScoreForCleared(clearedTiles.Count);
        }

        public IEnumerator Pulse3x3(int cx, int cy, List<Tile> clearedTiles)
        {
            PlayExpandRing(boardGrid.GridToWorld(cx, cy));

            var area = new List<Tile>();
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                Tile t = boardGrid.GetTile(cx + dx, cy + dy);
                if (t != null) area.Add(t);
            }

            yield return StartCoroutine(CircularClear(cx, cy, area, clearedTiles));
        }

        private IEnumerator CircularClear(int cx, int cy, List<Tile> area, List<Tile> cleared)
        {
            Tile centerTile = boardGrid.GetTile(cx, cy);
            if (centerTile != null && centerTile.State != TileState.Inactive)
                yield return StartCoroutine(BurstClear(centerTile, cleared));

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

            if (tile.Data != null && tile.Data.isSpecial && specialActivator != null)
            {
                cleared.Add(tile);
                yield return StartCoroutine(specialActivator.ChainActivate(tile));
                yield break;
            }

            if (tile.Data != null && tile.Data.isHardTile)
            {
                DamageHardTileDirect(tile, cleared);
                yield break;
            }

            if (tile.Data != null && tile.Data.isDropStone) yield break;

            ClearNormalTileTracked(tile, cleared);

            Vector3 pos = tile.transform.position;
            boardGrid.RemoveTile(tile.GridX, tile.GridY);
            tile.SetState(TileState.Matched);

            PlayParticleAt(pos);

            Sequence burst = DOTween.Sequence();
            burst.Append(tile.transform.DOScale(1.3f, 0.07f).SetEase(Ease.OutQuad));
            burst.Append(tile.transform.DOScale(0f,   0.1f).SetEase(Ease.InBack));
            burst.OnComplete(() => tile.transform.localScale = Vector3.one);

            if (!InstantBlast)
                yield return new WaitForSeconds(tileBlastDelay * 0.5f);
        }

        private void PlayExpandRing(Vector3 pos)
        {
            if (expandRingPrefab == null) return;

            GameObject ring = Instantiate(expandRingPrefab, pos, Quaternion.identity);
            ring.transform.localScale = Vector3.zero;
            ring.transform.DOScale(3.5f, ringExpandDuration).SetEase(Ease.OutCubic);

            SpriteRenderer sr = ring.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.DOFade(0f, ringExpandDuration).SetDelay(ringExpandDuration * 0.5f);

            Destroy(ring, ringExpandDuration + 0.1f);
        }
    }
}
