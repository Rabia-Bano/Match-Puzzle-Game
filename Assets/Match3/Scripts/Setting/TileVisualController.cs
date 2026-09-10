// ============================================================
//  TileVisualController.cs  —  MonoBehaviour, companion to Tile.cs
//
//  Adds three purely-visual layers on top of the existing Tile
//  logic, WITHOUT touching Tile.cs's own grid-position / swap /
//  clear tweens:
//
//    1. Idle bob   — a subtle up/down float, looping, random phase
//                    per tile so the whole board doesn't bob in sync.
//    2. Match pop  — scale-to-zero + a pooled particle burst tinted
//                    to the tile's colour.
//    3. Spawn bounce — scale-in with an overshoot ease when a tile
//                    is freshly spawned (works alongside — not
//                    instead of — BoardRefiller's existing fall-in
//                    DOMove).
//
//  WHY A SEPARATE "VisualPivot" CHILD:
//  Tile.cs's root transform already carries THREE different tweens
//  at different times: DOMove (swap in SwapController / fall-in in
//  BoardRefiller), DOScale-to-zero (clear pop in BoardController),
//  and DOPunchScale (hard-tile hit feedback). If the idle bob ALSO
//  animated the root transform's position/scale, it would collide
//  with all three and get killed/overridden constantly, or fight
//  DOKill() calls that were only meant for one of the others.
//  So idle bob + spawn bounce run on a dedicated CHILD transform
//  ("VisualPivot") that holds the renderers. The match pop leaves
//  BoardController's own root-transform DOScale exactly as-is (it
//  already does the shrink) and ONLY adds the particle burst here —
//  no scale conflict either.
//
//  PREFAB SETUP (see full guide below):
//    Tile (root)                 <- Tile.cs, TileVisualController.cs, Collider2D
//      └ VisualPivot             <- empty, position (0,0,0)
//          ├ TileRenderer        <- SpriteRenderer (the one Tile.cs uses)
//          └ HighlightRenderer   <- SpriteRenderer (the one Tile.cs uses)
//
//  Attach to: the Tile prefab, alongside Tile.cs.
// ============================================================

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

        // ── PARTICLE POOL ─────────────────────────────────────
        // Shared across every TileVisualController instance (static), keyed by
        // the prefab so different burst effects (e.g. a different one for
        // special-tile pops, if you add that later) each get their own pool.
        private static readonly Dictionary<ParticleSystem, Queue<ParticleSystem>> _particlePools = new();
        private const int ParticlePoolPrewarm = 12;

        // ── Idle bob ──────────────────────────────────────────

        /// <summary>Starts the looping idle float. Call from Tile.Initialize() / OnEnable.</summary>
        public void StartIdleAnimation()
        {
            StopIdleAnimation();
            if (visualPivot == null) return;

            Vector3 restLocalPos = visualPivot.localPosition;
            // Random phase: start each tile's loop already partway through, and
            // randomise duration slightly, so a whole row of tiles doesn't bob
            // perfectly in sync.
            float randomisedDuration = idleBobDuration * Random.Range(0.85f, 1.15f);

            _idleTween = visualPivot
                .DOLocalMoveY(restLocalPos.y + idleBobHeight, randomisedDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetDelay(Random.Range(0f, randomisedDuration));
        }

        /// <summary>Stops the idle float and snaps the pivot back to rest. Call before match pop / when returning to pool.</summary>
        public void StopIdleAnimation()
        {
            _idleTween?.Kill();
            _idleTween = null;
        }

        // ── Spawn bounce ──────────────────────────────────────

        /// <summary>
        /// Plays a scale-in overshoot bounce on the visual pivot. Call this
        /// right after Tile.Initialize() when a tile is freshly spawned
        /// (works fine alongside BoardRefiller's existing fall-in DOMove on
        /// the root transform — they animate different transforms).
        /// </summary>
        public void PlaySpawnAnimation()
        {
            if (visualPivot == null) return;

            visualPivot.DOKill();
            visualPivot.localScale = Vector3.zero;
            visualPivot.DOScale(Vector3.one, spawnBounceDuration).SetEase(spawnBounceEase)
                .OnComplete(StartIdleAnimation);
        }

        // ── Match pop ───────────────────────────────────────────

        /// <summary>
        /// Stops idle bob and fires a pooled particle burst tinted to this
        /// tile's colour at the tile's current world position. Does NOT
        /// touch the root transform's scale — BoardController.ClearTiles()
        /// already shrinks the root to zero; this just adds the sparkle.
        /// Safe to call even if matchBurstPrefab isn't assigned (no-ops the
        /// particle part, still stops the idle bob).
        /// </summary>
        public void PlayMatchBurst()
        {
            StopIdleAnimation();
            SpawnBurst(matchBurstPrefab, transform.position, GetTileTint());
        }

        /// <summary>
        /// Same idea as PlayMatchBurst() but uses the bigger special-tile
        /// explosion prefab. Call this from BoardController.cs at the same
        /// place the "special_activate" SFX fires.
        /// </summary>
        public void PlaySpecialBurst()
        {
            StopIdleAnimation();
            SpawnBurst(specialExplosionPrefab, transform.position, GetTileTint());
        }

        private Color GetTileTint()
        {
            return tileRenderer != null ? tileRenderer.color : Color.white;
        }

        /// <summary>
        /// Reusable "play a burst at a world position" for effects that
        /// AREN'T tied to a specific tile — e.g. a pet skill activating, or
        /// the win-celebration confetti. Uses the same pooling machinery as
        /// PlayMatchBurst()/PlaySpecialBurst() above, so callers never need
        /// their own pool.
        /// Usage: TileVisualController.PlayEffect(myPrefab, someWorldPos, Color.white);
        /// </summary>
        public static void PlayEffect(ParticleSystem prefab, Vector3 worldPos, Color tint)
        {
            SpawnBurst(prefab, worldPos, tint);
        }

        /// <summary>
        /// Convenience for UI-triggered effects (pet skill button, win/lose
        /// panels) that don't have a natural "world position" of their own —
        /// returns the world-space point at the centre of the camera's view,
        /// which for this game's 2D board is roughly the middle of the board.
        /// </summary>
        public static Vector3 ScreenCenterWorldPoint()
        {
            Camera cam = Camera.main;
            if (cam == null) return Vector3.zero;
            float distance = Mathf.Abs(cam.transform.position.z);
            return cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, distance));
        }

        // ── Pool helpers (static, shared) ───────────────────────

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

            // FIX (CS1061): ParticleSystem itself is NOT a MonoBehaviour, so it
            // has no StartCoroutine(). Each pooled instance carries a tiny
            // PooledParticleReturner helper component (added once, in
            // CreateInstance below) that DOES have StartCoroutine, and knows
            // how to hand itself back to this pool after it finishes playing.
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

            // FIX (MissingReferenceException): the pool is static and survives
            // scene reloads, but pooled particle GameObjects used to live
            // inside the scene and got destroyed on scene unload — leaving
            // "dead" references behind in the queue. Skip any dead ones here
            // instead of handing them back out.
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

            // FIX (MissingReferenceException): make pooled particle instances
            // persist across scene loads too — same reasoning as AudioManager /
            // JuiceManager — so they never get silently destroyed while a
            // reference to them still sits in the static pool.
            DontDestroyOnLoad(instance.gameObject);

            // Attach the coroutine-runner helper once per pooled instance.
            if (instance.GetComponent<PooledParticleReturner>() == null)
                instance.gameObject.AddComponent<PooledParticleReturner>();

            return instance;
        }

        /// <summary>
        /// Tiny MonoBehaviour whose only job is to own the "wait, then go back
        /// to the pool" coroutine — needed because ParticleSystem itself can't
        /// run coroutines. Added automatically to every pooled particle
        /// instance; nothing to wire in the Inspector.
        /// </summary>
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

        // ── Cleanup ──────────────────────────────────────────

        /// <summary>
        /// Kills every tween owned by this controller. Call this from
        /// Tile.ResetForPool()'s KillTweens() so a pooled-and-reused tile
        /// never inherits a leftover bob/bounce tween (same reasoning as
        /// Tile.cs's own KillTweens() for its root-transform tweens).
        /// </summary>
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