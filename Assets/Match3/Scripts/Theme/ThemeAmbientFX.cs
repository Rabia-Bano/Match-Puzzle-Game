using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Match3.Theme
{
    public class ThemeAmbientFX : MonoBehaviour
    {
        public static ThemeAmbientFX Instance { get; private set; }

        [Header("Canvas")]
        [Tooltip("Sorting order of the effect canvas. Your scene UI canvases are usually 0, so 1 draws the " +
                 "effect just above them. Use a negative value to draw BEHIND the scene UI (only visible " +
                 "where the scene UI is transparent).")]
        [SerializeField] private int defaultSortingOrder = 1;
        [SerializeField] private Vector2 referenceResolution = new Vector2(1080, 1920);

        [Header("Global")]
        [Tooltip("Master switch (e.g. for a 'Low graphics' setting).")]
        [SerializeField] private bool effectsEnabled = true;
        [Tooltip("Seconds for the fade when the theme changes.")]
        [SerializeField] private float fadeDuration = 0.8f;

        private Canvas        _canvas;
        private CanvasGroup   _group;
        private RectTransform _root;

        private class Particle
        {
            public RectTransform rt;
            public Image         img;
            public Vector2       pos, vel;
            public float         size, rot, rotSpeed, phase, phaseSpeed, alpha, life, maxLife;
            public Color         color;
            public bool          special;
        }

        private readonly List<Particle> _particles = new();
        private ThemeData _activeTheme;
        private ThemeAmbientType _type = ThemeAmbientType.None;
        private float _speed = 1f, _wind, _densityMul = 1f;
        private float _targetAlpha = 1f;
        private ThemeData _pendingTheme;
        private bool _switching;
        private float _shootingStarTimer;

        private static readonly Dictionary<string, Sprite> SpriteCache = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            BuildCanvas();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            if (ThemeManager.Instance != null)
            {
                ThemeManager.Instance.OnThemeChanged += HandleThemeChanged;
                if (ThemeManager.Instance.CurrentTheme != null)
                    Rebuild(ThemeManager.Instance.CurrentTheme);
            }
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (ThemeManager.Instance != null)
                ThemeManager.Instance.OnThemeChanged -= HandleThemeChanged;
        }

        public void SetEffectsEnabled(bool on)
        {
            effectsEnabled = on;
            if (_root != null) _root.gameObject.SetActive(on);
            if (on && _activeTheme != null) Rebuild(_activeTheme);
        }

        public bool EffectsEnabled => effectsEnabled;

        private void HandleThemeChanged(ThemeData theme)
        {
            if (theme == null) return;
            if (_activeTheme == null || _particles.Count == 0) { Rebuild(theme); return; }
            _pendingTheme = theme;
            _switching    = true;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_activeTheme != null) Rebuild(_activeTheme);
        }

        private void BuildCanvas()
        {
            var go = new GameObject("ThemeAmbientCanvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = defaultSortingOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.matchWidthOrHeight  = 0.5f;

            _group = go.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable   = false;

            _root = (RectTransform)go.transform;
        }

        private void Rebuild(ThemeData theme)
        {
            if (theme == null) return;
            Canvas.ForceUpdateCanvases();
            _activeTheme = theme;
            ClearParticles();

            var sceneSettings = FindFirstObjectByType<ThemeAmbientSceneSettings>();
            _densityMul = sceneSettings != null ? sceneSettings.densityMultiplier : 1f;
            _canvas.sortingOrder = sceneSettings != null && sceneSettings.overrideSortingOrder
                ? sceneSettings.sortingOrder : defaultSortingOrder;
            bool sceneAllows = sceneSettings == null || sceneSettings.showAmbientEffect;

            _type  = theme.ambientType;
            _speed = theme.ambientSpeed;
            _wind  = theme.ambientWind;

            _root.gameObject.SetActive(effectsEnabled && sceneAllows && _type != ThemeAmbientType.None);
            if (!_root.gameObject.activeSelf) return;

            int count = Mathf.RoundToInt(theme.ambientCount * _densityMul);
            Sprite sprite = theme.ambientCustomSprite != null ? theme.ambientCustomSprite : DefaultSpriteFor(_type);

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("fx", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_root, false);
                var p = new Particle
                {
                    rt  = (RectTransform)go.transform,
                    img = go.GetComponent<Image>()
                };
                p.img.sprite        = sprite;
                p.img.raycastTarget = false;
                p.rt.anchorMin = p.rt.anchorMax = new Vector2(0.5f, 0.5f);
                Respawn(p, prewarm: true);
                _particles.Add(p);
            }

            if (_type == ThemeAmbientType.Stars && count > 0)
            {
                var go = new GameObject("shootingStar", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_root, false);
                var p = new Particle { rt = (RectTransform)go.transform, img = go.GetComponent<Image>(), special = true };
                p.img.sprite = GetSprite("streak"); p.img.raycastTarget = false;
                p.rt.anchorMin = p.rt.anchorMax = new Vector2(0.5f, 0.5f);
                p.alpha = 0f; p.img.color = new Color(1, 1, 1, 0);
                _particles.Add(p);
                _shootingStarTimer = Random.Range(2f, 5f);
            }
        }

        private void ClearParticles()
        {
            foreach (var p in _particles)
                if (p.rt != null) Destroy(p.rt.gameObject);
            _particles.Clear();
        }

        private Vector2 Half
        {
            get
            {
                Vector2 size = _root.rect.size;
                if (size.x < 10f || size.y < 10f)
                {
                    float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 0.5625f;
                    size = new Vector2(referenceResolution.y * aspect, referenceResolution.y);
                }
                return size * 0.5f;
            }
        }

        private Color RandomTint()
        {
            if (_activeTheme == null) return Color.white;
            return Color.Lerp(_activeTheme.ambientTint, _activeTheme.ambientTint2, Random.value);
        }

        private void Respawn(Particle p, bool prewarm)
        {
            Vector2 h = Half;
            float  xRand = Random.Range(-h.x, h.x);
            float  yRand = Random.Range(-h.y, h.y);
            p.color = RandomTint();
            p.rot = Random.Range(0f, 360f);
            p.phase = Random.Range(0f, Mathf.PI * 2f);
            p.life = 0f;
            p.special = false;

            switch (_type)
            {
                case ThemeAmbientType.Snow:
                    p.size = Random.Range(14f, 38f);
                    p.vel  = new Vector2(0f, -Random.Range(70f, 170f) * (p.size / 26f));
                    p.rotSpeed = Random.Range(-40f, 40f);
                    p.phaseSpeed = Random.Range(0.6f, 1.4f);
                    p.alpha = Random.Range(0.55f, 0.95f);
                    p.pos = new Vector2(xRand, prewarm ? yRand : h.y + p.size);
                    break;

                case ThemeAmbientType.Leaves:
                case ThemeAmbientType.Petals:
                    p.size = _type == ThemeAmbientType.Leaves ? Random.Range(34f, 62f) : Random.Range(22f, 40f);
                    p.vel  = new Vector2(0f, -Random.Range(80f, 150f));
                    p.rotSpeed = Random.Range(60f, 180f) * (Random.value < 0.5f ? -1f : 1f);
                    p.phaseSpeed = Random.Range(0.8f, 1.6f);
                    p.alpha = Random.Range(0.75f, 1f);
                    p.pos = new Vector2(xRand, prewarm ? yRand : h.y + p.size);
                    break;

                case ThemeAmbientType.Sand:
                    bool puff = Random.value < 0.12f;
                    p.special = puff;
                    p.size = puff ? Random.Range(160f, 320f) : Random.Range(4f, 11f);
                    float dir = _wind >= 0f ? 1f : -1f;
                    p.vel  = new Vector2(dir * Random.Range(puff ? 90f : 260f, puff ? 180f : 520f), Random.Range(-25f, 25f));
                    p.rotSpeed = 0f;
                    p.phaseSpeed = Random.Range(1.5f, 3f);
                    p.alpha = puff ? Random.Range(0.08f, 0.18f) : Random.Range(0.5f, 0.9f);
                    p.pos = new Vector2(prewarm ? xRand : -dir * (h.x + p.size), yRand);
                    break;

                case ThemeAmbientType.Stars:
                    p.size = Random.Range(6f, 22f);
                    p.vel  = new Vector2(0f, -Random.Range(3f, 10f));
                    p.rotSpeed = Random.Range(-15f, 15f);
                    p.phaseSpeed = Random.Range(1f, 3.5f);
                    p.alpha = Random.Range(0.5f, 1f);
                    p.pos = new Vector2(xRand, prewarm ? yRand : h.y + p.size);
                    break;

                case ThemeAmbientType.Bubbles:
                    p.size = Random.Range(16f, 54f);
                    p.vel  = new Vector2(0f, Random.Range(60f, 150f));
                    p.rotSpeed = 0f;
                    p.phaseSpeed = Random.Range(1f, 2.2f);
                    p.alpha = Random.Range(0.4f, 0.8f);
                    p.pos = new Vector2(xRand, prewarm ? yRand : -h.y - p.size);
                    break;

                case ThemeAmbientType.Embers:
                    p.size = Random.Range(6f, 16f);
                    p.vel  = new Vector2(Random.Range(-20f, 20f), Random.Range(90f, 220f));
                    p.rotSpeed = 0f;
                    p.phaseSpeed = Random.Range(6f, 12f);
                    p.alpha = Random.Range(0.6f, 1f);
                    p.maxLife = Random.Range(2.5f, 5f);
                    p.pos = new Vector2(xRand, prewarm ? yRand : -h.y - p.size);
                    break;

                case ThemeAmbientType.Rain:
                    p.size = Random.Range(40f, 80f);
                    p.vel  = new Vector2(-180f + _wind * 300f, -Random.Range(1300f, 1800f));
                    p.rotSpeed = 0f;
                    p.alpha = Random.Range(0.2f, 0.45f);
                    p.pos = new Vector2(xRand, prewarm ? yRand : h.y + p.size);
                    break;

                case ThemeAmbientType.Fireflies:
                    p.size = Random.Range(10f, 22f);
                    p.vel  = Random.insideUnitCircle.normalized * Random.Range(15f, 45f);
                    p.rotSpeed = 0f;
                    p.phaseSpeed = Random.Range(1f, 2.5f);
                    p.alpha = 1f;
                    p.pos = new Vector2(xRand, yRand);
                    break;
            }

            p.rt.sizeDelta = _type == ThemeAmbientType.Rain
                ? new Vector2(p.size * 0.08f, p.size)
                : new Vector2(p.size, p.size);
            if (_type == ThemeAmbientType.Rain)
                p.rot = Mathf.Atan2(p.vel.x, -p.vel.y) * Mathf.Rad2Deg;
            if (p.special && _type == ThemeAmbientType.Sand)
                p.img.sprite = GetSprite("soft");
            else if (_type == ThemeAmbientType.Sand && _activeTheme != null && _activeTheme.ambientCustomSprite == null)
                p.img.sprite = GetSprite("soft");
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (_switching)
            {
                _group.alpha = Mathf.MoveTowards(_group.alpha, 0f, dt / Mathf.Max(0.01f, fadeDuration * 0.5f));
                if (_group.alpha <= 0.001f)
                {
                    _switching = false;
                    Rebuild(_pendingTheme);
                    _pendingTheme = null;
                }
            }
            else if (_group.alpha < _targetAlpha)
            {
                _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, dt / Mathf.Max(0.01f, fadeDuration * 0.5f));
            }

            if (!_root.gameObject.activeInHierarchy || _particles.Count == 0) return;

            Vector2 h = Half;
            float windPx = _wind * 120f;
            float sp = _speed;

            foreach (var p in _particles)
            {
                if (p.rt == null) continue;

                if (_type == ThemeAmbientType.Stars && p.special) { UpdateShootingStar(p, dt, h); continue; }

                p.life  += dt;
                p.phase += p.phaseSpeed * dt;
                float a = p.alpha;
                Vector2 offset = Vector2.zero;

                switch (_type)
                {
                    case ThemeAmbientType.Snow:
                        offset.x = Mathf.Sin(p.phase) * 18f;
                        p.pos.x += windPx * dt;
                        break;
                    case ThemeAmbientType.Leaves:
                    case ThemeAmbientType.Petals:
                        offset.x = Mathf.Sin(p.phase) * 55f;
                        p.pos.x += windPx * dt;
                        break;
                    case ThemeAmbientType.Sand:
                        offset.y = Mathf.Sin(p.phase) * (p.special ? 20f : 8f);
                        break;
                    case ThemeAmbientType.Stars:
                        a = p.alpha * (0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(p.phase)));
                        break;
                    case ThemeAmbientType.Bubbles:
                        offset.x = Mathf.Sin(p.phase) * 14f;
                        break;
                    case ThemeAmbientType.Embers:
                        a = p.alpha * (0.6f + 0.4f * Mathf.Sin(p.phase)) * (1f - Mathf.Clamp01(p.life / p.maxLife));
                        p.pos.x += windPx * dt * 0.5f;
                        if (p.life >= p.maxLife) { Respawn(p, false); continue; }
                        break;
                    case ThemeAmbientType.Fireflies:
                        a = 0.25f + 0.75f * (0.5f + 0.5f * Mathf.Sin(p.phase * 2f));
                        if (Random.value < dt * 0.6f)
                            p.vel = Random.insideUnitCircle.normalized * Random.Range(15f, 45f);
                        break;
                }

                p.pos += p.vel * sp * dt;
                p.rot += p.rotSpeed * sp * dt;

                float m = p.size + 40f;
                bool outside = p.pos.y < -h.y - m || p.pos.y > h.y + m || p.pos.x < -h.x - m || p.pos.x > h.x + m;
                if (outside)
                {
                    if (_type == ThemeAmbientType.Fireflies || _type == ThemeAmbientType.Stars)
                    {
                        if (p.pos.x < -h.x - m) p.pos.x = h.x + m; else if (p.pos.x > h.x + m) p.pos.x = -h.x - m;
                        if (p.pos.y < -h.y - m) p.pos.y = h.y + m; else if (p.pos.y > h.y + m) p.pos.y = -h.y - m;
                    }
                    else { Respawn(p, false); continue; }
                }

                p.rt.anchoredPosition = p.pos + offset;
                p.rt.localEulerAngles = new Vector3(0f, 0f, p.rot);
                var c = p.color; c.a *= a;
                p.img.color = c;
            }
        }

        private void UpdateShootingStar(Particle p, float dt, Vector2 h)
        {
            if (p.alpha <= 0f)
            {
                _shootingStarTimer -= dt;
                if (_shootingStarTimer > 0f) return;

                p.pos   = new Vector2(Random.Range(-h.x, h.x * 0.6f), Random.Range(h.y * 0.2f, h.y));
                float dir = Random.value < 0.5f ? 1f : -1f;
                p.vel   = new Vector2(dir * Random.Range(900f, 1300f), -Random.Range(450f, 650f));
                p.alpha = 1f; p.life = 0f; p.maxLife = 0.9f;
                p.rt.sizeDelta = new Vector2(6f, 140f);
                p.rt.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(p.vel.x, -p.vel.y) * Mathf.Rad2Deg);
                _shootingStarTimer = Random.Range(3f, 8f);
            }

            p.life += dt;
            p.pos  += p.vel * dt;
            float a = 1f - Mathf.Clamp01(p.life / p.maxLife);
            p.rt.anchoredPosition = p.pos;
            p.img.color = new Color(1f, 1f, 1f, a);
            if (a <= 0f) p.alpha = 0f;
        }

        private static Sprite DefaultSpriteFor(ThemeAmbientType type)
        {
            switch (type)
            {
                case ThemeAmbientType.Snow:      return GetSprite("flake");
                case ThemeAmbientType.Leaves:    return GetSprite("leaf");
                case ThemeAmbientType.Petals:    return GetSprite("petal");
                case ThemeAmbientType.Stars:     return GetSprite("sparkle");
                case ThemeAmbientType.Bubbles:   return GetSprite("bubble");
                case ThemeAmbientType.Rain:      return GetSprite("streak");
                default:                         return GetSprite("soft");
            }
        }

        private static Sprite GetSprite(string key)
        {
            if (SpriteCache.TryGetValue(key, out Sprite s) && s != null) return s;

            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px  = new Color[N * N];

            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N * 2f - 1f;
                float v = (y + 0.5f) / N * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float a = 0f;
                float shade = 1f;

                switch (key)
                {
                    case "soft":
                        a = Mathf.Clamp01(1f - r); a *= a;
                        break;

                    case "flake":
                    {
                        float ang = Mathf.Atan2(v, u);
                        float arms = Mathf.Abs(Mathf.Cos(ang * 3f));
                        float armMask = Mathf.Clamp01((arms - 0.86f) * 8f) * Mathf.Clamp01(1f - r);
                        float core = Mathf.Clamp01(1f - r * 3.2f);
                        float glow = Mathf.Clamp01(1f - r) * 0.25f;
                        a = Mathf.Clamp01(armMask + core + glow);
                        break;
                    }

                    case "leaf":
                    case "petal":
                    {
                        float w = key == "leaf" ? 0.55f : 0.75f;
                        float d1 = Vector2.Distance(new Vector2(u, v), new Vector2(-w, 0f));
                        float d2 = Vector2.Distance(new Vector2(u, v), new Vector2( w, 0f));
                        float rad = 1f + w * 0.05f;
                        float inside = Mathf.Min(rad - d1, rad - d2);
                        a = Mathf.Clamp01(inside * 12f);
                        if (key == "leaf" && Mathf.Abs(u) < 0.04f && Mathf.Abs(v) < 0.85f) shade = 0.7f;
                        else shade = 0.85f + 0.15f * (1f - Mathf.Abs(u));
                        break;
                    }

                    case "sparkle":
                    {
                        float cross = Mathf.Clamp01(1f - Mathf.Abs(u) * Mathf.Abs(v) * 40f) * Mathf.Clamp01(1f - r);
                        float core  = Mathf.Clamp01(1f - r * 2.5f);
                        a = Mathf.Clamp01(cross + core);
                        break;
                    }

                    case "bubble":
                    {
                        float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.82f) * 9f);
                        float fill = r < 0.82f ? 0.12f : 0f;
                        float hl   = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(-0.35f, 0.35f)) * 5f);
                        a = Mathf.Clamp01(ring + fill + hl);
                        break;
                    }

                    case "streak":
                    {
                        float across = Mathf.Clamp01(1f - Mathf.Abs(u) * 2.2f);
                        float along  = Mathf.Clamp01((v + 1f) * 0.5f);
                        a = across * along;
                        break;
                    }
                }

                px[y * N + x] = new Color(shade, shade, shade, a);
            }

            tex.SetPixels(px);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            s.name = "AmbientFX_" + key;
            SpriteCache[key] = s;
            return s;
        }
    }
}
