using System.Collections.Generic;
using Fusion;
using Nymora.Core.Data;
using Nymora.Core.Input;
using Nymora.Core.SceneFlow;
using Nymora.Core.ScriptableObjects;
using Nymora.Network.Backend;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nymora.Hub.Menu
{
    /// <summary>
    /// M1 — Coquille du nouveau menu hub (refonte UI "Échap").
    ///
    /// Posée sur un Canvas dédié (créé par Nymora > Setup > UI Menu > Create or Refresh Menu Shell).
    /// Construit TOUTE son UI en code via HubMenuUIFactory (zéro câblage manuel) :
    ///   - bouton hamburger ☰ (haut-gauche, toujours visible)
    ///   - touche Échap : ouvre/ferme (Échap ferme tout, décision Lorenzo)
    ///   - fond sombre + flou (snapshot écran downsamplé + voile), clics hub bloqués
    ///   - barre d'onglets haute (Social / Progression / Paramètres / Report bug / Déconnexion)
    ///   - 4 cartes d'accueil (Arène / Personnage / Battle Pass / Boutique)
    ///   - monnaies (Nymos / Shards) en haut-droite, live via le wallet existant
    ///
    /// En M1 les onglets et cartes ouvrent un écran placeholder (« contenu à venir ») avec
    /// un bouton Retour. Les vrais écrans arrivent en M2..M8.
    ///
    /// 100% View — pas de bump CombatRulesVersion.
    /// </summary>
    public sealed partial class HubMenuShell : MonoBehaviour
    {
        [SerializeField] private HubMenuTheme _theme;
        [SerializeField] private NymoraBackendSettings _backendSettings;
        [SerializeField] private NymoraClassDefinition[] _classDefinitions;
        [SerializeField] private SpellCatalog _spellCatalog;
        [SerializeField] private CosmeticSkinDefinition[] _skinDefinitions;
        [SerializeField] private CardArt[] _cardArt;

        [System.Serializable]
        public struct CardArt { public string Id; public Sprite Sprite; }

        // M8 — Lien Discord Nymora (section report de bug), ouvert dans le navigateur.
        private const string BugReportUrl = "https://discord.gg/3Nm3q2DX";

        // Scène de connexion (retour sur Déconnexion).
        private const string LoginSceneName = "00_Login";

        public static HubMenuShell Instance { get; private set; }

        // C1 — police de la DA menu (Ari), exposée pour les bulles de chat world-space.
        public static TMP_FontAsset MenuFont { get; private set; }

        // Thème menu exposé pour les widgets hub hors-shell qui veulent la même DA monochrome
        // (ex : ChallengePopup, menu contextuel avatar). Posé à l'init, comme MenuFont.
        public static HubMenuTheme MenuTheme { get; private set; }

        private HubMenuUIFactory _f;
        private GameObject _menuRoot;
        private RectTransform _contentArea;
        private GameObject _hamburger;
        private TextMeshProUGUI _nymosLabel;   // devise persistante (canvas menu, toujours visible)
        private TextMeshProUGUI _shardsLabel;

        private bool _isOpen;
        private GameObject _currentScreen;
        private string _currentScreenId = "home";

        // Matchmaking ranked (réutilise les events de file de HubChatClient)
        private bool _searching;
        private TextMeshProUGUI _mmStatus;
        private TextMeshProUGUI _lbText;  // statut (chargement / erreur / vide) centré
        private RectTransform _lbList;    // conteneur des lignes du classement (VLG)

        // M3 — Personnage
        private NymoraApiClient _api;
        private TextMeshProUGUI _pgClassName, _pgPseudo, _pgTitle, _pgLevel, _pgXp;
        private Image _pgXpFill;
        private Image _pgPortrait;
        private UISpriteAnimator _pgPortraitAnim;
        // Ancre + taille de base du portrait (anchoredPosition / sizeDelta au repos). Servent au
        // mode aligné de l'animation (anti jiggle + anti zoom des frames de classe trimmées) et à
        // restaurer le rect quand on repasse sur le chemin legacy du skin.
        private Vector2 _pgPortraitAnchor;
        private Vector2 _pgPortraitSize;
        // Box de CALCUL D'ÉCHELLE du portrait, identique à la prévisu boutique
        // (CosmeticPreviewTooltip : PanelSizeSkin 420×540 − largeur 32 − TopReserve 54 − BottomReserve 136).
        // Le perso/skin du deck builder s'affiche ainsi à la MÊME taille que dans la boutique (extraScale 1,
        // comme le shop, sans le boost MenuPortraitScale). Le placement vertical reste piloté par
        // _pgPortraitSize (centrage dans le rect visuel), seule la taille rendue est calée sur le shop.
        private static readonly Vector2 PortraitFitBox = new Vector2(388f, 350f);
        // Familier équipé affiché en idle à côté du perso (prévisu menu personnage). Pas de
        // class-lock : un seul familier, montré quelle que soit la classe sélectionnée.
        private Image _pgPetPortrait;
        private UISpriteAnimator _pgPetPortraitAnim;
        private Vector2 _pgPetBoxCenter;
        private Vector2 _pgPetBoxSize;
        // PATCH #2 — jeton de generation du portrait. RefreshClassPanel/ApplySkinToPortrait sont
        // async (requetes backend) : en equipant/desequipant vite, plusieurs sont en vol et peuvent
        // se terminer dans le DESORDRE -> un ancien applique un skin/echelle perimee dans la prevue.
        // Chaque refresh incremente ce compteur ; une completion async abandonne si elle n'est plus
        // la plus recente.
        private int _portraitGen;
        // PATCH #2 (anti-flash skin) — classe + skin actuellement affiches sur le portrait. Permet,
        // lors d'un simple changement de titre (classe + skin inchanges), de GARDER le skin courant
        // pendant le fetch async au lieu de flasher les frames de base ~1s. "" = frames de classe.
        private string _pgPortraitClass;
        private string _pgPortraitSkinId = "";
        private static PetCatalog _pgPetCatalog;
        private static bool _pgPetCatalogLoaded;
        private int _classIndex;
        private RectTransform _pgRightCol;
        private GameObject _pgRightContent;
        private List<TextMeshProUGUI> _pgSubLabels;
        private List<Image> _pgSubUnders;
        private int _pgSubIndex;

        private struct TabRef { public string Id; public TextMeshProUGUI Label; public Image Icon; }
        private readonly List<TabRef> _tabs = new List<TabRef>();

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (_theme == null)
            {
                Debug.LogError("[HubMenuShell] Theme non assigné — coquille désactivée. Relance Nymora > Setup > UI Menu > Create or Refresh Menu Shell.");
                enabled = false;
                return;
            }
            _f = new HubMenuUIFactory(_theme);
            MenuFont = _theme.Font;
            MenuTheme = _theme;
            if (_backendSettings != null) _api = new NymoraApiClient(_backendSettings);
            BuildUI();
        }

        private void Start()
        {
            var chat = HubChatClient.Instance;
            if (chat != null)
            {
                chat.OnWalletUpdate += HandleWallet;
                chat.OnRankedQueueJoined += HandleQueueJoined;
                chat.OnRankedMatchFound += HandleMatchFound;
                chat.OnRankedQueueLeft += HandleQueueLeft;
            }

            // La devise du menu remplace l'ancien wallet hub : on masque celui-ci (évite le doublon)
            // et on alimente notre affichage (fetch initial + push WS).
            if (HubWalletWidget.Instance != null) HubWalletWidget.Instance.gameObject.SetActive(false);
            FetchWalletAsync();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            var chat = HubChatClient.Instance;
            if (chat != null)
            {
                chat.OnWalletUpdate -= HandleWallet;
                chat.OnRankedQueueJoined -= HandleQueueJoined;
                chat.OnRankedMatchFound -= HandleMatchFound;
                chat.OnRankedQueueLeft -= HandleQueueLeft;
            }
        }

        private void Update()
        {
            // Rebind en cours (onglet Raccourcis) : la capture clavier monopolise ce frame -> on ne
            //   toggle pas le menu et on ne déclenche aucun raccourci pendant qu'on réassigne une touche.
            if (KeyRebindCapture.IsCapturing) { KeyRebindCapture.Tick(); return; }

            if (Input.GetKeyDown(KeyCode.Escape)) Toggle();

            // Raccourcis HUB rebindables (5 juin) : B/P/R/A/K -> ouvrent le menu correspondant.
            //   IMPÉRATIF (demande Lorenzo) : ignorés si on tape dans un champ texte (chat) -> écrire
            //   "boutique" ne doit pas ouvrir des menus en arrière-plan.
            if (IsTypingInInputField()) return;
            if      (KeybindingService.GetDown(Keybind.HubShop))       OpenScreen("shop");
            else if (KeybindingService.GetDown(Keybind.HubCharacter))  OpenScreen("character");
            else if (KeybindingService.GetDown(Keybind.HubReplay))     OpenScreen("replays");
            else if (KeybindingService.GetDown(Keybind.HubArena))      OpenScreen("arena");
            else if (KeybindingService.GetDown(Keybind.HubBattlePass)) OpenScreen("battlepass");
        }

        /// <summary>5 juin — raccourcis hub : ouvre le menu (si fermé) et navigue vers l'écran `id`.</summary>
        public void OpenScreen(string id)
        {
            if (!_isOpen) Open();
            ShowScreen(id);
        }

        /// <summary>True si le focus clavier est dans un champ de saisie (chat) -> on coupe les raccourcis hub.</summary>
        private static bool IsTypingInInputField()
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var go = es.currentSelectedGameObject;
            if (go == null) return false;
            var tmp = go.GetComponent<TMP_InputField>();
            if (tmp != null && tmp.isFocused) return true;
            var legacy = go.GetComponent<InputField>();
            return legacy != null && legacy.isFocused;
        }

        // ===== Ouverture / fermeture =====

        public void Toggle() { if (_isOpen) Close(); else Open(); }

        public void Open()
        {
            if (_isOpen || _menuRoot == null) return;
            _isOpen = true;
            CloseEmotePopup(); // E1 — referme le popup d'émote s'il était ouvert
            ShowScreen("home");
            _menuRoot.SetActive(true); // UiPanelAnimator joue le fondu+pop
        }

        public void Close()
        {
            if (!_isOpen || _menuRoot == null) return;
            if (_searching) CancelSearch();
            _isOpen = false;
            UiPanelAnimator.CloseAnimated(_menuRoot);
        }

        // ===== Construction UI =====

        private void BuildUI()
        {
            var canvasRT = transform as RectTransform;

            // Racine du menu (toggle + animée)
            _menuRoot = new GameObject("MenuRoot", typeof(RectTransform), typeof(CanvasGroup), typeof(UiPanelAnimator));
            var mrt = _menuRoot.GetComponent<RectTransform>();
            mrt.SetParent(canvasRT, false);
            HubMenuUIFactory.Stretch(mrt);

            // Voile sombre plein écran (clic = fermer)
            var veil = _f.MakeImage("Veil", mrt, _theme.Backdrop, rounded: false);
            HubMenuUIFactory.Stretch(veil.rectTransform);
            var veilBtn = veil.gameObject.AddComponent<Button>();
            veilBtn.transition = Selectable.Transition.None;
            veilBtn.onClick.AddListener(Close);

            // Barre d'onglets
            var bar = _f.MakeRect("TabBar", mrt);
            bar.anchorMin = new Vector2(0.5f, 1f); bar.anchorMax = new Vector2(0.5f, 1f); bar.pivot = new Vector2(0.5f, 1f);
            bar.anchoredPosition = new Vector2(0f, -22f);
            bar.sizeDelta = new Vector2(960f, _theme.TabHeight);
            var hlg = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = _theme.TabSpacing; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            AddTab(bar, "social", "Social");
            AddTab(bar, "progression", "Progression");
            AddTab(bar, "replays", "Replays");
            AddTab(bar, "settings", "Paramètres");
            AddTab(bar, "report", "Report bug");
            AddTab(bar, "logout", "Déconnexion");

            // Séparateur pleine largeur
            var divider = _f.MakeDivider(mrt);
            var drt = divider.rectTransform;
            drt.anchorMin = new Vector2(0f, 1f); drt.anchorMax = new Vector2(1f, 1f); drt.pivot = new Vector2(0.5f, 1f);
            drt.sizeDelta = new Vector2(-120f, 1.5f);
            drt.anchoredPosition = new Vector2(0f, -(22f + _theme.TabHeight + 6f));

            // Zone de contenu (sous la barre, au-dessus du hint)
            _contentArea = _f.MakeRect("Content", mrt);
            _contentArea.anchorMin = new Vector2(0f, 0f); _contentArea.anchorMax = new Vector2(1f, 1f);
            _contentArea.offsetMin = new Vector2(80f, 70f);
            _contentArea.offsetMax = new Vector2(-80f, -(22f + _theme.TabHeight + 30f));

            // Hint bas
            var hint = _f.MakeText("Hint", mrt, "Échap   Fermer", _theme.FontSizeSmall, _theme.TextMuted, _theme.Font, TextAlignmentOptions.Center);
            hint.raycastTarget = false;
            var hrt = hint.rectTransform;
            hrt.anchorMin = new Vector2(0.5f, 0f); hrt.anchorMax = new Vector2(0.5f, 0f); hrt.pivot = new Vector2(0.5f, 0f);
            hrt.anchoredPosition = new Vector2(0f, 22f);
            hrt.sizeDelta = new Vector2(400f, _theme.HintBarHeight);

            // Bouton hamburger (hors MenuRoot, toujours visible) — placé DERRIÈRE le menu
            BuildHamburger(canvasRT);
            if (_hamburger != null) _hamburger.transform.SetAsFirstSibling();

            // E1 — Bouton émote (à droite du hamburger) + popup de sélection (hors MenuRoot,
            // visible dans le hub ; couvert par le voile quand le menu Échap est ouvert).
            BuildEmoteButton(canvasRT);

            // Devise persistante (hors MenuRoot, sur le canvas menu = toujours visible, au-dessus
            // de tout y compris le voile). Icônes ash/blood + valeurs live.
            BuildPersistentCurrency(canvasRT);

            _menuRoot.SetActive(false);
        }

        private void AddTab(RectTransform bar, string id, string label)
        {
            var btn = _f.MakeTabButton(bar, label, out var lbl, out var ico);

            // Icône SVG (Resources/UI/Icons) si présente, sinon le placeholder reste.
            var sprite = HubMenuUIFactory.LoadIcon(IconNameForTab(id));
            if (sprite != null && ico != null)
            {
                ico.sprite = sprite;
                ico.type = Image.Type.Simple;
                ico.preserveAspect = true;
            }

            btn.onClick.AddListener(() => ShowScreen(id));
            _tabs.Add(new TabRef { Id = id, Label = lbl, Icon = ico });
        }

        private static string IconNameForTab(string id)
        {
            switch (id)
            {
                case "social": return "ui_icon_social";
                case "progression": return "ui_icon_progression";
                case "replays": return "ui_icon_replays";
                case "settings": return "ui_icon_settings";
                case "report": return "ui_icon_report_bug";
                case "logout": return "ui_icon_logout";
                default: return null;
            }
        }

        private void BuildHamburger(RectTransform parent)
        {
            var btnImg = _f.MakeImage("MenuButton", parent, _theme.ButtonGhostBg);
            var rt = btnImg.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, -24f);
            rt.sizeDelta = new Vector2(52f, 52f);

            var btn = btnImg.gameObject.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            var c = btn.colors;
            c.normalColor = _theme.ButtonGhostBg;
            c.highlightedColor = _theme.ButtonGhostBgHover;
            c.pressedColor = _theme.ButtonGhostBgHover;
            c.fadeDuration = 0.1f;
            btn.colors = c;
            btn.onClick.AddListener(Toggle);

            for (int i = 0; i < 3; i++)
            {
                var line = _f.MakeImage("Bar" + i, rt, _theme.TextPrimary, rounded: false);
                var brt = line.rectTransform;
                brt.anchorMin = new Vector2(0.5f, 0.5f); brt.anchorMax = new Vector2(0.5f, 0.5f); brt.pivot = new Vector2(0.5f, 0.5f);
                brt.sizeDelta = new Vector2(24f, 2.5f);
                brt.anchoredPosition = new Vector2(0f, (1 - i) * 7f);
                line.raycastTarget = false;
            }
            _hamburger = btnImg.gameObject;
        }

        // ===== Devise persistante (ash = Nymos / blood = Shards) =====

        private void BuildPersistentCurrency(RectTransform canvasRT)
        {
            var row = _f.MakeRect("Currency", canvasRT);
            row.anchorMin = new Vector2(1f, 1f); row.anchorMax = new Vector2(1f, 1f); row.pivot = new Vector2(1f, 1f);
            row.anchoredPosition = new Vector2(-28f, -20f);
            row.sizeDelta = new Vector2(380f, 48f);
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 22f; hlg.childAlignment = TextAnchor.MiddleRight;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            var fit = row.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; fit.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            MakeCurrencyEntry(row, "ui_icon_nymos", out _nymosLabel);
            MakeCurrencyEntry(row, "ui_icon_shards", out _shardsLabel);
        }

        private void MakeCurrencyEntry(RectTransform parent, string iconName, out TextMeshProUGUI label)
        {
            var cell = _f.MakeRect("Cur_" + iconName, parent);
            var chlg = cell.gameObject.AddComponent<HorizontalLayoutGroup>();
            chlg.spacing = 7f; chlg.childAlignment = TextAnchor.MiddleLeft;
            chlg.childControlWidth = true; chlg.childControlHeight = true;
            chlg.childForceExpandWidth = false; chlg.childForceExpandHeight = false;
            var cfit = cell.gameObject.AddComponent<ContentSizeFitter>();
            cfit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; cfit.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            // Icône (PNG couleur ash/blood -> pas de teinte : blanc = art original)
            var icon = _f.MakeImage("Icon", cell, Color.white, rounded: false);
            icon.preserveAspect = true; icon.raycastTarget = false;
            var sp = HubMenuUIFactory.LoadIcon(iconName);
            if (sp != null) icon.sprite = sp; else icon.color = new Color(1f, 1f, 1f, 0.15f);
            var ile = icon.gameObject.AddComponent<LayoutElement>(); ile.preferredWidth = 40f; ile.preferredHeight = 40f;

            label = _f.MakeText("Val", cell, "0", _theme.FontSizeBody, _theme.TextPrimary, _theme.FontBold, TextAlignmentOptions.MidlineLeft);
            label.enableWordWrapping = false;
            label.gameObject.AddComponent<LayoutElement>().minWidth = 44f;
        }

        private void HandleWallet(HubChatClient.WalletUpdateData d) => SetCurrency(d.Nymos, d.Shards);

        private void SetCurrency(int nymos, int shards)
        {
            if (_nymosLabel != null) _nymosLabel.text = nymos.ToString();
            if (_shardsLabel != null) _shardsLabel.text = shards.ToString();
        }

        private async void FetchWalletAsync()
        {
            if (_api == null) return;
            string token = HubChatClient.Instance?.DevToken;
            if (string.IsNullOrEmpty(token)) return;
            _api.SetBearerToken(token);
            var res = await _api.GetWalletMeAsync();
            if (res.IsSuccess) SetCurrency(res.Data.nymos, res.Data.shards);
        }

        // ===== Navigation (M1 : home + placeholders) =====

        private void ShowScreen(string id)
        {
            // Sortir du matchmaking annule la file backend.
            if (_searching && id != "matchmaking") CancelSearch();

            if (_currentScreen != null) { Destroy(_currentScreen); _currentScreen = null; }
            _mmStatus = null;
            _lbText = null;
            _lbList = null;
            _currentScreenId = id;
            for (int i = 0; i < _tabs.Count; i++)
                _f.SetTabActive(_tabs[i].Label, _tabs[i].Icon, _tabs[i].Id == id);

            if (id == "home") BuildHome();
            else if (id == "arena") BuildArena();
            else if (id == "matchmaking") BuildMatchmaking();
            else if (id == "leaderboard") BuildLeaderboard();
            else if (id == "character") BuildPersonnage();
            else if (id == "social") BuildSocial();
            else if (id == "progression") BuildProgression();
            else if (id == "replays") BuildReplays();
            else if (id == "settings") BuildSettings();
            else if (id == "shop") BuildShop();
            else if (id == "battlepass") BuildBattlePass();
            else if (id == "report") BuildReport();
            else if (id == "logout") BuildLogout();
            else BuildPlaceholder(id);
        }

        /// <summary>Bouton "‹ Retour" en haut-gauche de la zone de contenu (revient à l'accueil).</summary>
        private void AddBackButton(RectTransform parent, float x = 0f, float y = -2f)
        {
            var back = _f.MakeButton(parent, "‹ Retour", false, out _);
            var brt = (RectTransform)back.transform;
            brt.anchorMin = new Vector2(0f, 1f); brt.anchorMax = new Vector2(0f, 1f); brt.pivot = new Vector2(0f, 1f);
            brt.anchoredPosition = new Vector2(x, y);
            brt.sizeDelta = new Vector2(140f, 44f);
            back.onClick.AddListener(() => ShowScreen("home"));
        }

        // ===== M2 — Écran Arène (4 modes + Classement) =====

        private void BuildArena()
        {
            var holder = _f.MakeRect("Arena", _contentArea);
            HubMenuUIFactory.Stretch(holder);
            AddBackButton(holder, 468f, -50f);

            // Bouton Classement, en haut au centre (au-dessus des cartes)
            var lb = _f.MakeButton(holder, "Classement", false, out _);
            var lbrt = (RectTransform)lb.transform;
            lbrt.anchorMin = new Vector2(0.5f, 1f); lbrt.anchorMax = new Vector2(0.5f, 1f); lbrt.pivot = new Vector2(0.5f, 1f);
            lbrt.anchoredPosition = new Vector2(0f, -50f);
            lbrt.sizeDelta = new Vector2(220f, 44f);
            lb.onClick.AddListener(() => ShowScreen("leaderboard"));

            // Rangée des 4 modes — MÊME position que les cartes d'accueil (centrée).
            var row = _f.MakeRect("Modes", holder);
            HubMenuUIFactory.Stretch(row);
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = _theme.CardSpacing; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            _arenaRankBadges.Clear();
            MakeModeCard(row, "Entraînement", "Affronte l'IA", true,
                () => { Close(); if (HubArenaPanel.Instance != null) HubArenaPanel.Instance.StartTraining(); }, "mode_training");
            MakeModeCard(row, "Ranked 1v1", "Classé · 1 contre 1", true,
                () => ShowScreen("matchmaking"), "mode_1v1", showRank: true);
            MakeModeCard(row, "Ranked 2v2", "Bientôt disponible", false, null, "mode_2v2", showRank: true);
            MakeModeCard(row, "Ranked 3v3", "Bientôt disponible", false, null, "mode_3v3", showRank: true);

            _currentScreen = holder.gameObject;
            RefreshArenaRankBadges();
        }

        private void MakeModeCard(RectTransform parent, string title, string sub, bool enabled, System.Action onClick, string cardKey = null, bool showRank = false)
        {
            var btn = _f.MakeCard(parent, title, sub, out _, CardSprite(cardKey));
            if (showRank) AddRankBadge((RectTransform)btn.transform);
            if (enabled && onClick != null)
            {
                btn.onClick.AddListener(() => onClick());
            }
            else
            {
                btn.interactable = false;
                btn.gameObject.AddComponent<CanvasGroup>().alpha = 0.5f;
            }
        }

        // ===== Badge de rang sur les cartes ranked (icône + nom, haut de la carte) =====

        private readonly List<(Image icon, TextMeshProUGUI name, TextMeshProUGUI mmr)> _arenaRankBadges
            = new List<(Image, TextMeshProUGUI, TextMeshProUGUI)>();

        /// <summary>Pose un badge vide (icône + nom + MMR) en haut de la carte ; rempli par
        /// RefreshArenaRankBadges une fois le MMR récupéré.</summary>
        private void AddRankBadge(RectTransform cardRoot)
        {
            var badge = _f.MakeRect("RankBadge", cardRoot);
            badge.anchorMin = new Vector2(0.5f, 1f); badge.anchorMax = new Vector2(0.5f, 1f); badge.pivot = new Vector2(0.5f, 1f);
            badge.anchoredPosition = new Vector2(0f, -16f);
            badge.sizeDelta = new Vector2(_theme.CardSize.x - 24f, 96f);
            var vlg = badge.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 1f; vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            var icon = _f.MakeImage("Icon", badge, Color.white, rounded: false);
            icon.type = Image.Type.Simple; icon.preserveAspect = true; icon.raycastTarget = false;
            icon.enabled = false; // caché tant qu'aucune icône
            var ile = icon.gameObject.AddComponent<LayoutElement>();
            ile.preferredWidth = 48f; ile.preferredHeight = 48f; ile.minHeight = 48f;

            var name = _f.MakeText("Name", badge, "", _theme.FontSizeSmall, _theme.TextPrimary, _theme.FontBold, TextAlignmentOptions.Center);
            name.raycastTarget = false;
            name.enableWordWrapping = false;
            var nle = name.gameObject.AddComponent<LayoutElement>();
            nle.preferredHeight = 20f;

            var mmr = _f.MakeText("Mmr", badge, "", _theme.FontSizeSmall, _theme.TextSecondary, _theme.Font, TextAlignmentOptions.Center);
            mmr.raycastTarget = false;
            mmr.enableWordWrapping = false;
            var mle = mmr.gameObject.AddComponent<LayoutElement>();
            mle.preferredHeight = 18f;

            _arenaRankBadges.Add((icon, name, mmr));
        }

        private async void RefreshArenaRankBadges()
        {
            if (_arenaRankBadges.Count == 0) return;
            int mmr = HubLeaderboardPanel.Instance != null
                ? await HubLeaderboardPanel.Instance.FetchLocalMmrAsync()
                : -1;
            if (_currentScreenId != "arena") return; // écran quitté pendant le fetch
            if (mmr < 0) return;                       // MMR indisponible : badges restent vides

            var tier = RankLadder.Resolve(mmr);
            var icon = RankLadder.ResolveIcon(mmr);
            var color = ParseHex(tier.HexColor, _theme.TextPrimary);
            foreach (var (img, nameLabel, mmrLabel) in _arenaRankBadges)
            {
                if (img != null && icon != null) { img.sprite = icon; img.enabled = true; }
                if (nameLabel != null) { nameLabel.text = tier.Name; nameLabel.color = color; }
                if (mmrLabel != null) mmrLabel.text = $"{mmr} MMR";
            }
        }

        // ===== M2 — Fenêtre matchmaking ranked (style menu) =====

        private void BuildMatchmaking()
        {
            var holder = _f.MakeRect("Matchmaking", _contentArea);
            HubMenuUIFactory.Stretch(holder);
            AddBackButton(holder);

            var panel = _f.MakePanel(holder);
            var prt = panel.rectTransform;
            prt.anchorMin = new Vector2(0.5f, 0.5f); prt.anchorMax = new Vector2(0.5f, 0.5f); prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(640f, 340f);
            prt.anchoredPosition = Vector2.zero;

            var title = _f.MakeText("Title", prt, "Recherche classée 1v1", _theme.FontSizeHeader, _theme.TextPrimary, _theme.FontBold, TextAlignmentOptions.Center);
            title.raycastTarget = false;
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0.5f, 1f); trt.anchorMax = new Vector2(0.5f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -36f); trt.sizeDelta = new Vector2(560f, 40f);

            _mmStatus = _f.MakeText("Status", prt, "Préparation...", _theme.FontSizeBody, _theme.TextSecondary, _theme.Font, TextAlignmentOptions.Center);
            _mmStatus.raycastTarget = false;
            var srt = _mmStatus.rectTransform;
            srt.anchorMin = new Vector2(0.5f, 0.5f); srt.anchorMax = new Vector2(0.5f, 0.5f); srt.pivot = new Vector2(0.5f, 0.5f);
            srt.anchoredPosition = new Vector2(0f, 8f); srt.sizeDelta = new Vector2(560f, 120f);

            var cancel = _f.MakeButton(prt, "Annuler", false, out _);
            var crt = (RectTransform)cancel.transform;
            crt.anchorMin = new Vector2(0.5f, 0f); crt.anchorMax = new Vector2(0.5f, 0f); crt.pivot = new Vector2(0.5f, 0f);
            crt.anchoredPosition = new Vector2(0f, 34f); crt.sizeDelta = new Vector2(200f, 46f);
            cancel.onClick.AddListener(() => ShowScreen("arena"));

            _currentScreen = holder.gameObject;
            StartRankedSearch();
        }

        // Même garde-fou deck que HubRankedSearchPanel / HubArenaPanel, puis entre en file.
        private async void StartRankedSearch()
        {
            var chat = HubChatClient.Instance;
            if (chat == null || !chat.IsConnected) { SetMmStatus("Pas connecté au serveur. Réessaie dans un instant."); return; }

            var dbp = HubDeckBuilderPanel.Instance;
            if (dbp == null) { SetMmStatus("Ouvre le Deck Builder une fois avant de chercher une partie."); return; }

            string cls = SelectedClassPreferences.Get();
            if (string.IsNullOrEmpty(cls)) cls = "Soulrender";
            SetMmStatus($"Vérification du deck {cls}...");
            try { await dbp.EnsureClassLoadedAsync(cls); }
            catch (System.Exception ex)
            {
                Debug.LogError($"[HubMenuShell] EnsureClassLoadedAsync({cls}) échec : {ex.Message}");
                SetMmStatus("Erreur de chargement du deck. Réessaie.");
                return;
            }
            if (_currentScreenId != "matchmaking") return; // l'utilisateur a quitté pendant l'await

            if (dbp.MyDecks == null || dbp.MyDecks.Count == 0 || dbp.SelectedDeck == null)
            {
                SetMmStatus($"Aucun deck '{cls}' équipé.\nCrée un deck dans le Deck Builder d'abord.");
                return;
            }

            _searching = true;
            SetMmStatus("Connexion à la file classée...");
            chat.SendEnqueueRanked();
        }

        private void CancelSearch()
        {
            _searching = false;
            HubChatClient.Instance?.SendDequeueRanked();
        }

        private void HandleQueueJoined(int mmr)
        {
            if (_searching) SetMmStatus($"Recherche d'un adversaire... (MMR {mmr})\nLa fenêtre s'élargit avec le temps d'attente.");
        }

        private void HandleMatchFound(string matchId, string opponentSub, string opponentDisplayName)
        {
            _searching = false; // la transition vers la scène ranked est pilotée par HubMatchTransition
            SetMmStatus($"Adversaire trouvé : {opponentDisplayName} !\nLancement du combat...");
        }

        private void HandleQueueLeft()
        {
            if (_currentScreenId == "matchmaking" && !_searching) SetMmStatus("File quittée.");
        }

        private void SetMmStatus(string s) { if (_mmStatus != null) _mmStatus.text = s; }

        // ===== M2 — Écran Classement (3 onglets 1v1/2v2/3v3, style menu) =====

        // Largeur fixe de la table — assez large pour les pseudos. Décalée à gauche pour laisser
        // place au panneau des paliers à droite.
        private const float LbWidth = 640f;
        private const float LbTableCenterX = -150f;   // décalage horizontal de la table
        private const float LbLadderWidth = 230f;     // panneau "paliers" à droite
        private const float LbLadderCenterX = 320f;
        // Largeurs de colonnes (px), partagées en-tête + lignes pour un alignement parfait.
        private const float LbColRankW = 48f;
        private const float LbColTierW = 150f; // icône + nom du rang
        private const float LbColMmrW = 64f;
        private const float LbColWlW = 92f;

        private void BuildLeaderboard()
        {
            var holder = _f.MakeRect("Leaderboard", _contentArea);
            HubMenuUIFactory.Stretch(holder);
            AddBackButton(holder);

            var tabBar = _f.MakeRect("LbTabs", holder);
            tabBar.anchorMin = new Vector2(0.5f, 1f); tabBar.anchorMax = new Vector2(0.5f, 1f); tabBar.pivot = new Vector2(0.5f, 1f);
            tabBar.anchoredPosition = new Vector2(0f, -2f);
            tabBar.sizeDelta = new Vector2(420f, 44f);
            var hlg = tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10f; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true; hlg.childControlHeight = true; hlg.childForceExpandWidth = false;

            BuildLbHeaderRow(holder, LbWidth, LbTableCenterX);
            BuildLeaderboardScroll(holder, LbWidth, 90f, LbTableCenterX, out _lbList);
            BuildRankLadderPanel(holder, LbLadderCenterX, LbLadderWidth, 90f);

            // Texte de statut (chargement / erreur / vide), au-dessus de la zone de liste (alignée table).
            _lbText = _f.MakeText("LbStatus", holder, "", _theme.FontSizeBody, _theme.TextSecondary, _theme.Font, TextAlignmentOptions.Center);
            var strt = _lbText.rectTransform;
            strt.anchorMin = new Vector2(0.5f, 0.5f); strt.anchorMax = new Vector2(0.5f, 0.5f); strt.pivot = new Vector2(0.5f, 0.5f);
            strt.sizeDelta = new Vector2(440f, 60f);
            strt.anchoredPosition = new Vector2(LbTableCenterX, -10f);

            var labels = new List<TextMeshProUGUI>();
            string[] ids = { "1v1", "2v2", "3v3" };
            for (int i = 0; i < ids.Length; i++)
            {
                var b = _f.MakeButton(tabBar, ids[i], false, out var lbl);
                ((RectTransform)b.transform).sizeDelta = new Vector2(120f, 40f);
                labels.Add(lbl);
                int idx = i;
                b.onClick.AddListener(() => SelectLbTab(idx, labels, ids));
            }
            SelectLbTab(0, labels, ids);

            _currentScreen = holder.gameObject;
        }

        private void SelectLbTab(int idx, List<TextMeshProUGUI> labels, string[] ids)
        {
            for (int i = 0; i < labels.Count; i++)
                if (labels[i] != null)
                {
                    labels[i].fontStyle = (i == idx) ? FontStyles.Bold : FontStyles.Normal;
                    labels[i].color = (i == idx) ? _theme.TextPrimary : _theme.TextSecondary;
                }

            if (idx == 0) LoadLeaderboard1v1();
            else { ClearLeaderboardRows(); ShowLbStatus($"Classement {ids[idx]} — bientôt disponible."); }
        }

        private async void LoadLeaderboard1v1()
        {
            ClearLeaderboardRows();
            ShowLbStatus("Chargement du classement...");
            if (HubLeaderboardPanel.Instance == null) { ShowLbStatus("Classement indisponible."); return; }

            var (entries, error) = await HubLeaderboardPanel.Instance.GetLeaderboardEntriesAsync(100);
            if (_currentScreenId != "leaderboard") return; // écran changé pendant le fetch
            if (entries == null) { ShowLbStatus(error); return; }

            HideLbStatus();
            RenderLeaderboardRows(entries);
        }

        private void ShowLbStatus(string s)
        {
            if (_lbText == null) return;
            _lbText.text = s;
            _lbText.gameObject.SetActive(true);
        }

        private void HideLbStatus()
        {
            if (_lbText != null) _lbText.gameObject.SetActive(false);
        }

        private void ClearLeaderboardRows()
        {
            if (_lbList == null) return;
            for (int i = _lbList.childCount - 1; i >= 0; i--)
                Destroy(_lbList.GetChild(i).gameObject);
        }

        private void RenderLeaderboardRows(LeaderboardEntry[] entries)
        {
            if (_lbList == null) return;
            ClearLeaderboardRows();
            string myId = HubLeaderboardPanel.LocalUserId;
            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                bool isMe = !string.IsNullOrEmpty(myId) && e.userId == myId;
                MakeLbRow(_lbList, e, isMe, i);
            }
        }

        // ===== Lignes du classement (cartes DA menu) =====

        private static readonly Color LbMeRowBg = new Color(0.24f, 0.21f, 0.36f, 0.96f); // ligne "toi"
        private static readonly Color LbRowAltBg = new Color(1f, 1f, 1f, 0.03f);          // ligne paire (léger)

        private void MakeLbRow(RectTransform parent, LeaderboardEntry e, bool isMe, int index)
        {
            Color bg = isMe ? LbMeRowBg : (index % 2 == 0 ? _theme.CardBg : LbRowAltBg);
            var row = _f.MakeImage("LbRow", parent, bg, rounded: true);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 46f; le.minHeight = 46f;

            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(16, 16, 0, 0);
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;

            var nameFont = isMe ? _theme.FontBold : _theme.Font;
            string pseudo = isMe ? e.displayName + "  (toi)" : e.displayName;

            LbCell(row.transform, $"#{e.position}", LbColRankW, false, PodiumColor(e.position), TextAlignmentOptions.Center, _theme.FontBold, _theme.FontSizeBody);
            LbCell(row.transform, pseudo, 0f, true, _theme.TextPrimary, TextAlignmentOptions.MidlineLeft, nameFont, _theme.FontSizeBody);
            LbRankCell(row.transform, e.mmr, LbColTierW);
            LbCell(row.transform, e.mmr.ToString(), LbColMmrW, false, _theme.TextPrimary, TextAlignmentOptions.MidlineRight, _theme.FontBold, _theme.FontSizeBody);
            LbCell(row.transform, $"{e.rankedWins}V/{e.rankedLosses}D", LbColWlW, false, _theme.TextSecondary, TextAlignmentOptions.MidlineRight, _theme.Font, _theme.FontSizeSmall);
        }

        private void BuildLbHeaderRow(RectTransform holder, float width, float centerX)
        {
            var row = _f.MakeRect("LbHeaderRow", holder);
            row.anchorMin = new Vector2(0.5f, 1f); row.anchorMax = new Vector2(0.5f, 1f); row.pivot = new Vector2(0.5f, 1f);
            row.sizeDelta = new Vector2(width, 26f);
            row.anchoredPosition = new Vector2(centerX, -54f);

            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(16, 16, 0, 0);
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;

            LbCell(row, "#", LbColRankW, false, _theme.TextMuted, TextAlignmentOptions.Center, _theme.FontBold, _theme.FontSizeSmall);
            LbCell(row, "JOUEUR", 0f, true, _theme.TextMuted, TextAlignmentOptions.MidlineLeft, _theme.FontBold, _theme.FontSizeSmall);
            LbCell(row, "RANG", LbColTierW, false, _theme.TextMuted, TextAlignmentOptions.MidlineLeft, _theme.FontBold, _theme.FontSizeSmall);
            LbCell(row, "MMR", LbColMmrW, false, _theme.TextMuted, TextAlignmentOptions.MidlineRight, _theme.FontBold, _theme.FontSizeSmall);
            LbCell(row, "V/D", LbColWlW, false, _theme.TextMuted, TextAlignmentOptions.MidlineRight, _theme.FontBold, _theme.FontSizeSmall);

            var divider = _f.MakeImage("LbHeaderDivider", holder, _theme.Divider, rounded: false);
            var drt = divider.rectTransform;
            drt.anchorMin = new Vector2(0.5f, 1f); drt.anchorMax = new Vector2(0.5f, 1f); drt.pivot = new Vector2(0.5f, 1f);
            drt.sizeDelta = new Vector2(width, 1f);
            drt.anchoredPosition = new Vector2(centerX, -82f);
            divider.raycastTarget = false;
        }

        // ===== Panneau "Paliers" (à droite du classement) : les 8 rangs + MMR à atteindre =====

        private void BuildRankLadderPanel(RectTransform holder, float centerX, float width, float topInset)
        {
            var panel = _f.MakeImage("RankLadder", holder, _theme.PanelBg, rounded: true);
            var prt = panel.rectTransform;
            prt.anchorMin = new Vector2(0.5f, 0f); prt.anchorMax = new Vector2(0.5f, 1f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(width, -(topInset + 20f));
            prt.anchoredPosition = new Vector2(centerX, (20f - topInset) / 2f);

            var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 14, 12, 12);
            vlg.spacing = 5f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            var title = _f.MakeText("Title", panel.transform, "PALIERS", _theme.FontSizeHeader, _theme.TextPrimary, _theme.FontBold, TextAlignmentOptions.Center);
            title.raycastTarget = false;
            var tle = title.gameObject.AddComponent<LayoutElement>();
            tle.preferredHeight = 34f;

            // Du plus haut (Légende) au plus bas (Bronze) — sens d'une échelle.
            for (int i = RankLadder.TierCount - 1; i >= 0; i--)
                MakeLadderRow(panel.transform, RankLadder.ByIndex(i));
        }

        private void MakeLadderRow(Transform parent, RankTier tier)
        {
            var row = _f.MakeRect("LadderRow", parent);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 30f; le.minHeight = 30f;

            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f; hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;

            var icon = _f.MakeImage("Icon", row, Color.white, rounded: false);
            icon.sprite = RankLadder.ResolveIcon(tier.MinMmr);
            icon.type = Image.Type.Simple; icon.preserveAspect = true; icon.raycastTarget = false;
            if (icon.sprite == null) icon.enabled = false;
            var ile = icon.gameObject.AddComponent<LayoutElement>();
            ile.preferredWidth = 24f; ile.preferredHeight = 24f; ile.minWidth = 24f;

            var name = _f.MakeText("Name", row, tier.Name, _theme.FontSizeSmall,
                ParseHex(tier.HexColor, _theme.TextPrimary), _theme.Font, TextAlignmentOptions.MidlineLeft);
            name.enableWordWrapping = false; name.raycastTarget = false;
            var nle = name.gameObject.AddComponent<LayoutElement>();
            nle.flexibleWidth = 1f;

            string threshold = tier.MinMmr == 0 ? "0+" : $"{tier.MinMmr}+";
            var mmr = _f.MakeText("Mmr", row, threshold, _theme.FontSizeSmall, _theme.TextSecondary, _theme.Font, TextAlignmentOptions.MidlineRight);
            mmr.enableWordWrapping = false; mmr.raycastTarget = false;
            var mle = mmr.gameObject.AddComponent<LayoutElement>();
            mle.preferredWidth = 56f; mle.minWidth = 56f;
        }

        /// <summary>Cellule "Rang" : icône du palier (gauche) + nom coloré, largeur fixe.</summary>
        private void LbRankCell(Transform parent, int mmr, float width)
        {
            var cell = _f.MakeRect("RankCell", parent);
            var le = cell.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width; le.minWidth = width; le.flexibleWidth = 0f;

            var hlg = cell.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6f; hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            var tier = RankLadder.Resolve(mmr);
            var icon = RankLadder.ResolveIcon(mmr);
            if (icon != null)
            {
                var img = _f.MakeImage("RankIcon", cell, Color.white, rounded: false);
                img.sprite = icon; img.type = Image.Type.Simple; img.preserveAspect = true;
                img.raycastTarget = false;
                var ile = img.gameObject.AddComponent<LayoutElement>();
                ile.preferredWidth = 24f; ile.preferredHeight = 24f; ile.minWidth = 24f; ile.minHeight = 24f;
            }

            var name = _f.MakeText("RankName", cell, tier.Name, _theme.FontSizeBody,
                ParseHex(tier.HexColor, _theme.TextSecondary), _theme.Font, TextAlignmentOptions.MidlineLeft);
            name.enableWordWrapping = false;
            name.overflowMode = TextOverflowModes.Ellipsis;
            var nle = name.gameObject.AddComponent<LayoutElement>();
            nle.flexibleWidth = 1f;
        }

        /// <summary>Une cellule de colonne (TMP + LayoutElement). flexible = prend l'espace restant.</summary>
        private TextMeshProUGUI LbCell(Transform parent, string text, float prefWidth, bool flexible,
            Color color, TextAlignmentOptions align, TMP_FontAsset font, float size)
        {
            var tmp = _f.MakeText("Cell", parent, text, size, color, font, align);
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            var le = tmp.gameObject.AddComponent<LayoutElement>();
            if (flexible) { le.flexibleWidth = 1f; le.preferredWidth = 0f; le.minWidth = 0f; }
            else { le.flexibleWidth = 0f; le.preferredWidth = prefWidth; le.minWidth = prefWidth; }
            return tmp;
        }

        /// <summary>Zone scrollable verticale (liste de lignes en VerticalLayoutGroup), à largeur fixe et décalage horizontal.</summary>
        private void BuildLeaderboardScroll(RectTransform holder, float width, float topInset, float centerX, out RectTransform list)
        {
            var viewport = _f.MakeRect("LbScroll", holder);
            viewport.anchorMin = new Vector2(0.5f, 0f); viewport.anchorMax = new Vector2(0.5f, 1f);
            viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.sizeDelta = new Vector2(width, -(topInset + 20f));
            viewport.anchoredPosition = new Vector2(centerX, (20f - topInset) / 2f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var sr = viewport.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 28f;
            sr.viewport = viewport;

            list = _f.MakeRect("LbList", viewport);
            list.anchorMin = new Vector2(0f, 1f); list.anchorMax = new Vector2(1f, 1f); list.pivot = new Vector2(0.5f, 1f);
            list.offsetMin = new Vector2(0f, 0f); list.offsetMax = new Vector2(0f, 0f);
            var vlg = list.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4f; vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.childForceExpandWidth = true; vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false; vlg.childControlHeight = true;
            var fit = list.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = list;
        }

        // Couleur du rang : podium or/argent/bronze, sinon gris secondaire du thème.
        private Color PodiumColor(int position) => position switch
        {
            1 => ParseHex("#ffd24a", _theme.TextSecondary),
            2 => ParseHex("#c8d0da", _theme.TextSecondary),
            3 => ParseHex("#d99a5b", _theme.TextSecondary),
            _ => _theme.TextSecondary,
        };

        private static Color ParseHex(string hex, Color fallback)
            => ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;

        // ===== M3a — Écran Personnage (style "hero menu" de réf) =====

        private void BuildPersonnage()
        {
            _pgRightContent = null;
            var holder = _f.MakeRect("Personnage", _contentArea);
            HubMenuUIFactory.Stretch(holder);
            AddBackButton(holder);

            // Sous-onglets Classe / Cosmétique (icône+label+soulignement, centrés)
            var subBar = _f.MakeRect("SubTabs", holder);
            subBar.anchorMin = new Vector2(0.5f, 1f); subBar.anchorMax = new Vector2(0.5f, 1f); subBar.pivot = new Vector2(0.5f, 1f);
            subBar.anchoredPosition = new Vector2(0f, -2f);
            subBar.sizeDelta = new Vector2(440f, 44f);
            var sbl = subBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            sbl.spacing = 24f; sbl.childAlignment = TextAnchor.MiddleCenter;
            sbl.childControlWidth = true; sbl.childControlHeight = true; sbl.childForceExpandWidth = false;
            _pgSubLabels = new List<TextMeshProUGUI>();
            _pgSubUnders = new List<Image>();
            MakeSubTab(subBar, "Classe", 0);
            MakeSubTab(subBar, "Cosmétique", 1);

            // Corps : UN seul panneau (gauche perso + droite contenu) + séparateur vertical
            var body = _f.MakeRect("Body", holder);
            body.anchorMin = new Vector2(0f, 0f); body.anchorMax = new Vector2(1f, 1f);
            body.offsetMin = new Vector2(0f, 0f); body.offsetMax = new Vector2(0f, -56f);

            var bg = _f.MakePanel(body);
            HubMenuUIFactory.Stretch(bg.rectTransform);
            var panelRT = bg.rectTransform;

            BuildClassSide(panelRT);

            var sep = _f.MakeImage("Separator", panelRT, _theme.Divider, rounded: false);
            var sert = sep.rectTransform;
            sert.anchorMin = new Vector2(0f, 0f); sert.anchorMax = new Vector2(0f, 1f); sert.pivot = new Vector2(0.5f, 0.5f);
            sert.sizeDelta = new Vector2(2f, -48f); sert.anchoredPosition = new Vector2(700f, 0f);
            sep.raycastTarget = false;

            _pgRightCol = _f.MakeRect("RightCol", panelRT);
            _pgRightCol.anchorMin = new Vector2(0f, 0f); _pgRightCol.anchorMax = new Vector2(1f, 1f);
            _pgRightCol.offsetMin = new Vector2(720f, 0f); _pgRightCol.offsetMax = new Vector2(0f, 0f);

            // Index de la classe courante
            _classIndex = 0;
            string cur = CurrentClass();
            if (_classDefinitions != null)
                for (int i = 0; i < _classDefinitions.Length; i++)
                    if (_classDefinitions[i] != null && _classDefinitions[i].ClassId.ToString() == cur) { _classIndex = i; break; }

            _currentScreen = holder.gameObject;
            ShowPersonnageTab(0);
            RefreshClassPanel();
        }

        private void MakeSubTab(RectTransform bar, string label, int idx)
        {
            var rt = _f.MakeRect("Sub_" + label, bar);
            float w = Mathf.Max(150f, label.Length * 11f + 50f);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 40f; le.preferredWidth = w;
            var hit = rt.gameObject.AddComponent<Image>(); hit.color = new Color(0f, 0f, 0f, 0f);
            var btn = rt.gameObject.AddComponent<Button>(); btn.targetGraphic = hit;

            var lbl = _f.MakeText("Label", rt, label, _theme.FontSizeBody, _theme.TextSecondary, _theme.Font, TextAlignmentOptions.Center);
            HubMenuUIFactory.Stretch(lbl.rectTransform, 8f, 8f, 0f, 6f);
            lbl.enableWordWrapping = false; lbl.raycastTarget = false;

            var und = _f.MakeImage("Underline", rt, _theme.Accent, rounded: false);
            var urt = und.rectTransform;
            urt.anchorMin = new Vector2(0.5f, 0f); urt.anchorMax = new Vector2(0.5f, 0f); urt.pivot = new Vector2(0.5f, 0f);
            urt.sizeDelta = new Vector2(w * 0.7f, 2f); urt.anchoredPosition = new Vector2(0f, 2f);
            und.enabled = false; und.raycastTarget = false;

            btn.onClick.AddListener(() => ShowPersonnageTab(idx));
            _pgSubLabels.Add(lbl);
            _pgSubUnders.Add(und);
        }

        // Côté gauche : pseudo (gros, haut-centre) + sprite idle centré + switch ‹ Nom ›
        // centré + ligne "Niv. N" (Niv. plus grand non-gras, numéro gras) collée à la barre.
        // Tout est centré horizontalement sur le sprite (cx).
        private void BuildClassSide(RectTransform root)
        {
            const float cx = 350f;

            // Pseudo en haut, centré au-dessus du sprite (gros)
            _pgPseudo = _f.MakeText("Pseudo", root, "", _theme.FontSizeTitle, _theme.TextPrimary, _theme.FontBold, TextAlignmentOptions.Center);
            _pgPseudo.enableWordWrapping = false;
            var psrt = _pgPseudo.rectTransform;
            psrt.anchorMin = new Vector2(0f, 1f); psrt.anchorMax = new Vector2(0f, 1f); psrt.pivot = new Vector2(0.5f, 1f);
            psrt.sizeDelta = new Vector2(480f, 50f); psrt.anchoredPosition = new Vector2(cx, -24f);

            // Titre équipé, sous le pseudo, italique doré (comme le tooltip avatar), même taille que le pseudo
            _pgTitle = _f.MakeText("PlayerTitle", root, "", _theme.FontSizeTitle, new Color(1f, 0.843f, 0f, 1f), _theme.Font, TextAlignmentOptions.Center);
            _pgTitle.fontStyle = FontStyles.Italic; _pgTitle.enableWordWrapping = false; _pgTitle.raycastTarget = false;
            var ptrt = _pgTitle.rectTransform;
            ptrt.anchorMin = new Vector2(0f, 1f); ptrt.anchorMax = new Vector2(0f, 1f); ptrt.pivot = new Vector2(0.5f, 1f);
            ptrt.sizeDelta = new Vector2(480f, 46f); ptrt.anchoredPosition = new Vector2(cx, -78f);

            // Sprite idle animé (centré sur cx)
            _pgPortrait = _f.MakeImage("Portrait", root, Color.white, rounded: false);
            _pgPortrait.raycastTarget = false;
            _pgPortrait.preserveAspect = true;
            var prt = _pgPortrait.rectTransform;
            prt.anchorMin = new Vector2(0f, 0f); prt.anchorMax = new Vector2(0f, 0f); prt.pivot = new Vector2(0.5f, 0f);
            prt.sizeDelta = new Vector2(340f, 460f);
            prt.anchoredPosition = new Vector2(cx, 150f);
            _pgPortraitAnchor = new Vector2(cx, 150f);
            _pgPortraitSize = new Vector2(340f, 460f);
            _pgPortraitAnim = _pgPortrait.gameObject.AddComponent<UISpriteAnimator>();
            // PATCH #2 — portrait (re)créé vierge : on remet le suivi à zéro pour forcer un rendu
            // complet au prochain RefreshClassPanel (sinon le "keep" anti-flash garderait un état
            // périmé sur une Image neuve = portrait vide).
            _pgPortraitClass = null;
            _pgPortraitSkinId = "";

            // Familier équipé, idle animé, à droite du perso près des pieds (compagnon).
            _pgPetPortrait = _f.MakeImage("PetPreview", root, Color.white, rounded: false);
            _pgPetPortrait.raycastTarget = false;
            _pgPetPortrait.preserveAspect = true;
            var petrt = _pgPetPortrait.rectTransform;
            petrt.anchorMin = new Vector2(0f, 0f); petrt.anchorMax = new Vector2(0f, 0f); petrt.pivot = new Vector2(0.5f, 0f);
            _pgPetBoxSize = new Vector2(108f, 108f);          // plus petit (était 160)
            petrt.sizeDelta = _pgPetBoxSize;
            _pgPetBoxCenter = new Vector2(cx - 95f, 235f);    // rapproché du perso + remonté près des pieds (était cx-140, 215)
            petrt.anchoredPosition = _pgPetBoxCenter;
            _pgPetPortrait.enabled = false; // affiché seulement si un familier est équipé
            _pgPetPortraitAnim = _pgPetPortrait.gameObject.AddComponent<UISpriteAnimator>();

            // Switch ‹ NomClasse › (centré sous le sprite)
            var switchRow = _f.MakeRect("ClassSwitch", root);
            switchRow.anchorMin = new Vector2(0f, 0f); switchRow.anchorMax = new Vector2(0f, 0f); switchRow.pivot = new Vector2(0.5f, 0f);
            switchRow.anchoredPosition = new Vector2(cx, 92f); switchRow.sizeDelta = new Vector2(0f, 48f);
            var hlg = switchRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10f; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            var fit = switchRow.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var chL = _f.MakeButton(switchRow, "‹", false, out _);
            chL.gameObject.GetComponent<LayoutElement>().preferredWidth = 36f;
            chL.onClick.AddListener(() => CycleClass(-1));

            _pgClassName = _f.MakeText("ClassName", switchRow, "—", _theme.FontSizeTitle, _theme.TextPrimary, _theme.FontBold, TextAlignmentOptions.Center);
            _pgClassName.enableWordWrapping = false;
            _pgClassName.gameObject.AddComponent<LayoutElement>().minWidth = 170f;

            var chR = _f.MakeButton(switchRow, "›", false, out _);
            chR.gameObject.GetComponent<LayoutElement>().preferredWidth = 36f;
            chR.onClick.AddListener(() => CycleClass(1));

            // Ligne niveau + EXP (centrée sous le switch)
            var xpRow = _f.MakeRect("XpRow", root);
            xpRow.anchorMin = new Vector2(0f, 0f); xpRow.anchorMax = new Vector2(0f, 0f); xpRow.pivot = new Vector2(0.5f, 0f);
            xpRow.anchoredPosition = new Vector2(cx, 44f); xpRow.sizeDelta = new Vector2(0f, 38f);
            var xhlg = xpRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            xhlg.spacing = 16f; xhlg.childAlignment = TextAnchor.MiddleLeft;
            xhlg.childControlWidth = true; xhlg.childControlHeight = true;
            xhlg.childForceExpandWidth = false; xhlg.childForceExpandHeight = false;
            var xfit = xpRow.gameObject.AddComponent<ContentSizeFitter>();
            xfit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            xfit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // "Niv. N" : Niv. non-gras (plus grand) + numéro gras, dans un seul TMP rich text
            _pgLevel = _f.MakeText("Level", xpRow, "Niv. 1", _theme.FontSizeHeader, _theme.TextPrimary, _theme.Font, TextAlignmentOptions.MidlineLeft);
            _pgLevel.enableWordWrapping = false;
            _pgLevel.gameObject.AddComponent<LayoutElement>().minWidth = 96f;

            var barGroup = _f.MakeRect("BarGroup", xpRow);
            var bgle = barGroup.gameObject.AddComponent<LayoutElement>();
            bgle.preferredWidth = 340f; bgle.preferredHeight = 38f;

            _pgXp = _f.MakeText("XpText", barGroup, "", _theme.FontSizeSmall, _theme.TextMuted, _theme.Font, TextAlignmentOptions.BottomLeft);
            var xprt = _pgXp.rectTransform;
            xprt.anchorMin = new Vector2(0f, 1f); xprt.anchorMax = new Vector2(1f, 1f); xprt.pivot = new Vector2(0.5f, 1f);
            xprt.sizeDelta = new Vector2(0f, 18f); xprt.anchoredPosition = new Vector2(0f, 0f);

            var xpBg = _f.MakeImage("XpBg", barGroup, new Color(1f, 1f, 1f, 0.10f));
            var xbrt = xpBg.rectTransform;
            xbrt.anchorMin = new Vector2(0f, 0f); xbrt.anchorMax = new Vector2(1f, 0f); xbrt.pivot = new Vector2(0.5f, 0f);
            xbrt.sizeDelta = new Vector2(0f, 10f); xbrt.anchoredPosition = new Vector2(0f, 2f);
            _pgXpFill = _f.MakeImage("XpFill", xbrt, _theme.Accent);
            HubMenuUIFactory.Stretch(_pgXpFill.rectTransform);
            _pgXpFill.type = Image.Type.Filled;
            _pgXpFill.fillMethod = Image.FillMethod.Horizontal;
            _pgXpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _pgXpFill.fillAmount = 0f;
            _pgXpFill.raycastTarget = false;
        }

        private void CycleClass(int dir)
        {
            if (_classDefinitions == null || _classDefinitions.Length == 0) return;
            _classIndex = (_classIndex + dir + _classDefinitions.Length) % _classDefinitions.Length;
            var def = _classDefinitions[_classIndex];
            if (def == null) return;
            string classId = def.ClassId.ToString();
            if (HubAvatar.Local != null) HubAvatar.Local.SetClassFromLocalChoice(classId);
            if (HubDeckBuilderPanel.Instance != null) HubDeckBuilderPanel.Instance.SwitchClass(classId);
            RefreshClassPanel();
            ShowPersonnageTab(_pgSubIndex); // recharge la zone droite (deck builder) pour la nouvelle classe
        }

        private void ShowPersonnageTab(int idx)
        {
            _pgSubIndex = idx;
            for (int i = 0; _pgSubLabels != null && i < _pgSubLabels.Count; i++)
            {
                if (_pgSubLabels[i] != null)
                {
                    _pgSubLabels[i].color = (i == idx) ? _theme.TextPrimary : _theme.TextSecondary;
                    _pgSubLabels[i].fontStyle = (i == idx) ? FontStyles.Bold : FontStyles.Normal;
                }
                if (_pgSubUnders != null && i < _pgSubUnders.Count && _pgSubUnders[i] != null)
                    _pgSubUnders[i].enabled = (i == idx);
            }

            if (_pgRightContent != null) Destroy(_pgRightContent);
            if (_pgRightCol == null) return;

            var content = _f.MakeRect("RightContent", _pgRightCol);
            HubMenuUIFactory.Stretch(content, 24f, 24f, 24f, 24f);

            if (idx == 0)
            {
                if (_spellCatalog != null && _api != null)
                {
                    var def = (_classDefinitions != null && _classIndex >= 0 && _classIndex < _classDefinitions.Length)
                        ? _classDefinitions[_classIndex] : null;
                    new HubMenuDeckBuilder(_theme, _f, _api, _spellCatalog).Build(content, CurrentClass(), def);
                }
                else
                    PlaceholderMsg(content, "Deck builder indisponible (SpellCatalog ou backend manquant sur HubMenuCanvas).");
            }
            else
            {
                if (_api != null)
                    new HubMenuCosmetics(_theme, _f, _api).Build(content, CurrentClass());
                else
                    PlaceholderMsg(content, "Cosmétiques indisponibles (backend manquant sur HubMenuCanvas).");
            }
            _pgRightContent = content.gameObject;
        }

        private void PlaceholderMsg(RectTransform content, string msg)
        {
            var t = _f.MakeText("Msg", content, msg, _theme.FontSizeBody, _theme.TextSecondary, _theme.Font, TextAlignmentOptions.Center);
            t.raycastTarget = false;
            var trt = t.rectTransform;
            trt.anchorMin = new Vector2(0.5f, 0.5f); trt.anchorMax = new Vector2(0.5f, 0.5f); trt.pivot = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(560f, 60f); trt.anchoredPosition = Vector2.zero;
        }

        // PATCH #2 — Pose les frames idle de classe (mode aligné) sur le portrait. Factorisé pour
        // être réutilisé par le chemin "pas de skin" d'ApplySkinToPortrait (au lieu de réinitialiser
        // systématiquement dans RefreshClassPanel, ce qui flashait le skin de base pendant le fetch).
        private void ApplyClassFramesToPortrait(NymoraClassDefinition def)
        {
            if (_pgPortrait == null) return;
            _pgPortrait.rectTransform.localScale = Vector3.one; // baseline classe (le skin override ensuite)
            if (def != null && def.IdleFrames != null && def.IdleFrames.Length > 0)
            {
                _pgPortrait.enabled = true;
                if (_pgPortraitAnim != null)
                {
                    // Centre visé = centre de la box portrait (ancre basse + demi-hauteur) ->
                    // toutes les classes centrées au même endroit, quelle que soit leur taille.
                    // Échelle calée sur la box boutique (PortraitFitBox) -> même taille qu'en boutique.
                    Vector2 boxCenter = _pgPortraitAnchor + new Vector2(0f, _pgPortraitSize.y * 0.5f);
                    _pgPortraitAnim.PlayAligned(_pgPortrait, def.IdleFrames, def.IdleFps, boxCenter, PortraitFitBox);
                }
                else _pgPortrait.sprite = def.IdleFrames[0];
            }
            else if (def != null && def.PortraitSprite != null) { _pgPortrait.enabled = true; _pgPortrait.sprite = def.PortraitSprite; }
            else _pgPortrait.enabled = false;
            _pgPortraitSkinId = ""; // frames de classe affichées (aucun skin)
        }

        private async void RefreshClassPanel()
        {
            // PATCH #2 — nouvelle generation : invalide toute completion async d'un refresh anterieur.
            int gen = ++_portraitGen;

            var def = (_classDefinitions != null && _classIndex >= 0 && _classIndex < _classDefinitions.Length)
                ? _classDefinitions[_classIndex] : null;
            string cls = def != null ? def.ClassId.ToString() : CurrentClass();

            if (_pgClassName != null) _pgClassName.text = def != null ? def.DisplayName : cls;
            if (_pgPseudo != null) _pgPseudo.text = HubChatClient.Instance?.MyDisplayName ?? "";
            if (_pgTitle != null) _pgTitle.text = HubAvatar.Local != null ? HubAvatar.Local.NetTitle.ToString() : "";

            // PATCH #2 (anti-flash) — si un SKIN est déjà affiché et qu'on ne change PAS de classe
            // (ex : simple changement de titre), on NE réinitialise PAS le portrait aux frames de
            // base pendant le fetch async : on garde le skin courant. ApplySkinToPortrait posera
            // l'état final (même skin = aucun changement visible ; skin retiré = frames de classe ;
            // skin changé = nouveau skin). Sinon (1re fois / changement de classe), on pose les
            // frames de classe comme placeholder pendant le gap.
            bool classChanged = cls != _pgPortraitClass;
            _pgPortraitClass = cls;
            bool keepCurrentDuringGap = !classChanged && !string.IsNullOrEmpty(_pgPortraitSkinId);
            if (!keepCurrentDuringGap)
                ApplyClassFramesToPortrait(def);

            // Le sprite reflète le skin équipé s'il y en a un (sinon reste sur les frames de classe).
            ApplySkinToPortrait(cls, gen);

            SetXp(1, 0, 0);
            if (_api == null) return;
            string token = HubChatClient.Instance?.DevToken;
            if (string.IsNullOrEmpty(token)) return;
            _api.SetBearerToken(token);
            var res = await _api.GetProgressionMeAsync();
            if (!res.IsSuccess || res.Data?.progressions == null) return;
            if (gen != _portraitGen || _currentScreenId != "character") return;
            foreach (var p in res.Data.progressions)
                if (p.classId == cls) { SetXp(p.level, p.xp, p.xpToNext); break; }
        }

        // Re-construit le portrait (frames classe + override skin équipé). Appelé après
        // équiper/déséquiper un cosmétique depuis l'onglet Cosmétique.
        public void RefreshClassPortrait()
        {
            if (_currentScreenId == "character" && _pgPortrait != null) RefreshClassPanel();
        }

        // Si un skin est équipé pour la classe affichée, joue SES frames idle sur le portrait.
        private async void ApplySkinToPortrait(string cls, int gen)
        {
            // _skinDefinitions peut être vide : FindSkinDef retombe sur le catalogue Resources.
            if (_api == null || _pgPortrait == null || _pgPortraitAnim == null) return;
            string token = HubChatClient.Instance?.DevToken;
            if (string.IsNullOrEmpty(token)) return;
            _api.SetBearerToken(token);
            // activeClass = classe affichée -> le familier équipé reflété est celui de CETTE classe.
            var res = await _api.GetInventoryAsync(cls);
            if (!res.IsSuccess || res.Data.items == null) return;
            // PATCH #2 — abandonne si un refresh plus recent est passe pendant l'await (sinon on
            // applique un skin/echelle perimee par-dessus le portrait courant).
            if (gen != _portraitGen || _currentScreenId != "character" || _pgPortrait == null) return;

            // Familier équipé (pas de class-lock, un seul) -> idle à côté du perso, peu importe la classe.
            string petId = null;
            string skinId = null;
            string titleName = null;
            foreach (var it in res.Data.items)
            {
                if (it == null || !it.equipped) continue;
                if (it.type == "skin" && it.classLock == cls && skinId == null) skinId = it.id;
                else if (it.type == "pet" && petId == null) petId = it.id;
                // PATCH #2 — titre equipe : resolu depuis l'inventaire FRAIS (et non NetTitle qui se
                // met a jour async via RefreshEquippedSkin). Corrige : titre qui reste apres desequip
                // / mauvais titre apres re-equip dans la prevue. Vide => plus de titre affiche.
                else if (it.type == "title" && titleName == null) titleName = it.name;
            }
            ApplyPetToPreview(petId);
            if (_pgTitle != null)
                _pgTitle.text = string.IsNullOrEmpty(titleName) ? "" : HubAvatar.ExtractTitleText(titleName);

            // PATCH #2 — pas de skin equipe -> (re)pose les frames de classe. C'est ICI (et non plus
            // systematiquement dans RefreshClassPanel) qu'on revient aux frames de base, une fois
            // l'inventaire frais connu -> pas de flash du skin de base pendant le fetch.
            if (string.IsNullOrEmpty(skinId))
            {
                ApplyClassFramesToPortrait(CurrentClassDef());
                return;
            }

            // PATCH #2 — anti-flash/anti-hitch : le bon skin est deja affiche -> rien a refaire
            // (cas typique d'un simple changement de titre, skin inchange).
            if (skinId == _pgPortraitSkinId) return;

            var skin = FindSkinDef(skinId);
            if (skin != null && skin.IdleFrames != null && skin.IdleFrames.Length > 0)
            {
                _pgPortrait.enabled = true;
                // 5.12 — MÊME rendu que les frames de classe (PlayAligned : échelle constante ajustée
                // à la box + centrage par pivot). Avant, le skin passait par Play + un boost manuel
                // (SkinPortraitBoost) calé sur Ashen -> les nouveaux skins ressortaient plus GRANDS
                // que les classes. PlayAligned normalise chaque skin à la box -> taille homogène.
                _pgPortrait.rectTransform.localScale = Vector3.one;
                // Échelle calée sur la box boutique (PortraitFitBox) + extraScale 1 (comme le shop) ->
                // le skin s'affiche à la MÊME taille que dans la boutique (plus de boost MenuPortraitScale).
                Vector2 boxCenter = _pgPortraitAnchor + new Vector2(0f, _pgPortraitSize.y * 0.5f);
                _pgPortraitAnim.PlayAligned(_pgPortrait, skin.IdleFrames, skin.IdleFps, boxCenter, PortraitFitBox);
                _pgPortraitSkinId = skinId; // skin desormais affiche (anti-flash au prochain refresh)
            }
        }

        // Affiche (ou masque) le familier équipé en idle à côté du perso. Mode aligné comme le
        // portrait (échelle constante + centré dans sa box) -> stable malgré le trim des frames.
        private void ApplyPetToPreview(string petId)
        {
            if (_pgPetPortrait == null || _pgPetPortraitAnim == null) return;
            var def = FindPetDef(petId);
            if (def == null || def.IdleFrames == null || def.IdleFrames.Length == 0)
            {
                _pgPetPortrait.enabled = false;
                return;
            }
            _pgPetPortrait.enabled = true;
            _pgPetPortraitAnim.PlayAligned(_pgPetPortrait, def.IdleFrames, def.IdleFps, _pgPetBoxCenter, _pgPetBoxSize);
        }

        // Résout un familier via le PetCatalog (Resources, chargé une fois). Même source que le hub/combat.
        private static PetDefinition FindPetDef(string cosmeticId)
        {
            if (string.IsNullOrEmpty(cosmeticId)) return null;
            if (!_pgPetCatalogLoaded)
            {
                _pgPetCatalog = Resources.Load<PetCatalog>("Cosmetics/PetCatalog");
                _pgPetCatalogLoaded = true;
            }
            return _pgPetCatalog != null ? _pgPetCatalog.Resolve(cosmeticId) : null;
        }

        private NymoraClassDefinition CurrentClassDef()
        {
            return (_classDefinitions != null && _classIndex >= 0 && _classIndex < _classDefinitions.Length)
                ? _classDefinitions[_classIndex] : null;
        }

        private static CosmeticSkinCatalog _skinCatalogFallback;
        private static bool _skinCatalogLoaded;

        private CosmeticSkinDefinition FindSkinDef(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_skinDefinitions != null)
                foreach (var s in _skinDefinitions) if (s != null && s.CosmeticId == id) return s;
            // Repli sur le catalogue global (Resources/Cosmetics/CosmeticSkinCatalog), comme
            // HubMenuShop : source UNIQUE de tous les skins -> pas besoin de recâbler le tableau
            // local _skinDefinitions par scène à chaque nouveau skin (ex : les 4 skins 5.12).
            if (!_skinCatalogLoaded)
            {
                _skinCatalogFallback = Resources.Load<CosmeticSkinCatalog>("Cosmetics/CosmeticSkinCatalog");
                _skinCatalogLoaded = true;
            }
            return _skinCatalogFallback != null ? _skinCatalogFallback.Resolve(id) : null;
        }

        private void SetXp(int level, int xp, int xpToNext)
        {
            if (_pgLevel != null) _pgLevel.text = $"Niv. <b>{level}</b>";
            if (_pgXp != null) _pgXp.text = xpToNext > 0 ? $"EXP {xp} / {xpToNext}" : "EXP MAX";
            if (_pgXpFill != null) _pgXpFill.fillAmount = xpToNext > 0 ? Mathf.Clamp01((float)xp / xpToNext) : 1f;
        }

        private static string CurrentClass()
        {
            string c = SelectedClassPreferences.Get();
            return string.IsNullOrEmpty(c) ? "Soulrender" : c;
        }

        // ===== M4 — Écran Social (Amis + Clan) =====

        private void BuildSocial()
        {
            var holder = _f.MakeRect("Social", _contentArea);
            HubMenuUIFactory.Stretch(holder);

            if (_api != null)
            {
                // Panneau centré borné (comme Personnage/Matchmaking) : la liste vit DEDANS,
                // clippée, au lieu de baver pleine largeur sur le hub.
                var panel = _f.MakePanel(holder);
                panel.sprite = HubMenuUIFactory.RoundedSprite(28f); panel.type = UnityEngine.UI.Image.Type.Sliced;
                var prt = panel.rectTransform;
                prt.anchorMin = new Vector2(0.5f, 0f); prt.anchorMax = new Vector2(0.5f, 1f); prt.pivot = new Vector2(0.5f, 0.5f);
                prt.sizeDelta = new Vector2(1040f, -8f);
                prt.anchoredPosition = Vector2.zero;
                new HubMenuSocial(_theme, _f, _api).Build(prt);
            }
            else
            {
                PlaceholderMsg(holder, "Social indisponible (backend manquant sur HubMenuCanvas).");
            }

            // Retour par-dessus, dans la marge à gauche du panneau.
            AddBackButton(holder);
            _currentScreen = holder.gameObject;
        }

        // ===== M5 — Écran Progression (Quêtes + Succès) =====

        private void BuildProgression()
        {
            var holder = _f.MakeRect("Progression", _contentArea);
            HubMenuUIFactory.Stretch(holder);

            if (_api != null)
            {
                var panel = _f.MakePanel(holder);
                panel.sprite = HubMenuUIFactory.RoundedSprite(28f); panel.type = UnityEngine.UI.Image.Type.Sliced;
                var prt = panel.rectTransform;
                prt.anchorMin = new Vector2(0.5f, 0f); prt.anchorMax = new Vector2(0.5f, 1f); prt.pivot = new Vector2(0.5f, 0.5f);
                prt.sizeDelta = new Vector2(1040f, -8f);
                prt.anchoredPosition = Vector2.zero;
                new HubMenuProgression(_theme, _f, _api).Build(prt);
            }
            else
            {
                PlaceholderMsg(holder, "Progression indisponible (backend manquant sur HubMenuCanvas).");
            }

            AddBackButton(holder);
            _currentScreen = holder.gameObject;
        }

        // ===== Écran Replays (liste des .nymrep du joueur) =====

        private void BuildReplays()
        {
            var holder = _f.MakeRect("Replays", _contentArea);
            HubMenuUIFactory.Stretch(holder);

            var panel = _f.MakePanel(holder);
            panel.sprite = HubMenuUIFactory.RoundedSprite(28f); panel.type = UnityEngine.UI.Image.Type.Sliced;
            var prt = panel.rectTransform;
            prt.anchorMin = new Vector2(0.5f, 0f); prt.anchorMax = new Vector2(0.5f, 1f); prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(1040f, -8f);
            prt.anchoredPosition = Vector2.zero;
            new HubMenuReplays(_theme, _f).Build(prt);

            AddBackButton(holder);
            _currentScreen = holder.gameObject;
        }

        // ===== M6 — Écran Paramètres (Audio + Affichage) =====

        private void BuildSettings()
        {
            var holder = _f.MakeRect("Settings", _contentArea);
            HubMenuUIFactory.Stretch(holder);

            var panel = _f.MakePanel(holder);
            panel.sprite = HubMenuUIFactory.RoundedSprite(28f); panel.type = UnityEngine.UI.Image.Type.Sliced;
            var prt = panel.rectTransform;
            prt.anchorMin = new Vector2(0.5f, 0f); prt.anchorMax = new Vector2(0.5f, 1f); prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(760f, -8f);
            prt.anchoredPosition = Vector2.zero;
            new HubMenuSettings(_theme, _f).Build(prt);

            AddBackButton(holder);
            _currentScreen = holder.gameObject;
        }

        // ===== M7b — Écran Battle Pass =====

        private void BuildBattlePass()
        {
            var holder = _f.MakeRect("BattlePass", _contentArea);
            HubMenuUIFactory.Stretch(holder);

            var title = _f.MakeText("Title", holder, "Battle Pass", _theme.FontSizeHeader, _theme.TextPrimary, _theme.FontBold, TextAlignmentOptions.Center);
            title.raycastTarget = false;
            var ttrt = title.rectTransform;
            ttrt.anchorMin = new Vector2(0.5f, 1f); ttrt.anchorMax = new Vector2(0.5f, 1f); ttrt.pivot = new Vector2(0.5f, 1f);
            ttrt.sizeDelta = new Vector2(400f, 40f); ttrt.anchoredPosition = new Vector2(0f, -8f);

            if (_api != null)
            {
                var panel = _f.MakePanel(holder);
                panel.sprite = HubMenuUIFactory.RoundedSprite(28f); panel.type = UnityEngine.UI.Image.Type.Sliced;
                var prt = panel.rectTransform;
                prt.anchorMin = new Vector2(0f, 0f); prt.anchorMax = new Vector2(1f, 1f); prt.pivot = new Vector2(0.5f, 0.5f);
                prt.offsetMin = new Vector2(0f, 0f); prt.offsetMax = new Vector2(0f, -48f);
                new HubMenuBattlePass(_theme, _f, _api).Build(prt);
            }
            else
            {
                PlaceholderMsg(holder, "Battle Pass indisponible (backend manquant sur HubMenuCanvas).");
            }

            AddBackButton(holder);
            _currentScreen = holder.gameObject;
        }

        // ===== M7a — Écran Boutique =====

        private void BuildShop()
        {
            var holder = _f.MakeRect("Shop", _contentArea);
            HubMenuUIFactory.Stretch(holder);

            // Titre (pas de sous-onglets)
            var title = _f.MakeText("Title", holder, "Boutique", _theme.FontSizeHeader, _theme.TextPrimary, _theme.FontBold, TextAlignmentOptions.Center);
            title.raycastTarget = false;
            var ttrt = title.rectTransform;
            ttrt.anchorMin = new Vector2(0.5f, 1f); ttrt.anchorMax = new Vector2(0.5f, 1f); ttrt.pivot = new Vector2(0.5f, 1f);
            ttrt.sizeDelta = new Vector2(400f, 40f); ttrt.anchoredPosition = new Vector2(0f, -8f);

            if (_api != null)
            {
                // Boutique = vitrine quasi plein écran (occupe toute la zone de contenu sous le titre)
                var panel = _f.MakePanel(holder);
                panel.sprite = HubMenuUIFactory.RoundedSprite(28f); panel.type = UnityEngine.UI.Image.Type.Sliced;
                var prt = panel.rectTransform;
                prt.anchorMin = new Vector2(0f, 0f); prt.anchorMax = new Vector2(1f, 1f); prt.pivot = new Vector2(0.5f, 0.5f);
                prt.offsetMin = new Vector2(0f, 0f); prt.offsetMax = new Vector2(0f, -48f);
                new HubMenuShop(_theme, _f, _api).Build(prt);
            }
            else
            {
                PlaceholderMsg(holder, "Boutique indisponible (backend manquant sur HubMenuCanvas).");
            }

            AddBackButton(holder);
            _currentScreen = holder.gameObject;
        }

        private void BuildHome()
        {
            var row = _f.MakeRect("Home", _contentArea);
            HubMenuUIFactory.Stretch(row);
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = _theme.CardSpacing; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            MakeHomeCard(row, "arena", "Arène", "Affronte d'autres joueurs");
            MakeHomeCard(row, "character", "Personnage", "Classe, deck, cosmétiques");
            MakeHomeCard(row, "battlepass", "Battle Pass", "Progresse et débloque");
            MakeHomeCard(row, "shop", "Boutique", "Skins et cosmétiques");
            _currentScreen = row.gameObject;
        }

        // Tuto hub (Brique B) : rects des cartes d'accueil, pour que HubTutorialDirector y pose ses
        // coach marks. Repeuplé à chaque BuildHome (les anciens GameObjects sont détruits au changement
        // d'écran ; on garde donc toujours les rects de l'écran home courant).
        private readonly System.Collections.Generic.Dictionary<string, RectTransform> _homeCards
            = new System.Collections.Generic.Dictionary<string, RectTransform>();

        /// <summary>Tuto : rect de la carte d'accueil <paramref name="id"/> (arena/character/shop...),
        /// ou null si l'écran home n'est pas affiché.</summary>
        public RectTransform GetHomeCardRect(string id)
            => _homeCards.TryGetValue(id, out var rt) && rt != null ? rt : null;

        /// <summary>Tuto : true si le menu Échap est ouvert.</summary>
        public bool IsMenuOpen => _isOpen;

        private void MakeHomeCard(RectTransform parent, string id, string title, string sub)
        {
            var btn = _f.MakeCard(parent, title, sub, out _, CardSprite(id));
            btn.onClick.AddListener(() => ShowScreen(id));
            _homeCards[id] = (RectTransform)btn.transform;
        }

        private Sprite CardSprite(string id)
        {
            if (_cardArt == null) return null;
            foreach (var c in _cardArt) if (c.Id == id) return c.Sprite;
            return null;
        }

        // ===== M8 — Report bug + Déconnexion =====

        /// <summary>Panneau de dialogue centré (titre + message + bouton Retour). Renvoie le
        /// RectTransform du panneau pour y ajouter des boutons d'action.</summary>
        private RectTransform BuildCenterDialog(string title, string message)
        {
            var holder = _f.MakeRect("Dialog", _contentArea);
            HubMenuUIFactory.Stretch(holder);

            var panel = _f.MakePanel(holder);
            panel.sprite = HubMenuUIFactory.RoundedSprite(28f); panel.type = Image.Type.Sliced;
            var prt = panel.rectTransform;
            prt.anchorMin = new Vector2(0.5f, 0.5f); prt.anchorMax = new Vector2(0.5f, 0.5f); prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(640f, 340f); prt.anchoredPosition = Vector2.zero;

            var t = _f.MakeText("Title", prt, title, _theme.FontSizeTitle, _theme.TextPrimary, _theme.FontBold, TextAlignmentOptions.Center);
            t.raycastTarget = false;
            var trt = t.rectTransform;
            trt.anchorMin = new Vector2(0.5f, 1f); trt.anchorMax = new Vector2(0.5f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.sizeDelta = new Vector2(580f, 48f); trt.anchoredPosition = new Vector2(0f, -44f);

            var m = _f.MakeText("Msg", prt, message, _theme.FontSizeBody, _theme.TextSecondary, _theme.Font, TextAlignmentOptions.Center);
            m.raycastTarget = false;
            var mrt = m.rectTransform;
            mrt.anchorMin = new Vector2(0.5f, 0.5f); mrt.anchorMax = new Vector2(0.5f, 0.5f); mrt.pivot = new Vector2(0.5f, 0.5f);
            mrt.sizeDelta = new Vector2(560f, 110f); mrt.anchoredPosition = new Vector2(0f, 16f);

            AddBackButton(holder);
            _currentScreen = holder.gameObject;
            return prt;
        }

        private void BuildReport()
        {
            Application.OpenURL(BugReportUrl);
            var panel = BuildCenterDialog("Report bug",
                "Le Discord de Nymora s'ouvre dans ton navigateur.\nPoste ton bug dans la section dédiée — merci !");

            var btn = _f.MakeButton(panel, "Ouvrir le Discord", true, out _);
            var brt = (RectTransform)btn.transform;
            brt.anchorMin = new Vector2(0.5f, 0f); brt.anchorMax = new Vector2(0.5f, 0f); brt.pivot = new Vector2(0.5f, 0f);
            brt.sizeDelta = new Vector2(240f, 46f); brt.anchoredPosition = new Vector2(0f, 34f);
            btn.onClick.AddListener(() => Application.OpenURL(BugReportUrl));
        }

        private void BuildLogout()
        {
            var panel = BuildCenterDialog("Déconnexion",
                "Se déconnecter et revenir à l'écran de connexion ?");

            var row = _f.MakeRect("Btns", panel);
            row.anchorMin = new Vector2(0.5f, 0f); row.anchorMax = new Vector2(0.5f, 0f); row.pivot = new Vector2(0.5f, 0f);
            row.sizeDelta = new Vector2(460f, 50f); row.anchoredPosition = new Vector2(0f, 30f);
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 16f; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            var cancel = _f.MakeButton(row, "Annuler", false, out _);
            cancel.gameObject.GetComponent<LayoutElement>().preferredWidth = 180f;
            cancel.onClick.AddListener(() => ShowScreen("home"));

            var confirm = _f.MakeButton(row, "Se déconnecter", false, out var cl);
            confirm.gameObject.GetComponent<LayoutElement>().preferredWidth = 220f;
            var cimg = confirm.GetComponent<Image>(); if (cimg != null) cimg.color = Color.white;
            var cc = confirm.colors;
            var red = new Color(0.55f, 0.27f, 0.27f, 1f);
            cc.normalColor = red; cc.highlightedColor = new Color(0.66f, 0.33f, 0.33f, 1f);
            cc.pressedColor = new Color(0.48f, 0.22f, 0.22f, 1f); cc.selectedColor = red; cc.fadeDuration = 0.1f;
            confirm.colors = cc; cl.color = Color.white;
            confirm.onClick.AddListener(DoLogout);
        }

        private void DoLogout()
        {
            // Efface la session (JWT en PlayerPrefs) proprement via AuthService.
            if (_api != null) { try { new AuthService(_api).Logout(); } catch { } }
            _isOpen = false;

            // FIX double-perso : on ferme le NetworkRunner Fusion PROPREMENT (= leave de la room
            // Photon) AVANT de revenir au login. Sans ça, détruire le runner via l'unload de scène
            // ne notifie pas toujours le serveur à temps : l'avatar reste "zombie" côté Photon
            // le temps du timeout, et à la reconnexion rapide le joueur récupère son ancien perso
            // EN PLUS de son nouveau spawn (= double, invisible pour les autres une fois le zombie
            // expiré). ALT+F4 ne souffre pas du bug car l'OS ferme le socket d'un coup (despawn
            // immédiat). On capture le runner AVANT la transition (la scène hub sera unloadée).
            var runner = FindFirstObjectByType<NetworkRunner>();

            // Le shutdown réseau tourne sous le voile opaque (hook whileCovered) -> jamais visible.
            SceneTransition.LoadAsync(LoginSceneName, async () =>
            {
                if (runner != null)
                {
                    try { await runner.Shutdown(); }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[Logout] Shutdown runner Fusion a échoué : {ex.Message} — on continue.");
                    }
                }
                // Client de chat/session (DontDestroyOnLoad) : détruit pour repartir propre au login.
                if (HubChatClient.Instance != null) Destroy(HubChatClient.Instance.gameObject);
            });
        }

        private void BuildPlaceholder(string id)
        {
            var panel = _f.MakePanel(_contentArea);
            var prt = panel.rectTransform;
            prt.anchorMin = new Vector2(0.5f, 0.5f); prt.anchorMax = new Vector2(0.5f, 0.5f); prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(740f, 420f);
            prt.anchoredPosition = Vector2.zero;

            var title = _f.MakeText("Title", prt, TitleFor(id), _theme.FontSizeTitle, _theme.TextPrimary, _theme.FontBold, TextAlignmentOptions.Center);
            title.raycastTarget = false;
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0.5f, 1f); trt.anchorMax = new Vector2(0.5f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -48f); trt.sizeDelta = new Vector2(660f, 50f);

            var msg = _f.MakeText("Msg", prt, "Contenu à venir — on le construit dans une prochaine brique.", _theme.FontSizeBody, _theme.TextSecondary, _theme.Font, TextAlignmentOptions.Center);
            msg.raycastTarget = false;
            var mrt = msg.rectTransform;
            mrt.anchorMin = new Vector2(0.5f, 0.5f); mrt.anchorMax = new Vector2(0.5f, 0.5f); mrt.pivot = new Vector2(0.5f, 0.5f);
            mrt.anchoredPosition = Vector2.zero; mrt.sizeDelta = new Vector2(640f, 60f);

            var back = _f.MakeButton(prt, "‹ Retour", false, out _);
            var brt = (RectTransform)back.transform;
            brt.anchorMin = new Vector2(0.5f, 0f); brt.anchorMax = new Vector2(0.5f, 0f); brt.pivot = new Vector2(0.5f, 0f);
            brt.anchoredPosition = new Vector2(0f, 40f); brt.sizeDelta = new Vector2(200f, 46f);
            back.onClick.AddListener(() => ShowScreen("home"));

            _currentScreen = panel.gameObject;
        }

        private static string TitleFor(string id)
        {
            switch (id)
            {
                case "arena": return "Arène";
                case "character": return "Personnage";
                case "battlepass": return "Battle Pass";
                case "shop": return "Boutique";
                case "social": return "Social";
                case "progression": return "Progression";
                case "replays": return "Replays";
                case "settings": return "Paramètres";
                case "report": return "Report bug";
                case "logout": return "Déconnexion";
                default: return id;
            }
        }

    }
}
