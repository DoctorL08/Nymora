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

        // Fix 18 mai : highlight overlay au-dessus des terrains/pieges au sol (qui sont
        // a SortingOrder+1 via TerrainView/TrapView). Sans ce sprite separe, ApplyHighlight
        // teintait le sprite floor (SortingOrder 0) qui etait soit masque par l'overlay
        // terrain (Sang Coagule, piege, etc.), soit DESACTIVE par SetFloorVisible(false)
        // -> impossible de voir la portee de sort sur les cases avec terrain.
        // Offset +50 = au-dessus des terrains (+1) mais sous les combatants (700-1000).
        private const int HighlightSortingOffset = 50;
        private SpriteRenderer _highlightSprite;

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
        /// Utilise un sprite overlay dedie (au-dessus des terrains/pieges au sol) au lieu
        /// de teinter le sprite floor lui-meme — cf fix 18 mai.
        /// </summary>
        public void ApplyHighlight(Color highlightColor)
        {
            EnsureHighlightSprite();
            if (_highlightSprite == null) return;
            _highlightSprite.color = highlightColor;
            _highlightSprite.enabled = true;
        }

        /// <summary>
        /// Restaure la couleur de base (echiquier). En pratique : desactive le sprite overlay.
        /// </summary>
        public void ClearHighlight()
        {
            if (_highlightSprite != null) _highlightSprite.enabled = false;
        }

        /// <summary>
        /// Lazy spawn du sprite overlay highlight. Cree un child "Highlight" qui copie la
        /// forme/taille du sprite floor et rend a SortingOrder + 50 (au-dessus des
        /// terrains/pieges +1, sous les combatants ~700-1000).
        /// </summary>
        private void EnsureHighlightSprite()
        {
            if (_highlightSprite != null) return;
            if (_sprite == null) return;
            var go = new GameObject("Highlight");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = _sprite.transform.localScale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _sprite.sprite;
            sr.sortingLayerID = _sprite.sortingLayerID;
            sr.sortingOrder = _sprite.sortingOrder + HighlightSortingOffset;
            sr.enabled = false;
            _highlightSprite = sr;
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
