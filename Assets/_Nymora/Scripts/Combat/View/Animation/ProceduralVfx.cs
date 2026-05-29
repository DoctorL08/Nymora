using System;
using UnityEngine;

namespace Nymora.Combat.View.Animation
{
    /// <summary>
    /// Primitives VFX PROCÉDURALES (zéro asset dessiné) pour les sorts de combat.
    ///
    /// Brique de feel : on ne se contente plus de "pâtés" de particules — on compose des effets
    /// crédibles à partir de briques de VFX réelles :
    ///   - Flash  : cœur lumineux qui "pop" (release).
    ///   - Shockwave : anneau d'onde de choc qui se propage (impact).
    ///   - CastCharge : énergie qui converge vers les mains du caster (wind-up = "vrai cast").
    ///   - Burst : éclats denses, petits et rapides (étincelles).
    ///   - Aura / Zone / Projectile / Slash.
    ///
    /// Sprites (point doux + anneau) et matériau unlit générés une fois et mis en cache.
    /// 100% View : rien ne touche la simulation. Tout s'auto-détruit après sa durée de vie.
    /// </summary>
    public static class ProceduralVfx
    {
        private static Texture2D _dotTex;
        private static Sprite _dotSprite;
        private static Texture2D _ringTex;
        private static Sprite _ringSprite;
        private static Material _particleMat;

        // ---------- Assets générés ----------

        public static Sprite DotSprite()
        {
            if (_dotSprite != null) return _dotSprite;
            EnsureDotTexture();
            _dotSprite = Sprite.Create(_dotTex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64f);
            return _dotSprite;
        }

