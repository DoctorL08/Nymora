using UnityEngine;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 4.X — Scale auto de la map background pour matcher l'emprise iso de la grille.
    ///
    /// IMPORTANT : modifier `_scaleMultiplier` et `_yOffset` ci-dessous (PAS le Transform).
    /// Avec `[ExecuteAlways]`, l'autoScale s'applique aussi en Editor (preview instantanée
    /// quand on bouge les sliders), sinon les ajustements Transform faits manuellement
    /// sont écrasés au Start.
    ///
    /// Si on veut prendre la main sur le Transform : désactiver `_autoScale`.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class HubBackgroundImage : MonoBehaviour
    {
        [SerializeField] private HubGridRenderer _grid;
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [Tooltip("Si false, le Transform reste tel que défini manuellement (utile pour tweaking artistique).")]
        [SerializeField] private bool _autoScale = true;
        [Tooltip("Multiplicateur pour zoomer/dézoomer la map au-delà de la largeur de la grille")]
        [SerializeField, Range(0.3f, 4f)] private float _scaleMultiplier = 1.4f;
        [Tooltip("Décalage Y manuel (la map iso n'est pas centrée verticalement = trees en haut)")]
        [SerializeField] private float _yOffset = 0f;

        private void OnEnable()
        {
            if (_autoScale) ApplyAutoScale();
        }

        private void OnValidate()
        {
            // Preview temps réel quand Lorenzo bouge les sliders (Edit Mode + Play Mode)
            if (_autoScale) ApplyAutoScale();
        }

        private void ApplyAutoScale()
        {
            if (_grid == null) _grid = FindFirstObjectByType<HubGridRenderer>();
            if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_grid == null || _spriteRenderer == null || _spriteRenderer.sprite == null) return;

            float gridIsoWidth = _grid.Width * _grid.TileWorldWidth * _scaleMultiplier;
            float spriteNaturalWidth = _spriteRenderer.sprite.bounds.size.x;
            if (spriteNaturalWidth <= 0f) return;

            float scaleFactor = gridIsoWidth / spriteNaturalWidth;
            transform.localScale = new Vector3(scaleFactor, scaleFactor, 1f);
            transform.position = new Vector3(0f, _yOffset, transform.position.z);
        }
    }
}
