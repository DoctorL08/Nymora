using UnityEngine;

namespace Nymora.Hub
{
    /// <summary>
    /// GFX — Profondeur/atmosphère du hub (fond plat + caméra fixe → aucun relief de base).
    ///
    /// Trois couches, toutes teintées par le PROFIL GRAPHIQUE actif et coupées en "Sans effets" :
    /// - PARTICULES atmosphériques (poussières/braises) : petites, vivantes (Noise + dérive),
    ///   tailles/vitesses variées -> profondeur sans relief.
    /// - BRUME de profondeur : voile vertical doux, dense vers le haut (le "fond" de la salle),
    ///   nul vers le bas (plan du joueur) -> perspective atmosphérique.
    /// - HALOS de torches : un <see cref="HubTorchHalo"/> est auto-posé sur chaque TorchLightFlicker.
    ///
    /// 100% code (sprites doux générés au runtime), auto-ajouté par HubBootstrap. 100% View.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HubAtmosphere : MonoBehaviour
    {
        [Header("Particules")]
        [Tooltip("Teinte de base (multipliée par la teinte du profil graphique). Alpha = opacité.")]
        [SerializeField] private Color _moteColor = new Color(1f, 0.88f, 0.66f, 0.20f);
        [SerializeField] private float _emissionRate = 9f;
        [SerializeField] private int _maxParticles = 150;
        [SerializeField] private int _sortingOrder = 1000; // au-dessus du sol ; sous l'UI overlay

        [Header("Brume de profondeur")]
        [Tooltip("Couleur de la brume (multipliée par la teinte du profil). Alpha = densité max en haut.")]
        [SerializeField] private Color _fogColor = new Color(0.66f, 0.71f, 0.82f, 0.10f);

        [Header("Lucioles")]
        [Tooltip("Couleur des lucioles (multipliée par la teinte du profil).")]
        [SerializeField] private Color _fireflyColor = new Color(0.78f, 0.96f, 0.50f, 0.9f);
        [SerializeField] private float _fireflyEmission = 3f;
        [SerializeField] private int _fireflyMax = 32;

        [Header("Couches actives (décocher selon la scène — ex. reflets OFF en combat)")]
        [SerializeField] private bool _enableMotes = true;
        [SerializeField] private bool _enableFireflies = true;
        [SerializeField] private bool _enableFog = true;
        [SerializeField] private bool _enableHalos = true;
        [SerializeField] private bool _enableContactShadows = true;
        [SerializeField] private bool _enableReflections = true;

        private static Texture2D _moteTex;
        private static Material _moteMat;
        private static Sprite _fogSprite;

        private ParticleSystem _ps;
        private ParticleSystem _fireflies;
        private SpriteRenderer _fog;
        private Color _fogBaseColor = Color.white; // couleur teintée courante (avant respiration)
        private float _fogDriftAmp;                 // amplitude de la dérive verticale (monde)
        private int _lastProfileIndex = -999;

        private void Start()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var hubCam = FindFirstObjectByType<HubCamera>();
                if (hubCam != null) cam = hubCam.GetComponent<Camera>();
            }
            if (cam == null) cam = FindFirstObjectByType<Camera>(); // combat : caméra non taggée MainCamera
            if (cam == null || !cam.orthographic) return;

