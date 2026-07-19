// ============================================================
//  SpecialTileEffect.cs  —  Abstract Base Class
//
//  Har special tile effect is class se inherit karta hai.
//  Subclasses: StripedTileEffect, WrappedTileEffect, ColorBombEffect
//
//  REDESIGN (bug report ke baad — hard tile damage):
//    Pehle hard tile is blast ke path mein aane par bilkul SKIP ho
//    jaata tha (immune), aur uske baad poori "cleared positions" list
//    se ADJACENT hard tiles ko separately damage kiya jata tha.
//    Ab wo adjacency mechanic bilkul hata di gayi hai. Ab jab bhi
//    hard tile is blast ke apne target path (row/column/3x3/5x5/
//    colour-sweep) mein khud aata hai, wahi ek DIRECT HIT gina jata
//    hai — DamageHardTileDirect() turant 1 damage laga deta hai.
//    Hard tile kabhi bhi sirf "paas wali cell clear hui" isliye
//    damage nahi leta.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace Match3
{
    public abstract class SpecialTileEffect : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────

        [Header("Particle FX Prefabs")]
        [SerializeField] protected GameObject blastParticlePrefab;
        [SerializeField] protected GameObject sparkleParticlePrefab;

        [Header("Timings")]
        [SerializeField] protected float tileBlastDelay = 0.02f;
        [SerializeField] protected float effectDuration = 0.15f;

        // ── References (set by SpecialCombinations in Awake) ──
        [HideInInspector] public BoardGrid              boardGrid;
        [HideInInspector] public LevelManager           levelManager;
        [HideInInspector] public SpecialTileActivator   specialActivator;
        [HideInInspector] public JellyManager           jellyManager;

        public abstract IEnumerator Activate(Vector2Int position, List<Tile> clearedTiles);

        // ─────────────────────────────────────────────────────
        //  SHARED HELPER — clear a single NORMAL tile correctly
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// Reports goal progress + peels jelly for a NORMAL tile that a combo
        /// blast is clearing. Callers must have already ruled out special /
        /// hard / drop-stone tiles before calling this.
        /// </summary>
        protected void ClearNormalTileTracked(Tile t, List<Tile> cleared)
        {
            levelManager?.OnTileCleared(t.Data);

            if (jellyManager != null && jellyManager.DecrementAt(t.GridX, t.GridY))
                levelManager?.OnJellyCleared();

            cleared.Add(t);
        }

        // ─────────────────────────────────────────────────────
        //  SHARED HELPER — a hard tile caught DIRECTLY in this
        //  blast's own path (its cell IS one of the target cells)
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// Call this when a hard tile's OWN cell is one of the cells this
        /// blast is actually targeting (a row/column cell, a 3x3/5x5 cell,
        /// a colour-sweep hit) — i.e. a genuine DIRECT hit, never just
        /// "next to something that cleared". Applies exactly 1 point of
        /// damage. If that breaks it (HP reaches 0) it's cleared through
        /// the normal goal/score/animation path and added to `cleared`.
        /// If it survives, it's left exactly where it is — Tile.DamageObstacle()
        /// already played its own crack-sprite + punch-scale feedback.
        /// </summary>
        protected void DamageHardTileDirect(Tile t, List<Tile> cleared)
        {
            bool broke = t.DamageObstacle();
            if (!broke) return;   // took damage, still standing

            levelManager?.OnHardTileCleared();
            cleared.Add(t);
            boardGrid.RemoveTile(t.GridX, t.GridY);
            t.SetState(TileState.Matched);
            t.transform.DOScale(Vector3.zero, 0.15f).SetEase(Ease.InBack)
                .OnComplete(() => t.transform.localScale = Vector3.one);
        }

        // ─────────────────────────────────────────────────────
        //  SHARED HELPERS
        // ─────────────────────────────────────────────────────

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