using System.Collections;
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
        // Public : TrapView s'aligne dessus (#8) pour rendre les pièges AU-DESSUS du surlignage de
        // portée (sinon le bleu castable recouvre la rune et on ne voit plus le piège pendant le ciblage).
        public const int HighlightSortingOffset = 50;
        private SpriteRenderer _highlightSprite;

        public int GridX { get; private set; }
        public int GridY { get; private set; }

        public string SortingLayerName => _sprite != null ? _sprite.sortingLayerName : "Default";
        public int SortingOrder => _sprite != null ? _sprite.sortingOrder : 0;

        /// <summary>2.14 — Sprite du sol (utilise par FogOfWarView pour creer un overlay
        /// de la meme forme/taille que la tile).</summary>
        public Sprite FloorSprite => _sprite != null ? _sprite.sprite : null;

        private Color _baseColor;
        // J8 — scale d'origine du sprite floor, capture AVANT l'anim d'apparition. Sert au
        // highlight (EnsureHighlightSprite) pour ne JAMAIS copier un scale mi-animation (sinon
        // les previews de portee PM apparaissent rapetissees a vie — regression J8).
        private Vector3 _floorBaseScale = Vector3.one;

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
                _floorBaseScale = _sprite.transform.localScale; // capture AVANT l'anim
                _sprite.color = baseColor;
                // J8 — la tile SORT DU SOL a l'init : pop + fondu, decale selon la profondeur iso
                // (gx+gy) pour une vague fluide d'assemblage du plateau. No-op si GO inactif.
                if (isActiveAndEnabled) StartCoroutine(EmergeRoutine((gx + gy) * 0.012f));
            }
        }

        // J8 — Apparition de la tile : invisible le temps du delai, puis pop (back-out) + fondu.
        private IEnumerator EmergeRoutine(float delay)
        {
            if (_sprite == null) yield break;
            Transform st = _sprite.transform;
            Vector3 baseScale = st.localScale;
            Color baseCol = _sprite.color;

            var hidden = baseCol; hidden.a = 0f; _sprite.color = hidden;
            st.localScale = new Vector3(0f, 0f, baseScale.z);

            float w = 0f;
            while (w < delay) { w += Time.deltaTime; yield return null; }

            const float dur = 0.30f;
            float e = 0f;
            while (e < dur && _sprite != null)
            {
                e += Time.deltaTime;
                float k = Mathf.Clamp01(e / dur);
                float s = GroundEmergeEase.BackOut(k);
                st.localScale = new Vector3(baseScale.x * s, baseScale.y * s, baseScale.z);
                var c = baseCol; c.a = baseCol.a * Mathf.Clamp01(k * 2f); _sprite.color = c;
                yield return null;
            }
            if (_sprite != null)
            {
                st.localScale = baseScale;
                _sprite.color = baseCol;
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
            go.transform.localScale = _floorBaseScale; // scale d'origine, pas le scale mi-anim J8
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