            if (_enableMotes) BuildMotes(cam);
            if (_enableFireflies) BuildFireflies(cam);
            if (_enableFog) BuildFog(cam);
            if (_enableHalos) SpawnTorchHalos();
            if (_enableContactShadows) SpawnContactShadows();
            if (_enableReflections) SpawnFloorReflections();
            ApplyProfile();
        }

        private void Update()
        {
            // Re-teinte/coupe les couches quand le joueur change de profil graphique (les particules
            // déjà nées gardent leur teinte -> transition douce sur ~1 durée de vie).
            var disp = DisplaySettingsController.Instance;
            if (disp != null && disp.GraphicsProfileIndex != _lastProfileIndex) ApplyProfile();

            AnimateFog();
        }

        // Brume vivante : respiration lente de densité (Perlin = organique) + dérive verticale très
        // lente (roulis). Amplitudes faibles ; le surdimensionnement ×1.6 absorbe la dérive sans bord.
        private void AnimateFog()
        {
            if (_fog == null || !_fog.enabled) return;
            float t = Time.time;
            float breathe = 0.80f + 0.20f * Mathf.PerlinNoise(t * 0.07f, 0.37f);
            var c = _fogBaseColor;
            c.a = _fogBaseColor.a * breathe;
            _fog.color = c;

            float drift = Mathf.Sin(t * 0.12f) * _fogDriftAmp;
            var lp = _fog.transform.localPosition;
            _fog.transform.localPosition = new Vector3(lp.x, drift, lp.z);
        }

        private void ApplyProfile()
        {
            var disp = DisplaySettingsController.Instance;
            Color tint = Color.white;
            bool effectsOn = true;
            if (disp != null)
            {
                _lastProfileIndex = disp.GraphicsProfileIndex;
                var p = disp.CurrentProfile;
                tint = p.LightTint;
                effectsOn = p.PostProcess; // "Sans effets" -> on coupe les couches décoratives
            }

            if (_ps != null)
            {
                var main = _ps.main;
                main.startColor = new Color(_moteColor.r * tint.r, _moteColor.g * tint.g, _moteColor.b * tint.b, _moteColor.a);
                var emission = _ps.emission;
                emission.enabled = effectsOn;
            }

            if (_fireflies != null)
            {
                var fmain = _fireflies.main;
                fmain.startColor = new Color(_fireflyColor.r * tint.r, _fireflyColor.g * tint.g, _fireflyColor.b * tint.b, _fireflyColor.a);
                var femission = _fireflies.emission;
                femission.enabled = effectsOn;
            }

            if (_fog != null)
            {
                _fogBaseColor = new Color(_fogColor.r * tint.r, _fogColor.g * tint.g, _fogColor.b * tint.b, _fogColor.a);
                _fog.color = _fogBaseColor; // la respiration (AnimateFog) module l'alpha par-dessus
                _fog.enabled = effectsOn;
            }
        }

        private void BuildMotes(Camera cam)
        {
            var go = new GameObject("HubAtmosphere_Motes");
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, cam.transform.position.z + 10f);

            _ps = go.AddComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.40f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.13f); // plus petites + variance = profondeur
            main.startColor = _moteColor;
            main.gravityModifier = 0f;
            main.maxParticles = _maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;

            var emission = _ps.emission;
            emission.rateOverTime = _emissionRate;

            var shape = _ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            float h = cam.orthographicSize * 2f * 1.25f;
            float w = h * Mathf.Max(0.1f, cam.aspect);
            shape.scale = new Vector3(w, h, 0.1f);

            // Dérive ascendante plus marquée + balancement.
            var vel = _ps.velocityOverLifetime;
            vel.enabled = true;
            vel.y = new ParticleSystem.MinMaxCurve(0.28f, 0.65f);
            vel.x = new ParticleSystem.MinMaxCurve(-0.22f, 0.22f);
            // Z DOIT être dans le même mode (TwoConstants) que X/Y, sinon Unity spamme
            // "Particle Velocity curves must all be in the same mode" chaque frame.
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            // Turbulence : c'est ce qui rend le mouvement VIVANT (avant : trop lent/figé).
            var noise = _ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.5f);
            noise.frequency = 0.45f;
            noise.scrollSpeed = 0.35f;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            var col = _ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.2f),
                    new GradientAlphaKey(1f, 0.8f),
                    new GradientAlphaKey(0f, 1f),
                });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.material = MoteMaterial();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.sortingOrder = _sortingOrder;

            _ps.Play();
        }

        // Lucioles : points lumineux peu nombreux qui dérivent et CLIGNOTENT (alpha qui pulse sur la
        // durée de vie) -> le hub respire. Teintées par le profil, coupées en "Sans effets".
        private void BuildFireflies(Camera cam)
        {
            var go = new GameObject("HubAtmosphere_Fireflies");
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, cam.transform.position.z + 9.5f);

            _fireflies = go.AddComponent<ParticleSystem>();
            _fireflies.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _fireflies.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            main.startColor = _fireflyColor;
            main.gravityModifier = 0f;
            main.maxParticles = _fireflyMax;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;

            var emission = _fireflies.emission;
            emission.rateOverTime = _fireflyEmission;

            var shape = _fireflies.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            float h = cam.orthographicSize * 2f * 1.15f;
            float w = h * Mathf.Max(0.1f, cam.aspect);
            shape.scale = new Vector3(w, h, 0.1f);

            // Dérive lente + balancement.
            var vel = _fireflies.velocityOverLifetime;
            vel.enabled = true;
            vel.y = new ParticleSystem.MinMaxCurve(0.05f, 0.30f);
            vel.x = new ParticleSystem.MinMaxCurve(-0.30f, 0.30f);
            // Z aligné sur le même mode (TwoConstants) que X/Y -> pas de warning de mode mixte.
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            // Mouvement erratique (les lucioles ne vont pas droit).
            var noise = _fireflies.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.7f);
            noise.frequency = 0.5f;
            noise.scrollSpeed = 0.5f;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            // Clignotement : alpha qui s'allume/s'éteint plusieurs fois sur la durée de vie.
            var col = _fireflies.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(0.15f, 0.30f),
                    new GradientAlphaKey(1f, 0.48f),
                    new GradientAlphaKey(0.12f, 0.64f),
                    new GradientAlphaKey(1f, 0.80f),
                    new GradientAlphaKey(0f, 1f),
                });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.material = MoteMaterial();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.sortingOrder = _sortingOrder + 1; // au-dessus des poussières

            _fireflies.Play();
        }

        // Voile vertical doux, parenté à la caméra (suit la vue) : dense vers le haut, nul vers le
        // bas -> perspective atmosphérique. Juste sous les particules, au-dessus du décor.
        private void BuildFog(Camera cam)
        {
            var go = new GameObject("HubAtmosphere_Fog");
            go.transform.SetParent(cam.transform, worldPositionStays: false);
            go.transform.localPosition = new Vector3(0f, 0f, 10f);
            go.transform.localRotation = Quaternion.identity;

            _fog = go.AddComponent<SpriteRenderer>();
            _fog.sprite = FogSprite();
            _fog.sortingOrder = _sortingOrder - 1;

            float viewH = cam.orthographicSize * 2f;
            float viewW = viewH * Mathf.Max(0.1f, cam.aspect);
            _fogDriftAmp = viewH * 0.04f; // dérive verticale ≈ 4% de la vue (absorbée par l'oversize)
            var b = _fog.sprite.bounds.size;
            // Surdimensionne FORT pour que les BORDS du sprite restent largement hors champ
            // (on ne voit jamais la limite du dégradé -> aucune coupure).
            if (b.x > 0f && b.y > 0f)
                go.transform.localScale = new Vector3(viewW * 1.6f / b.x, viewH * 1.6f / b.y, 1f);
        }

        // Pose un halo lumineux sur chaque torche du hub (respire avec le flicker, cf HubTorchHalo).
        private static void SpawnTorchHalos()
        {
            var torches = FindObjectsByType<TorchLightFlicker>(FindObjectsSortMode.None);
            foreach (var t in torches)
            {
                if (t == null) continue;
                if (t.GetComponent<HubTorchHalo>() == null) t.gameObject.AddComponent<HubTorchHalo>();
            }
        }

        // Pose une ombre de contact sous chaque prop iso DEBOUT (layer "Personnages"), hors torches
        // (= sources de lumiere). Les avatars ont deja leur blob -> on ne cible que le decor statique.
        private static void SpawnContactShadows()
        {
            var props = FindObjectsByType<IsoDepthSort>(FindObjectsSortMode.None);
            foreach (var p in props)
            {
                if (p == null) continue;
                if (p.GetComponent<TorchLightFlicker>() != null) continue;
                if (p.GetComponent<HubContactShadow>() != null) continue;
                var sr = p.GetComponent<SpriteRenderer>();
                if (sr == null || sr.sortingLayerName != "Personnages") continue;
                p.gameObject.AddComponent<HubContactShadow>();
            }
        }

        // Pose une réflexion "sol mouillé" sous chaque prop iso debout (mêmes cibles que les ombres
        // de contact : torches + props sur "Personnages"). Le perso est traité à part (cf HubAvatar).
        private static void SpawnFloorReflections()
        {
            var props = FindObjectsByType<IsoDepthSort>(FindObjectsSortMode.None);
            foreach (var p in props)
            {
                if (p == null) continue;
                if (p.GetComponent<TorchLightFlicker>() != null) continue;
                if (p.GetComponent<HubFloorReflection>() != null) continue;
                var sr = p.GetComponent<SpriteRenderer>();
                if (sr == null || sr.sortingLayerName != "Personnages") continue;
                p.gameObject.AddComponent<HubFloorReflection>();
            }
        }

        // Dégradé vertical couvrant TOUTE la hauteur : haze partout (densité min en bas, max en haut),
        // fondu doux d'un bord à l'autre -> aucune coupure visible. Teinté/échelonné dans ApplyProfile.
        private static Sprite FogSprite()
        {
            if (_fogSprite != null) return _fogSprite;
            const int w = 4, h = 128;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            for (int y = 0; y < h; y++)
            {
                float yn = (float)y / (h - 1);            // 0 = bas (plan joueur), 1 = haut (fond)
                float a = Mathf.SmoothStep(0f, 1f, yn);   // fond doux jusqu'à 0 en bas -> aucun bord franc
                for (int x = 0; x < w; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            _fogSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            return _fogSprite;
        }

        private static Material MoteMaterial()
        {
            if (_moteMat != null) return _moteMat;
            if (_moteTex == null)
            {
                const int s = 32;
                _moteTex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                Vector2 c = new Vector2((s - 1) * 0.5f, (s - 1) * 0.5f);
                float rad = s * 0.5f;
                for (int y = 0; y < s; y++)
                {
                    for (int x = 0; x < s; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), c) / rad;
                        float a = Mathf.Clamp01(1f - d);
                        a *= a;
                        _moteTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }
                _moteTex.Apply();
            }
            var sh = Shader.Find("Sprites/Default");
            _moteMat = new Material(sh) { mainTexture = _moteTex };
            return _moteMat;
        }
    }
}
