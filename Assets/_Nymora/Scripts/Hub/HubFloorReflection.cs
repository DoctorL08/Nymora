using UnityEngine;

namespace Nymora.Hub
{
    /// <summary>
    /// GFX — Ombre projetée "miroir" d'un sprite. 100% View (aucune sim Quantum).
    ///
    /// Pose une COPIE MIROIR verticale NOIRE du sprite source, atténuée, sous l'objet, avec un léger
    /// ondulé -> silhouette d'ombre parfaite (épouse exactement la forme, ex. flamme de torche).
    /// Copie le sprite courant de la source chaque frame -> suit les animations (flammes, skins…).
    ///
    /// Le flip se fait autour du pivot du sprite : avec un pivot aux PIEDS (cf <see cref="IsoDepthSort"/>),
    /// l'ombre descend proprement depuis le sol. Matériau unlit -> noir pur, insensible aux lumières.
    ///
    /// Coupée en profil "Sans effets". Auto-posée par <see cref="HubAtmosphere"/> sur les props iso debout.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HubFloorReflection : MonoBehaviour
    {
        [Tooltip("Opacité de l'ombre à la base (faible = subtile). Le dégradé l'estompe vers la pointe.")]
        [SerializeField, Range(0f, 1f)] private float _alpha = 0.14f;
        [Tooltip("Compression verticale de l'ombre (un peu écrasée au sol).")]
        [SerializeField] private float _heightScale = 0.9f;
        [Tooltip("Inclinaison de base de l'ombre (degrés). Positif = pointe vers la droite.")]
        [SerializeField] private float _leanDeg = 18f;
        [Tooltip("Amplitude de l'ondulé (degrés) — l'ombre vacille avec la flamme.")]
        [SerializeField] private float _wobbleDeg = 1.4f;
        [SerializeField] private float _wobbleSpeed = 1.1f;
        [Tooltip("Sous la source, au-dessus du sol.")]
        [SerializeField] private int _orderOffset = -1;

        private SpriteRenderer _src;
        private SpriteRenderer _refl;
        private Transform _reflT;
        private float _seed;

        private void Start()
        {
            _src = GetComponent<SpriteRenderer>();
            if (_src == null) { enabled = false; return; }

            var go = new GameObject("FloorShadow");
            _reflT = go.transform;
            _reflT.SetParent(transform, worldPositionStays: false);
            _reflT.localPosition = Vector3.zero;
            _reflT.localScale = new Vector3(1f, -_heightScale, 1f); // flip vertical autour du pivot

            _refl = go.AddComponent<SpriteRenderer>();
            _refl.sharedMaterial = ShadowMaterial(); // unlit + dégradé -> noir estompé
            _seed = Random.value * 10f;
        }

        private void LateUpdate()
        {
            if (_src == null || _refl == null) return;

            var disp = DisplaySettingsController.Instance;
            bool on = disp == null || disp.CurrentProfile.PostProcess;
            if (!on)
            {
                if (_refl.enabled) _refl.enabled = false;
                return;
            }
            if (!_refl.enabled) _refl.enabled = true;

            // Suit le sprite courant de la source (flammes animées, skins…).
            _refl.sprite = _src.sprite;
            _refl.flipX = _src.flipX;

            // Silhouette NOIRE = ombre projetée parfaite (épouse la forme du sprite).
            _refl.color = new Color(0f, 0f, 0f, _src.color.a * _alpha);

            // Juste derrière la source, même sorting layer -> posée sur le sol sous l'objet.
            _refl.sortingLayerID = _src.sortingLayerID;
            _refl.sortingOrder = _src.sortingOrder + _orderOffset;

            // Inclinaison de base (pointe vers la droite) + ondulé qui vacille avec la flamme.
            float wob = Mathf.Sin(Time.time * _wobbleSpeed + _seed) * _wobbleDeg;
            _reflT.localRotation = Quaternion.Euler(0f, 0f, _leanDeg + wob);
        }

        // Matériau partagé : shader dédié (silhouette noire + dégradé d'opacité), fallback Sprites/Default.
        private static Material _shadowMat;

        private static Material ShadowMaterial()
        {
            if (_shadowMat != null) return _shadowMat;
            var sh = Shader.Find("Nymora/FloorShadow");
            if (sh != null)
            {
                _shadowMat = new Material(sh);
                _shadowMat.SetFloat("_Alpha", 1f);
                _shadowMat.SetFloat("_FadeBase", 1f); // opaque près de la base (pieds)
                _shadowMat.SetFloat("_FadeTip", 0f);  // transparent à la pointe
            }
            else
            {
                _shadowMat = new Material(Shader.Find("Sprites/Default"));
            }
            return _shadowMat;
        }
    }
}
