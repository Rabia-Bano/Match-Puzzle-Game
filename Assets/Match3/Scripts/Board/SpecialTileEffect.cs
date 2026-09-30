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
        /// NEW (Rabia's rule, corrected): controls whether ClearNormalTileTracked()
        /// below reports each cleared weakness-colour tile to
        /// BossDamageEvents.OnSpecialTileCleared.
        ///
        /// FALSE (default) — a STANDALONE single special-tile blast (Striped or
        /// Wrapped fired alone against a normal tile) never hurts the boss.
        /// Color Bomb's standalone blast doesn't use this flag at all — it
        /// always damages the boss through its own separate, dedicated
        /// BossDamageEvents.OnColorBombBlast event (see ColorBombEffect.Activate()).
        ///
        /// TRUE — set by SpecialCombinations.cs right before it fires a
        /// 2-special-tile combo (Striped+Striped, Wrapped+Striped,
        /// Wrapped+Wrapped, Rainbow+Other), and reset back to false once that
        /// combo finishes — this is a PROPERTY (not a method parameter)
        /// specifically because BlastRow/BlastColumn/Pulse3x3/Activate() are
        /// the SAME shared methods used by both a standalone single blast and
        /// a combo — a property lets the caller (single activation vs combo)
        /// toggle the behaviour without changing every method signature in
        /// between.
        /// </summary>
        public bool ReportBossDamageOnClear { get; set; } = false;

        /// <summary>
        /// NEW — when true, the wave/pulse per-tile animation delay
        /// (tileBlastDelay) inside WaveClearHorizontal/Vertical (StripedTileEffect)
        /// and BurstClear (WrappedTileEffect) is skipped, so the whole row/column/
        /// 3x3 area clears in the same frame instead of a left-to-right or
        /// center-outward sweep. FALSE (default) for a standalone single blast
        /// (keeps its normal sweep feel). SpecialCombinations sets this to TRUE
        /// right before firing a 2-special-tile combo — Rabia's request: "jab do
        /// special-tiles swap ho to ek daam se blast ho tiles" (whole combo clears
        /// at once, same as the Rainbow+Rainbow board-clear already does) — and
        /// resets it back to false once the combo finishes.
        /// </summary>
        public bool InstantBlast { get; set; } = false;

        /// <summary>
        /// Reports goal progress + peels jelly for a NORMAL tile that a blast
        /// is clearing. Callers must have already ruled out special / hard /
        /// drop-stone tiles before calling this. Whether this also damages the
        /// boss depends on ReportBossDamageOnClear above.
        /// </summary>
        protected void ClearNormalTileTracked(Tile t, List<Tile> cleared)
        {
            levelManager?.OnTileCleared(t.Data);

            if (jellyManager != null && jellyManager.DecrementAt(t.GridX, t.GridY))
                levelManager?.OnJellyCleared();

            if (ReportBossDamageOnClear && t.Data != null)
                BossDamageEvents.OnSpecialTileCleared?.Invoke(t.Data.color);

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