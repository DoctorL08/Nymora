using Quantum;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Panel UI tooltip pour un sort. Hidden par defaut.
    ///
    /// Show(spell, anchorRect) :
    ///   - Populate les textes (nom, cout PA, cout HG, portee, description Bible)
    ///   - Position au-dessus du slot anchor. Flip dessous si depasse l'ecran.
    ///   - Affiche le panel.
    ///
    /// Hide() : cache le panel.
    ///
    /// Le panel doit etre dans le meme Canvas que les slots (pour partager les coords UI).
    ///
    /// Layout : largeur LOCKEE a <see cref="_fixedWidth"/>, hauteur AUTO-FIT au contenu
    /// (la description Bible peut faire 2 a 8 lignes selon le sort). Configuration
    /// VerticalLayoutGroup + ContentSizeFitter auto-ajoutee au Awake si manquante
    /// pour eviter manip Unity scene-by-scene.
    /// </summary>
    public class SpellTooltipView : MonoBehaviour
    {
        [SerializeField] private RectTransform _panel;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _costText;
        [SerializeField] private TMP_Text _descriptionText;

        [Header("Layout")]
        [SerializeField] private float _verticalGap = 12f;

        [Header("Sizing")]
        [Tooltip("Largeur fixe du panel en pixels UI. La hauteur s'ajuste auto au contenu (word-wrap description Bible).")]
        [SerializeField] private float _fixedWidth = 320f;
        [Tooltip("Padding interne du panel (left/right/top/bottom) en pixels. Applique seulement si le VerticalLayoutGroup est cree par ce script.")]
        [SerializeField] private int _panelPadding = 10;
        [Tooltip("Espacement vertical entre titre / cout / description. Applique seulement si le VerticalLayoutGroup est cree par ce script.")]
        [SerializeField] private float _panelSpacing = 6f;

        [Header("Lisibilite")]
        [Tooltip("Multiplicateur applique UNE fois (Awake) aux tailles de police du tooltip " +
                 "(titre / cout / description) pour la lisibilite en combat. 1 = inchange.")]
        [SerializeField, Min(1f)] private float _textSizeMultiplier = 1.12f;

        private bool _fontSizesScaled;

        private void Awake()
        {
            EnsureLayoutWiring();
            ApplyTextSize();
            Hide();
        }

        /// <summary>
        /// Agrandit legerement les polices du tooltip (titre/cout/description) pour la lisibilite combat.
        /// Applique UNE seule fois (garde _fontSizesScaled) : Awake ne tourne qu'au demarrage, donc pas
        /// de compounding ; on capture la taille serialisee de la scene et on la multiplie.
        /// </summary>
        private void ApplyTextSize()
        {
            if (_fontSizesScaled || _textSizeMultiplier <= 1f) return;
            if (_titleText != null) _titleText.fontSize *= _textSizeMultiplier;
            if (_costText != null) _costText.fontSize *= _textSizeMultiplier;
            if (_descriptionText != null) _descriptionText.fontSize *= _textSizeMultiplier;
            _fontSizesScaled = true;
        }

        /// <summary>
        /// Force la config layout du panel pour "largeur fixe / hauteur auto-fit".
        /// Self-healing : ajoute VerticalLayoutGroup + ContentSizeFitter s'ils manquent,
        /// active le word-wrap des TMPs, lock la largeur du RectTransform a _fixedWidth.
        /// Idempotent : re-run au Awake n'overrideke pas un VLG/Fitter pre-existant (preserve
        /// padding / spacing custom Lorenzo).
        /// </summary>
        private void EnsureLayoutWiring()
        {
            if (_panel != null)
            {
                var vlg = _panel.GetComponent<VerticalLayoutGroup>();
                if (vlg == null)
                {
                    vlg = _panel.gameObject.AddComponent<VerticalLayoutGroup>();
                    vlg.padding = new RectOffset(_panelPadding, _panelPadding, _panelPadding, _panelPadding);
                    vlg.spacing = _panelSpacing;
                    vlg.childAlignment = TextAnchor.UpperLeft;
                    vlg.childControlWidth = true;
                    vlg.childControlHeight = true;
                    vlg.childForceExpandWidth = true;
                    vlg.childForceExpandHeight = false;
                }

                var fitter = _panel.GetComponent<ContentSizeFitter>();
                if (fitter == null)
                {
                    fitter = _panel.gameObject.AddComponent<ContentSizeFitter>();
                }
                // OVERWRITE : on tient absolument a horizontal=Unconstrained pour que
                // la largeur fixe ne soit pas ecrasee par le preferred size du texte.
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                // Lock la largeur du panel a _fixedWidth (la hauteur sera ecrite par
                // ContentSizeFitter au prochain layout pass).
                _panel.sizeDelta = new Vector2(_fixedWidth, _panel.sizeDelta.y);
            }

            // Word-wrap obligatoire sur la description Bible (2-8 lignes attendues).
            // Title / cost sont generalement 1 ligne mais on active aussi par securite.
            if (_titleText != null) _titleText.enableWordWrapping = true;
            if (_costText != null) _costText.enableWordWrapping = true;
            if (_descriptionText != null) _descriptionText.enableWordWrapping = true;
        }

        public void Show(SpellId spell, RectTransform anchor) => Show(spell, anchor, -1, default, false);

        public void Show(SpellId spell, RectTransform anchor, int effectivePaCost)
            => Show(spell, anchor, effectivePaCost, default, false);

        // effectivePaCost >= 0 : coût PA réel (avec bonus -1 Soulrender/Ghostra/Effondrement,
        // Permutation gratuite) calculé par le HUD. -1 = afficher le coût de base du sort.
        // caster/hasCaster : combattant local pour résoudre les valeurs buffées (dégâts/portée en vert)
        //   dans la description + ligne de coût. hasCaster=false -> valeurs de base (plain).
        public void Show(SpellId spell, RectTransform anchor, int effectivePaCost, in Combatant caster, bool hasCaster)
        {
            if (_panel == null || spell == SpellId.None || anchor == null) return;

            if (_titleText != null) _titleText.text = SpellDisplayInfo.GetDisplayName(spell);

            if (_costText != null) _costText.text = BuildCostLine(spell, effectivePaCost, caster, hasCaster);
            if (_descriptionText != null) _descriptionText.text = SpellTooltipText.ResolveDescription(spell, caster, hasCaster);

            // Re-lock la largeur a chaque Show (au cas ou un layout pass tier l'aurait modifiee).
            // La hauteur reste ecrite par le ContentSizeFitter au rebuild ci-dessous.
            _panel.sizeDelta = new Vector2(_fixedWidth, _panel.sizeDelta.y);

            // Force le rebuild du layout pour que la hauteur reflete le contenu apres set text.
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);

            // Positionne au-dessus du slot ; flip dessous si dépasse le haut.
            Vector3[] slotCorners = new Vector3[4];
            anchor.GetWorldCorners(slotCorners);
            float slotCenterX = (slotCorners[0].x + slotCorners[2].x) * 0.5f;
            float slotTopY = slotCorners[1].y;
            float slotBottomY = slotCorners[0].y;

            Vector3[] panelCorners = new Vector3[4];
            _panel.GetWorldCorners(panelCorners);
            float panelHeight = panelCorners[1].y - panelCorners[0].y;

            float canvasTop = float.MaxValue;
            var canvas = _panel.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var canvasRT = canvas.transform as RectTransform;
                if (canvasRT != null)
                {
                    Vector3[] canvasCorners = new Vector3[4];
                    canvasRT.GetWorldCorners(canvasCorners);
                    canvasTop = canvasCorners[1].y;
                }
            }

            bool flipBelow = (slotTopY + _verticalGap + panelHeight) > canvasTop;
            Vector3 target = flipBelow
                ? new Vector3(slotCenterX, slotBottomY - _verticalGap, 0f)
                : new Vector3(slotCenterX, slotTopY + _verticalGap, 0f);

            _panel.pivot = flipBelow ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
            _panel.position = target;
            _panel.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_panel != null) _panel.gameObject.SetActive(false);
        }

        private static string BuildCostLine(SpellId spell, int effectivePaCost, in Combatant caster, bool hasCaster)
        {
            if (!SpellRegistry.TryGet(spell, out SpellDef def)) return string.Empty;

            string filterTag = def.Filter == TargetingFilter.Self ? "Self"
                             : def.Filter == TargetingFilter.Enemy ? "Cible ennemie"
                             : "Case";

            // Portée effective (bonus de phase Nightseer +1) en vert si buffée par rapport au sort.
            int rangeMaxEff = def.RangeMax;
            bool rangeBuffed = false;
            if (hasCaster && def.RangeMax > 0)
                rangeMaxEff = SpellTooltipText.EffectiveRange(caster, def, out rangeBuffed);
            string rangeMaxStr = rangeBuffed ? $"<color={SpellTooltipText.GreenHex}>{rangeMaxEff}</color>" : rangeMaxEff.ToString();

            string rangeBlock = def.RangeMax == 0
                ? "(case caster)"
                : (def.RangeMin == def.RangeMax
                    ? $"Portee {rangeMaxStr}"
                    : $"Portee {def.RangeMin}-{rangeMaxStr}");

            string hgBlock = string.Empty;
            if (def.HGCostMandatory > 0 || def.HGCostMaxOptional > 0)
            {
                hgBlock = $" | HG {def.HGCostMandatory}";
                if (def.HGCostMaxOptional > 0) hgBlock += $" (+{def.HGCostMaxOptional} max)";
            }

            // Coût PA effectif (bonus inclus) : en vert s'il est réduit par rapport au coût de base.
            int pa = effectivePaCost >= 0 ? effectivePaCost : def.PACost;
            string paStr = pa < def.PACost ? $"<color=#7CFC8A>{pa}</color>" : pa.ToString();

            return $"{paStr} PA{hgBlock}\n{filterTag} | {rangeBlock}";
        }
    }
}
