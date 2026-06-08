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
    /// Skins (31 mai) : si un joueur a equipe un skin combat, la timeline joue les IdleFrames
    /// du SKIN (resolu depuis CosmeticSkinCatalog via le SkinId du RuntimePlayer, comme le
    /// CombatantRenderer / le lobby pre-combat) ; fallback sur les frames de la classe sinon.
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
        [SerializeField] private Vector2 _slotSize = new Vector2(124f, 144f);
        [SerializeField] private float _slotSpacing = 12f;
        [SerializeField] private Color _frameActive = new Color(0.93f, 0.94f, 0.96f, 1f);   // accent clair = actif
        [SerializeField] private Color _frameInactive = new Color(0.16f, 0.165f, 0.185f, 1f); // surface carte
        [SerializeField] private Color _portraitActive = Color.white;
        [SerializeField] private Color _portraitInactive = new Color(0.55f, 0.55f, 0.58f, 1f);
        [SerializeField] private int _frameBorderPx = 4;

        [Tooltip("Patch UI combat 8 juin — hauteur (px) de la bande HP affichée sous chaque portrait " +
                 "de la timeline (remplace les panneaux d'infos haut-gauche/droite). Mets 0 pour ne pas " +
                 "afficher de bande HP.")]
        [SerializeField] private float _hpStripHeight = 30f;

        [Tooltip("Patch UI combat 8 juin (3b) — hauteur (px) des chips de statuts affichées au-dessus " +
                 "de chaque portrait. Mets 0 pour désactiver les chips.")]
        [SerializeField] private float _chipHeight = 28f;

        private CombatUISpriteAnimator _animator0;
        private CombatUISpriteAnimator _animator1;
        // Patch UI combat 8 juin — libellés HP/MaxHP sous chaque portrait (lecture d'un coup d'œil).
        private TMP_Text _hp0;
        private TMP_Text _hp1;
        // Patch UI combat 8 juin (3b) — rangées de chips de statuts (pool réutilisé chaque frame, 0 GC).
        private const int MaxChips = 10;
        private RectTransform _chipRow0;
        private RectTransform _chipRow1;
        private readonly List<StatusChip> _chips0 = new List<StatusChip>(MaxChips);
        private readonly List<StatusChip> _chips1 = new List<StatusChip>(MaxChips);
        private Dictionary<NymoraClass, NymoraClassDefinition> _classByEnum;

        // Skins combat : catalogue charge a la demande (meme chemin Resources que CombatantRenderer).
        private const string SkinCatalogResourcePath = "Cosmetics/CosmeticSkinCatalog";
        private CosmeticSkinCatalog _skinCatalog;
        private bool _skinCatalogLoaded;
        private string _p0SkinId = "";
        private string _p1SkinId = "";

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
            ApplySlot(_slot0Frame, _portrait0, _animator0, p0Class, _p0SkinId, activePlayerIndex == 0);
            ApplySlot(_slot1Frame, _portrait1, _animator1, p1Class, _p1SkinId, activePlayerIndex == 1);
        }

        /// <summary>
        /// Surcharge avec les Combatant structs complets — necessaire pour le tooltip hover
        /// qui affiche HP/Ressource/Statuses/Marque. Appelee par CombatHUDController.OnUpdateView.
        /// Les SkinId (RuntimePlayer, sync Quantum) pilotent le visuel : skin equipe > classe.
        /// </summary>
        public void RefreshWithCombatants(int activePlayerIndex, Combatant p0, bool hasP0, Combatant p1, bool hasP1, int turnNumber, string p0SkinId, string p1SkinId)
        {
            _currentP0 = p0; _hasP0 = hasP0;
            _currentP1 = p1; _hasP1 = hasP1;
            _currentTurnNumber = turnNumber;
            _p0SkinId = p0SkinId ?? "";
            _p1SkinId = p1SkinId ?? "";
            NymoraClass p0Cls = hasP0 ? p0.Class : NymoraClass.None;
            NymoraClass p1Cls = hasP1 ? p1.Class : NymoraClass.None;
            Refresh(activePlayerIndex, p0Cls, p1Cls);

            // Patch UI combat 8 juin — HP/MaxHP sous chaque portrait (remplace les panneaux haut G/D).
            SetHpLabel(_hp0, hasP0, p0, activePlayerIndex == 0);
            SetHpLabel(_hp1, hasP1, p1, activePlayerIndex == 1);

            // Patch UI combat 8 juin (3b) — chips de statuts (malus/bonus) au-dessus de chaque portrait.
            RefreshChips(_chipRow0, _chips0, hasP0, p0);
            RefreshChips(_chipRow1, _chips1, hasP1, p1);
        }

        private void RefreshChips(RectTransform row, List<StatusChip> pool, bool has, in Combatant c)
        {
            if (row == null) return;
            int used = 0;
            if (has)
            {
                // 1) VENIN Necram — champ dédié VeninStacks (PAS dans Statuses[]). C'était l'état manquant.
                if (c.VeninStacks > 0)
                {
                    EmitChip(row, pool, ref used, $"VENx{c.VeninStacks}", StatusIconInfo.Polarity.Malus);
                }

                // 2) MARQUE Nightseer — champ dédié CurrentMark (PAS dans Statuses[]).
                if (c.CurrentMark != MarkKind.None && c.MarkTurnsLeft > 0)
                {
                    EmitChip(row, pool, ref used, "TRAQ", StatusIconInfo.Polarity.Malus);
                }

                // 3) STATUSES temporisés (buffs/debuffs du tableau Statuses[8]).
                for (int i = 0; i < 8; i++)
                {
                    var s = c.Statuses[i];
                    if (s.Kind == StatusKind.None || s.TurnsLeft <= 0) continue;
                    if (!StatusIconInfo.TryGet(s.Kind, s.Magnitude, out string code, out var pol)) continue;
                    EmitChip(row, pool, ref used, code, pol);
                }
            }
            // Désactive les chips du pool non utilisées ce frame.
            for (int i = used; i < pool.Count; i++)
            {
                if (pool[i] != null) pool[i].gameObject.SetActive(false);
            }
        }

        // Émet une chip (code + couleur). No-op si le cap MaxChips est atteint. La description détaillée
        // est dans le tooltip du slot (survol du portrait/chips), pas par chip.
        private void EmitChip(RectTransform row, List<StatusChip> pool, ref int used, string code, StatusIconInfo.Polarity pol)
        {
            if (used >= MaxChips) return;
            var chip = GetChip(row, pool, used);
            chip.gameObject.SetActive(true);
            chip.Label.text = code;
            chip.Bg.color = PolarityColor(pol);
            chip.Layout.preferredWidth = Mathf.Clamp(code.Length * 10f + 16f, 34f, 110f);
            chip.Layout.preferredHeight = _chipHeight;
            used++;
        }

        private StatusChip GetChip(RectTransform row, List<StatusChip> pool, int index)
        {
            while (pool.Count <= index) pool.Add(CreateChip(row));
            return pool[index];
        }

        private StatusChip CreateChip(RectTransform row)
        {
            var go = new GameObject("Chip", typeof(RectTransform), typeof(LayoutElement), typeof(Image));
            go.transform.SetParent(row, false);
            var img = go.GetComponent<Image>();
            CombatUiKit.ApplyRounded(img, 5f);
            img.raycastTarget = true;
            var chip = go.AddComponent<StatusChip>();

            var txtGo = new GameObject("Code", typeof(RectTransform));
            txtGo.transform.SetParent(go.transform, false);
            var trt = (RectTransform)txtGo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var txt = txtGo.AddComponent<TextMeshProUGUI>();
            txt.alignment = TextAlignmentOptions.Center;
            txt.fontSize = 14f;
            txt.fontStyle = FontStyles.Bold;
            txt.color = Color.white;
            txt.raycastTarget = false;
            txt.enableWordWrapping = false;

            chip.Rect = (RectTransform)go.transform;
            chip.Layout = go.GetComponent<LayoutElement>();
            chip.Bg = img;
            chip.Label = txt;
            return chip;
        }

        private static Color PolarityColor(StatusIconInfo.Polarity p)
        {
            switch (p)
            {
                case StatusIconInfo.Polarity.Malus:   return new Color(0.84f, 0.20f, 0.22f, 0.95f); // rouge
                case StatusIconInfo.Polarity.Buff:    return new Color(0.82f, 0.64f, 0.24f, 0.95f); // ambre
                case StatusIconInfo.Polarity.Defense: return new Color(0.25f, 0.48f, 0.82f, 0.95f); // bleu
                default:                              return new Color(0.40f, 0.40f, 0.45f, 0.95f); // gris
            }
        }

        private static void SetHpLabel(TMP_Text label, bool has, in Combatant c, bool isActive)
        {
            if (label == null) return;
            if (!has) { label.text = ""; return; }
            label.text = $"{c.HP} / {c.MaxHP}";
            label.color = HpColor(c.HP, c.MaxHP, isActive);
        }

        // Vert > 50%, ambre 25-50%, rouge < 25%, gris si mort. Légèrement estompé hors tour actif.
        // (float toléré : code View, pas la simulation Quantum.)
        private static Color HpColor(int hp, int maxHp, bool isActive)
        {
            if (maxHp <= 0 || hp <= 0) return new Color(0.5f, 0.5f, 0.5f, 1f);
            float ratio = hp / (float)maxHp;
            Color col = ratio > 0.5f ? new Color(0.55f, 0.85f, 0.55f, 1f)
                      : ratio > 0.25f ? new Color(1f, 0.82f, 0.38f, 1f)
                      : new Color(1f, 0.40f, 0.40f, 1f);
            if (!isActive) col.a = 0.72f;
            return col;
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

        private void ApplySlot(Image frame, Image portrait, CombatUISpriteAnimator anim, NymoraClass cls, string skinId, bool isActive)
        {
            if (frame != null) frame.color = isActive ? _frameActive : _frameInactive;
            if (portrait == null) return;

            NymoraClassDefinition def = null;
            _classByEnum?.TryGetValue(cls, out def);

            // Skin equipe pour CETTE classe : ses IdleFrames priment sur celles de la classe.
            var skin = ResolveSkin(skinId, cls);
            Sprite[] idleFrames = (skin != null && skin.IdleFrames != null && skin.IdleFrames.Length > 0)
                ? skin.IdleFrames
                : (def != null ? def.IdleFrames : null);
            float idleFps = skin != null && skin.IdleFrames != null && skin.IdleFrames.Length > 0
                ? skin.IdleFps
                : (def != null ? def.IdleFps : 8f);

            if (idleFrames != null && idleFrames.Length > 0)
            {
                portrait.enabled = true;
                portrait.color = isActive ? _portraitActive : _portraitInactive;
                if (anim != null) anim.Play(portrait, idleFrames, idleFps);
            }
            else
            {
                // Fallback : portrait statique si pas de frames idle dispo
                portrait.sprite = def != null ? def.PortraitSprite : null;
                portrait.enabled = portrait.sprite != null;
                portrait.color = isActive ? _portraitActive : _portraitInactive;
            }
        }

        /// <summary>
        /// Resout le skin combat equipe (CosmeticId == skinId) avec le meme garde-fou class-lock
        /// que CombatantRenderer.ResolveSkinFor : un skin ne s'applique qu'a sa classe. Retourne
        /// null si pas de skin, catalogue absent, ou classe non concordante.
        /// </summary>
        private CosmeticSkinDefinition ResolveSkin(string skinId, NymoraClass cls)
        {
            if (string.IsNullOrEmpty(skinId)) return null;
            if (!_skinCatalogLoaded)
            {
                _skinCatalog = Resources.Load<CosmeticSkinCatalog>(SkinCatalogResourcePath);
                _skinCatalogLoaded = true;
            }
            if (_skinCatalog == null) return null;

            var skin = _skinCatalog.Resolve(skinId);
            if (skin == null) return null;

            // ClassId (Core.Enums) vs cls (Quantum) : comparaison par nom (memes libelles).
            if (skin.ClassId != Nymora.Core.Enums.NymoraClass.None
                && skin.ClassId.ToString() != cls.ToString())
                return null;

            return skin;
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

            _slot0Root = BuildSlot(container.transform, "Slot_P0", 0, out _slot0Frame, out _portrait0, out _animator0, out _hp0);
            _slot1Root = BuildSlot(container.transform, "Slot_P1", 1, out _slot1Frame, out _portrait1, out _animator1, out _hp1);

            // Patch UI combat 8 juin (3b) — rangée de chips de statuts au-dessus de chaque portrait.
            if (_chipHeight > 0f)
            {
                _chipRow0 = BuildChipRow(_slot0Root, "Chips_P0");
                _chipRow1 = BuildChipRow(_slot1Root, "Chips_P1");
            }
        }

        // Rangée horizontale (au-dessus du slot, débordant vers le haut/droite) qui héberge le pool
        // de chips de statuts. ContentSizeFitter -> se dimensionne à son contenu.
        private RectTransform BuildChipRow(RectTransform slotRoot, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            go.transform.SetParent(slotRoot, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);   // coin haut-gauche du slot
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0f);        // grandit vers le haut + la droite
            rt.anchoredPosition = new Vector2(0f, 4f);

            var hlg = go.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 3f;
            hlg.childAlignment = TextAnchor.LowerLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var fitter = go.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rt;
        }

        private RectTransform BuildSlot(Transform parent, string name, int slotIndex, out Image frame, out Image portrait, out CombatUISpriteAnimator anim, out TMP_Text hpLabel)
        {
            // Patch UI combat 8 juin — le slot est plus haut : portrait en haut, bande HP en bas.
            float totalHeight = _slotSize.y + Mathf.Max(0f, _hpStripHeight);

            var slotGo = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(Image));
            slotGo.transform.SetParent(parent, false);
            var slotRt = (RectTransform)slotGo.transform;
            slotRt.sizeDelta = new Vector2(_slotSize.x, totalHeight);
            var le = slotGo.GetComponent<LayoutElement>();
            le.preferredWidth = _slotSize.x;
            le.preferredHeight = totalHeight;
            frame = slotGo.GetComponent<Image>();
            frame.color = _frameInactive;
            frame.raycastTarget = true;
            var hoverProxy = slotGo.AddComponent<TimelineSlotHoverProxy>();
            hoverProxy.Bind(this, slotIndex);

            // Portrait : occupe la partie HAUTE du slot, laisse une bande en bas pour les HP.
            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image), typeof(CombatUISpriteAnimator));
            portraitGo.transform.SetParent(slotGo.transform, false);
            var pRt = (RectTransform)portraitGo.transform;
            pRt.anchorMin = Vector2.zero;
            pRt.anchorMax = Vector2.one;
            pRt.offsetMin = new Vector2(_frameBorderPx, _frameBorderPx + Mathf.Max(0f, _hpStripHeight));
            pRt.offsetMax = new Vector2(-_frameBorderPx, -_frameBorderPx);
            portrait = portraitGo.GetComponent<Image>();
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            anim = portraitGo.GetComponent<CombatUISpriteAnimator>();

            hpLabel = null;
            if (_hpStripHeight > 0f)
            {
                // Bande HP : fond sombre arrondi (lisible sur cadre clair = actif) + chiffre centré.
                var hpBgGo = new GameObject("HpStrip", typeof(RectTransform), typeof(Image));
                hpBgGo.transform.SetParent(slotGo.transform, false);
                var hpBgRt = (RectTransform)hpBgGo.transform;
                hpBgRt.anchorMin = new Vector2(0f, 0f);
                hpBgRt.anchorMax = new Vector2(1f, 0f);
                hpBgRt.pivot = new Vector2(0.5f, 0f);
                hpBgRt.offsetMin = new Vector2(_frameBorderPx, _frameBorderPx);
                hpBgRt.offsetMax = new Vector2(-_frameBorderPx, _frameBorderPx + _hpStripHeight);
                var hpBg = hpBgGo.GetComponent<Image>();
                CombatUiKit.ApplyRounded(hpBg, 6f);
                hpBg.color = new Color(0.06f, 0.06f, 0.08f, 0.85f);
                hpBg.raycastTarget = false;

                var hpTextGo = new GameObject("HpValue", typeof(RectTransform));
                hpTextGo.transform.SetParent(hpBgGo.transform, false);
                var hpTRt = (RectTransform)hpTextGo.transform;
                hpTRt.anchorMin = Vector2.zero; hpTRt.anchorMax = Vector2.one;
                hpTRt.offsetMin = Vector2.zero; hpTRt.offsetMax = Vector2.zero;
                hpLabel = hpTextGo.AddComponent<TextMeshProUGUI>();
                hpLabel.alignment = TextAlignmentOptions.Center;
                hpLabel.fontSize = 17f;
                hpLabel.fontStyle = FontStyles.Bold;
                hpLabel.color = Color.white;
                hpLabel.raycastTarget = false;
                hpLabel.enableWordWrapping = false;
            }

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

    /// <summary>
    /// Patch UI combat 8 juin (3b) — Chip de statut au-dessus d'un portrait timeline. Simple holder
    /// de refs UI réutilisé via pool. Pas de handler de survol propre : la chip est enfant du slot,
    /// donc survoler une chip déclenche le tooltip du SLOT (méga-complet, TimelineSlotTooltipBuilder)
    /// par bubbling. Ça évite le conflit "chip vs slot" et le flicker. raycastTarget reste true pour
    /// que le survol des chips remonte bien au proxy du slot.
    /// </summary>
    internal sealed class StatusChip : MonoBehaviour
    {
        public RectTransform Rect;
        public LayoutElement Layout;
        public Image Bg;
        public TMP_Text Label;
    }
}
