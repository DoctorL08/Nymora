using Quantum;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
        private NymoraClass _currentClass = NymoraClass.None;
        private bool _hoverHookAttached;

        public void Init(SpellIconRegistry registry)
        {
            _registry = registry;
        }

        public void Refresh(in Combatant c)
        {
            int maxResource = CombatantStats.GetMaxResource(c.Class);
            string tag = ResourceTag(c.Class);
            _currentClass = c.Class;
            EnsureHoverHook();

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

        /// <summary>
        /// Attache un hover proxy sur le panel root (= ce GameObject) si pas deja fait.
        /// Idempotent. Le panel root (et pas l'icone) car l'icone peut etre disabled si
        /// sprite null -> pas de raycast. Ajoute un Image transparent au panel si absent
        /// (necessaire pour que le raycast UI fonctionne).
        /// </summary>
        private void EnsureHoverHook()
        {
            if (_hoverHookAttached) return;
            // Garantit un raycast target sur le panel root
            var img = GetComponent<Image>();
            if (img == null)
            {
                img = gameObject.AddComponent<Image>();
                img.color = new Color(0f, 0f, 0f, 0.001f); // quasi-transparent, juste pour capturer raycast
            }
            img.raycastTarget = true;
            var hover = gameObject.GetComponent<PassiveIconHoverProxy>();
            if (hover == null) hover = gameObject.AddComponent<PassiveIconHoverProxy>();
            hover.Bind(this);
            _hoverHookAttached = true;
            Debug.Log("[PassivePanelView] Hover hook attached on panel root.");
        }

        /// <summary>Appele par le proxy hover quand la souris entre le panel passif.</summary>
        internal void OnPassiveIconHoverEnter()
        {
            if (_currentClass == NymoraClass.None)
            {
                Debug.LogWarning("[PassivePanelView] Hover enter mais _currentClass=None (Refresh pas encore appele) — skip tooltip.");
                return;
            }
            string content = PassiveTooltipBuilder.Build(_currentClass);
            // Anchor = ce panel root (RectTransform) pour le positionnement du tooltip.
            // Instance est un lazy singleton -> auto-cree si pas encore instancie.
            PassiveTooltipView.Instance.Show(content, transform as RectTransform);
        }

        internal void OnPassiveIconHoverExit()
        {
            PassiveTooltipView.Instance.Hide();
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

    /// <summary>
    /// Proxy hover attache a l'icone passif. Delegue les events PointerEnter/Exit au panel
    /// parent (PassivePanelView) qui affiche/cache le tooltip.
    /// </summary>
    internal sealed class PassiveIconHoverProxy : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private PassivePanelView _panel;

        public void Bind(PassivePanelView panel)
        {
            _panel = panel;
        }

        public void OnPointerEnter(PointerEventData _)
        {
            Debug.Log("[PassiveIconHoverProxy] PointerEnter");
            _panel?.OnPassiveIconHoverEnter();
        }
        public void OnPointerExit(PointerEventData _)
        {
            Debug.Log("[PassiveIconHoverProxy] PointerExit");
            _panel?.OnPassiveIconHoverExit();
        }
    }
}
