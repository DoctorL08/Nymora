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
    /// Timeline bas-droite : cadre avec les sprites IDLE ANIMES des combatants cote a cote.
    /// Le combatant actif est mis en evidence (cadre clair + alpha plein) ; les autres sont
    /// teintes par EQUIPE (bleu = equipe 0, rouge = equipe 1) et legerement grises.
    ///
    /// 5.5d (2v2/3v3, 9 juin) — refonte N slots. Anciennement 2 slots hardcodes P0/P1 ;
    /// desormais un slot par PlayerIndex (jusqu'a TurnConstants.MaxPlayers=6), construits a la
    /// demande selon CombatState.PlayerCount. INVARIANT 1v1 : 2 slots, rendu identique (le slot
    /// actif passe en cadre clair ; en 1v1 chaque joueur EST sa propre equipe -> couleur d'equipe
    /// = bleu/rouge comme avant via TeamId == PlayerIndex).
    ///
    /// Skins (31 mai) : si un joueur a equipe un skin combat, la timeline joue les IdleFrames du
    /// SKIN (resolu depuis CosmeticSkinCatalog via le SkinId du RuntimePlayer) ; fallback classe.
    ///
    /// Auto-init complet : le TimelineView spawn sa propre structure Container + Slots a la
    /// premiere Refresh. Lorenzo n'a qu'a poser le component sur un GameObject UI bas-droite.
    /// </summary>
    public class TimelineView : MonoBehaviour
    {
        [Header("Style (re-skin DA hub : monochrome)")]
        [SerializeField] private Vector2 _slotSize = new Vector2(124f, 144f);
        [SerializeField] private float _slotSpacing = 12f;
        [SerializeField] private Color _frameActive = new Color(0.93f, 0.94f, 0.96f, 1f);   // accent clair = actif
        [SerializeField] private Color _frameInactive = new Color(0.16f, 0.165f, 0.185f, 1f); // surface carte (mort / vide)
        [SerializeField] private Color _portraitActive = Color.white;
        [SerializeField] private Color _portraitInactive = new Color(0.55f, 0.55f, 0.58f, 1f);
        [SerializeField] private int _frameBorderPx = 4;

        [Tooltip("5.5d — couleur de cadre des combatants de l'EQUIPE 0 (hors tour actif). Le combatant " +
                 "actif passe en _frameActive (clair) quelle que soit son equipe.")]
        [SerializeField] private Color _frameTeam0 = new Color(0.30f, 0.55f, 0.95f, 1f); // bleu
        [Tooltip("5.5d — couleur de cadre des combatants de l'EQUIPE 1 (hors tour actif).")]
        [SerializeField] private Color _frameTeam1 = new Color(0.92f, 0.34f, 0.34f, 1f); // rouge

        [Tooltip("Patch UI combat 8 juin — hauteur (px) de la bande HP affichée sous chaque portrait " +
                 "de la timeline. Mets 0 pour ne pas afficher de bande HP.")]
        [SerializeField] private float _hpStripHeight = 30f;

        [Tooltip("Patch UI combat 8 juin (3b) — hauteur (px) des chips de statuts affichées au-dessus " +
                 "de chaque portrait. Mets 0 pour désactiver les chips.")]
        [SerializeField] private float _chipHeight = 28f;

        [Header("B8 (22 mai) — Position")]
        [Tooltip("Remonte la timeline de N px pour loger le bouton Abandonner en dessous. " +
                 "Applique une fois au chargement. Mets 0 pour ne pas bouger.")]
        [SerializeField] private float _verticalNudge = 80f;

        private const int MaxSlots = 6;  // 3v3
        private const int MaxChips = 10;

        // 5.5d — un slot par joueur (PlayerIndex). Construits a la demande (EnsureSlots).
        private sealed class TeamSlot
        {
            public RectTransform Root;
            public Image Frame;
            public Image Portrait;
            public CombatUISpriteAnimator Anim;
            public TMP_Text Hp;
            public RectTransform ChipRow;
            public readonly List<StatusChip> Chips = new List<StatusChip>(MaxChips);
            // Etat courant cache pour le tooltip hover.
            public Combatant Combatant;
            public bool Has;
            public string SkinId = "";
        }

        private readonly List<TeamSlot> _slots = new List<TeamSlot>(MaxSlots);
        private RectTransform _container;
        private int _currentTurnNumber;

        private Dictionary<NymoraClass, NymoraClassDefinition> _classByEnum;

        // Skins combat : catalogue charge a la demande (meme chemin Resources que CombatantRenderer).
        private const string SkinCatalogResourcePath = "Cosmetics/CosmeticSkinCatalog";
        private CosmeticSkinCatalog _skinCatalog;
        private bool _skinCatalogLoaded;

        private void Awake()
        {
            // B8 (22 mai) — remonte la timeline d'un cran pour loger le bouton Abandonner sous elle.
            var selfRt = transform as RectTransform;
            if (selfRt != null && !Mathf.Approximately(_verticalNudge, 0f))
            {
                selfRt.anchoredPosition += new Vector2(0f, _verticalNudge);
            }

            // Cache l'ancien label legacy "P0 | P1" hardcode dans la scene s'il existe (fix 19 mai).
            // Lu AVANT de construire nos slots (qui ajoutent leurs propres TMP_Text).
            var legacyLabel = GetComponentInChildren<TMP_Text>(true);
            if (legacyLabel != null) legacyLabel.gameObject.SetActive(false);

            EnsureContainer();
        }

        /// <summary>
        /// Setup les definitions de classes (drag par CombatHUDController). Cree un lookup
        /// dict pour fast access aux IdleFrames + IdleFps par classe.
        /// </summary>
        public void Init(NymoraClassDefinition[] classDefinitions)
        {
            _classByEnum = new Dictionary<NymoraClass, NymoraClassDefinition>(6);
            if (classDefinitions == null) return;
            foreach (var def in classDefinitions)
            {
                if (def == null) continue;
                var key = (NymoraClass)(byte)def.ClassId; // cast Core.Enums -> Quantum.NymoraClass (memes valeurs)
                _classByEnum[key] = def;
            }
        }

        /// <summary>
        /// 5.5d — Refresh N joueurs. Les tableaux de DONNEES (byPlayer/present/skinByPlayer) sont
        /// indexes PAR PlayerIndex. `order` donne l'ORDRE D'AFFICHAGE = la séquence de jeu du round
        /// (TurnOrder : A0, B0, A1, B1...) : le slot d'écran `k` montre le joueur `order[k]`, donc le
        /// highlight du joueur actif progresse de gauche à droite. `present[i]` = un Combatant existe.
        /// Appelee chaque frame par CombatHUDController.OnUpdateView.
        /// </summary>
        public void RefreshTeam(int activePlayerIndex, Combatant[] byPlayer, bool[] present, string[] skinByPlayer, int[] order, int playerCount, int turnNumber)
        {
            _currentTurnNumber = turnNumber;
            int count = Mathf.Clamp(playerCount, 0, MaxSlots);
            EnsureSlots(count);

            for (int k = 0; k < _slots.Count; k++)
            {
                var slot = _slots[k];
                bool show = k < count;
                if (slot.Root != null && slot.Root.gameObject.activeSelf != show)
                    slot.Root.gameObject.SetActive(show);
                if (!show) continue;

                // Le slot d'écran k affiche le joueur à la position k de l'ordre de jeu.
                int pi = (order != null && k < order.Length) ? order[k] : k;
                if (byPlayer == null || pi < 0 || pi >= byPlayer.Length) pi = k;

                bool has = present != null && pi < present.Length && present[pi];
                Combatant c = (byPlayer != null && pi < byPlayer.Length) ? byPlayer[pi] : default;
                string skinId = (skinByPlayer != null && pi < skinByPlayer.Length) ? (skinByPlayer[pi] ?? "") : "";
                slot.Combatant = c; slot.Has = has; slot.SkinId = skinId;

                bool isActive = pi == activePlayerIndex;
                bool dead = has && c.HP <= 0;
                NymoraClass cls = has ? c.Class : NymoraClass.None;

                // Cadre : actif (vivant) = clair ; mort/vide = surface grise ; sinon couleur d'equipe.
                Color frameCol = (!has || dead) ? _frameInactive
                               : isActive ? _frameActive
                               : (c.TeamId == 0 ? _frameTeam0 : _frameTeam1);

                ApplySlot(slot.Frame, slot.Portrait, slot.Anim, cls, skinId, isActive && !dead, frameCol);
                SetHpLabel(slot.Hp, has, c, isActive);
                RefreshChips(slot.ChipRow, slot.Chips, has, c);
            }
        }

        private void RefreshChips(RectTransform row, List<StatusChip> pool, bool has, in Combatant c)
        {
            if (row == null) return;
            int used = 0;
            if (has)
            {
                // 1) VENIN Necram — champ dédié VeninStacks (PAS dans Statuses[]).
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
            used++;
        }

        private StatusChip GetChip(RectTransform row, List<StatusChip> pool, int index)
        {
            while (pool.Count <= index) pool.Add(CreateChip(row));
            return pool[index];
        }

        private StatusChip CreateChip(RectTransform row)
        {
            var go = new GameObject("Chip", typeof(RectTransform), typeof(Image));
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
            if (slotIndex < 0 || slotIndex >= _slots.Count) return;
            var slot = _slots[slotIndex];
            if (!slot.Has) return;
            string text = TimelineSlotTooltipBuilder.Build(slot.Combatant, _currentTurnNumber);
            PassiveTooltipView.Instance.ShowAbove(text, slot.Root);
        }

        internal void OnSlotHoverExit()
        {
            PassiveTooltipView.Instance.Hide();
        }

        private void ApplySlot(Image frame, Image portrait, CombatUISpriteAnimator anim, NymoraClass cls, string skinId, bool isActive, Color frameColor)
        {
            if (frame != null) frame.color = frameColor;
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

        private void EnsureContainer()
        {
            if (_container != null) return;

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
            _container = containerRt;
        }

        // 5.5d — construit les slots manquants jusqu'a `count` (lazy, reutilises ensuite).
        private void EnsureSlots(int count)
        {
            EnsureContainer();
            while (_slots.Count < count)
            {
                int idx = _slots.Count;
                var slot = new TeamSlot();
                slot.Root = BuildSlot(_container, $"Slot_P{idx}", idx, out slot.Frame, out slot.Portrait, out slot.Anim, out slot.Hp);
                if (_chipHeight > 0f) slot.ChipRow = BuildChipRow(slot.Root, $"Chips_P{idx}");
                // Re-skin DA hub : cadre arrondi (sprite généré au runtime). La couleur est repeinte
                // chaque frame par ApplySlot ; la forme arrondie persiste.
                if (slot.Frame != null) CombatUiKit.ApplyRounded(slot.Frame, 10f);
                _slots.Add(slot);
            }
        }

        // Grille de chips au-dessus du slot : 2 chips par étage, étages empilés VERS LE HAUT
        // (1ère paire en bas près du portrait, le 3e+ ouvre un nouvel étage au-dessus). startCorner
        // LowerLeft + pivot bas -> la grille grandit vers le haut. ContentSizeFitter la dimensionne.
        private RectTransform BuildChipRow(RectTransform slotRoot, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            go.transform.SetParent(slotRoot, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);   // coin haut-gauche du slot
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0f);        // grandit vers le haut
            rt.anchoredPosition = new Vector2(0f, 4f);

            var grid = go.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(56f, _chipHeight);
            grid.spacing = new Vector2(4f, 4f);
            grid.startCorner = GridLayoutGroup.Corner.LowerLeft; // 1ère chip en bas, étages au-dessus
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.LowerLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2; // 2 chips par étage

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
        public Image Bg;
        public TMP_Text Label;
    }
}
