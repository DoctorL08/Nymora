using System.Collections.Generic;
using Nymora.Core.ScriptableObjects;
using Quantum;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Timeline bas-droite : cadre avec les 2 sprites IDLE ANIMES des combatants cote a
    /// cote (refacto 19 mai — anciennement portraits 128px statiques). Le combatant actif
    /// est mis en evidence (cadre jaune + alpha plein), l'inactif est grise.
    ///
    /// Auto-init complet : si _slot0Root/_slot1Root sont null, le TimelineView spawn sa
    /// propre structure Container + 2 Slots + Image portraits a l'Awake. Lorenzo n'a qu'a
    /// poser le component sur un GameObject UI dans la zone bas-droite du HUD.
    /// </summary>
    public class TimelineView : MonoBehaviour
    {
        [Header("Refs optionnelles (auto-spawn si null)")]
        [SerializeField] private RectTransform _slot0Root;
        [SerializeField] private RectTransform _slot1Root;
        [SerializeField] private Image _portrait0;
        [SerializeField] private Image _portrait1;
        [SerializeField] private Image _slot0Frame;
        [SerializeField] private Image _slot1Frame;
        [SerializeField] private TMP_Text _legacyLabel; // ancien texte P0/P1, hide si auto-spawn

        [Header("Style (re-skin DA hub : monochrome)")]
        [SerializeField] private Vector2 _slotSize = new Vector2(96f, 112f);
        [SerializeField] private float _slotSpacing = 10f;
        [SerializeField] private Color _frameActive = new Color(0.93f, 0.94f, 0.96f, 1f);   // accent clair = actif
        [SerializeField] private Color _frameInactive = new Color(0.16f, 0.165f, 0.185f, 1f); // surface carte
        [SerializeField] private Color _portraitActive = Color.white;
        [SerializeField] private Color _portraitInactive = new Color(0.55f, 0.55f, 0.58f, 1f);
        [SerializeField] private int _frameBorderPx = 4;

        private CombatUISpriteAnimator _animator0;
        private CombatUISpriteAnimator _animator1;
        private Dictionary<NymoraClass, NymoraClassDefinition> _classByEnum;

        // Combatants courants caches pour le tooltip (cf Refresh).
        private Combatant _currentP0;
        private bool _hasP0;
        private Combatant _currentP1;
        private bool _hasP1;
        private int _currentTurnNumber;

        [Header("B8 (22 mai) — Position")]
        [Tooltip("Remonte la timeline de N px pour loger le bouton Abandonner en dessous. " +
                 "Applique une fois au chargement. Mets 0 pour ne pas bouger.")]
        [SerializeField] private float _verticalNudge = 80f;

        private void Awake()
        {
            // B8 (22 mai) — remonte la timeline d'un cran pour loger le bouton Abandonner sous elle.
            var selfRt = transform as RectTransform;
            if (selfRt != null && !Mathf.Approximately(_verticalNudge, 0f))
            {
                selfRt.anchoredPosition += new Vector2(0f, _verticalNudge);
            }

            // Auto-find le label legacy si pas drag-drop dans Inspector. Sans ce cleanup,
            // l'ancien TMP "P0 | P1" hardcode dans la scene reste visible sous les nouveaux
            // slots animes (fix 19 mai).
            if (_legacyLabel == null)
            {
                _legacyLabel = GetComponentInChildren<TMP_Text>(true);
            }
            if (_legacyLabel != null) _legacyLabel.gameObject.SetActive(false);
            EnsureSlots();

            // Re-skin DA hub : cadres arrondis (sprite généré au runtime). ApplySlot ne change
            // que la couleur du cadre -> la forme arrondie persiste.
            if (_slot0Frame != null) CombatUiKit.ApplyRounded(_slot0Frame, 10f);
            if (_slot1Frame != null) CombatUiKit.ApplyRounded(_slot1Frame, 10f);
        }

        /// <summary>
        /// Setup les definitions de classes (drag par CombatHUDController). Cree un lookup
        /// dict pour fast access aux IdleFrames + IdleFps par classe.
        /// </summary>
        public void Init(NymoraClassDefinition[] classDefinitions)
        {
            _classByEnum = new Dictionary<NymoraClass, NymoraClassDefinition>(5);
            if (classDefinitions == null) return;
            foreach (var def in classDefinitions)
            {
                if (def == null) continue;
                var key = (NymoraClass)(byte)def.ClassId; // cast Core.Enums -> Quantum.NymoraClass (memes valeurs)
                _classByEnum[key] = def;
            }
        }

        public void Refresh(int activePlayerIndex, NymoraClass p0Class, NymoraClass p1Class)
        {
            EnsureSlots();
            ApplySlot(_slot0Frame, _portrait0, _animator0, p0Class, activePlayerIndex == 0);
            ApplySlot(_slot1Frame, _portrait1, _animator1, p1Class, activePlayerIndex == 1);
        }

        /// <summary>
        /// Surcharge avec les Combatant structs complets — necessaire pour le tooltip hover
        /// qui affiche HP/Ressource/Statuses/Marque. Appelee par CombatHUDController.OnUpdateView.
        /// </summary>
        public void RefreshWithCombatants(int activePlayerIndex, Combatant p0, bool hasP0, Combatant p1, bool hasP1, int turnNumber)
        {
            _currentP0 = p0; _hasP0 = hasP0;
            _currentP1 = p1; _hasP1 = hasP1;
            _currentTurnNumber = turnNumber;
            NymoraClass p0Cls = hasP0 ? p0.Class : NymoraClass.None;
            NymoraClass p1Cls = hasP1 ? p1.Class : NymoraClass.None;
            Refresh(activePlayerIndex, p0Cls, p1Cls);
        }

        internal void OnSlotHoverEnter(int slotIndex)
        {
            string text;
            RectTransform anchor;
            if (slotIndex == 0)
            {
                if (!_hasP0) return;
                text = TimelineSlotTooltipBuilder.Build(_currentP0, _currentTurnNumber);
                anchor = _slot0Root;
            }
            else
            {
                if (!_hasP1) return;
                text = TimelineSlotTooltipBuilder.Build(_currentP1, _currentTurnNumber);
                anchor = _slot1Root;
            }
            PassiveTooltipView.Instance.ShowAbove(text, anchor);
        }

        internal void OnSlotHoverExit()
        {
            PassiveTooltipView.Instance.Hide();
        }

        private void ApplySlot(Image frame, Image portrait, CombatUISpriteAnimator anim, NymoraClass cls, bool isActive)
        {
            if (frame != null) frame.color = isActive ? _frameActive : _frameInactive;
            if (portrait == null) return;

            NymoraClassDefinition def = null;
            _classByEnum?.TryGetValue(cls, out def);

            if (def != null && def.IdleFrames != null && def.IdleFrames.Length > 0)
            {
                portrait.enabled = true;
                portrait.color = isActive ? _portraitActive : _portraitInactive;
                if (anim != null) anim.Play(portrait, def.IdleFrames, def.IdleFps);
            }
            else
            {
                // Fallback : portrait statique si pas de frames idle dispo
                portrait.sprite = def != null ? def.PortraitSprite : null;
                portrait.enabled = portrait.sprite != null;
                portrait.color = isActive ? _portraitActive : _portraitInactive;
            }
        }

        private void EnsureSlots()
        {
            if (_slot0Root != null && _slot1Root != null) return;

            var container = new GameObject("Container",
                typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            container.transform.SetParent(transform, false);
            var containerRt = (RectTransform)container.transform;
            // Anchor right-center : le container se developpe vers la GAUCHE depuis le bord
            // droit du panel parent, ne deborde jamais a droite (fix coupure ecran 19 mai).
            containerRt.anchorMin = new Vector2(1f, 0.5f);
            containerRt.anchorMax = new Vector2(1f, 0.5f);
            containerRt.pivot = new Vector2(1f, 0.5f);
            containerRt.anchoredPosition = Vector2.zero;
            // sizeDelta calc auto via ContentSizeFitter selon les slots
            var hlg = container.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = _slotSpacing;
            hlg.childAlignment = TextAnchor.MiddleRight;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            var fitter = container.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _slot0Root = BuildSlot(container.transform, "Slot_P0", 0, out _slot0Frame, out _portrait0, out _animator0);
            _slot1Root = BuildSlot(container.transform, "Slot_P1", 1, out _slot1Frame, out _portrait1, out _animator1);
        }

        private RectTransform BuildSlot(Transform parent, string name, int slotIndex, out Image frame, out Image portrait, out CombatUISpriteAnimator anim)
        {
            var slotGo = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(Image));
            slotGo.transform.SetParent(parent, false);
            var slotRt = (RectTransform)slotGo.transform;
            slotRt.sizeDelta = _slotSize;
            var le = slotGo.GetComponent<LayoutElement>();
            le.preferredWidth = _slotSize.x;
            le.preferredHeight = _slotSize.y;
            frame = slotGo.GetComponent<Image>();
            frame.color = _frameInactive;
            frame.raycastTarget = true;
            var hoverProxy = slotGo.AddComponent<TimelineSlotHoverProxy>();
            hoverProxy.Bind(this, slotIndex);

            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image), typeof(CombatUISpriteAnimator));
            portraitGo.transform.SetParent(slotGo.transform, false);
            var pRt = (RectTransform)portraitGo.transform;
            pRt.anchorMin = Vector2.zero;
            pRt.anchorMax = Vector2.one;
            pRt.offsetMin = new Vector2(_frameBorderPx, _frameBorderPx);
            pRt.offsetMax = new Vector2(-_frameBorderPx, -_frameBorderPx);
            portrait = portraitGo.GetComponent<Image>();
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            anim = portraitGo.GetComponent<CombatUISpriteAnimator>();

            return slotRt;
        }
    }

    /// <summary>
    /// Proxy attache a chaque slot de la timeline. Delegue les events PointerEnter/Exit au
    /// TimelineView qui resoud le combatant correspondant + affiche le tooltip.
    /// </summary>
    internal sealed class TimelineSlotHoverProxy : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private TimelineView _timeline;
        private int _slotIndex;

        public void Bind(TimelineView timeline, int slotIndex)
        {
            _timeline = timeline;
            _slotIndex = slotIndex;
        }

        public void OnPointerEnter(PointerEventData _) => _timeline?.OnSlotHoverEnter(_slotIndex);
        public void OnPointerExit(PointerEventData _) => _timeline?.OnSlotHoverExit();
    }
}
