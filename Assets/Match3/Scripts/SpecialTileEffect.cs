// ============================================================
//  SpecialTileEffect.cs  —  Abstract Base Class
//
//  Har special tile effect is class se inherit karta hai.
//  Activate() override karo apna effect implement karne ke liye.
//
//  DO NOT attach this directly — yeh abstract hai.
//  Subclasses: StripedTileEffect, WrappedTileEffect, ColorBombEffect
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace Match3
{
    /// <summary>
    /// Base class for all special tile effects.
    /// Subclasses implement Activate() to define blast pattern.
    /// </summary>
    public abstract class SpecialTileEffect : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────

        [Header("Particle FX Prefabs")]
        [Tooltip("Board pe ek jagah play hone wala particle effect")]
        [SerializeField] protected GameObject blastParticlePrefab;

        [Tooltip("Sirf Color Bomb ke liye — chhota spark effect")]
        [SerializeField] protected GameObject sparkleParticlePrefab;

        [Header("Timings")]
        [SerializeField] protected float tileBlastDelay = 0.04f;
        [SerializeField] protected float effectDuration = 0.15f;

        // ── References (set by SpecialCombinations in Awake) ──
        [HideInInspector] public BoardGrid    boardGrid;
        [HideInInspector] public LevelManager levelManager;

        // ─────────────────────────────────────────────────────
        //  ABSTRACT — subclass must implement this
        // ─────────────────────────────────────────────────────

        public abstract IEnumerator Activate(Vector2Int position, List<Tile> clearedTiles);

        // ─────────────────────────────────────────────────────
        //  SHARED HELPERS
        // ─────────────────────────────────────────────────────

        protected IEnumerator ClearSingleTile(Tile tile, List<Tile> cleared)
        {
            if (tile == null)                                          yield break;
            if (tile.State == TileState.Inactive)                      yield break;
            if (boardGrid.GetTile(tile.GridX, tile.GridY) != tile)     yield break;

            // GoalTracker ko inform karo
            if (tile.Data != null && !tile.Data.isSpecial)
                levelManager?.OnTileCleared(tile.Data);

            cleared.Add(tile);

            Vector3 worldPos = tile.transform.position;
            boardGrid.RemoveTile(tile.GridX, tile.GridY);
            tile.SetState(TileState.Matched);

            PlayParticleAt(worldPos);

            // FIX: ?? operator DOTween ke saath nahi chalta
            // SpriteRenderer alag se lo, null check manually karo
            SpriteRenderer sr = tile.GetComponent<SpriteRenderer>();

            Sequence seq = DOTween.Sequence();
            seq.Join(tile.transform.DOScale(Vector3.zero, effectDuration).SetEase(Ease.InBack));
            if (sr != null)
                seq.Join(sr.DOFade(0f, effectDuration));

            seq.OnComplete(() =>
            {
                tile.transform.localScale = Vector3.one;
                if (sr != null) sr.color = Color.white;
            });

            yield return new WaitForSeconds(tileBlastDelay);
        }

        protected IEnumerator ClearTileList(List<Tile> tiles, List<Tile> cleared)
        {
            foreach (Tile t in tiles)
                yield return StartCoroutine(ClearSingleTile(t, cleared));
        }

        protected void PlayParticleAt(Vector3 worldPos)
        {
            if (blastParticlePrefab == null) return;
            GameObject fx = Instantiate(blastParticlePrefab, worldPos, Quaternion.identity);
            Destroy(fx, 2f);
        }

        protected void PlaySparkleAt(Vector3 worldPos)
        {
            if (sparkleParticlePrefab == null) return;
            GameObject fx = Instantiate(sparkleParticlePrefab, worldPos, Quaternion.identity);
            Destroy(fx, 1.5f);
        }

        protected void AddScoreForCleared(int count)
        {
            if (count > 0)
                levelManager?.AddScore(count * 50);
        }
    }
}