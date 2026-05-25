using System.Collections.Generic;
using Nymora.Core.Data;
using Nymora.Core.SceneFlow;
using Nymora.Core.ScriptableObjects;
using Nymora.Network.Backend;
using TMPro;
using UnityEngine;
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
    public sealed class HubMenuShell : MonoBehaviour
    {
        [SerializeField] private HubMenuTheme _theme;
        [SerializeField] private NymoraBackendSettings _backendSettings;
        [SerializeField] private NymoraClassDefinition[] _classDefinitions;
        [SerializeField] private SpellCatalog _spellCatalog;
        [SerializeField] private CosmeticSkinDefinition[] _skinDefinitions;
        [SerializeField] private CardArt[] _cardArt;

        [System.Serializable]
        public struct CardArt { public string Id; public Sprite Sprite; }

        // Boost d'échelle du sprite quand un skin est équipé (frames natives + petites que la classe).
        private const float SkinPortraitBoost = 1.4f;

        // M8 — Lien Discord Nymora (section report de bug), ouvert dans le navigateur.
        private const string BugReportUrl = "https://discord.gg/haRtXKCERx";

        // Scène de connexion (retour sur Déconnexion).
        private const string LoginSceneName = "00_Login";

        public static HubMenuShell Instance { get; private set; }

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
        private TextMeshProUGUI _lbText;

        // M3 — Personnage
        private NymoraApiClient _api;
        private TextMeshProUGUI _pgClassName, _pgPseudo, _pgTitle, _pgLevel, _pgXp;
        private Image _pgXpFill;
        private Image _pgPortrait;
        private UISpriteAnimator _pgPortraitAnim;
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
            if (Input.GetKeyDown(KeyCode.Escape)) Toggle();
        }

        // ===== Ouverture / fermeture =====

        public void Toggle() { if (_isOpen) Close(); else Open(); }

        public void Open()
        {
            if (_isOpen || _menuRoot == null) return;
            _isOpen = true;
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

            MakeModeCard(row, "Entraînement", "Affronte l'IA", true,
                () => { Close(); if (HubArenaPanel.Instance != null) HubArenaPanel.Instance.StartTraining(); }, "mode_training");
            MakeModeCard(row, "Ranked 1v1", "Classé · 1 contre 1", true,
                () => ShowScreen("matchmaking"), "mode_1v1");
            MakeModeCard(row, "Ranked 2v2", "Bientôt disponible", false, null, "mode_2v2");
            MakeModeCard(row, "Ranked 3v3", "Bientôt disponible", false, null, "mode_3v3");

            _currentScreen = holder.gameObject;
        }

        private void MakeModeCard(RectTransform parent, string title, string sub, bool enabled, System.Action onClick, string cardKey = null)
        {
            var btn = _f.MakeCard(parent, title, sub, out _, CardSprite(cardKey));
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

            BuildScroll(holder, out _lbText);

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
            else SetLb($"Classement {ids[idx]} — bientôt disponible.");
        }

        private async void LoadLeaderboard1v1()
        {
            SetLb("Chargement du classement...");
            if (HubLeaderboardPanel.Instance == null) { SetLb("Classement indisponible."); return; }
            var txt = await HubLeaderboardPanel.Instance.GetLeaderboardTextAsync(100);
            if (_currentScreenId == "leaderboard") SetLb(txt);
        }

        private void SetLb(string s) { if (_lbText != null) _lbText.text = s; }

        /// <summary>Zone scrollable verticale avec un TMP de contenu (auto-dimensionné).</summary>
        private void BuildScroll(RectTransform parent, out TextMeshProUGUI content)
        {
            var viewport = _f.MakeRect("Scroll", parent);
            viewport.anchorMin = new Vector2(0f, 0f); viewport.anchorMax = new Vector2(1f, 1f);
            viewport.offsetMin = new Vector2(40f, 20f);
            viewport.offsetMax = new Vector2(-40f, -54f); // sous les onglets
            var vpImg = viewport.gameObject.AddComponent<Image>();
            vpImg.color = new Color(1f, 1f, 1f, 0.02f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var sr = viewport.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 24f;
            sr.viewport = viewport;

            content = _f.MakeText("Lines", viewport, "", _theme.FontSizeBody, _theme.TextPrimary, _theme.Font, TextAlignmentOptions.TopLeft);
            var crt = content.rectTransform;
            crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f); crt.pivot = new Vector2(0.5f, 1f);
            crt.offsetMin = new Vector2(10f, 0f); crt.offsetMax = new Vector2(-10f, 0f);
            content.enableWordWrapping = true;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = crt;
        }

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
            _pgPortraitAnim = _pgPortrait.gameObject.AddComponent<UISpriteAnimator>();

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

        private async void RefreshClassPanel()
        {
            var def = (_classDefinitions != null && _classIndex >= 0 && _classIndex < _classDefinitions.Length)
                ? _classDefinitions[_classIndex] : null;
            string cls = def != null ? def.ClassId.ToString() : CurrentClass();

            if (_pgClassName != null) _pgClassName.text = def != null ? def.DisplayName : cls;
            if (_pgPseudo != null) _pgPseudo.text = HubChatClient.Instance?.MyDisplayName ?? "";
            if (_pgTitle != null) _pgTitle.text = HubAvatar.Local != null ? HubAvatar.Local.NetTitle.ToString() : "";

            if (_pgPortrait != null)
            {
                _pgPortrait.rectTransform.localScale = Vector3.one; // baseline classe (le skin override ensuite)
                if (def != null && def.IdleFrames != null && def.IdleFrames.Length > 0)
                {
                    _pgPortrait.enabled = true;
                    if (_pgPortraitAnim != null) _pgPortraitAnim.Play(_pgPortrait, def.IdleFrames, def.IdleFps);
                    else _pgPortrait.sprite = def.IdleFrames[0];
                }
                else if (def != null && def.PortraitSprite != null) { _pgPortrait.enabled = true; _pgPortrait.sprite = def.PortraitSprite; }
                else _pgPortrait.enabled = false;
            }

            // Le sprite reflète le skin équipé s'il y en a un (sinon reste sur les frames de classe).
            ApplySkinToPortrait(cls);

            SetXp(1, 0, 0);
            if (_api == null) return;
            string token = HubChatClient.Instance?.DevToken;
            if (string.IsNullOrEmpty(token)) return;
            _api.SetBearerToken(token);
            var res = await _api.GetProgressionMeAsync();
            if (!res.IsSuccess || res.Data?.progressions == null) return;
            if (_currentScreenId != "character") return;
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
        private async void ApplySkinToPortrait(string cls)
        {
            if (_api == null || _skinDefinitions == null || _pgPortrait == null || _pgPortraitAnim == null) return;
            string token = HubChatClient.Instance?.DevToken;
            if (string.IsNullOrEmpty(token)) return;
            _api.SetBearerToken(token);
            var res = await _api.GetInventoryAsync();
            if (!res.IsSuccess || res.Data.items == null) return;
            if (_currentScreenId != "character" || _pgPortrait == null) return;

            string skinId = null;
            foreach (var it in res.Data.items)
                if (it != null && it.equipped && it.type == "skin" && it.classLock == cls) { skinId = it.id; break; }
            if (string.IsNullOrEmpty(skinId)) return; // pas de skin -> on garde les frames de classe

            var skin = FindSkinDef(skinId);
            if (skin != null && skin.IdleFrames != null && skin.IdleFrames.Length > 0)
            {
                _pgPortrait.enabled = true;
                _pgPortraitAnim.Play(_pgPortrait, skin.IdleFrames, skin.IdleFps);
                // Les frames de skin sont souvent plus petites (contenu natif) -> boost d'échelle
                // pour matcher la taille du sprite de classe. SkinPortraitBoost ajustable.
                var cdef = CurrentClassDef();
                float classScale = (cdef != null && cdef.HubVisualScale > 0f) ? cdef.HubVisualScale : 1f;
                float skinScale = skin.HubVisualScale > 0f ? skin.HubVisualScale : 1f;
                _pgPortrait.rectTransform.localScale = Vector3.one * (skinScale / classScale) * SkinPortraitBoost;
            }
        }

        private NymoraClassDefinition CurrentClassDef()
        {
            return (_classDefinitions != null && _classIndex >= 0 && _classIndex < _classDefinitions.Length)
                ? _classDefinitions[_classIndex] : null;
        }

        private CosmeticSkinDefinition FindSkinDef(string id)
        {
            if (_skinDefinitions == null) return null;
            foreach (var s in _skinDefinitions) if (s != null && s.CosmeticId == id) return s;
            return null;
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

        private void MakeHomeCard(RectTransform parent, string id, string title, string sub)
        {
            var btn = _f.MakeCard(parent, title, sub, out _, CardSprite(id));
            btn.onClick.AddListener(() => ShowScreen(id));
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
            // Le client de chat/session est DontDestroyOnLoad : on le détruit pour repartir
            // propre au login (le NetworkRunner Fusion, lui, meurt avec la scène hub).
            if (HubChatClient.Instance != null) Destroy(HubChatClient.Instance.gameObject);
            _isOpen = false;
            SceneTransition.Load(LoginSceneName);
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
                case "settings": return "Paramètres";
                case "report": return "Report bug";
                case "logout": return "Déconnexion";
                default: return id;
            }
        }

    }
}
