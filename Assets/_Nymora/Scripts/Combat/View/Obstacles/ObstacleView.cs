using System.Collections;
using Quantum;
using TMPro;
using UnityEngine;

namespace Nymora.Combat.View.Obstacles
{
    /// <summary>
    /// 3.1 — MonoBehaviour leger attache a chaque GameObject Obstacle cote View.
    /// Bind a une entity Quantum + display HP via TMP_Text en world space.
    ///
    /// Pas d'animation, pas de logique gameplay — uniquement la representation
    /// visuelle de l'obstacle. Le ObstacleRenderer pousse les data a chaque
    /// CallbackUpdateView.
    ///
    /// Sera enrichi en 3.3.b avec :
    ///   - Sprites par ObstacleKind (Pilier/Mur visuels distincts)
    ///   - Anim destruction (poof particle quand HP=0)
    ///   - Highlight si selectionne par un sort de targeting
    /// </summary>
    public class ObstacleView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _sprite;
        [Tooltip("HP label (TMP world space). Optionnel — si null, on n'affiche pas le HP.")]
        [SerializeField] private TextMeshPro _hpLabel;

        // -----------------------------------------------------------------
        // Style HP label (demande Lorenzo : petit + lisible avec fond sombre).
        // NB : valeurs en CONSTANTES code, PAS en [SerializeField]. Un champ serialise ajoute
        // apres coup se fait "baker" dans le prefab existant avec son ancienne valeur par defaut
        // -> changer le defaut dans le code n'avait plus aucun effet (bug fond "toujours aussi
        // gros"). En constante, le runtime applique TOUJOURS ces valeurs, prefab inchange.
        // -----------------------------------------------------------------
        private const float HpFontSize = 1.6f;
        private const float HpOutlineWidth = 0.3f;
        private const bool HpUseBackground = true;
        // Le fond se dimensionne sur le texte (GetPreferredValues) + ce padding -> toujours snug.
        private const float HpBackgroundPadX = 0.22f;
        private const float HpBackgroundPadY = 0.12f;
        private static readonly Color HpTextColor = new Color(1f, 0.96f, 0.75f, 1f);
        private static readonly Color HpOutlineColor = new Color(0f, 0f, 0f, 1f);
        private static readonly Color HpBackgroundColor = new Color(0f, 0f, 0f, 0.7f);

        [Tooltip("Frames pilotees par le ratio HP/MaxHP. 4 frames attendus : 100% > 75% > 50% > 25%. Vide = sprite statique.")]
        [SerializeField] private Sprite[] _hpFrames;

        public EntityRef Entity { get; private set; }
        public int GridX { get; private set; }
        public int GridY { get; private set; }

        // J8 — anim "sort du sol" jouee une fois a l'apparition de l'obstacle.
        private const float EmergeDuration = 0.32f;
        private bool _emerged;

