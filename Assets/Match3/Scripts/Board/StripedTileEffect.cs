using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace Match3
{
    public class StripedTileEffect : SpecialTileEffect
    {
        [Header("Stripe Settings")]
        [SerializeField] private GameObject hLaserTrailPrefab;
        [SerializeField] private GameObject vLaserTrailPrefab;
        [SerializeField] private float laserDuration = 0.4f;

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

        private IEnumerator WaveClearHorizontal(List<Tile> tiles, List<Tile> cleared)
        {
            tiles.Sort((a, b) => a.GridX.CompareTo(b.GridX));

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

                if (t.Data != null && t.Data.isHardTile)
                {
                    DamageHardTileDirect(t, cleared);
                    continue;
                }

                if (t.Data != null && t.Data.isDropStone) continue;

                Sequence stretchSeq = DOTween.Sequence();
                stretchSeq.Append(t.transform.DOScaleX(1.4f, 0.06f).SetEase(Ease.OutQuad));
                stretchSeq.Append(t.transform.DOScaleX(0f,   0.08f).SetEase(Ease.InQuad));

                ClearNormalTileTracked(t, cleared);

                PlayParticleAt(t.transform.position);
                boardGrid.RemoveTile(t.GridX, t.GridY);
                t.SetState(TileState.Matched);

                if (!InstantBlast)
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

                if (!InstantBlast)
                    yield return new WaitForSeconds(tileBlastDelay);
            }

            foreach (Tile t in tiles)
                if (t != null) t.transform.localScale = Vector3.one;
        }

        private void PlayLaserTrail(Vector3 origin, bool horizontal)
        {
            GameObject prefab = horizontal ? hLaserTrailPrefab : vLaserTrailPrefab;
            if (prefab == null) return;

            GameObject trail = Instantiate(prefab, origin, Quaternion.identity);
            Destroy(trail, laserDuration);
        }
    }
}
