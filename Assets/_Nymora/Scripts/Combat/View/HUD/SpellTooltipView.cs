using Quantum;
using TMPro;
using UnityEngine;

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
    /// </summary>
    public class SpellTooltipView : MonoBehaviour
    {
        [SerializeField] private RectTransform _panel;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _costText;
        [SerializeField] private TMP_Text _descriptionText;

        [Header("Layout")]
        [SerializeField] private float _verticalGap = 12f;

        private void Awake()
        {
            Hide();
        }

        public void Show(SpellId spell, RectTransform anchor)
        {
            if (_panel == null || spell == SpellId.None || anchor == null) return;

            if (_titleText != null) _titleText.text = SpellDisplayInfo.GetDisplayName(spell);

            if (_costText != null) _costText.text = BuildCostLine(spell);
            if (_descriptionText != null) _descriptionText.text = SpellDescriptions.Get(spell);

            // Force le rebuild du layout pour que la hauteur reflete le contenu apres set text.
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);

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

        private static string BuildCostLine(SpellId spell)
        {
            if (!SpellRegistry.TryGet(spell, out SpellDef def)) return string.Empty;

            string filterTag = def.Filter == TargetingFilter.Self ? "Self"
                             : def.Filter == TargetingFilter.Enemy ? "Cible ennemie"
                             : "Case";

            string rangeBlock = def.RangeMax == 0
                ? "(case caster)"
                : (def.RangeMin == def.RangeMax
                    ? $"Portee {def.RangeMax}"
                    : $"Portee {def.RangeMin}-{def.RangeMax}");

            string hgBlock = string.Empty;
            if (def.HGCostMandatory > 0 || def.HGCostMaxOptional > 0)
            {
                hgBlock = $" | HG {def.HGCostMandatory}";
                if (def.HGCostMaxOptional > 0) hgBlock += $" (+{def.HGCostMaxOptional} max)";
            }

            return $"{def.PACost} PA{hgBlock}\n{filterTag} | {rangeBlock}";
        }
    }
}