        public void Bind(EntityRef entity)
        {
            Entity = entity;
            if (_sprite == null) _sprite = GetComponentInChildren<SpriteRenderer>();
            if (_hpLabel == null) _hpLabel = GetComponentInChildren<TextMeshPro>();
            if (_hpLabel != null)
            {
                StyleHpLabel();
                // Polish 3.3.d : HP cache par defaut, affiche uniquement au survol souris (TileHoverView).
                _hpLabel.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Applique le style du HP label au runtime (demande Lorenzo : police petite + lisible).
        /// Fait ici plutot que dans le prefab pour ne PAS exiger une re-generation des prefabs.
        /// Le contour passe par le materiau INSTANCIE (_hpLabel.fontMaterial) -> n'affecte aucun
        /// autre texte de la scene. Le fond est cree ici puis DIMENSIONNE sur le texte dans UpdateData.
        /// </summary>
        private void StyleHpLabel()
        {
            _hpLabel.fontSize = HpFontSize;
            _hpLabel.color = HpTextColor;
            _hpLabel.fontStyle |= TMPro.FontStyles.Bold;
            if (HpOutlineWidth > 0f)
            {
                // L'acces a fontMaterial cree une instance dediee (copie du shared material) :
                // le contour ne deborde donc pas sur les autres TMP.
                _ = _hpLabel.fontMaterial;
                _hpLabel.outlineColor = HpOutlineColor;
                _hpLabel.outlineWidth = HpOutlineWidth;
            }
            if (HpUseBackground) EnsureHpBackground();
        }

        // Fond sombre derriere le HP (cree une fois, enfant du label -> suit l'affichage/masquage).
        private SpriteRenderer _hpBg;

        private void EnsureHpBackground()
        {
            if (_hpBg == null)
            {
                var bgGO = new GameObject("HPBackground");
                bgGO.transform.SetParent(_hpLabel.transform, false);
                bgGO.transform.localPosition = Vector3.zero;
                _hpBg = bgGO.AddComponent<SpriteRenderer>();
                _hpBg.sprite = GetSolidSprite();
                // Juste derriere le texte (TMP HP label = sortingOrder 1100), meme couche.
                _hpBg.sortingOrder = _hpLabel.sortingOrder - 1;
            }
            _hpBg.color = HpBackgroundColor;
            ResizeHpBackgroundToText();
        }

        /// <summary>
        /// Dimensionne le fond sur la taille reelle du texte HP (GetPreferredValues) + padding ->
        /// toujours snug, plus de taille en dur a deviner. Appele a chaque update de texte.
        /// </summary>
        private void ResizeHpBackgroundToText()
        {
            if (_hpBg == null || _hpLabel == null) return;
            Vector2 pref = _hpLabel.GetPreferredValues();
            float w = pref.x + HpBackgroundPadX;
            float h = pref.y + HpBackgroundPadY;
            // Le sprite plein fait 1x1 unite monde (PPU = taille px) -> scale = taille voulue.
            _hpBg.transform.localScale = new Vector3(w, h, 1f);
        }

        // Sprite blanc plein 1x1 unite monde, partage (cache static). Tinte via SpriteRenderer.color.
        private static Sprite _solidSprite;

        private static Sprite GetSolidSprite()
        {
            if (_solidSprite != null) return _solidSprite;
            const int Size = 4;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "ObstacleHpBgTex" };
            var pixels = new Color32[Size * Size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            // PPU = Size -> le sprite couvre 1x1 unite monde (scale ensuite a la taille voulue).
            _solidSprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
            _solidSprite.name = "ObstacleHpBgSprite";
            return _solidSprite;
        }

        /// <summary>
        /// Update les data visuelles depuis le composant Obstacle Quantum (lu par valeur).
        /// Appele par ObstacleRenderer a chaque CallbackUpdateView.
        /// </summary>
        public void UpdateData(Obstacle data, Vector3 worldPos)
        {
            GridX = data.GridX;
            GridY = data.GridY;
            transform.position = worldPos;

            // J8 — au tout premier update (apparition), l'obstacle SORT DU SOL : son sprite
            // grandit verticalement depuis sa base (pivot bas) + fondu. Le scale du sprite est
            // independant de transform.position (pousse chaque frame ici), donc pas de conflit.
            if (!_emerged && _sprite != null)
            {
                _emerged = true;
                StartCoroutine(EmergeFromGround());
            }

            if (_hpLabel != null)
            {
                // Update le text meme si cache : TileHoverView peut l'activer instantanement.
                _hpLabel.text = $"{data.HP}/{data.MaxHP}";
                // Fond redimensionne sur le texte courant (la largeur varie selon les HP).
                if (_hpBg != null) ResizeHpBackgroundToText();
            }
            if (_sprite != null)
            {
                // Sorting order : pareille convention que CombatantView (1000 - (gx+gy)*10).
                // Les obstacles partagent la meme couche que les combattants.
                _sprite.sortingOrder = 1000 - (data.GridX + data.GridY) * 10;

                // Frame piloté par ratio HP/MaxHP (4 paliers : >75% / >50% / >25% / sinon).
                // Pillar : 4 frames de degradation progressive (fissures + cristal qui s'exposent).
                // Wall : pas de frames, sprite statique inchangé.
                if (_hpFrames != null && _hpFrames.Length > 0 && data.MaxHP > 0)
                {
                    int idx;
                    int maxHp = data.MaxHP;
                    int hp = data.HP;
                    // Compare en entiers (4*hp vs maxHp*N) pour éviter tout float côté View.
                    if (4 * hp > 3 * maxHp) idx = 0;        // >75%
                    else if (4 * hp > 2 * maxHp) idx = 1;   // >50%
                    else if (4 * hp > 1 * maxHp) idx = 2;   // >25%
                    else idx = 3;                            // <=25%
                    if (idx >= _hpFrames.Length) idx = _hpFrames.Length - 1;
                    if (_hpFrames[idx] != null) _sprite.sprite = _hpFrames[idx];
                }
            }
        }

        // J8 — Le sprite grandit verticalement depuis sa base (sort du sol) + fondu, une fois.
        private IEnumerator EmergeFromGround()
        {
            Transform st = _sprite.transform;
            Vector3 baseScale = st.localScale;
            Color baseColor = _sprite.color;
            float e = 0f;
            while (e < EmergeDuration && _sprite != null)
            {
                e += Time.deltaTime;
                float k = Mathf.Clamp01(e / EmergeDuration);
                float yScale = GroundEmergeEase.BackOut(k);
                st.localScale = new Vector3(baseScale.x, baseScale.y * yScale, baseScale.z);
                var c = baseColor;
                c.a = baseColor.a * Mathf.Clamp01(k * 2.2f);
                _sprite.color = c;
                yield return null;
            }
            if (_sprite != null)
            {
                st.localScale = baseScale;
                _sprite.color = baseColor;
            }
        }

        /// <summary>
        /// Polish 3.3.d : affiche ou cache le HP label. Pilote par TileHoverView au survol souris.
        /// </summary>
        public void SetHpVisible(bool visible)
        {
            if (_hpLabel != null) _hpLabel.gameObject.SetActive(visible);
        }
    }
}
