using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Nymora.Hub
{
    /// <summary>
    /// GFX — Halo lumineux doux autour d'une torche. 100% View (aucune sim Quantum).
    ///
    /// Pose un sprite radial transparent (genere au runtime) au niveau de la flamme et le fait
    /// RESPIRER en synchro avec l'intensite de la Light2D : on lit l'intensite courante (deja
    /// vacillee par <see cref="TorchLightFlicker"/>) et on module l'alpha/echelle du halo ->
    /// le halo vit avec la flamme sans dupliquer le bruit de Perlin. Le bloom du post-process
    /// finit le glow.
    ///
    /// Teinte = couleur de la light (flamme) * teinte du profil graphique actif. Masque quand le
    /// profil est "Sans effets" (PostProcess == false).
    ///
    /// IMPORTANT : ne touche jamais l'intensite/position de la Light2D (preserve le reglage de
    /// Lorenzo cf flicker) — le halo est purement decoratif et additionnel.
    ///
    /// Auto-ajoute par <see cref="HubAtmosphere"/> sur chaque TorchLightFlicker du hub.
    /// </summary>
    [RequireComponent(typeof(Light2D))]
    [DisallowMultipleComponent]
    public sealed class HubTorchHalo : MonoBehaviour
    {
        [Tooltip("Rayon monde du halo (taille du glow autour de la flamme).")]
        [SerializeField] private float _radius = 1.15f;
        [Tooltip("Opacite du halo au pic d'intensite de la flamme.")]
        [SerializeField, Range(0f, 1f)] private float _baseAlpha = 0.42f;
        [Tooltip("Au-dessus du decor, sous les particules/UI.")]
        [SerializeField] private int _sortingOrder = 50;

        private Light2D _light;
        private SpriteRenderer _halo;
        private Transform _haloT;
        private float _baseLightIntensity = 1f;
        private Color _flameColor = new Color(1f, 0.62f, 0.30f);

        private void Start()
        {
            _light = GetComponent<Light2D>();
            if (_light == null) return;

            _baseLightIntensity = Mathf.Max(0.01f, _light.intensity);
            // Couleur de flamme : la couleur de la light si elle est teintee, sinon orange chaud.
            if (_light.color != Color.white && _light.color != Color.clear) _flameColor = _light.color;
            _flameColor.a = 1f;

            var go = new GameObject("TorchHalo");
            _haloT = go.transform;
            _haloT.SetParent(transform, worldPositionStays: false);
            _haloT.localPosition = Vector3.zero;
            _haloT.localScale = Vector3.one * _radius;

            _halo = go.AddComponent<SpriteRenderer>();
            _halo.sprite = HaloSprite();
            // Materiau UNLIT (Sprites/Default) : le halo brille de maniere constante, sans etre
            // assombri par l'eclairage 2D de la scene (sinon plus de glow dans les coins sombres).
            _halo.sharedMaterial = HaloMaterial();
            // Layer "Default" : hors "Personnages" pour ne PAS s'iso-trier devant/derriere les persos
            // (le halo doit rester un voile lumineux constant autour de la flamme).
            _halo.sortingOrder = _sortingOrder;
        }

        private void LateUpdate()
        {
            if (_halo == null || _light == null) return;

            var disp = DisplaySettingsController.Instance;
            bool on = disp == null || disp.CurrentProfile.PostProcess;
            if (!on)
            {
                if (_halo.enabled) _halo.enabled = false;
                return;
            }
            if (!_halo.enabled) _halo.enabled = true;

            // Respiration : ratio intensite courante / base (la flamme vacille deja via le flicker).
            float pulse = Mathf.Clamp(_light.intensity / _baseLightIntensity, 0.35f, 1.7f);

            Color tint = disp != null ? disp.CurrentProfile.LightTint : Color.white;
            Color c = _flameColor * tint;
            c.a = Mathf.Clamp01(_baseAlpha * pulse);
            _halo.color = c;

            // Le halo gonfle/retrecit tres legerement avec la flamme -> vivant.
            float s = _radius * (0.92f + 0.08f * pulse);
            _haloT.localScale = new Vector3(s, s, 1f);
        }

        // ===== Sprite + materiau radial partages (generes une seule fois) =====

        private static Sprite _haloSprite;
        private static Material _haloMat;

        private static Material HaloMaterial()
        {
            if (_haloMat == null) _haloMat = new Material(Shader.Find("Sprites/Default"));
            return _haloMat;
        }

        private static Sprite HaloSprite()
        {
            if (_haloSprite != null) return _haloSprite;
            const int s = 128;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            Vector2 c = new Vector2((s - 1) * 0.5f, (s - 1) * 0.5f);
            float rad = s * 0.5f;
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / rad;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * a; // chute douce -> glow concentre, bords tres diffus
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            // PixelsPerUnit = s -> le sprite fait 1 unite monde a scale 1 (on multiplie par _radius).
            _haloSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
            return _haloSprite;
        }
    }
}
