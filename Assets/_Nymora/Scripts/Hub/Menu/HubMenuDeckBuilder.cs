using System.Collections.Generic;
using Nymora.Core.Data;
using Nymora.Core.Enums;
using Nymora.Core.ScriptableObjects;
using Nymora.Network.Backend;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nymora.Hub.Menu
{
    /// <summary>
    /// M3b — Deck builder complet au style du nouveau menu (colonne droite de l'onglet Classe).
    ///
    /// Gauche : liste des decks + "Nouveau deck". Droite : nom du deck (input) + Save/Supprimer,
    /// 6 slots équipés (clic = retirer), onglets catégories (Offensifs/Tactiques/Survie) + grille
    /// de sorts (clic = ajouter au prochain slot vide).
    ///
    /// Réutilise les règles Bible (6 sorts uniques, 5 decks max, signature auto) et les appels
    /// backend de HubDeckBuilderPanel. Après save/select, synchronise le deck actif du combat
    /// via HubDeckBuilderPanel.SetActiveDeckAsync.
    ///
    /// Classe plain (appels backend async/await).
    /// </summary>
    public sealed class HubMenuDeckBuilder
    {
        private readonly HubMenuTheme _t;
        private readonly HubMenuUIFactory _f;
        private readonly NymoraApiClient _api;
        private readonly SpellCatalog _catalog;

        private string _classId;
        private NymoraClassDefinition _classDef;
        private readonly List<DeckDto> _decks = new List<DeckDto>();
        private readonly string[] _slots = new string[6]; // null = vide
        private string _editingDeckId;                    // null = nouveau
        private SpellCategory _activeCategory = SpellCategory.Offensive;
        private bool _busy;

        // UI refs
        private RectTransform _decksContent;
        private RectTransform _slotsRow;
        private RectTransform _catTabsRow;
        private RectTransform _spellGridContent;
        private TMP_InputField _nameInput;
        private TextMeshProUGUI _status;
        private TextMeshProUGUI _saveLabel;
        private GameObject _tooltipGo;
        private TextMeshProUGUI _tooltipText;
        private readonly List<GameObject> _deckItems = new List<GameObject>();
        private readonly List<GameObject> _slotItems = new List<GameObject>();
        private readonly List<(SpellCategory cat, Image bg, TextMeshProUGUI label)> _catTabs = new List<(SpellCategory, Image, TextMeshProUGUI)>();
        private readonly List<GameObject> _spellCells = new List<GameObject>();

        public HubMenuDeckBuilder(HubMenuTheme t, HubMenuUIFactory f, NymoraApiClient api, SpellCatalog catalog)
        {
            _t = t; _f = f; _api = api; _catalog = catalog;
        }

        public void Build(RectTransform parent, string classId, NymoraClassDefinition classDef)
        {
            _classId = classId;
            _classDef = classDef;

            BuildSidebar(parent);
            BuildMain(parent);
            BuildTooltip(parent);

            RenderSlots();
            RenderCatTabs();
            RenderSpellGrid();
            LoadAsync();
        }

        // ===================== Sidebar (liste decks + Nouveau) =====================

        private void BuildSidebar(RectTransform parent)
        {
            var sidebar = _f.MakeRect("DecksSidebar", parent);
            sidebar.anchorMin = new Vector2(0f, 0f); sidebar.anchorMax = new Vector2(0f, 1f); sidebar.pivot = new Vector2(0f, 0.5f);
            sidebar.sizeDelta = new Vector2(210f, 0f); sidebar.anchoredPosition = new Vector2(0f, 0f);

            var title = _f.MakeText("Title", sidebar, "Mes decks", _t.FontSizeHeader, _t.TextPrimary, _t.FontBold, TextAlignmentOptions.MidlineLeft);
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.sizeDelta = new Vector2(-8f, 34f); trt.anchoredPosition = new Vector2(0f, -4f);

            // Scroll list (laisse 56px en bas pour le bouton Nouveau)
            var viewport = _f.MakeRect("Scroll", sidebar);
            viewport.anchorMin = new Vector2(0f, 0f); viewport.anchorMax = new Vector2(1f, 1f);
            viewport.offsetMin = new Vector2(0f, 56f); viewport.offsetMax = new Vector2(0f, -44f);
            var vpImg = viewport.gameObject.AddComponent<Image>(); vpImg.color = new Color(1f, 1f, 1f, 0.03f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var sr = viewport.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 24f; sr.viewport = viewport;
            var content = _f.MakeRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f; vlg.padding = new RectOffset(6, 6, 6, 6); vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize; fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            sr.content = content;
            _decksContent = content;

            var nouveau = _f.MakeButton(sidebar, "+ Nouveau deck", false, out _);
            var nrt = (RectTransform)nouveau.transform;
            nrt.anchorMin = new Vector2(0f, 0f); nrt.anchorMax = new Vector2(1f, 0f); nrt.pivot = new Vector2(0.5f, 0f);
            nrt.sizeDelta = new Vector2(0f, 46f); nrt.anchoredPosition = new Vector2(0f, 4f);
            nouveau.onClick.AddListener(OnNewDeck);
        }

        // ===================== Main (édition) =====================

        private void BuildMain(RectTransform parent)
        {
            var main = _f.MakeRect("DeckMain", parent);
            main.anchorMin = new Vector2(0f, 0f); main.anchorMax = new Vector2(1f, 1f);
            main.offsetMin = new Vector2(234f, 0f); main.offsetMax = new Vector2(0f, 0f);

            // Header : nom (input) + Supprimer / Save (droite)
            _nameInput = MakeInput(main, "Nom du deck");
            _nameInput.characterLimit = 19; // borne le nom pour qu'il tienne sur une ligne dans la liste
            var nirt = _nameInput.GetComponent<RectTransform>();
            nirt.anchorMin = new Vector2(0f, 1f); nirt.anchorMax = new Vector2(0f, 1f); nirt.pivot = new Vector2(0f, 1f);
            nirt.sizeDelta = new Vector2(320f, 44f); nirt.anchoredPosition = new Vector2(0f, -4f);

            var del = _f.MakeButton(main, "Supprimer", false, out _);
            var drt = (RectTransform)del.transform;
            drt.anchorMin = new Vector2(1f, 1f); drt.anchorMax = new Vector2(1f, 1f); drt.pivot = new Vector2(1f, 1f);
            drt.sizeDelta = new Vector2(140f, 44f); drt.anchoredPosition = new Vector2(0f, -4f);
            del.onClick.AddListener(OnDelete);

            var save = _f.MakeButton(main, "Save", true, out _saveLabel);
            var srt = (RectTransform)save.transform;
            srt.anchorMin = new Vector2(1f, 1f); srt.anchorMax = new Vector2(1f, 1f); srt.pivot = new Vector2(1f, 1f);
            srt.sizeDelta = new Vector2(150f, 44f); srt.anchoredPosition = new Vector2(-152f, -4f);
            save.onClick.AddListener(OnSave);

            // Slots
            _slotsRow = _f.MakeRect("Slots", main);
            _slotsRow.anchorMin = new Vector2(0f, 1f); _slotsRow.anchorMax = new Vector2(1f, 1f); _slotsRow.pivot = new Vector2(0.5f, 1f);
            _slotsRow.sizeDelta = new Vector2(-8f, 124f); _slotsRow.anchoredPosition = new Vector2(0f, -58f);
            var shlg = _slotsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            shlg.spacing = 10f; shlg.childAlignment = TextAnchor.MiddleCenter;
            shlg.childControlWidth = true; shlg.childControlHeight = true;
            shlg.childForceExpandWidth = false; shlg.childForceExpandHeight = false;

            _status = _f.MakeText("Status", main, "", _t.FontSizeSmall, _t.TextMuted, _t.Font, TextAlignmentOptions.Center);
            var strt = _status.rectTransform;
            strt.anchorMin = new Vector2(0f, 1f); strt.anchorMax = new Vector2(1f, 1f); strt.pivot = new Vector2(0.5f, 1f);
            strt.sizeDelta = new Vector2(-8f, 22f); strt.anchoredPosition = new Vector2(0f, -186f);

            // Onglets catégories
            _catTabsRow = _f.MakeRect("CatTabs", main);
            _catTabsRow.anchorMin = new Vector2(0f, 1f); _catTabsRow.anchorMax = new Vector2(1f, 1f); _catTabsRow.pivot = new Vector2(0.5f, 1f);
            _catTabsRow.sizeDelta = new Vector2(-8f, 42f); _catTabsRow.anchoredPosition = new Vector2(0f, -214f);
            var chlg = _catTabsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            chlg.spacing = 8f; chlg.childAlignment = TextAnchor.MiddleCenter;
            chlg.childControlWidth = true; chlg.childControlHeight = true;
            chlg.childForceExpandWidth = false; chlg.childForceExpandHeight = false;

            // Rangée de sorts : les 5 sorts de la catégorie active sur UNE ligne (centrée)
            _spellGridContent = _f.MakeRect("SpellRow", main);
            _spellGridContent.anchorMin = new Vector2(0f, 1f); _spellGridContent.anchorMax = new Vector2(1f, 1f); _spellGridContent.pivot = new Vector2(0.5f, 1f);
            _spellGridContent.sizeDelta = new Vector2(-8f, 200f); _spellGridContent.anchoredPosition = new Vector2(0f, -262f);
            var grow = _spellGridContent.gameObject.AddComponent<HorizontalLayoutGroup>();
            grow.spacing = 10f; grow.childAlignment = TextAnchor.MiddleCenter;
            grow.childControlWidth = true; grow.childControlHeight = true;
            grow.childForceExpandWidth = false; grow.childForceExpandHeight = false;

            // Section infos classe : Passif + Signature (sous la rangée de sorts)
            BuildClassInfo(main);
        }

        private void BuildClassInfo(RectTransform main)
        {
            // Remplit toute la zone sous la rangée de sorts jusqu'au bas du panneau (tout rentre).
            var row = _f.MakeRect("ClassInfo", main);
            row.anchorMin = new Vector2(0f, 0f); row.anchorMax = new Vector2(1f, 1f);
            row.offsetMin = new Vector2(0f, 8f); row.offsetMax = new Vector2(-8f, -472f);
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12f; hlg.childAlignment = TextAnchor.UpperLeft;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;

            // Passif : uniquement bonus / paliers / obtention de ressource (pas de lore).
            string passifTitle = _classDef != null ? $"Passif : {_classDef.PassiveName}" : "Passif";
            MakeInfoBox(row, passifTitle, PhasesText(_classDef));

            var sig = FindSignature();
            string sigTitle = _classDef != null ? $"Signature : {_classDef.SignatureName}" : "Signature";
            string sigBody;
            if (sig != null)
                sigBody = $"<color=#cce>{sig.ActionPointCost} PA · portée {sig.MinRange}-{sig.MaxRange}</color>\n" +
                          (!string.IsNullOrEmpty(sig.Description) ? sig.Description : (_classDef != null ? _classDef.SignatureDescription : ""));
            else
                sigBody = (_classDef != null && !string.IsNullOrEmpty(_classDef.SignatureDescription)) ? _classDef.SignatureDescription : "—";
            MakeInfoBox(row, sigTitle, sigBody);
        }

        // Encart : titre fixe + corps scrollable. Remplit la zone allouée (rien ne déborde).
        private void MakeInfoBox(RectTransform parent, string title, string body)
        {
            var box = _f.MakePanel(parent, _t.CardBg);
            var t = _f.MakeText("Title", box.rectTransform, title, _t.FontSizeBody, _t.TextPrimary, _t.FontBold, TextAlignmentOptions.TopLeft);
            t.raycastTarget = false; t.enableWordWrapping = false;
            var trt = t.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.sizeDelta = new Vector2(-24f, 26f); trt.anchoredPosition = new Vector2(0f, -10f);

            var vp = _f.MakeRect("BodyVP", box.rectTransform);
            vp.anchorMin = new Vector2(0f, 0f); vp.anchorMax = new Vector2(1f, 1f);
            vp.offsetMin = new Vector2(14f, 12f); vp.offsetMax = new Vector2(-14f, -42f);
            vp.gameObject.AddComponent<RectMask2D>();
            var sr = vp.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 20f; sr.viewport = vp;

            var b = _f.MakeText("Body", vp, body, 14f, _t.TextSecondary, _t.Font, TextAlignmentOptions.TopLeft);
            b.raycastTarget = false; b.enableWordWrapping = true;
            var brt = b.rectTransform;
            brt.anchorMin = new Vector2(0f, 1f); brt.anchorMax = new Vector2(1f, 1f); brt.pivot = new Vector2(0.5f, 1f);
            brt.sizeDelta = Vector2.zero; brt.anchoredPosition = Vector2.zero;
            b.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = brt;
        }

        // Bonus / paliers / génération de ressource par classe (Bible V7.1). Repris du Class Selector.
        private static string PhasesText(NymoraClassDefinition def)
        {
            if (def == null) return "—";
            string body, generation;
            switch (def.ClassId)
            {
                case NymoraClass.Soulrender:
                    generation = "<color=#ffd060><b>Génération HG :</b></color>  <b>+1 HG par sort qui inflige des dégâts</b> (max 1/sort).  Bonus +1 si la cible était <i>Marquée de Carnage</i>.  Cap 5.";
                    body = "<color=#ffe2a0><b>Stage 0</b></color> — <b>0-1 HG</b> : peau normale\n" +
                           "  <i>Bonus : aucun (accumulation passive via dégâts subis)</i>\n\n" +
                           "<color=#ffb060><b>Stage 1</b></color> — <b>2-4 HG</b> : aura rouge progressive\n" +
                           "  <i>Bonus : aucun (HG dépensable sur sorts à coût HG)</i>\n\n" +
                           "<color=#ff6060><b>Stage 2</b></color> — <b>5 HG (cap)</b> : fissures écarlates\n" +
                           "  <i><color=#ffd060>Bonus : <b>signature Âme Lacérée prête</b> (320 dgts + heal 50% dgts passés)</color></i>";
                    break;
                case NymoraClass.Colossar:
                    generation = "<color=#a0d0ff><b>Génération FD :</b></color>  <b>+1 FD par obstacle spawné</b> (pilier ou mur).  Le compteur d'obstacles actifs déclenche aussi le passif <i>Densité Inerte</i> (réduction dgts).  Cap 3.";
                    body = "<color=#a0c8ff><b>Stage 0</b></color> — <b>0 obstacle actif</b> : posture standard\n" +
                           "  <i>Bonus : aucune réduction de dégâts</i>\n\n" +
                           "<color=#80b0ff><b>Stage 1</b></color> — <b>1-2 obstacles</b> : densité partielle\n" +
                           "  <i><color=#a0d0ff>Bonus : <b>-8% à -16% dégâts subis</b> + +20 dmg sorts portée 1-2 si adjacent à un obstacle</color></i>\n\n" +
                           "<color=#6090ff><b>Stage 2</b></color> — <b>3 obstacles (cap)</b> : densité maximale\n" +
                           "  <i><color=#a0d0ff>Bonus : <b>-24% dégâts subis</b> (cap) + +30 HP à chaque destruction de pilier + signature prête</color></i>";
                    break;
                case NymoraClass.Ghostra:
                    generation = "<color=#d0b0ff><b>Génération RM :</b></color>  <b>Resource = nombre de leurres actifs</b> sur le terrain (synchro auto avec les sorts Réplique).  Pose / perte / expiration de leurre met à jour le compteur en temps réel.  Cap 3.";
                    body = "<color=#c0a0ff><b>Stage 0</b></color> — <b>Angle 1 (0 leurre)</b> : posture neutre\n" +
                           "  <i>Bonus : aucun bonus dorsal (sauf via Marque de l'Ombre)</i>\n\n" +
                           "<color=#a080ff><b>Stage 1</b></color> — <b>Angle 2 (1-2 leurres)</b> : aura spectrale\n" +
                           "  <i><color=#d0b0ff>Bonus : <b>+50 dégâts dorsaux</b> + applique <b>Plaie Ouverte</b> auto sur dorsal (40/tour × 2t)</color></i>\n\n" +
                           "<color=#8060ff><b>Stage 2</b></color> — <b>Angle 3 (3 leurres)</b> : forme finale\n" +
                           "  <i><color=#d0b0ff>Bonus : <b>+80 dégâts dorsaux</b> + Plaie Ouverte auto + signature Exécution Spectrale prête</color></i>";
                    break;
                case NymoraClass.Necram:
                    generation = $"<color=#b0e090><b>Génération PT :</b></color>  <b>+1 PT par marque de venin/peste appliquée</b> (cap +2/tour via marques).  <b>+1 PT par tick global</b> de marques sur la map.  Cap {def.ResourceCap}.";
                    body = $"<color=#a0e090><b>Stage 0</b></color> — ressource basse : posture standard\n" +
                           $"  <i>Bonus : marques de venin/peste appliquées par les sorts (DoT empilable)</i>\n\n" +
                           $"<color=#80c070><b>Stage 1</b></color> — ressource moyenne : aura putride\n" +
                           $"  <i><color=#b0e090>Bonus : Floraison augmente le tick des marques (DoT plus violent)</color></i>\n\n" +
                           $"<color=#60a050><b>Stage 2</b></color> — ressource au cap ({def.ResourceCap} {def.ResourceKind})\n" +
                           $"  <i><color=#b0e090>Bonus : <b>signature Virus Fatal prête</b> (déclenche ×3 tous les ticks de marques en un coup)</color></i>";
                    break;
                case NymoraClass.Nightseer:
                    generation = "<color=#c0b0d0><b>Génération PR :</b></color>  <b>+1 PR par round SANS dégâts subis</b>.  <b>-1 PR par round avec dégâts subis</b> (plancher 0).  Récompense la furtivité totale.  Cap 4.";
                    body = "<color=#9090a0><b>Pas de phases visuelles</b></color>\n\n" +
                           "Le Nightseer reste discret. Sa puissance se mesure aux pièges et voiles posés, pas à son apparence.\n\n" +
                           "<i><color=#b0a0c0>Les pièges sous voile sont invisibles pour l'adversaire (information asymétrique).</color></i>";
                    break;
                default:
                    generation = "";
                    body = "<i>(Phases non documentées pour cette classe)</i>";
                    break;
            }
            string genBlock = string.IsNullOrEmpty(generation) ? "" : $"<size=14>{generation}</size>\n";
            return ($"{genBlock}<size=14>{body}</size>").Replace("\n\n", "\n");
        }

        // ===================== Tooltip survol sort =====================

        private void BuildTooltip(RectTransform parent)
        {
            var panel = _f.MakePanel(parent, new Color(0.05f, 0.05f, 0.07f, 0.98f));
            panel.raycastTarget = false;
            var rt = panel.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 0f); rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(380f, 0f);
            var cg = panel.gameObject.AddComponent<CanvasGroup>(); cg.blocksRaycasts = false; cg.interactable = false;
            var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 14, 12, 12); vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var fit = panel.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize; fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            _tooltipText = _f.MakeText("TT", panel.rectTransform, "", _t.FontSizeSmall, _t.TextPrimary, _t.Font, TextAlignmentOptions.TopLeft);
            _tooltipText.raycastTarget = false; _tooltipText.enableWordWrapping = true;

            panel.gameObject.SetActive(false);
            _tooltipGo = panel.gameObject;
        }

        private void ShowTooltip(string text)
        {
            if (_tooltipGo == null) return;
            _tooltipText.text = text;
            _tooltipGo.SetActive(true);
            _tooltipGo.transform.SetAsLastSibling();
            var rt = (RectTransform)_tooltipGo.transform;
            Vector3 pos = Input.mousePosition;
            // Flip à gauche si on est trop près du bord droit.
            bool flip = pos.x + 400f > Screen.width;
            rt.pivot = flip ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            float dx = flip ? -18f : 18f;
            _tooltipGo.transform.position = new Vector3(pos.x + dx, pos.y - 12f, 0f);
        }

        private void HideTooltip()
        {
            if (_tooltipGo != null) _tooltipGo.SetActive(false);
        }

        private static string BuildSpellTooltipText(SpellDefinition def)
        {
            string desc = string.IsNullOrEmpty(def.Description) ? "<i><color=#888>(description à remplir)</color></i>" : def.Description;
            string lore = string.IsNullOrEmpty(def.LoreFlavor) ? "" : $"\n\n<size=90%><i><color=#9988aa>{def.LoreFlavor}</color></i></size>";
            return $"<size=120%><b>{def.DisplayName}</b></size>\n<size=85%><color=#9aa>{def.Category} · {def.ClassId}</color></size>\n\n" +
                   $"<color=#ffdd55><b>{def.ActionPointCost} PA</b> · portée {def.MinRange}-{def.MaxRange} · {def.Filter}</color>\n\n{desc}{lore}";
        }

        private void AddSpellTooltip(GameObject go, SpellDefinition def)
        {
            if (go == null || def == null) return;
            go.AddComponent<SpellTooltipProxy>().Init(ShowTooltip, HideTooltip, BuildSpellTooltipText(def));
        }

        private SpellDefinition FindSignature()
        {
            if (_catalog == null) return null;
            if (!System.Enum.TryParse(_classId, out NymoraClass cls)) return null;
            foreach (var s in _catalog.FindByClass(cls, includeSignature: true))
                if (s != null && s.Category == SpellCategory.Signature) return s;
            return null;
        }

        private TMP_InputField MakeInput(RectTransform parent, string placeholder)
        {
            var go = new GameObject("NameInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.18f, 0.22f, 0.30f, 1f);
            img.sprite = HubMenuUIFactory.RoundedSprite(_t.CornerRadius); img.type = Image.Type.Sliced;
            var input = go.GetComponent<TMP_InputField>();

            var textArea = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            var tart = (RectTransform)textArea.transform; tart.SetParent(rt, false);
            tart.anchorMin = Vector2.zero; tart.anchorMax = Vector2.one; tart.offsetMin = new Vector2(12f, 6f); tart.offsetMax = new Vector2(-12f, -6f);

            var ph = _f.MakeText("Placeholder", tart, placeholder, _t.FontSizeBody, _t.TextMuted, _t.Font, TextAlignmentOptions.MidlineLeft);
            HubMenuUIFactory.Stretch(ph.rectTransform); ph.raycastTarget = false; ph.enableWordWrapping = false;
            var txt = _f.MakeText("Text", tart, "", _t.FontSizeBody, _t.TextPrimary, _t.Font, TextAlignmentOptions.MidlineLeft);
            HubMenuUIFactory.Stretch(txt.rectTransform); txt.raycastTarget = false; txt.enableWordWrapping = false;

            input.textViewport = tart;
            input.textComponent = txt;
            input.placeholder = ph;
            input.fontAsset = _t.Font;
            input.pointSize = _t.FontSizeBody;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 24;
            input.text = "";
            return input;
        }

        // ===================== Chargement + rendu liste =====================

        private async void LoadAsync()
        {
            SetStatus("Chargement des decks...");
            if (!EnsureToken()) { SetStatus("Non connecté."); return; }

            var res = await _api.GetDecksAsync(_classId);
            if (_decksContent == null) return; // écran fermé pendant le fetch
            if (!res.IsSuccess) { SetStatus($"Erreur {res.StatusCode}."); return; }

            _decks.Clear();
            if (res.Data.decks != null) foreach (var d in res.Data.decks) _decks.Add(d);

            string last = SelectedClassPreferences.GetLastEditedDeckId(_classId);
            if (!string.IsNullOrEmpty(last) && _decks.Exists(d => d.id == last)) LoadDeckIntoEditor(last);
            else if (_decks.Count > 0) LoadDeckIntoEditor(_decks[0].id);
            else { ClearComposition(); RenderDecks(); }

            UpdateStatus();
        }

        private void RenderDecks()
        {
            if (_decksContent == null) return;
            foreach (var go in _deckItems) if (go != null) Object.Destroy(go);
            _deckItems.Clear();

            foreach (var deck in _decks)
            {
                string id = deck.id;
                var img = _f.MakeImage("Deck_" + id, _decksContent, id == _editingDeckId ? _t.CardBgHover : _t.CardBg);
                img.gameObject.AddComponent<LayoutElement>().minHeight = 56f;
                var btn = img.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
                var lbl = _f.MakeText("L", img.rectTransform, deck.name, _t.FontSizeBody, _t.TextPrimary, _t.FontBold, TextAlignmentOptions.Center);
                HubMenuUIFactory.Stretch(lbl.rectTransform, 14f, 14f, 6f, 6f);
                lbl.enableWordWrapping = true; lbl.raycastTarget = false;
                btn.onClick.AddListener(() => LoadDeckIntoEditor(id));
                _deckItems.Add(img.gameObject);
            }
        }

        private void LoadDeckIntoEditor(string deckId)
        {
            var deck = _decks.Find(d => d.id == deckId);
            if (deck == null) return;
            _editingDeckId = deckId;
            for (int i = 0; i < 6; i++)
                _slots[i] = (deck.spellIds != null && i < deck.spellIds.Length) ? deck.spellIds[i] : null;
            if (_nameInput != null) _nameInput.text = deck.name;
            SelectedClassPreferences.SetLastEditedDeckId(_classId, deckId);

            RenderDecks();
            RenderSlots();
            RenderSpellGrid();
            UpdateStatus();
            UpdateSaveLabel();
            SyncOldPanel(deckId); // deck actif pour le combat
        }

        // ===================== Slots =====================

        private void RenderSlots()
        {
            if (_slotsRow == null) return;
            foreach (var go in _slotItems) if (go != null) Object.Destroy(go);
            _slotItems.Clear();

            for (int i = 0; i < 6; i++)
            {
                int slotIndex = i;
                string sid = _slots[i];
                SpellDefinition def = (!string.IsNullOrEmpty(sid) && _catalog != null) ? _catalog.FindBySpellId(sid) : null;

                var box = _f.MakeImage("Slot_" + i, _slotsRow, def != null ? _t.CardBg : new Color(1f, 1f, 1f, 0.04f));
                var le = box.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 116f; le.preferredHeight = 116f;
                var btn = box.gameObject.AddComponent<Button>(); btn.targetGraphic = box;
                btn.onClick.AddListener(() => RemoveSlot(slotIndex));

                if (def != null && def.IconSprite != null)
                {
                    var icon = _f.MakeImage("Icon", box.rectTransform, Color.white, rounded: false);
                    icon.sprite = def.IconSprite; icon.preserveAspect = true; icon.raycastTarget = false;
                    var irt = icon.rectTransform;
                    irt.anchorMin = new Vector2(0.5f, 1f); irt.anchorMax = new Vector2(0.5f, 1f); irt.pivot = new Vector2(0.5f, 1f);
                    irt.sizeDelta = new Vector2(56f, 56f); irt.anchoredPosition = new Vector2(0f, -10f);
                }

                var lbl = _f.MakeText("L", box.rectTransform,
                    def != null ? $"{def.DisplayName}\n<color=#cce>{def.ActionPointCost} PA</color>" : "<color=#666>vide</color>",
                    _t.FontSizeSmall, _t.TextPrimary, _t.Font, TextAlignmentOptions.Bottom);
                lbl.raycastTarget = false;
                var lrt = lbl.rectTransform;
                lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 0f); lrt.pivot = new Vector2(0.5f, 0f);
                lrt.sizeDelta = new Vector2(-8f, 46f); lrt.anchoredPosition = new Vector2(0f, 6f);

                if (def != null) AddSpellTooltip(box.gameObject, def);
                _slotItems.Add(box.gameObject);
            }
        }

        private void RemoveSlot(int i)
        {
            if (_slots[i] == null) return;
            _slots[i] = null;
            RenderSlots();
            RenderSpellGrid();
            UpdateStatus();
        }

        // ===================== Onglets catégories + grille =====================

        private void RenderCatTabs()
        {
            if (_catTabsRow == null) return;
            _catTabs.Clear();
            SpawnCatTab(SpellCategory.Offensive, "Offensifs");
            SpawnCatTab(SpellCategory.Tactical, "Tactiques");
            SpawnCatTab(SpellCategory.Survival, "Survie");
            UpdateCatTabStyles();
        }

        private void SpawnCatTab(SpellCategory cat, string label)
        {
            var img = _f.MakeImage("Cat_" + cat, _catTabsRow, _t.ButtonGhostBg);
            var le = img.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 150f; le.preferredHeight = 40f;
            var btn = img.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
            var lbl = _f.MakeText("L", img.rectTransform, label, _t.FontSizeBody, _t.TextSecondary, _t.Font, TextAlignmentOptions.Center);
            HubMenuUIFactory.Stretch(lbl.rectTransform); lbl.raycastTarget = false; lbl.enableWordWrapping = false;
            btn.onClick.AddListener(() => { _activeCategory = cat; RenderSpellGrid(); UpdateCatTabStyles(); });
            _catTabs.Add((cat, img, lbl));
        }

        private void UpdateCatTabStyles()
        {
            foreach (var (cat, bg, label) in _catTabs)
            {
                bool active = cat == _activeCategory;
                if (bg != null) bg.color = active ? _t.Accent : _t.ButtonGhostBg;
                if (label != null)
                {
                    label.color = active ? _t.TextOnLight : _t.TextSecondary;
                    label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
                }
            }
        }

        private void RenderSpellGrid()
        {
            if (_spellGridContent == null) return;
            foreach (var go in _spellCells) if (go != null) Object.Destroy(go);
            _spellCells.Clear();
            if (_catalog == null) { SetStatus("SpellCatalog manquant."); return; }
            if (!System.Enum.TryParse(_classId, out NymoraClass cls)) return;

            foreach (var def in _catalog.FindByClass(cls, includeSignature: false))
            {
                if (def.Category != _activeCategory) continue;
                string spellId = def.SpellId;
                bool equipped = System.Array.IndexOf(_slots, spellId) >= 0;

                var cell = _f.MakeImage("Spell_" + spellId, _spellGridContent, equipped ? _t.CardBgHover : _t.CardBg);
                var cle = cell.gameObject.AddComponent<LayoutElement>();
                cle.preferredWidth = 150f; cle.preferredHeight = 196f;
                var btn = cell.gameObject.AddComponent<Button>(); btn.targetGraphic = cell; btn.interactable = !equipped;
                btn.onClick.AddListener(() => AddSpell(spellId));

                if (def.IconSprite != null)
                {
                    var icon = _f.MakeImage("Icon", cell.rectTransform, Color.white, rounded: false);
                    icon.sprite = def.IconSprite; icon.preserveAspect = true; icon.raycastTarget = false;
                    var irt = icon.rectTransform;
                    irt.anchorMin = new Vector2(0.5f, 1f); irt.anchorMax = new Vector2(0.5f, 1f); irt.pivot = new Vector2(0.5f, 1f);
                    irt.sizeDelta = new Vector2(60f, 60f); irt.anchoredPosition = new Vector2(0f, -10f);
                }

                var name = _f.MakeText("Name", cell.rectTransform, def.DisplayName, _t.FontSizeSmall, _t.TextPrimary, _t.FontBold, TextAlignmentOptions.Center);
                name.raycastTarget = false;
                var nrt = name.rectTransform;
                nrt.anchorMin = new Vector2(0f, 1f); nrt.anchorMax = new Vector2(1f, 1f); nrt.pivot = new Vector2(0.5f, 1f);
                nrt.sizeDelta = new Vector2(-10f, 44f); nrt.anchoredPosition = new Vector2(0f, def.IconSprite != null ? -74f : -10f);

                var stats = _f.MakeText("Stats", cell.rectTransform,
                    $"<b>{def.ActionPointCost} PA</b>  ·  <size=85%>portée {def.MinRange}-{def.MaxRange}</size>",
                    _t.FontSizeSmall, _t.TextSecondary, _t.Font, TextAlignmentOptions.Bottom);
                stats.raycastTarget = false;
                var srt = stats.rectTransform;
                srt.anchorMin = new Vector2(0f, 0f); srt.anchorMax = new Vector2(1f, 0f); srt.pivot = new Vector2(0.5f, 0f);
                srt.sizeDelta = new Vector2(-10f, 30f); srt.anchoredPosition = new Vector2(0f, 8f);

                AddSpellTooltip(cell.gameObject, def);
                _spellCells.Add(cell.gameObject);
            }
        }

        private void AddSpell(string spellId)
        {
            for (int i = 0; i < 6; i++)
            {
                if (_slots[i] == null)
                {
                    _slots[i] = spellId;
                    RenderSlots();
                    RenderSpellGrid();
                    UpdateStatus();
                    return;
                }
            }
            SetStatus("Tous les slots sont remplis. Clique un slot pour retirer un sort.");
        }

        // ===================== Boutons (Nouveau / Save / Supprimer) =====================

        private void OnNewDeck()
        {
            ClearComposition();
            SelectedClassPreferences.ClearLastEditedDeckId(_classId);
            RenderDecks();
            RenderSlots();
            RenderSpellGrid();
            UpdateStatus();
            UpdateSaveLabel();
            SetStatus("Nouveau deck — choisis 6 sorts puis Save.");
        }

        private async void OnSave()
        {
            if (_busy) return;
            string name = _nameInput != null ? _nameInput.text?.Trim() : null;
            if (string.IsNullOrEmpty(name)) { SetStatus("Nom de deck obligatoire."); return; }

            var spellIds = new List<string>();
            for (int i = 0; i < 6; i++) if (_slots[i] != null) spellIds.Add(_slots[i]);
            if (spellIds.Count != 6) { SetStatus($"6 sorts requis ({spellIds.Count}/6)."); return; }
            if (new HashSet<string>(spellIds).Count != 6) { SetStatus("Les 6 sorts doivent être uniques."); return; }

            if (!EnsureToken()) { SetStatus("Non connecté."); return; }
            _busy = true;
            SetStatus(_editingDeckId == null ? "Création..." : "Mise à jour...");

            if (_editingDeckId == null)
            {
                if (_decks.Count >= 5) { _busy = false; SetStatus("Maximum 5 decks par classe."); return; }
                var res = await _api.CreateDeckAsync(_classId, name, spellIds.ToArray());
                _busy = false;
                if (_decksContent == null) return;
                if (!res.IsSuccess) { SetStatus($"Erreur {res.StatusCode} : {res.ErrorMessage}"); return; }
                _decks.Add(res.Data.deck);
                _editingDeckId = res.Data.deck.id;
                SelectedClassPreferences.SetLastEditedDeckId(_classId, _editingDeckId);
                SetStatus($"Deck '{name}' créé.");
            }
            else
            {
                var res = await _api.UpdateDeckAsync(_editingDeckId, name, spellIds.ToArray());
                _busy = false;
                if (_decksContent == null) return;
                if (!res.IsSuccess) { SetStatus($"Erreur {res.StatusCode} : {res.ErrorMessage}"); return; }
                int idx = _decks.FindIndex(d => d.id == _editingDeckId);
                if (idx >= 0) _decks[idx] = res.Data.deck;
                SetStatus($"Deck '{name}' mis à jour.");
            }

            RenderDecks();
            UpdateSaveLabel();
            SyncOldPanel(_editingDeckId);
        }

        private async void OnDelete()
        {
            if (_busy) return;
            if (_editingDeckId == null) { SetStatus("Sélectionne un deck à supprimer."); return; }
            if (!EnsureToken()) { SetStatus("Non connecté."); return; }

            string deletingId = _editingDeckId;
            _busy = true;
            SetStatus("Suppression...");
            var res = await _api.DeleteDeckAsync(deletingId);
            _busy = false;
            if (_decksContent == null) return;
            if (!res.IsSuccess) { SetStatus($"Erreur {res.StatusCode} : {res.ErrorMessage}"); return; }

            _decks.RemoveAll(d => d.id == deletingId);
            if (SelectedClassPreferences.GetLastEditedDeckId(_classId) == deletingId)
                SelectedClassPreferences.ClearLastEditedDeckId(_classId);
            ClearComposition();
            RenderDecks();
            RenderSlots();
            RenderSpellGrid();
            UpdateStatus();
            UpdateSaveLabel();
            SetStatus("Deck supprimé.");
        }

        // ===================== Helpers =====================

        private void ClearComposition()
        {
            for (int i = 0; i < 6; i++) _slots[i] = null;
            _editingDeckId = null;
            if (_nameInput != null) _nameInput.text = "";
        }

        private void SyncOldPanel(string deckId)
        {
            if (string.IsNullOrEmpty(deckId)) return;
            if (HubDeckBuilderPanel.Instance != null) SyncOldPanelAsync(deckId);
        }

        private async void SyncOldPanelAsync(string deckId)
        {
            await HubDeckBuilderPanel.Instance.SetActiveDeckAsync(_classId, deckId);
        }

        private void UpdateSaveLabel()
        {
            if (_saveLabel != null) _saveLabel.text = string.IsNullOrEmpty(_editingDeckId) ? "Save" : "Modifier";
        }

        private void UpdateStatus()
        {
            int filled = 0;
            for (int i = 0; i < 6; i++) if (_slots[i] != null) filled++;
            string mode = _editingDeckId != null ? "édition" : "nouveau";
            SetStatus($"{filled}/6 sorts · {_decks.Count}/5 decks · {mode}");
        }

        private bool EnsureToken()
        {
            string token = HubChatClient.Instance?.DevToken;
            if (_api == null || string.IsNullOrEmpty(token)) return false;
            _api.SetBearerToken(token);
            return true;
        }

        private void SetStatus(string s) { if (_status != null) _status.text = s; }
    }

    /// <summary>Affiche/masque un tooltip de sort au survol (instantané).</summary>
    internal sealed class SpellTooltipProxy : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private System.Action<string> _show;
        private System.Action _hide;
        private string _text;

        public void Init(System.Action<string> show, System.Action hide, string text)
        {
            _show = show; _hide = hide; _text = text;
        }

        public void OnPointerEnter(PointerEventData _) => _show?.Invoke(_text);
        public void OnPointerExit(PointerEventData _) => _hide?.Invoke();
    }
}
