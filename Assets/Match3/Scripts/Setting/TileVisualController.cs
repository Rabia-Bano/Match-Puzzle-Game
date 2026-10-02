using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace Match3
{
    public class TileVisualController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Child transform holding the tile's renderers. Idle bob and spawn " +
                 "bounce animate THIS, never the root transform (see class comment).")]
        [SerializeField] private Transform visualPivot;

        [Tooltip("Renderer to read the current tile colour from, for tinting the match burst particle.")]
        [SerializeField] private SpriteRenderer tileRenderer;

        [Header("Idle Bob")]
        [SerializeField] private float idleBobHeight   = 0.06f;
        [SerializeField] private float idleBobDuration = 1.1f;

        [Header("Spawn Bounce")]
        [SerializeField] private float spawnBounceDuration = 0.28f;
        [SerializeField] private Ease  spawnBounceEase      = Ease.OutBack;

        [Header("Match Pop")]
        [Tooltip("Particle prefab for the match-pop burst. Assign the same prefab on " +
                 "every Tile instance — it's pooled internally (see PARTICLE POOL below), " +
                 "so this does NOT create one pool per tile.")]
        [SerializeField] private ParticleSystem matchBurstPrefab;

        [Header("Special Tile Explosion")]
        [Tooltip("Particle prefab for special-tile (striped/wrapped/colour-bomb) activation. " +
                 "Bigger/more dramatic than the normal match pop.")]
        [SerializeField] private ParticleSystem specialExplosionPrefab;

        private Tween _idleTween;

        private static readonly Dictionary<ParticleSystem, Queue<ParticleSystem>> _particlePools = new();
        private const int ParticlePoolPrewarm = 12;

        public void StartIdleAnimation()
        {
            StopIdleAnimation();
            if (visualPivot == null) return;

            Vector3 restLocalPos = visualPivot.localPosition;
            float randomisedDuration = idleBobDuration * Random.Range(0.85f, 1.15f);

            _idleTween = visualPivot
                .DOLocalMoveY(restLocalPos.y + idleBobHeight, randomisedDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetDelay(Random.Range(0f, randomisedDuration));
        }

        public void StopIdleAnimation()
        {
            _idleTween?.Kill();
            _idleTween = null;
        }

        public void PlaySpawnAnimation()
        {
            if (visualPivot == null) return;

            visualPivot.DOKill();
            visualPivot.localScale = Vector3.zero;
            visualPivot.DOScale(Vector3.one, spawnBounceDuration).SetEase(spawnBounceEase)
                .OnComplete(StartIdleAnimation);
        }

        public void PlayMatchBurst()
        {
            StopIdleAnimation();
            SpawnBurst(matchBurstPrefab, transform.position, GetTileTint());
        }

        public void PlaySpecialBurst()
        {
            StopIdleAnimation();
            SpawnBurst(specialExplosionPrefab, transform.position, GetTileTint());
        }

        private Color GetTileTint()
        {
            return tileRenderer != null ? tileRenderer.color : Color.white;
        }

        public static void PlayEffect(ParticleSystem prefab, Vector3 worldPos, Color tint)
        {
            SpawnBurst(prefab, worldPos, tint);
        }

        public static Vector3 ScreenCenterWorldPoint()
        {
            Camera cam = Camera.main;
            if (cam == null) return Vector3.zero;
            float distance = Mathf.Abs(cam.transform.position.z);
            return cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, distance));
        }

        private static void SpawnBurst(ParticleSystem prefab, Vector3 worldPos, Color tint)
        {
            if (prefab == null) return;

            ParticleSystem instance = GetFromPool(prefab);
            instance.transform.position = worldPos;

            var main = instance.main;
            main.startColor = tint;

            instance.gameObject.SetActive(true);
            instance.Clear();
            instance.Play();

            float lifetime = main.duration + main.startLifetime.constantMax;
            instance.GetComponent<PooledParticleReturner>().ReturnAfter(prefab, lifetime);
        }

        private static ParticleSystem GetFromPool(ParticleSystem prefab)
        {
            if (!_particlePools.TryGetValue(prefab, out Queue<ParticleSystem> pool))
            {
                pool = new Queue<ParticleSystem>();
                _particlePools[prefab] = pool;
                for (int i = 0; i < ParticlePoolPrewarm; i++)
                    pool.Enqueue(CreateInstance(prefab));
            }

            while (pool.Count > 0)
            {
                ParticleSystem candidate = pool.Dequeue();
                if (candidate != null) return candidate;
            }

            return CreateInstance(prefab);
        }

        private static ParticleSystem CreateInstance(ParticleSystem prefab)
        {
            ParticleSystem instance = Instantiate(prefab);
            instance.gameObject.SetActive(false);
            var stopAction = instance.main;
            stopAction.stopAction = ParticleSystemStopAction.None;

            DontDestroyOnLoad(instance.gameObject);

            if (instance.GetComponent<PooledParticleReturner>() == null)
                instance.gameObject.AddComponent<PooledParticleReturner>();

            return instance;
        }

        private class PooledParticleReturner : MonoBehaviour
        {
            public void ReturnAfter(ParticleSystem prefabKey, float delay)
            {
                StopAllCoroutines();
                StartCoroutine(ReturnRoutine(prefabKey, delay));
            }

            private System.Collections.IEnumerator ReturnRoutine(ParticleSystem prefabKey, float delay)
            {
                yield return new WaitForSeconds(delay);
                gameObject.SetActive(false);
                _particlePools[prefabKey].Enqueue(GetComponent<ParticleSystem>());
            }
        }

        public void KillAllTweens()
        {
            StopIdleAnimation();
            if (visualPivot != null)
            {
                visualPivot.DOKill();
                visualPivot.localScale = Vector3.one;
                visualPivot.localPosition = Vector3.zero;
            }
        }

        private void OnDisable() => StopIdleAnimation();
    }
}
