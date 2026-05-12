using Quantum;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Bloc bas-gauche : icone du passif de la classe (Hemoglyphe pour Soulrender) +
    /// compteur "HG x/max". Refresh chaque tick avec l'etat du combattant local.
    ///
    /// 2.13.a : seul Soulrender a un sprite passif dispo. Les autres classes seront
    /// completees en 2.14+ (Nightseer Prescience, Phase 3 reste).
    /// </summary>
    public class PassivePanelView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _counter;
        [SerializeField] private TMP_Text _label;

        private SpellIconRegistry _registry;

        public void Init(SpellIconRegistry registry)
        {
            _registry = registry;
        }

        public void Refresh(in Combatant c)
        {
            int maxResource = CombatantStats.GetMaxResource(c.Class);
            string tag = ResourceTag(c.Class);

            if (_icon != null)
            {
                Sprite sprite = _registry != null ? _registry.PassifIconFor(c.Class) : null;
                _icon.sprite = sprite;
                _icon.enabled = sprite != null;
            }
            if (_counter != null)
            {
                _counter.text = maxResource > 0 ? $"{c.Resource}/{maxResource}" : "-";
            }
            if (_label != null)
            {
                _label.text = tag;
            }
        }

        public void Clear()
        {
            if (_icon != null) _icon.enabled = false;
            if (_counter != null) _counter.text = "-";
            if (_label != null) _label.text = string.Empty;
        }

        private static string ResourceTag(NymoraClass cls)
        {
            switch (cls)
            {
                case NymoraClass.Soulrender: return "HG";
                case NymoraClass.Nightseer:  return "PR";
                case NymoraClass.Colossar:   return "FD";
                case NymoraClass.Necram:     return "PT";
                case NymoraClass.Ghostra:    return "RM";
                default: return string.Empty;
            }
        }
    }
}
