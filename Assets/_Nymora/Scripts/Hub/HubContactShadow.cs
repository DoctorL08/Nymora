using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Nymora.Hub
{
    /// <summary>
    /// GFX — Ombre de contact douce sous un prop du decor. 100% View, code-only.
    ///
    /// Ancre le prop au sol (grounding) ET lie l'ombre a la lumiere : l'ellipse est decalee a
    /// l'OPPOSE de la torche la plus proche et son opacite/longueur RESPIRENT avec l'intensite de
    /// cette torche (deja vacillee par le flicker) -> profondeur de lumiere/ombre sans ShadowCaster2D.
    ///
    /// Les torches etant statiques, la DIRECTION de l'ombre est calculee une fois au Start ; seule
    /// l'opacite/echelle est modulee par frame. Coupee en profil "Sans effets".
    ///
    /// Materiau UNLIT noir (alpha-blend) : assombrit le sol de maniere constante (une vraie ombre ne
    /// s'eclaire pas). Auto-posee par <see cref="HubAtmosphere"/> sur les props iso debout.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HubContactShadow : MonoBehaviour
    {
        [Tooltip("Largeur monde de l'ellipse d'ombre.")]
        [SerializeField] private float _width = 1.05f;
        [Tooltip("Hauteur monde (aplatissement) de l'ellipse.")]
        [SerializeField] private float _height = 0.40f;
        [Tooltip("Opacite de l'ombre a l'intensite de base de la torche.")]
        [SerializeField, Range(0f, 1f)] private float _baseAlpha = 0.50f;
        [Tooltip("Decalage monde de l'ombre, a l'oppose de la torche la plus proche.")]
        [SerializeField] private float _offset = 0.30f;
        [Tooltip("Abaisse l'ombre vers les pieds si le pivot du prop n'est pas tout en bas.")]
        [SerializeField] private float _footDrop = 0.05f;

        private SpriteRenderer _shadow;
        private Transform _shadowT;
        private Light2D _keyLight;
        private float _keyBaseIntensity = 1f;
        private Vector3 _basePos;

        private void Start()
        {
            // Torche la plus proche -> direction d'ombre + reference pour la respiration.
            Light2D keyLight = null;
            Vector3 keyPos = transform.position;
            float best = float.MaxValue;
            var torches = FindObjectsByType<TorchLightFlicker>(FindObjectsSortMode.None);
            foreach (var t in torches)
            {
                if (t == null) continue;
                var l = t.GetComponent<Light2D>();
                if (l == null) continue;
                float d = (t.transform.position - transform.position).sqrMagnitude;
                if (d < best) { best = d; keyLight = l; keyPos = t.transform.position; }
            }
            _keyLight = keyLight;
            if (_keyLight != null) _keyBaseIntensity = Mathf.Max(0.01f, _keyLight.intensity);

            // Direction = a l'oppose de la torche, aplatie (l'ombre s'allonge surtout a l'horizontale).
            Vector3 dir = transform.position - keyPos;
            dir.z = 0f; dir.y *= 0.45f;
            Vector2 d2 = dir.sqrMagnitude > 0.0001f ? ((Vector2)dir).normalized : Vector2.down;
            Vector3 worldOffset = new Vector3(d2.x * _offset, d2.y * _offset - _footDrop, 0f);

            var go = new GameObject("ContactShadow");
            _shadowT = go.transform;
            _basePos = transform.position + worldOffset;
            _shadowT.position = _basePos;                 // pas parente : echelle/espace constants
            _shadowT.localScale = new Vector3(_width, _height, 1f);

            _shadow = go.AddComponent<SpriteRenderer>();
            _shadow.sprite = ShadowSprite();
            _shadow.sharedMaterial = ShadowMaterial();
            _shadow.color = new Color(0f, 0f, 0f, _baseAlpha);

            // Sous le prop, meme sorting layer (au-dessus du sol Default si Personnages est devant).
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                _shadow.sortingLayerName = sr.sortingLayerName;
                _shadow.sortingOrder = sr.sortingOrder - 1;
            }
        }

        private void LateUpdate()
        {
            if (_shadow == null) return;

            var disp = DisplaySettingsController.Instance;
            bool on = disp == null || disp.CurrentProfile.PostProcess;
            if (!on)
            {
                if (_shadow.enabled) _shadow.enabled = false;
                return;
            }
            if (!_shadow.enabled) _shadow.enabled = true;

            float pulse = 1f;
            if (_keyLight != null) pulse = Mathf.Clamp(_keyLight.intensity / _keyBaseIntensity, 0.5f, 1.6f);

            var c = _shadow.color;
            c.a = Mathf.Clamp01(_baseAlpha * pulse);
            _shadow.color = c;
            // Une torche plus forte projette une ombre un peu plus longue/marquee.
            _shadowT.localScale = new Vector3(_width * (0.9f + 0.1f * pulse), _height, 1f);
        }

        // ===== Sprite + materiau partages (ellipse douce noire) =====

        private static Sprite _shadowSprite;
        private static Material _shadowMat;

        private static Material ShadowMaterial()
        {
            if (_shadowMat == null) _shadowMat = new Material(Shader.Find("Sprites/Default"));
            return _shadowMat;
        }

        private static Sprite ShadowSprite()
        {
            if (_shadowSprite != null) return _shadowSprite;
            const int s = 96;
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
                    float dd = Vector2.Distance(new Vector2(x, y), c) / rad;
                    float a = Mathf.Clamp01(1f - dd);
                    a *= a; // bords doux
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            // PPU = s -> sprite de 1 unite monde a scale 1 (on l'aplatit via localScale w x h).
            _shadowSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
            return _shadowSprite;
        }
    }
}