        public static Sprite RingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            EnsureRingTexture();
            _ringSprite = Sprite.Create(_ringTex, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f), 128f);
            return _ringSprite;
        }

        public static Material ParticleMaterial()
        {
            if (_particleMat != null) return _particleMat;
            EnsureDotTexture();
            _particleMat = new Material(Shader.Find("Sprites/Default")) { mainTexture = _dotTex };
            return _particleMat;
        }

        private static void EnsureDotTexture()
        {
            if (_dotTex != null) return;
            const int size = 64;
            _dotTex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxR = size * 0.5f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / maxR;
                    float a = Mathf.Clamp01(1f - d); a = a * a;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            _dotTex.SetPixels32(px); _dotTex.Apply();
        }

        private static void EnsureRingTexture()
        {
            if (_ringTex != null) return;
            const int size = 128;
            _ringTex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxR = size * 0.5f;
            const float ringR = 0.82f;     // position de l'anneau (fraction du rayon)
            const float halfBand = 0.16f;  // demi-épaisseur de l'anneau
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / maxR;
                    float t = Mathf.Abs(d - ringR);
                    float a = Mathf.Clamp01(1f - t / halfBand); a = a * a;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            _ringTex.SetPixels32(px); _ringTex.Apply();
        }

        // ---------- Particules ----------

        public struct BurstParams
        {
            public Color Color;
            public int Count;
            public float Speed;
            public float Size;
            public float Lifetime;
            public float Radius;
            public float Gravity;
            public string SortingLayer;
            public int SortingOrder;
        }

        /// <summary>Éclats denses (étincelles) à <paramref name="pos"/>. Auto-détruit.</summary>
        public static void Burst(Transform parent, Vector3 pos, in BurstParams p)
        {
            var go = NewVfxGo(parent, "vfx_burst", pos);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = p.Lifetime;
            main.startSpeed = p.Speed;
            main.startSize = p.Size;
            main.startColor = p.Color;
            main.gravityModifier = p.Gravity;
            main.maxParticles = Mathf.Max(8, p.Count + 8);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(1, p.Count)) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Max(0.01f, p.Radius);
            shape.radiusThickness = 1f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = SparkGradient();

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, ShrinkCurve());

            SetupParticleRenderer(go, p.SortingLayer, p.SortingOrder);
            ps.Play();
            UnityEngine.Object.Destroy(go, p.Lifetime + 0.5f);
        }

        /// <summary>Aura montante (buff) ancrée sur <paramref name="follow"/> (ou pos fixe).</summary>
        public static void Aura(Transform parent, Transform follow, Vector3 pos, Color color, float duration,
                                string sortingLayer, int sortingOrder)
        {
            var go = NewVfxGo(parent, "vfx_aura", pos);
            if (follow != null) go.transform.SetParent(follow, worldPositionStays: true);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = duration; main.loop = false; main.playOnAwake = false;
            main.startLifetime = 0.6f; main.startSpeed = 1.0f; main.startSize = 0.14f;
            main.startColor = color; main.gravityModifier = -0.2f;
            main.maxParticles = 200; main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission; emission.rateOverTime = 32f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 0.3f; shape.radiusThickness = 0.5f;
            // X/Y/Z doivent TOUS être dans le même mode (Constant) sinon Unity spamme
            // "Particle Velocity curves must all be in the same mode" chaque frame.
            var vol = ps.velocityOverLifetime; vol.enabled = true; vol.space = ParticleSystemSimulationSpace.World;
            vol.x = new ParticleSystem.MinMaxCurve(0f);
            vol.y = new ParticleSystem.MinMaxCurve(1.2f);
            vol.z = new ParticleSystem.MinMaxCurve(0f);
            var col = ps.colorOverLifetime; col.enabled = true; col.color = FadeInOutGradient();

            SetupParticleRenderer(go, sortingLayer, sortingOrder);
            ps.Play();
            UnityEngine.Object.Destroy(go, duration + 0.9f);
        }

        /// <summary>Nappe de zone persistante (brume/flaque) — compacte (1 case).</summary>
        public static void Zone(Transform parent, Vector3 pos, Color color, float duration,
                                string sortingLayer, int sortingOrder)
        {
            var go = NewVfxGo(parent, "vfx_zone", pos);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = duration; main.loop = false; main.playOnAwake = false;
            main.startLifetime = 1.0f; main.startSpeed = 0.15f; main.startSize = 0.3f;
            main.startColor = color; main.gravityModifier = -0.03f;
            main.maxParticles = 160; main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission; emission.rateOverTime = 16f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 0.34f; shape.radiusThickness = 1f;
            var col = ps.colorOverLifetime; col.enabled = true; col.color = FadeInOutGradient();

            SetupParticleRenderer(go, sortingLayer, sortingOrder);
            ps.Play();
            UnityEngine.Object.Destroy(go, duration + 1.3f);
        }

        // ---------- Sprites tweenés ----------

        /// <summary>Cœur lumineux qui "pop" (flash de release). Blanc-chaud teinté.</summary>
        public static void Flash(Transform parent, Vector3 pos, Color color, float scale, float dur,
                                 string sortingLayer, int sortingOrder)
        {
            var go = NewVfxGo(parent, "vfx_flash", pos);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = DotSprite();
            Color hot = Color.Lerp(color, Color.white, 0.6f); hot.a = 1f;
            sr.color = hot;
            sr.sortingLayerName = sortingLayer; sr.sortingOrder = sortingOrder;
            go.AddComponent<ProceduralVfxStreak>().Init(Vector3.one * scale * 0.35f, Vector3.one * scale, hot, dur);
        }

        /// <summary>Onde de choc : anneau qui se propage et s'efface.</summary>
        public static void Shockwave(Transform parent, Vector3 pos, Color color, float maxDiameter, float dur,
                                     string sortingLayer, int sortingOrder)
        {
            var go = NewVfxGo(parent, "vfx_shock", pos);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = RingSprite();
            Color c = color; c.a = 0.85f;
            sr.color = c;
            sr.sortingLayerName = sortingLayer; sr.sortingOrder = sortingOrder;
            // Squash iso 2:1 sur Y pour coller à la perspective du sol.
            go.AddComponent<ProceduralVfxStreak>().Init(
                new Vector3(0.15f, 0.08f, 1f),
                new Vector3(maxDiameter, maxDiameter * 0.55f, 1f), c, dur);
        }

        /// <summary>
        /// Wind-up de cast : des étincelles convergent vers <paramref name="center"/> (mains du
        /// caster) + un cœur qui grossit → lecture "il prépare un sort".
        /// </summary>
        public static void CastCharge(Transform parent, Vector3 center, Color color, float dur,
                                      string sortingLayer, int sortingOrder)
        {
            const int n = 12;
            const float radius = 0.55f;
            for (int i = 0; i < n; i++)
            {
                float a = (i / (float)n) * Mathf.PI * 2f;
                var start = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius * 0.7f, 0f);
                var go = NewVfxGo(parent, "vfx_charge", start);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = DotSprite(); sr.color = color;
                sr.sortingLayerName = sortingLayer; sr.sortingOrder = sortingOrder;
                go.transform.localScale = Vector3.one * 0.16f;
                go.AddComponent<ProceduralVfxConverge>().Init(start, center, dur, color);
            }
            Flash(parent, center, color, 0.45f, dur, sortingLayer, sortingOrder + 1);
        }

        /// <summary>Projectile lumineux + traînée de A à B, puis onArrive (impact) et destruction.</summary>
        public static void Projectile(Transform parent, Vector3 from, Vector3 to, Color color, float travelTime,
                                      string sortingLayer, int sortingOrder, Action onArrive)
        {
            var go = NewVfxGo(parent, "vfx_projectile", from);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = DotSprite(); sr.color = color;
            sr.sortingLayerName = sortingLayer; sr.sortingOrder = sortingOrder;
            go.transform.localScale = Vector3.one * 0.42f;

            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.16f; trail.startWidth = 0.26f; trail.endWidth = 0f; trail.numCornerVertices = 2;
            trail.material = ParticleMaterial();
            trail.sortingLayerName = sortingLayer; trail.sortingOrder = sortingOrder - 1;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = grad;

            go.AddComponent<ProceduralVfxMover>().Init(from, to, Mathf.Max(0.05f, travelTime), onArrive);
        }

        /// <summary>Slash mêlée : lame étirée orientée vers <paramref name="dir"/>.</summary>
        public static void Slash(Transform parent, Vector3 pos, Vector2 dir, Color color,
                                 string sortingLayer, int sortingOrder)
        {
            var go = NewVfxGo(parent, "vfx_slash", pos);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = DotSprite(); sr.color = color;
            sr.sortingLayerName = sortingLayer; sr.sortingOrder = sortingOrder;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f;
            go.transform.rotation = Quaternion.Euler(0f, 0f, ang);
            go.AddComponent<ProceduralVfxStreak>().Init(new Vector3(0.12f, 1.1f, 1f), new Vector3(1.2f, 0.35f, 1f), color, 0.18f);
        }

        /// <summary>
        /// Trait/chaîne entre 2 points (LineRenderer). Si <paramref name="retract"/>, l'extrémité
        /// "to" se rétracte vers "from" (effet de happement type Empoignade). Sinon, fondu simple.
        /// </summary>
        public static void Beam(Transform parent, Vector3 from, Vector3 to, Color color, float width, float dur,
                                string sortingLayer, int sortingOrder, bool retract)
        {
            var go = NewVfxGo(parent, "vfx_beam", from);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = ParticleMaterial();
            lr.textureMode = LineTextureMode.Stretch;
            lr.numCapVertices = 2;
            lr.useWorldSpace = true;
            lr.startWidth = width; lr.endWidth = width;
            lr.sortingLayerName = sortingLayer; lr.sortingOrder = sortingOrder;
            lr.positionCount = 2;
            lr.startColor = color; lr.endColor = color;
            go.AddComponent<ProceduralVfxBeam>().Init(lr, from, to, Mathf.Max(0.05f, dur), retract, color);
        }

        /// <summary>
        /// Anneau (ou dôme) qui apparaît, tient, puis s'efface — boucliers, ripostes, sceaux.
        /// <paramref name="diameter"/> permet un squash iso (x != y) ou un cercle (x == y).
        /// </summary>
        public static void RingHold(Transform parent, Vector3 pos, Color color, Vector2 diameter, float dur,
                                    string sortingLayer, int sortingOrder)
        {
            var go = NewVfxGo(parent, "vfx_ringhold", pos);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = RingSprite();
            sr.color = color;
            sr.sortingLayerName = sortingLayer; sr.sortingOrder = sortingOrder;
            go.transform.localScale = new Vector3(diameter.x, diameter.y, 1f);
            go.AddComponent<ProceduralVfxFade>().Init(sr, color, Mathf.Max(0.1f, dur), 0.18f);
        }

        // ---------- Helpers ----------

        /// <summary>
        /// Bouffée de FUMÉE one-shot à <paramref name="pos"/> : gris doux, monte légèrement,
        /// s'étale et se dissipe. Refonte 29 mai — feedback de déclenchement du Piège Bondissant.
        /// Auto-détruit.
        /// </summary>
        public static void Smoke(Transform parent, Vector3 pos, string sortingLayer, int sortingOrder,
                                 Color? tint = null, int count = 20, float lifetime = 0.85f)
        {
            var go = NewVfxGo(parent, "vfx_smoke", pos);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = lifetime;
            main.startSpeed = 0.5f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.24f, 0.42f);
            main.startColor = tint ?? new Color(0.80f, 0.80f, 0.82f, 0.7f);
            main.gravityModifier = -0.05f; // monte légèrement (fumée)
            main.maxParticles = count + 8;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(1, count)) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.22f;
            shape.radiusThickness = 1f;

            // Montée + léger étalement. X/Y/Z dans le MÊME mode (Constant) sinon Unity spamme
            // "Particle Velocity curves must all be in the same mode" chaque frame.
            var vol = ps.velocityOverLifetime; vol.enabled = true; vol.space = ParticleSystemSimulationSpace.World;
            vol.x = new ParticleSystem.MinMaxCurve(0f);
            vol.y = new ParticleSystem.MinMaxCurve(0.6f);
            vol.z = new ParticleSystem.MinMaxCurve(0f);

            var col = ps.colorOverLifetime; col.enabled = true; col.color = FadeInOutGradient();

            var sol = ps.sizeOverLifetime; sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, GrowCurve()); // la fumée s'étale

            SetupParticleRenderer(go, sortingLayer, sortingOrder);
            ps.Play();
            UnityEngine.Object.Destroy(go, lifetime + 0.5f);
        }

        private static GameObject NewVfxGo(Transform parent, string name, Vector3 pos)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = pos;
            return go;
        }

        private static void SetupParticleRenderer(GameObject go, string sortingLayer, int sortingOrder)
        {
            var psr = go.GetComponent<ParticleSystemRenderer>();
            if (psr == null) return;
            psr.material = ParticleMaterial();
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            if (!string.IsNullOrEmpty(sortingLayer)) psr.sortingLayerName = sortingLayer;
            psr.sortingOrder = sortingOrder;
        }

        private static ParticleSystem.MinMaxGradient SparkGradient()
        {
            // Blanc-chaud bref -> couleur -> transparent : feel "étincelle".
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 0.12f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
            return new ParticleSystem.MinMaxGradient(g);
        }

        private static ParticleSystem.MinMaxGradient FadeInOutGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.25f), new GradientAlphaKey(0.85f, 0.7f), new GradientAlphaKey(0f, 1f) });
            return new ParticleSystem.MinMaxGradient(g);
        }

        private static AnimationCurve ShrinkCurve()
        {
            return new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.05f));
        }

        private static AnimationCurve GrowCurve()
        {
            // Fumée : démarre petite, gonfle en se dissipant.
            return new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 1.5f));
        }
    }

    /// <summary>Déplace un VFX de A à B puis invoke onArrive et se détruit.</summary>
    public sealed class ProceduralVfxMover : MonoBehaviour
    {
        private Vector3 _from, _to; private float _dur, _t; private Action _onArrive; private bool _done;
        public void Init(Vector3 from, Vector3 to, float dur, Action onArrive)
        { _from = from; _to = to; _dur = dur; _onArrive = onArrive; _t = 0f; transform.position = from; }
        private void Update()
        {
            if (_done) return;
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / _dur);
            transform.position = Vector3.Lerp(_from, _to, k);
            if (k >= 1f)
            {
                _done = true;
                try { _onArrive?.Invoke(); } catch (Exception e) { Debug.LogWarning($"[ProceduralVfx] onArrive: {e.Message}"); }
                Destroy(gameObject);
            }
        }
    }

    /// <summary>Interpole l'échelle A->B + fade alpha sur une courte durée (slash/flash/shockwave).</summary>
    public sealed class ProceduralVfxStreak : MonoBehaviour
    {
        private Vector3 _from, _to; private float _dur, _t; private SpriteRenderer _sr; private Color _baseColor;
        public void Init(Vector3 fromScale, Vector3 toScale, Color color, float dur)
        { _from = fromScale; _to = toScale; _dur = Mathf.Max(0.05f, dur); _t = 0f; _baseColor = color; _sr = GetComponent<SpriteRenderer>(); transform.localScale = fromScale; }
        private void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / _dur);
            transform.localScale = Vector3.Lerp(_from, _to, k);
            if (_sr != null) { var c = _baseColor; c.a = _baseColor.a * (1f - k); _sr.color = c; }
            if (k >= 1f) Destroy(gameObject);
        }
    }

    /// <summary>Converge un point vers un centre (ease-in) avec fade in/out — wind-up de cast.</summary>
    public sealed class ProceduralVfxConverge : MonoBehaviour
    {
        private Vector3 _from, _to; private float _dur, _t; private SpriteRenderer _sr; private Color _c;
        public void Init(Vector3 from, Vector3 to, float dur, Color c)
        { _from = from; _to = to; _dur = Mathf.Max(0.05f, dur); _c = c; _sr = GetComponent<SpriteRenderer>(); transform.position = from; }
        private void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / _dur);
            transform.position = Vector3.Lerp(_from, _to, k * k); // accélère vers le centre
            float alpha = k < 0.3f ? (k / 0.3f) : (1f - (k - 0.3f) / 0.7f);
            if (_sr != null) { var c = _c; c.a = _c.a * Mathf.Clamp01(alpha); _sr.color = c; }
            transform.localScale = Vector3.one * Mathf.Lerp(0.16f, 0.04f, k);
            if (k >= 1f) Destroy(gameObject);
        }
    }

    /// <summary>Trait LineRenderer : fondu, avec rétractation optionnelle de l'extrémité "to" vers "from".</summary>
    public sealed class ProceduralVfxBeam : MonoBehaviour
    {
        private LineRenderer _lr; private Vector3 _from, _to; private float _dur, _t; private bool _retract; private Color _c;
        public void Init(LineRenderer lr, Vector3 from, Vector3 to, float dur, bool retract, Color c)
        { _lr = lr; _from = from; _to = to; _dur = dur; _retract = retract; _c = c; Apply(0f); }
        private void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / _dur);
            Apply(k);
            if (k >= 1f) Destroy(gameObject);
        }
        private void Apply(float k)
        {
            if (_lr == null) return;
            Vector3 end = _retract ? Vector3.Lerp(_to, _from, k * k) : _to; // happe vers le caster
            _lr.SetPosition(0, _from);
            _lr.SetPosition(1, end);
            // fondu : apparition rapide puis disparition.
            float a = _retract ? (1f - k) : (k < 0.2f ? k / 0.2f : 1f - (k - 0.2f) / 0.8f);
            var col = _c; col.a = _c.a * Mathf.Clamp01(a);
            _lr.startColor = col; _lr.endColor = col;
        }
    }

    /// <summary>Sprite qui apparaît (fadeIn), tient, puis disparaît — anneaux/dômes (RingHold).</summary>
    public sealed class ProceduralVfxFade : MonoBehaviour
    {
        private SpriteRenderer _sr; private Color _c; private float _dur, _fade, _t;
        public void Init(SpriteRenderer sr, Color color, float dur, float fadeFrac)
        { _sr = sr; _c = color; _dur = dur; _fade = Mathf.Clamp(fadeFrac, 0.05f, 0.49f); }
        private void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / _dur);
            float a = k < _fade ? (k / _fade) : (k > 1f - _fade ? (1f - k) / _fade : 1f);
            if (_sr != null) { var col = _c; col.a = _c.a * Mathf.Clamp01(a); _sr.color = col; }
            if (k >= 1f) Destroy(gameObject);
        }
    }
}
