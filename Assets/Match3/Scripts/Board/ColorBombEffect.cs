// ============================================================
//  ColorBombEffect.cs  —  SpecialTileEffect Subclass
//
//  Board par jis color ki tiles sabse zyada hon, ya jis tile ke
//  saath swap hua ho — us color ki SARI tiles hataata hai.
//
//  REDESIGN (bug report ke baad — hard tile damage):
//    Agar kabhi hard tile ka apna color us target color se match
//    kare (rare — hard tiles usually TileColor.None hote hain), to
//    yeh ab uska cell DIRECT hit ginta hai aur damage lagata hai,
//    adjacency ke through nahi.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;

namespace Match3
{
    public class ColorBombEffect : SpecialTileEffect
    {
        // ── Inspector ─────────────────────────────────────────

        [Header("Color Bomb Settings")]
        [SerializeField] private GameObject arcParticlePrefab;
        [SerializeField] private GameObject rainbowFlashPrefab;
        [SerializeField] private float flashDuration = 0.18f;
        [SerializeField] private float minClearDelay = 0.02f;

        // ── Runtime state ─────────────────────────────────────

        private TileColor _targetColor = TileColor.None;
        private bool _targetColorSet = false;

        // ─────────────────────────────────────────────────────

        public void SetTargetColor(TileColor color)
        {
            _targetColor     = color;
            _targetColorSet  = true;
        }

        // ─────────────────────────────────────────────────────
        //  PUBLIC OVERRIDE
        // ─────────────────────────────────────────────────────

        public override IEnumerator Activate(Vector2Int position, List<Tile> clearedTiles)
        {
            Vector3 bombWorldPos = boardGrid.GridToWorld(position.x, position.y);

            TileColor colorToClear = _targetColorSet
                ? _targetColor
                : GetMostCommonColor();

            _targetColor    = TileColor.None;
            _targetColorSet = false;

            if (colorToClear == TileColor.None)
            {
                Debug.LogWarning("[ColorBombEffect] No valid target color found.");
                yield break;
            }

            // FIX (same root cause as the swap-activation bug): GetAllTilesOfColor()
            // below explicitly excludes special tiles, so the color bomb's OWN cell
            // (isSpecial == true) never appeared in `targets` and its jelly was
            // never peeled — even though the bomb clearly fires from that cell.
            // Peel its own cell's jelly here, same as every other clear path does.
            if (jellyManager != null && jellyManager.DecrementAt(position.x, position.y))
                levelManager?.OnJellyCleared();

            PlayRainbowFlash(bombWorldPos);
            yield return new WaitForSeconds(flashDuration * 0.5f);

            var targets = GetAllTilesOfColor(colorToClear);

            Debug.Log($"[ColorBombEffect] Clearing {targets.Count} tiles of color {colorToClear}");

            yield return StartCoroutine(ArcAndClear(bombWorldPos, targets, clearedTiles));

            AddScoreForCleared(clearedTiles.Count);

            // NEW (Rabia's request): a Color Bomb's single blast always hurts
            // the boss at the "5+ weakness tiles" tier — regardless of which
            // colour it actually cleared. Every other special tile's single
            // blast does zero boss damage now (see SpecialTileEffect.
            // ClearNormalTileTracked's reportBossDamage default).
            if (targets.Count > 0)
                BossDamageEvents.OnColorBombBlast?.Invoke();
        }

        // ─────────────────────────────────────────────────────
        //  ARC CLEAR ANIMATION
        // ─────────────────────────────────────────────────────

        private IEnumerator ArcAndClear(Vector3 origin, List<Tile> targets, List<Tile> cleared)
        {
            targets.Sort((a, b) =>
            {
                float da = Vector3.Distance(origin, a.transform.position);
                float db = Vector3.Distance(origin, b.transform.position);
                return da.CompareTo(db);
            });

            foreach (Tile t in targets)
            {
                if (t == null || t.State == TileState.Inactive) continue;
                if (boardGrid.GetTile(t.GridX, t.GridY) != t)  continue;

                Vector3 tilePos = t.transform.position;
                PlayArcParticle(origin, tilePos);

                Sequence glow = DOTween.Sequence();
                glow.Append(t.transform.DOScale(1.25f, 0.07f).SetEase(Ease.OutFlash, 2));

                yield return new WaitForSeconds(minClearDelay);

                if (t == null || t.State == TileState.Inactive) continue;
                if (boardGrid.GetTile(t.GridX, t.GridY) != t)  continue;

                // Special tile caught by the color bomb's sweep — chain-fire it.
                if (t.Data != null && t.Data.isSpecial && specialActivator != null)
                {
                    cleared.Add(t);
                    yield return StartCoroutine(specialActivator.ChainActivate(t));
                    continue;
                }

                // FIX: hard tile whose colour happened to match is a DIRECT
                // hit — damage it right here (1 point).
                if (t.Data != null && t.Data.isHardTile)
                {
                    DamageHardTileDirect(t, cleared);
                    continue;
                }

                ClearNormalTileTracked(t, cleared);

                PlayParticleAt(tilePos);
                PlaySparkleAt(tilePos);

                boardGrid.RemoveTile(t.GridX, t.GridY);
                t.SetState(TileState.Matched);

                t.transform.DOScale(Vector3.zero, 0.12f)
                    .SetEase(Ease.InBack)
                    .OnComplete(() => t.transform.localScale = Vector3.one);
            }

            yield return new WaitForSeconds(0.05f);
        }

        // ─────────────────────────────────────────────────────
        //  HELPERS
        // ─────────────────────────────────────────────────────

        private List<Tile> GetAllTilesOfColor(TileColor color)
        {
            var list = new List<Tile>();
            for (int x = 0; x < boardGrid.Width;  x++)
            for (int y = 0; y < boardGrid.Height; y++)
            {
                Tile t = boardGrid.GetTile(x, y);
                if (t != null && t.Data != null
                    && !t.Data.isSpecial
                    && t.Data.color == color)
                    list.Add(t);
            }
            return list;
        }

        private TileColor GetMostCommonColor()
        {
            var counts = new Dictionary<TileColor, int>();
            for (int x = 0; x < boardGrid.Width;  x++)
            for (int y = 0; y < boardGrid.Height; y++)
            {
                Tile t = boardGrid.GetTile(x, y);
                if (t == null || t.Data == null) continue;
                if (t.Data.color == TileColor.None || t.Data.isSpecial) continue;

                counts.TryGetValue(t.Data.color, out int c);
                counts[t.Data.color] = c + 1;
            }
            return counts.Count == 0
                ? TileColor.None
                : counts.OrderByDescending(kv => kv.Value).First().Key;
        }

        private void PlayRainbowFlash(Vector3 pos)
        {
            if (rainbowFlashPrefab == null) return;
            GameObject flash = Instantiate(rainbowFlashPrefab, pos, Quaternion.identity);

            flash.transform.localScale = Vector3.one * 0.1f;
            flash.transform.DOScale(4f, flashDuration).SetEase(Ease.OutQuart);

            SpriteRenderer sr = flash.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.DOFade(0f, flashDuration).SetDelay(flashDuration * 0.3f);

            Destroy(flash, flashDuration + 0.1f);
        }

        private void PlayArcParticle(Vector3 from, Vector3 to)
        {
            if (arcParticlePrefab == null) return;

            GameObject arc = Instantiate(arcParticlePrefab, from, Quaternion.identity);

            arc.transform.DOMove(to, 0.12f)
                .SetEase(Ease.InQuad)
                .OnComplete(() => Destroy(arc));
        }
    }
}