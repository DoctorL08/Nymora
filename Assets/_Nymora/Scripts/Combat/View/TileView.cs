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

        public void Setup(int gx, int gy, Color baseColor)
        {
            GridX = gx;
            GridY = gy;

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
    }
}
