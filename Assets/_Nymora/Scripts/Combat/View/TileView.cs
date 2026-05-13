using UnityEngine;

namespace Nymora.Combat.View
{
    /// <summary>
    /// Composant attache sur chaque tile instanciee par GridRenderer.
    /// Stocke ses coordonnees logiques et expose un highlight pour la suite
    /// (preview de mouvement / targeting en briques 2.4+).
    /// </summary>
    public class TileView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _sprite;

        public int GridX { get; private set; }
        public int GridY { get; private set; }

        public string SortingLayerName => _sprite != null ? _sprite.sortingLayerName : "Default";
        public int SortingOrder => _sprite != null ? _sprite.sortingOrder : 0;

        /// <summary>2.14 — Sprite du sol (utilise par FogOfWarView pour creer un overlay
        /// de la meme forme/taille que la tile).</summary>
        public Sprite FloorSprite => _sprite != null ? _sprite.sprite : null;

        private Color _baseColor;

        public void Setup(int gx, int gy, Color baseColor)
        {
            GridX = gx;
            GridY = gy;
            _baseColor = baseColor;

            if (_sprite == null)
            {
                _sprite = GetComponentInChildren<SpriteRenderer>();
            }

            if (_sprite != null)
            {
                _sprite.color = baseColor;
            }
        }

        public void SetSortingOrder(string layer, int order)
        {
            if (_sprite == null) return;
            _sprite.sortingLayerName = layer;
            _sprite.sortingOrder = order;
        }

        /// <summary>
        /// Surligne la tile avec une couleur de highlight (typiquement pour targeting preview).
        /// </summary>
        public void ApplyHighlight(Color highlightColor)
        {
            if (_sprite != null) _sprite.color = highlightColor;
        }

        /// <summary>
        /// Restaure la couleur de base (echiquier).
        /// </summary>
        public void ClearHighlight()
        {
            if (_sprite != null) _sprite.color = _baseColor;
        }

        /// <summary>
        /// 2.13.e : cache/affiche le sprite du sol echiquier. Utilise par TerrainView pour
        /// remplacer visuellement la case quand un terrain est pose (la case ne doit pas
        /// transparaitre sous le terrain — comportement Bible V7.1 attendu).
        /// </summary>
        public void SetFloorVisible(bool visible)
        {
            if (_sprite != null) _sprite.enabled = visible;
        }
    }
}
