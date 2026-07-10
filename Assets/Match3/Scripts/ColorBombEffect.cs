// ============================================================
//  ColorBombEffect.cs  —  SpecialTileEffect Subclass
//
//  Board par jis color ki tiles sabse zyada hon, ya
//  jis tile ke saath swap hua ho — us color ki SARI tiles
//  board se hatata hai.
//
//  Swapped tile color → SpecialCombinations set karta hai
//  SetTargetColor() ke zariye Activate se pehle.
//
//  Standalone activate → sabse common color auto-detect.
//
//  Attach to: ColorBombEffect GameObject
//  Assign in: SpecialCombinations Inspector
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
        [Tooltip("Tiles ke target color ki taraf jaane wala arc particle (optional)")]
        [SerializeField] private GameObject arcParticlePrefab;

        [Tooltip("Center par rainbow flash prefab (optional)")]
        [SerializeField] private GameObject rainbowFlashPrefab;

        [Tooltip("Flash duration")]
        [SerializeField] private float flashDuration = 0.3f;

        [Tooltip("Tile clear karne ke beech minimum gap")]
        [SerializeField] private float minClearDelay = 0.03f;

        // ── Runtime state ─────────────────────────────────────

        // SpecialCombinations set karta hai swap se pehle
        private TileColor _targetColor = TileColor.None;
        private bool _targetColorSet = false;

        // ─────────────────────────────────────────────────────
        //  PUBLIC — SpecialCombinations call karta hai swap ke waqt
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// Swap ke waqt Color Bomb ne jo tile touch ki,
        /// us ka color yahan set karo.
        /// </summary>
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

            // Color determine karo
            TileColor colorToClear = _targetColorSet
                ? _targetColor
                : GetMostCommonColor();

            // Reset flag for next use
            _targetColor    = TileColor.None;
            _targetColorSet = false;

            if (colorToClear == TileColor.None)
            {
                Debug.LogWarning("[ColorBombEffect] No valid target color found.");
                yield break;
            }

            // Rainbow flash at bomb position
            PlayRainbowFlash(bombWorldPos);
            yield return new WaitForSeconds(flashDuration * 0.5f);

            // Sari target-color tiles collect karo
            var targets = GetAllTilesOfColor(colorToClear);

            Debug.Log($"[ColorBombEffect] Clearing {targets.Count} tiles of color {colorToClear}");

            // Arc animation + clear — har tile ki taraf ek arc jaata hai
            yield return StartCoroutine(ArcAndClear(bombWorldPos, targets, clearedTiles));

            AddScoreForCleared(clearedTiles.Count);
        }

        // ─────────────────────────────────────────────────────
        //  ARC CLEAR ANIMATION
        // ─────────────────────────────────────────────────────

        private IEnumerator ArcAndClear(Vector3 origin, List<Tile> targets, List<Tile> cleared)
        {
            // Distance ke hisaab se sort — nearest first
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

                // Optional arc particle origin → tile
                PlayArcParticle(origin, tilePos);

                // Tile glow animation
                Sequence glow = DOTween.Sequence();
                glow.Append(t.transform.DOScale(1.25f, 0.07f).SetEase(Ease.OutFlash, 2));

                // Clear after short delay (arc travel time feel)
                yield return new WaitForSeconds(minClearDelay);

                if (t == null || t.State == TileState.Inactive) continue;
                if (boardGrid.GetTile(t.GridX, t.GridY) != t)  continue;

                if (t.Data != null && !t.Data.isSpecial)
                    levelManager?.OnTileCleared(t.Data);

                cleared.Add(t);
                PlayParticleAt(tilePos);
                PlaySparkleAt(tilePos);

                boardGrid.RemoveTile(t.GridX, t.GridY);
                t.SetState(TileState.Matched);

                // Pop zero animation
                t.transform.DOScale(Vector3.zero, 0.12f)
                    .SetEase(Ease.InBack)
                    .OnComplete(() => t.transform.localScale = Vector3.one);
            }

            yield return new WaitForSeconds(0.1f);
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

            // Scale up flash
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

            // Arc ko target ki taraf move karo
            arc.transform.DOMove(to, 0.12f)
                .SetEase(Ease.InQuad)
                .OnComplete(() => Destroy(arc));
        }
    }
}
