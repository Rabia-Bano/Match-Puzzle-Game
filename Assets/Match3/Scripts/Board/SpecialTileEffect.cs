using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace Match3
{
    public abstract class SpecialTileEffect : MonoBehaviour
    {
        [Header("Particle FX Prefabs")]
        [SerializeField] protected GameObject blastParticlePrefab;
        [SerializeField] protected GameObject sparkleParticlePrefab;

        [Header("Timings")]
        [SerializeField] protected float tileBlastDelay = 0.02f;
        [SerializeField] protected float effectDuration = 0.15f;

        [HideInInspector] public BoardGrid              boardGrid;
        [HideInInspector] public LevelManager           levelManager;
        [HideInInspector] public SpecialTileActivator   specialActivator;
        [HideInInspector] public JellyManager           jellyManager;

        public abstract IEnumerator Activate(Vector2Int position, List<Tile> clearedTiles);

        public bool ReportBossDamageOnClear { get; set; } = false;

        public bool InstantBlast { get; set; } = false;

        protected void ClearNormalTileTracked(Tile t, List<Tile> cleared)
        {
            levelManager?.OnTileCleared(t.Data);

            if (jellyManager != null && jellyManager.DecrementAt(t.GridX, t.GridY))
                levelManager?.OnJellyCleared();

            if (ReportBossDamageOnClear && t.Data != null)
                BossDamageEvents.OnSpecialTileCleared?.Invoke(t.Data.color);

            cleared.Add(t);
        }

        protected void DamageHardTileDirect(Tile t, List<Tile> cleared)
        {
            bool broke = t.DamageObstacle();
            if (!broke) return;

            levelManager?.OnHardTileCleared();
            cleared.Add(t);
            boardGrid.RemoveTile(t.GridX, t.GridY);
            t.SetState(TileState.Matched);
            t.transform.DOScale(Vector3.zero, 0.15f).SetEase(Ease.InBack)
                .OnComplete(() => t.transform.localScale = Vector3.one);
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
