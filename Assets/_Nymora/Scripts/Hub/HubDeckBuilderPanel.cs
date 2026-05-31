using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Nymora.Core.Data;
using Nymora.Core.Enums;
using Nymora.Core.ScriptableObjects;
using Nymora.Network.Backend;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 5.3.e — Deck Builder UI complet.
    ///
    /// Pipeline :
    ///   1. Open : fetch /decks?classId={_currentClassId} + lit SpellCatalog
    ///   2. Render Header (nom classe + signature read-only)
    ///   3. Render 6 slots horizontaux (toggle remove on click)
    ///   4. Render Grid 15 sorts non-signature de la classe (toggle add on click)
    ///   5. Render Liste decks save (max 5) — click item = load deck dans le builder
    ///   6. Boutons : Nouveau / Save / Renommer / Supprimer + champ Name
    ///
    /// Constraints Bible V7.1 :
    ///   - 6 sorts par deck (signature auto-equipe, slot separe)
    ///   - 5 decks max par classe (cap backend)
    ///   - Tous les sorts dispo des le start (pas de gating level)
    ///
    /// WS handler OnDeckChanged : re-fetch quand un deck est cree/update/delete.
    /// Class Selector (changer classe) : 5.3.f.
    /// </summary>
    public sealed class HubDeckBuilderPanel : MonoBehaviour
    {
        [Header("Backend")]
        [SerializeField] private NymoraBackendSettings _backendSettings;

        [Header("Data")]
        [SerializeField] private SpellCatalog _spellCatalog;

        [Header("Root")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private Button _closeButton;

        [Header("Header")]
        [SerializeField] private TextMeshProUGUI _classNameLabel;
        [SerializeField] private TextMeshProUGUI _signatureLabel;
        [SerializeField] private Button _changeClassButton; // 5.3.f wire

        [Header("Slots row (6 sorts equipes)")]
        [SerializeField] private RectTransform _slotsRow;

        [Header("Onglets categorie (5.3.e refactor onglets)")]
        [SerializeField] private RectTransform _categoryTabsRow;

        [Header("Grid 5 sorts de l'onglet actif")]
        [SerializeField] private RectTransform _spellsGrid;

        [Header("Decks sidebar")]
        [SerializeField] private RectTransform _decksList;
        [SerializeField] private TMP_InputField _deckNameInput;
        [SerializeField] private Button _newDeckButton;
        [SerializeField] private Button _saveDeckButton;
        [SerializeField] private Button _deleteDeckButton;
        [SerializeField] private TextMeshProUGUI _statusLabel;

        [Header("Tooltip (5.3.e.iii basique)")]
        [SerializeField] private GameObject _tooltipPanel;
        [SerializeField] private TextMeshProUGUI _tooltipText;

        [Header("Visuals")]
        [SerializeField] private Color _slotEmptyColor = new Color(0.22f, 0.22f, 0.26f, 1f);
        [SerializeField] private Color _slotFilledColor = new Color(0.30f, 0.45f, 0.55f, 1f);
        [SerializeField] private Color _spellGridUnselectedColor = new Color(0.18f, 0.20f, 0.25f, 1f);
        [SerializeField] private Color _spellGridSelectedColor = new Color(0.35f, 0.55f, 0.40f, 1f);
        [SerializeField] private Color _deckItemBg = new Color(0.18f, 0.20f, 0.25f, 1f);
        [SerializeField] private Color _deckItemActiveBg = new Color(0.30f, 0.45f, 0.55f, 1f);
        // Couleurs par categorie (headers de sections)
        [SerializeField] private Color _categoryOffensiveColor = new Color(0.55f, 0.28f, 0.28f, 1f);
        [SerializeField] private Color _categoryTacticalColor = new Color(0.30f, 0.45f, 0.60f, 1f);
        [SerializeField] private Color _categorySurvivalColor = new Color(0.30f, 0.50f, 0.32f, 1f);

        public static HubDeckBuilderPanel Instance { get; private set; }

        // ====== State ======
        private NymoraApiClient _api;
        private bool _hasFetchedOnce;
        // Default fallback "Soulrender" — override au Awake par SelectedClassPreferences.Get()
        // pour restaurer la classe choisie pre-deconnexion (sinon retour systematique
        // Soulrender en post-reco alors que l'avatar hub affiche la bonne classe).
        private string _currentClassId = "Soulrender";
        private readonly List<DeckDto> _myDecks = new List<DeckDto>();

        // Composition courante (deck en cours d'edition)
        private readonly string[] _slotSpellIds = new string[6]; // null = vide
        private string _editingDeckId; // null = nouveau deck

        // Onglet actif (categorie courante affichee dans la grid)
        private SpellCategory _activeCategory = SpellCategory.Offensive;

        // Runtime spawned UI objects (pour cleanup avant re-render)
        private readonly List<GameObject> _spawnedSlots = new List<GameObject>();
        private readonly List<GameObject> _spawnedTabs = new List<GameObject>();
        private readonly List<GameObject> _spawnedSpells = new List<GameObject>();
        private readonly List<GameObject> _spawnedDeckItems = new List<GameObject>();

        public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;
        public string CurrentClassId => _currentClassId;
        public IReadOnlyList<DeckDto> MyDecks => _myDecks;

        /// <summary>
        /// 18 mai — Deck actuellement selectionne par l'utilisateur dans la liste (clique
        /// en dernier). Si aucun deck selectionne, fallback sur MyDecks[0] (premier cree).
        /// Utilise par HubArenaPanel + HubMatchTransition pour lancer le combat avec
        /// la BONNE composition de sorts (au lieu de MyDecks[0] systematique).
        /// </summary>
        public DeckDto SelectedDeck
        {
            get
            {
                if (_myDecks == null || _myDecks.Count == 0) return null;
                if (!string.IsNullOrEmpty(_editingDeckId))
                {
                    var sel = _myDecks.Find(d => d.id == _editingDeckId);
                    if (sel != null) return sel;
                }
                return _myDecks[0];
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            if (_backendSettings == null)
            {
                Debug.LogError("[DeckBuilderPanel] _backendSettings non assigne — panel desactive.");
                enabled = false;
                return;
            }
            // Restaure la classe selectionnee pre-deco (sinon Soulrender hardcode par defaut).
            // Cohere avec l'avatar hub qui lit deja SelectedClassPreferences.Get() au Spawn.
            _currentClassId = SelectedClassPreferences.Get();
            _api = new NymoraApiClient(_backendSettings);
            if (_panelRoot != null) _panelRoot.SetActive(false);
            if (_tooltipPanel != null) _tooltipPanel.SetActive(false);

            // Polish UI (fix 18 mai) : init layout DecksList + style input + label bouton Save.
            EnsureDecksListLayout();
            EnsureNameInputStyle();
            UpdateSaveButtonLabel();
        }

        private void OnEnable()
        {
            if (_closeButton != null) _closeButton.onClick.AddListener(Close);
            if (_newDeckButton != null) _newDeckButton.onClick.AddListener(OnNewDeckClicked);
            if (_saveDeckButton != null) _saveDeckButton.onClick.AddListener(OnSaveClicked);
            if (_deleteDeckButton != null) _deleteDeckButton.onClick.AddListener(OnDeleteClicked);
            if (_changeClassButton != null) _changeClassButton.onClick.AddListener(OnChangeClassClicked);
        }

        private void OnDisable()
        {
            if (_closeButton != null) _closeButton.onClick.RemoveAllListeners();
            if (_newDeckButton != null) _newDeckButton.onClick.RemoveAllListeners();
            if (_saveDeckButton != null) _saveDeckButton.onClick.RemoveAllListeners();
            if (_deleteDeckButton != null) _deleteDeckButton.onClick.RemoveAllListeners();
            if (_changeClassButton != null) _changeClassButton.onClick.RemoveAllListeners();
        }

        private void OnChangeClassClicked()
        {
            if (HubClassSelectorPanel.Instance == null)
            {
                Debug.LogWarning("[DeckBuilderPanel] HubClassSelectorPanel.Instance null — lance Nymora > Setup > Patch Class Selector Panel.");
                return;
            }
            HubClassSelectorPanel.Instance.Open();
        }

        private void Start()
        {
            if (HubChatClient.Instance != null)
            {
                HubChatClient.Instance.OnDeckChanged += HandleDeckChanged;
                // POLISH-7 polish (20 mai) — pre-fetch au login pour que SelectedDeck (consume
                // par HubArenaPanel + HubMatchTransition) soit dispo SANS qu'il faille ouvrir
                // le Deck Builder. Sinon, clic direct Arena post-login -> MyDecks vide ->
                // SelectedDeck null -> fallback Soulrender deck #0 au lieu du dernier deck
                // utilise par Lorenzo. Pattern miroir HubWalletWidget.
                HubChatClient.Instance.OnWelcome += HandleWelcomePreFetch;
                if (!string.IsNullOrEmpty(HubChatClient.Instance.MyUserId))
                {
                    // WELCOME deja recu (Start arrive apres) -> fetch direct.
                    FetchDecksAsync().Forget();
                }
            }
        }

        private void HandleWelcomePreFetch(string sub, string email, string displayName)
        {
            if (_hasFetchedOnce) return;
            FetchDecksAsync().Forget();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (HubChatClient.Instance != null)
            {
                HubChatClient.Instance.OnDeckChanged -= HandleDeckChanged;
                HubChatClient.Instance.OnWelcome -= HandleWelcomePreFetch;
            }
        }

        public void Open()
        {
            if (_panelRoot == null) return;
            _panelRoot.SetActive(true);
            SetArenaButtonVisible(false); // fix 18 mai : arena pas pertinent en mode Deck Builder
            // POLISH-7 polish (20 mai) : le wallet widget (Nymos/Shards en haut-droite) chevauchait
            // visuellement le panel Deck Builder. Masque le temps de l'edition (les balances
            // restent visibles via l'onglet Wallet du profil).
            SetWalletWidgetVisible(false);
            if (!_hasFetchedOnce) FetchDecksAsync().Forget();
            else RenderAll();
        }

        public void Close()
        {
            UiPanelAnimator.CloseAnimated(_panelRoot);
            SetArenaButtonVisible(true);
            SetWalletWidgetVisible(true);
            HideTooltip();
        }

        /// <summary>
        /// Cache/affiche le bouton Arena du hub. Appele au Open/Close du DeckBuilder pour
        /// eviter que le bouton Arena chevauche le panel (et n'a pas de sens en mode edition
        /// de deck). Find one-shot (negligeable perf), idempotent si bouton absent.
        /// </summary>
        private static void SetArenaButtonVisible(bool visible)
        {
            var arenaBtn = Object.FindAnyObjectByType<HubArenaButton>(FindObjectsInactive.Include);
            if (arenaBtn != null) arenaBtn.gameObject.SetActive(visible);
        }

        /// <summary>
        /// POLISH-7 polish (20 mai) — cache/affiche le wallet widget (Nymos + Shards en
        /// haut-droite du hub). Meme mecanique que SetArenaButtonVisible : evite que le
        /// widget chevauche visuellement le panel Deck Builder. Les balances restent
        /// consultables via l'onglet Wallet du profil pendant l'edition.
        /// </summary>
        private static void SetWalletWidgetVisible(bool visible)
        {
            if (HubWalletWidget.Instance != null)
            {
                HubWalletWidget.Instance.gameObject.SetActive(visible);
            }
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void SwitchClass(string classId)
        {
            if (string.IsNullOrEmpty(classId) || classId == _currentClassId) return;
            _currentClassId = classId;
            ClearComposition();
            FetchDecksAsync().Forget();
        }

        /// <summary>
        /// 4.14.e hotfix — Sync `_currentClassId` sur `classId` ET await le fetch backend
        /// avant que l'appelant lise `MyDecks`. Utilise par HubMatchTransition pour
        /// recuperer les decks de la CLASSE SELECTIONNEE (SelectedClassPreferences) au
        /// moment de l'accept du defi, sinon `MyDecks[0]` pioche le premier deck de la
        /// classe qui se trouve etre ouverte dans le DeckBuilder (souvent Soulrender default).
        /// </summary>
        public async UniTask EnsureClassLoadedAsync(string classId)
        {
            if (string.IsNullOrEmpty(classId)) return;
            // Si deja la bonne classe ET deja fetchee, no-op.
            if (_currentClassId == classId && _hasFetchedOnce) return;
            _currentClassId = classId;
            ClearComposition();
            await FetchDecksAsync();
        }

        /// <summary>
        /// Lobby pré-combat (B1) — Récupère le MMR du joueur local via /profile/me, pour
        /// l'afficher dans le lobby et le diffuser en P2P à l'adversaire (PreCombatBridge.LocalMmr).
        /// Réutilise le NymoraApiClient + le token déjà gérés par ce panel. Retourne 0 si échec
        /// (le lobby affichera alors un MMR neutre, non bloquant).
        /// </summary>
        public async UniTask<int> FetchLocalMmrAsync()
        {
            string token = ResolveDevToken();
            if (string.IsNullOrEmpty(token)) return 0;
            _api.SetBearerToken(token);
            var res = await _api.GetProfileMeAsync();
            return res.IsSuccess && res.Data != null ? res.Data.mmr : 0;
        }

        /// <summary>
        /// 31 mai — Comme FetchLocalMmrAsync mais retourne aussi rankedGames (pour le K-factor du
        /// preview ELO du menu de fin de combat). Un seul appel /profile/me. (0,0) si échec.
        /// </summary>
        public async UniTask<(int mmr, int rankedGames)> FetchLocalProfileAsync()
        {
            string token = ResolveDevToken();
            if (string.IsNullOrEmpty(token)) return (0, 0);
            _api.SetBearerToken(token);
            var res = await _api.GetProfileMeAsync();
            return res.IsSuccess && res.Data != null ? (res.Data.mmr, res.Data.rankedGames) : (0, 0);
        }

        /// <summary>
        /// M3b (25 mai) — Sélectionne un deck depuis le nouveau menu (HubMenuDeckBuilder).
        /// Synchronise _editingDeckId + composition + pref, pour que SelectedDeck (lu par
        /// HubArenaPanel / HubMatchTransition au lancement du combat) pointe sur le bon deck,
        /// même si l'édition se fait dans le nouveau menu et plus dans ce panneau.
        /// </summary>
        public async UniTask SetActiveDeckAsync(string classId, string deckId)
        {
            if (string.IsNullOrEmpty(classId) || string.IsNullOrEmpty(deckId)) return;
            await EnsureClassLoadedAsync(classId);
            var deck = _myDecks.Find(d => d.id == deckId);
            if (deck == null)
            {
                _currentClassId = classId;
                await FetchDecksAsync();
                deck = _myDecks.Find(d => d.id == deckId);
            }
            if (deck == null) return;
            _editingDeckId = deckId;
            for (int i = 0; i < 6; i++)
                _slotSpellIds[i] = (deck.spellIds != null && i < deck.spellIds.Length) ? deck.spellIds[i] : null;
            SelectedClassPreferences.SetLastEditedDeckId(classId, deckId);
            if (_deckNameInput != null) _deckNameInput.text = deck.name;
            RenderAll();
        }

        // ====== Fetch decks ======

        private async UniTask FetchDecksAsync()
        {
            SetStatus($"Chargement {_currentClassId}...");
            string token = ResolveDevToken();
            if (string.IsNullOrEmpty(token))
            {
                SetStatus("Pas de JWT (HubChatClient._devToken vide).");
                return;
            }
            _api.SetBearerToken(token);

            var res = await _api.GetDecksAsync(_currentClassId);
            if (!res.IsSuccess)
            {
                SetStatus($"Erreur {res.StatusCode} : {res.ErrorMessage}");
                return;
            }
            _myDecks.Clear();
            if (res.Data.decks != null) foreach (var d in res.Data.decks) _myDecks.Add(d);
            _hasFetchedOnce = true;

            // Restaure le dernier deck en edition (PlayerPrefs par classe) s'il existe
            // encore cote backend. Permet a Lorenzo de retomber pile sur le deck qu'il
            // editait avant deco au lieu de partir d'une compo vide.
            TryRestoreLastEditedDeck();

            RenderAll();
        }

        private void TryRestoreLastEditedDeck()
        {
            // Si un deck est deja selectionne (l'utilisateur a cliqu dans la liste depuis
            // l'ouverture du panel), ne pas l'ecraser.
            if (!string.IsNullOrEmpty(_editingDeckId)) return;
            string lastId = SelectedClassPreferences.GetLastEditedDeckId(_currentClassId);
            if (string.IsNullOrEmpty(lastId)) return;
            var deck = _myDecks.Find(d => d.id == lastId);
            if (deck == null)
            {
                // Le deck a ete supprime cote backend depuis la derniere session — clean.
                SelectedClassPreferences.ClearLastEditedDeckId(_currentClassId);
                return;
            }
            _editingDeckId = deck.id;
            for (int i = 0; i < 6; i++)
                _slotSpellIds[i] = (deck.spellIds != null && i < deck.spellIds.Length) ? deck.spellIds[i] : null;
            if (_deckNameInput != null) _deckNameInput.text = deck.name;
        }

        // ====== Render ======

        private void RenderAll()
        {
            RenderHeader();
            RenderSlots();
            RenderCategoryTabs();
            RenderSpellsGrid();
            RenderDecksList();
            UpdateStatus();
        }

        private void RenderHeader()
        {
            if (_classNameLabel != null) _classNameLabel.text = _currentClassId;
            if (_signatureLabel != null)
            {
                var sig = FindSignatureForClass(_currentClassId);
                _signatureLabel.text = sig != null ? $"Signature : <b>{sig.DisplayName}</b>" : "Signature : —";
            }
        }

        private void RenderSlots()
        {
            ClearSpawned(_spawnedSlots);
            if (_slotsRow == null) return;

            for (int i = 0; i < 6; i++)
            {
                int slotIndex = i;
                var slotGo = new GameObject($"Slot_{i}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                slotGo.transform.SetParent(_slotsRow, false);
                var le = slotGo.GetComponent<LayoutElement>();
                le.preferredWidth = 120f;
                le.preferredHeight = 120f;
                var img = slotGo.GetComponent<Image>();
                img.color = _slotSpellIds[i] != null ? _slotFilledColor : _slotEmptyColor;
                var btn = slotGo.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => OnSlotClicked(slotIndex));

                // Numero slot en haut a gauche (1-6)
                var numGo = new GameObject("SlotNum", typeof(RectTransform), typeof(TextMeshProUGUI));
                numGo.transform.SetParent(slotGo.transform, false);
                var numRt = numGo.GetComponent<RectTransform>();
                numRt.anchorMin = new Vector2(0f, 1f);
                numRt.anchorMax = new Vector2(0f, 1f);
                numRt.pivot = new Vector2(0f, 1f);
                numRt.anchoredPosition = new Vector2(6f, -4f);
                numRt.sizeDelta = new Vector2(20f, 20f);
                var numTmp = numGo.GetComponent<TextMeshProUGUI>();
                numTmp.text = (i + 1).ToString();
                numTmp.fontSize = 14f;
                numTmp.color = new Color(0.7f, 0.75f, 0.85f);
                numTmp.alignment = TextAlignmentOptions.TopLeft;
                numTmp.fontStyle = FontStyles.Bold;

                // Icone du slot (centre, 56x56). Affiche si _slotSpellIds[i] non-null et
                // def.IconSprite assigne. Label texte rendu en dessous (cost + nom abrege).
                SpellDefinition slotDef = _slotSpellIds[i] != null ? _spellCatalog?.FindBySpellId(_slotSpellIds[i]) : null;
                if (slotDef != null && slotDef.IconSprite != null)
                {
                    var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                    iconGo.transform.SetParent(slotGo.transform, false);
                    var iconRt = iconGo.GetComponent<RectTransform>();
                    iconRt.anchorMin = new Vector2(0.5f, 1f);
                    iconRt.anchorMax = new Vector2(0.5f, 1f);
                    iconRt.pivot = new Vector2(0.5f, 1f);
                    iconRt.anchoredPosition = new Vector2(0f, -22f);
                    iconRt.sizeDelta = new Vector2(56f, 56f);
                    var iconImg = iconGo.GetComponent<Image>();
                    iconImg.sprite = slotDef.IconSprite;
                    iconImg.preserveAspect = true;
                    iconImg.raycastTarget = false;
                }

                // Label central (bas) — affichage texte PA + nom abrege en dessous de l'icone
                var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelGo.transform.SetParent(slotGo.transform, false);
                var labelRt = labelGo.GetComponent<RectTransform>();
                labelRt.anchorMin = new Vector2(0f, 0f);
                labelRt.anchorMax = new Vector2(1f, 0f);
                labelRt.pivot = new Vector2(0.5f, 0f);
                labelRt.anchoredPosition = new Vector2(0f, 4f);
                labelRt.sizeDelta = new Vector2(-6f, 38f);
                var tmp = labelGo.GetComponent<TextMeshProUGUI>();
                tmp.fontSize = 11f;
                tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontStyle = FontStyles.Bold;
                tmp.enableWordWrapping = true;
                tmp.richText = true;
                if (slotDef != null)
                {
                    tmp.text = $"<size=10>{slotDef.DisplayName}</size>\n<color=#cce>{slotDef.ActionPointCost} PA</color>";
                }
                else
                {
                    tmp.text = "<color=#666>vide</color>";
                }

                AddHoverHandler(slotGo, _slotSpellIds[i]);

                _spawnedSlots.Add(slotGo);
            }
        }

        private void RenderCategoryTabs()
        {
            ClearSpawned(_spawnedTabs);
            if (_categoryTabsRow == null) return;

            SpawnCategoryTab(SpellCategory.Offensive, "OFFENSIFS", _categoryOffensiveColor);
            SpawnCategoryTab(SpellCategory.Tactical,  "TACTIQUES", _categoryTacticalColor);
            SpawnCategoryTab(SpellCategory.Survival,  "SURVIE",    _categorySurvivalColor);
        }

        private void SpawnCategoryTab(SpellCategory cat, string label, Color color)
        {
            bool isActive = cat == _activeCategory;
            var tabGo = new GameObject($"Tab_{cat}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            tabGo.transform.SetParent(_categoryTabsRow, false);
            var le = tabGo.GetComponent<LayoutElement>();
            le.preferredHeight = 60f;
            le.flexibleWidth = 1f;

            var img = tabGo.GetComponent<Image>();
            // Onglet actif : couleur pleine. Inactif : couleur assombrie ~40%.
            img.color = isActive ? color : new Color(color.r * 0.35f, color.g * 0.35f, color.b * 0.35f, 1f);

            var btn = tabGo.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.interactable = !isActive;
            btn.onClick.AddListener(() => OnCategoryTabClicked(cat));

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(tabGo.transform, false);
            StretchToParent(labelGo);
            var tmp = labelGo.GetComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 22f;
            tmp.color = isActive ? Color.white : new Color(0.75f, 0.78f, 0.82f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;

            _spawnedTabs.Add(tabGo);
        }

        private void OnCategoryTabClicked(SpellCategory cat)
        {
            if (_activeCategory == cat) return;
            _activeCategory = cat;
            RenderCategoryTabs();
            RenderSpellsGrid();
        }

        private void RenderSpellsGrid()
        {
            ClearSpawned(_spawnedSpells);
            if (_spellsGrid == null) return;
            if (_spellCatalog == null)
            {
                SetStatus("SpellCatalog non assigne — drag-le sur le panel Inspector.");
                return;
            }

            if (!System.Enum.TryParse(_currentClassId, out NymoraClass cls)) return;
            var allSpells = _spellCatalog.FindByClass(cls, includeSignature: false);

            // N'affiche que les 5 sorts de l'onglet actif (gros boutons)
            foreach (var def in allSpells)
            {
                if (def.Category != _activeCategory) continue;

                string spellId = def.SpellId;
                bool isEquipped = System.Array.IndexOf(_slotSpellIds, spellId) >= 0;

                var go = new GameObject($"Spell_{def.SpellId}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                go.transform.SetParent(_spellsGrid, false);
                var le = go.GetComponent<LayoutElement>();
                le.preferredWidth = 240f;
                le.preferredHeight = 200f;
                var img = go.GetComponent<Image>();
                img.color = isEquipped ? _spellGridSelectedColor : _spellGridUnselectedColor;
                var btn = go.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.interactable = !isEquipped;
                btn.onClick.AddListener(() => OnSpellGridClicked(spellId));

                // Icone (centre haut, 64x64). Affiche si def.IconSprite assigne via
                // PopulateSpellCatalog (18 mai). Le nom passe sous l'icone.
                if (def.IconSprite != null)
                {
                    var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                    iconGo.transform.SetParent(go.transform, false);
                    var iconRt = iconGo.GetComponent<RectTransform>();
                    iconRt.anchorMin = new Vector2(0.5f, 1f);
                    iconRt.anchorMax = new Vector2(0.5f, 1f);
                    iconRt.pivot = new Vector2(0.5f, 1f);
                    iconRt.anchoredPosition = new Vector2(0f, -8f);
                    iconRt.sizeDelta = new Vector2(64f, 64f);
                    var iconImg = iconGo.GetComponent<Image>();
                    iconImg.sprite = def.IconSprite;
                    iconImg.preserveAspect = true;
                    iconImg.raycastTarget = false;
                }

                // Nom du sort (sous l'icone, fontSize legerement plus petit pour laisser place a l'icone)
                var nameGo = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
                nameGo.transform.SetParent(go.transform, false);
                var nameRt = nameGo.GetComponent<RectTransform>();
                nameRt.anchorMin = new Vector2(0f, 1f);
                nameRt.anchorMax = new Vector2(1f, 1f);
                nameRt.pivot = new Vector2(0.5f, 1f);
                nameRt.anchoredPosition = new Vector2(0f, def.IconSprite != null ? -78f : -10f);
                nameRt.sizeDelta = new Vector2(-20f, def.IconSprite != null ? 40f : 70f);
                var nameTmp = nameGo.GetComponent<TextMeshProUGUI>();
                nameTmp.text = def.DisplayName;
                nameTmp.fontSize = def.IconSprite != null ? 16f : 20f;
                nameTmp.color = Color.white;
                nameTmp.alignment = TextAlignmentOptions.Center;
                nameTmp.fontStyle = FontStyles.Bold;
                nameTmp.enableWordWrapping = true;

                // Stats (bas)
                var statsGo = new GameObject("Stats", typeof(RectTransform), typeof(TextMeshProUGUI));
                statsGo.transform.SetParent(go.transform, false);
                var statsRt = statsGo.GetComponent<RectTransform>();
                statsRt.anchorMin = new Vector2(0f, 0f);
                statsRt.anchorMax = new Vector2(1f, 0f);
                statsRt.pivot = new Vector2(0.5f, 0f);
                statsRt.anchoredPosition = new Vector2(0f, 12f);
                statsRt.sizeDelta = new Vector2(-20f, 80f);
                var statsTmp = statsGo.GetComponent<TextMeshProUGUI>();
                statsTmp.text = $"<b>{def.ActionPointCost} PA</b>\n<size=14>range {def.MinRange}-{def.MaxRange}</size>\n<size=13><color=#aab>{def.Filter}</color></size>";
                statsTmp.fontSize = 18f;
                statsTmp.color = new Color(0.9f, 0.9f, 0.95f);
                statsTmp.alignment = TextAlignmentOptions.Center;
                statsTmp.richText = true;

                AddHoverHandler(go, spellId);

                _spawnedSpells.Add(go);
            }
        }

        private RectTransform _decksScrollContent;

        /// <summary>
        /// Refonte ergo sidebar (fix 18 mai ter) :
        ///   1. Reordonne les enfants : Title -> NameInput -> ButtonsRow -> DecksList (avec
        ///      scroll). Les boutons restent fixes en haut, l'utilisateur les voit toujours
        ///      meme avec 5 decks.
        ///   2. Transforme le DecksList en ScrollView : ajoute RectMask2D + ScrollRect, et
        ///      cree un child "Content" qui hebergera les items (avec VerticalLayoutGroup +
        ///      ContentSizeFitter pour scroll vertical natif).
        /// Idempotent : si la structure ScrollRect existe deja, on n'y touche pas, juste on
        /// re-applique l'ordering (cas hot-reload Unity).
        /// </summary>
        private void EnsureDecksListLayout()
        {
            if (_decksList == null) return;

            // ===== 1. Setup ScrollRect + Content si pas deja fait =====
            var existingContent = _decksList.Find("Content") as RectTransform;
            if (existingContent == null)
            {
                // a) Disable le VerticalLayoutGroup du DecksList (sinon il essaie de layouter
                //    le Content child, ce qui fout en l'air le scroll).
                var oldVlg = _decksList.GetComponent<VerticalLayoutGroup>();

                // b) Add RectMask2D pour clipper les items qui depassent.
                if (_decksList.GetComponent<RectMask2D>() == null)
                    _decksList.gameObject.AddComponent<RectMask2D>();

                // c) Add Image en background (sinon le RectMask2D n'a rien a clipper visuellement
                //    et certains raycasts peuvent ne pas passer).
                if (_decksList.GetComponent<Image>() == null)
                {
                    var bgImg = _decksList.gameObject.AddComponent<Image>();
                    bgImg.color = new Color(0f, 0f, 0f, 0.001f); // quasi-transparent, juste pour raycast
                }

                // d) Add ScrollRect.
                var sr = _decksList.GetComponent<ScrollRect>();
                if (sr == null) sr = _decksList.gameObject.AddComponent<ScrollRect>();
                sr.horizontal = false;
                sr.vertical = true;
                sr.viewport = _decksList;
                sr.movementType = ScrollRect.MovementType.Clamped;
                sr.scrollSensitivity = 30f;

                // e) Cree un child "Content" anchored top-stretch, qui grandira vers le bas.
                var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                contentGo.transform.SetParent(_decksList, false);
                var contentRt = (RectTransform)contentGo.transform;
                contentRt.anchorMin = new Vector2(0f, 1f);
                contentRt.anchorMax = new Vector2(1f, 1f);
                contentRt.pivot = new Vector2(0.5f, 1f);
                contentRt.anchoredPosition = Vector2.zero;
                contentRt.sizeDelta = new Vector2(0f, 0f);

                var newVlg = contentGo.GetComponent<VerticalLayoutGroup>();
                if (oldVlg != null)
                {
                    newVlg.spacing = oldVlg.spacing;
                    newVlg.childAlignment = oldVlg.childAlignment;
                    newVlg.childForceExpandWidth = oldVlg.childForceExpandWidth;
                    newVlg.childForceExpandHeight = oldVlg.childForceExpandHeight;
                    newVlg.childControlWidth = oldVlg.childControlWidth;
                    newVlg.childControlHeight = oldVlg.childControlHeight;
                    oldVlg.enabled = false; // disable l'ancien pour eviter double-layout
                }
                else
                {
                    newVlg.spacing = 4f;
                    newVlg.childAlignment = TextAnchor.UpperLeft;
                    newVlg.childForceExpandWidth = true;
                    newVlg.childForceExpandHeight = false;
                    newVlg.childControlWidth = true;
                    newVlg.childControlHeight = false;
                }

                var fitter = contentGo.GetComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

                sr.content = contentRt;
                existingContent = contentRt;
            }
            _decksScrollContent = existingContent;

            // ===== 1.bis Le DecksList prend tout l'espace vertical restant du sidebar =====
            // Demande Lorenzo (18 mai) : la zone scroll doit aller jusqu'en bas du menu, pas
            // se limiter aux 2/3 (320px fixe etait trop petit vs sidebar ~500px).
            //
            // Solution : flexibleHeight=1 sur le LayoutElement du DecksList + activer
            // childControlHeight=true sur le VerticalLayoutGroup du sidebar parent. Le VLG
            // distribue alors l'espace restant (apres Title + NameInput + ButtonsRow qui ont
            // des preferredHeight stricts) au DecksList.
            var le = _decksList.GetComponent<LayoutElement>();
            if (le != null)
            {
                if (!Mathf.Approximately(le.minHeight, 120f)) le.minHeight = 120f; // au moins 2 items visibles
                if (le.flexibleHeight != 1f) le.flexibleHeight = 1f; // prend tout l'espace restant
            }
            var sidebarVlg = _decksList.parent != null
                ? _decksList.parent.GetComponent<VerticalLayoutGroup>()
                : null;
            if (sidebarVlg != null && !sidebarVlg.childControlHeight)
            {
                sidebarVlg.childControlHeight = true;
                // childForceExpandHeight reste a false : seuls les enfants avec flexibleHeight>0
                // recoivent l'espace flex (= DecksList uniquement).
            }

            // ===== 2. Reordonne les enfants du sidebar parent =====
            //    Cible : Title (sibling 0) -> NameInput (1) -> ButtonsRow (2) -> DecksList (3)
            var sidebar = _decksList.parent;
            if (sidebar != null && _deckNameInput != null && _saveDeckButton != null)
            {
                Transform nameInputT = _deckNameInput.transform;
                Transform buttonsRowT = _saveDeckButton.transform.parent;
                // Trouve l'index du Title (si present). Sinon part de 0.
                int baseIdx = 0;
                var title = sidebar.Find("SidebarTitle") ?? sidebar.Find("Title");
                if (title != null) baseIdx = title.GetSiblingIndex() + 1;

                if (nameInputT != null && nameInputT.parent == sidebar)
                    nameInputT.SetSiblingIndex(baseIdx);
                if (buttonsRowT != null && buttonsRowT.parent == sidebar)
                    buttonsRowT.SetSiblingIndex(baseIdx + 1);
                _decksList.SetSiblingIndex(baseIdx + 2);
            }
        }

        /// <summary>
        /// Force un rebuild immediate du layout DecksList apres ajout/clear d'items.
        /// Sans ca, au PREMIER render (connexion : Open -> FetchDecksAsync -> RenderAll),
        /// les items se chevauchent visuellement car le ContentSizeFitter+VerticalLayoutGroup
        /// n'ont pas encore eu de frame pour recalculer. Cliquer "Nouveau" trigger un
        /// 2e RenderAll qui passe par le rebuild correct, d'ou le bug "visible uniquement
        /// a la connexion".
        /// </summary>
        private void ForceDecksListRebuild()
        {
            if (_decksList == null) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_decksList);
        }

        /// <summary>
        /// Style l'input "Nom du deck" avec une couleur distincte du sidebar sombre, pour
        /// que l'utilisateur le repere clairement comme champ editable. Idempotent.
        /// </summary>
        private void EnsureNameInputStyle()
        {
            if (_deckNameInput == null) return;
            var img = _deckNameInput.GetComponent<Image>();
            if (img == null) return;
            // Bleu profond Nymora-themed, contraste avec le fond sidebar (0.10-0.15 gris).
            var target = new Color(0.18f, 0.26f, 0.38f, 1f);
            if (img.color != target) img.color = target;
        }

        /// <summary>
        /// Met a jour le label du bouton _saveDeckButton selon le contexte :
        /// - Deck selectionne dans la liste (_editingDeckId != null) -> "Modifier"
        /// - Nouveau deck ou liste vide (_editingDeckId == null) -> "Save"
        /// </summary>
        private void UpdateSaveButtonLabel()
        {
            if (_saveDeckButton == null) return;
            var tmp = _saveDeckButton.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            if (tmp == null) return;
            string targetText = string.IsNullOrEmpty(_editingDeckId) ? "Save" : "Modifier";
            if (tmp.text != targetText) tmp.text = targetText;
        }

        private void RenderDecksList()
        {
            ClearSpawned(_spawnedDeckItems);
            if (_decksList == null) return;

            // Self-healing layout (fix 18 mai) : le RectTransform du DecksList etait fige
            // a 100x100 par defaut alors que LayoutElement.preferredHeight=320 -> seul 1 item
            // visible. Force ContentSizeFitter.PreferredSize pour que le decksList grandisse
            // avec ses enfants empilles verticalement (max 5 decks Bible * 60px = 300px,
            // tient sous la limite preferredHeight=320 du sidebar parent).
            EnsureDecksListLayout();

            // Spawn dans _decksScrollContent (cree par EnsureDecksListLayout). Fallback sur
            // _decksList direct si la setup ScrollRect a echoue pour une raison X (degrade
            // gracieux : pas de scroll, mais les decks restent visibles).
            Transform parentForItems = (_decksScrollContent != null) ? (Transform)_decksScrollContent : (Transform)_decksList;

            foreach (var deck in _myDecks)
            {
                string deckId = deck.id;
                var item = new GameObject($"DeckItem_{deck.id}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                item.transform.SetParent(parentForItems, false);
                item.GetComponent<LayoutElement>().preferredHeight = 56f;
                var img = item.GetComponent<Image>();
                bool isActive = deck.id == _editingDeckId;
                img.color = isActive ? _deckItemActiveBg : _deckItemBg;
                var btn = item.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => OnDeckListItemClicked(deckId));

                var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelGo.transform.SetParent(item.transform, false);
                StretchToParent(labelGo);
                var tmp = labelGo.GetComponent<TextMeshProUGUI>();
                tmp.fontSize = 18f;
                tmp.color = Color.white;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                tmp.margin = new Vector4(14f, 0f, 14f, 0f);
                tmp.text = deck.name;

                _spawnedDeckItems.Add(item);
            }

            // Force rebuild immediate apres ajout des items pour eviter le chevauchement
            // visuel au premier render (la frame suivante recalculerait, mais entre temps
            // l'utilisateur voit le bug fugace).
            ForceDecksListRebuild();
            UpdateSaveButtonLabel();
        }

        // ====== Interactions ======

        private void OnSpellGridClicked(string spellId)
        {
            // Ajoute au prochain slot vide
            for (int i = 0; i < 6; i++)
            {
                if (_slotSpellIds[i] == null)
                {
                    _slotSpellIds[i] = spellId;
                    RenderSlots();
                    RenderSpellsGrid();
                    UpdateStatus();
                    return;
                }
            }
            SetStatus("Tous les slots sont remplis. Clique un slot pour retirer.");
        }

        private void OnSlotClicked(int slotIndex)
        {
            if (_slotSpellIds[slotIndex] == null) return;
            _slotSpellIds[slotIndex] = null;
            RenderSlots();
            RenderSpellsGrid();
            UpdateStatus();
        }

        private void OnNewDeckClicked()
        {
            ClearComposition();
            // L'utilisateur veut start fresh — efface le memo "dernier deck edite" pour
            // que la prochaine reco ne retombe pas sur l'ancien deck.
            SelectedClassPreferences.ClearLastEditedDeckId(_currentClassId);
            RenderAll();
            SetStatus("Nouveau deck — selectionne 6 sorts puis Save.");
        }

        private void OnDeckListItemClicked(string deckId)
        {
            var deck = _myDecks.Find(d => d.id == deckId);
            if (deck == null) return;
            _editingDeckId = deckId;
            for (int i = 0; i < 6; i++) _slotSpellIds[i] = (deck.spellIds != null && i < deck.spellIds.Length) ? deck.spellIds[i] : null;
            if (_deckNameInput != null) _deckNameInput.text = deck.name;
            // Memorise le deck en edition pour restauration post-reco.
            SelectedClassPreferences.SetLastEditedDeckId(_currentClassId, deckId);
            RenderAll();
            SetStatus($"Edition : {deck.name}");
        }

        private async void OnSaveClicked()
        {
            // Validation locale
            string name = _deckNameInput != null ? _deckNameInput.text?.Trim() : null;
            if (string.IsNullOrEmpty(name))
            {
                SetStatus("Erreur : nom de deck obligatoire.");
                return;
            }
            // Filtre les slots remplis
            var spellIds = new List<string>();
            for (int i = 0; i < 6; i++) if (_slotSpellIds[i] != null) spellIds.Add(_slotSpellIds[i]);
            if (spellIds.Count != 6)
            {
                SetStatus($"Erreur : 6 sorts requis ({spellIds.Count}/6 actuellement).");
                return;
            }
            if (new HashSet<string>(spellIds).Count != 6)
            {
                SetStatus("Erreur : les 6 sorts doivent etre uniques.");
                return;
            }

            string token = ResolveDevToken();
            if (string.IsNullOrEmpty(token)) { SetStatus("Pas de JWT."); return; }
            _api.SetBearerToken(token);

            SetStatus(_editingDeckId == null ? "Creation du deck..." : "Mise a jour du deck...");

            if (_editingDeckId == null)
            {
                // Cree
                var res = await _api.CreateDeckAsync(_currentClassId, name, spellIds.ToArray());
                if (!res.IsSuccess)
                {
                    SetStatus($"Erreur {res.StatusCode} : {res.ErrorMessage}");
                    return;
                }
                _myDecks.Add(res.Data.deck);
                _editingDeckId = res.Data.deck.id;
                // Memorise pour restauration post-reco.
                SelectedClassPreferences.SetLastEditedDeckId(_currentClassId, _editingDeckId);
                SetStatus($"Deck '{name}' cree.");
            }
            else
            {
                // Update
                var res = await _api.UpdateDeckAsync(_editingDeckId, name, spellIds.ToArray());
                if (!res.IsSuccess)
                {
                    SetStatus($"Erreur {res.StatusCode} : {res.ErrorMessage}");
                    return;
                }
                // Update local cache
                int idx = _myDecks.FindIndex(d => d.id == _editingDeckId);
                if (idx >= 0) _myDecks[idx] = res.Data.deck;
                // Refresh pref (idempotent ; insure le memo si le user save sans avoir clique
                // dans la liste, par ex apres restauration auto au boot puis save direct).
                SelectedClassPreferences.SetLastEditedDeckId(_currentClassId, _editingDeckId);
                SetStatus($"Deck '{name}' mis a jour.");
            }
            RenderAll();
        }

        private async void OnDeleteClicked()
        {
            if (_editingDeckId == null)
            {
                SetStatus("Aucun deck a supprimer (selectionne-en un dans la liste).");
                return;
            }
            string token = ResolveDevToken();
            if (string.IsNullOrEmpty(token)) { SetStatus("Pas de JWT."); return; }
            _api.SetBearerToken(token);

            string deletingId = _editingDeckId;
            SetStatus("Suppression...");
            var res = await _api.DeleteDeckAsync(deletingId);
            if (!res.IsSuccess)
            {
                SetStatus($"Erreur {res.StatusCode} : {res.ErrorMessage}");
                return;
            }
            _myDecks.RemoveAll(d => d.id == deletingId);
            // Clean le memo si on vient de delete le deck memorise (sinon prochaine reco
            // tentera de le restaurer puis trouvera null -> clean a posteriori, c'est plus
            // propre de clean tout de suite).
            if (SelectedClassPreferences.GetLastEditedDeckId(_currentClassId) == deletingId)
                SelectedClassPreferences.ClearLastEditedDeckId(_currentClassId);
            ClearComposition();
            RenderAll();
            SetStatus("Deck supprime.");
        }

        // ====== Tooltip basique (5.3.e.iii) ======

        private void AddHoverHandler(GameObject go, string spellId)
        {
            if (string.IsNullOrEmpty(spellId) || _spellCatalog == null) return;
            var handler = go.AddComponent<TooltipHoverProxy>();
            handler.Init(this, spellId);
        }

        public void ShowTooltipForSpell(string spellId)
        {
            if (_tooltipPanel == null || _tooltipText == null || _spellCatalog == null) return;
            var def = _spellCatalog.FindBySpellId(spellId);
            if (def == null) return;
            string desc = string.IsNullOrEmpty(def.Description)
                ? "<i><color=#888>(Description Bible V7.1 a remplir — lancer Nymora &gt; Setup &gt; Populate Spell Catalog)</color></i>"
                : def.Description;
            string lore = string.IsNullOrEmpty(def.LoreFlavor)
                ? string.Empty
                : $"\n\n<size=14><i><color=#9988aa>{def.LoreFlavor}</color></i></size>";
            _tooltipText.text =
                $"<size=26><b>{def.DisplayName}</b></size>\n" +
                $"<size=14><color=#9aa>{def.Category}  ·  {def.ClassId}</color></size>\n\n" +
                $"<color=#ffdd55><b>{def.ActionPointCost} PA</b>  ·  range {def.MinRange}-{def.MaxRange}  ·  {def.Filter}  ·  {def.Shape}</color>\n\n" +
                desc + lore;
            _tooltipPanel.SetActive(true);
        }

        public void HideTooltip()
        {
            if (_tooltipPanel != null) _tooltipPanel.SetActive(false);
        }

        // ====== Helpers ======

        private void ClearComposition()
        {
            for (int i = 0; i < 6; i++) _slotSpellIds[i] = null;
            _editingDeckId = null;
            if (_deckNameInput != null) _deckNameInput.text = string.Empty;
        }

        private void UpdateStatus()
        {
            int filled = 0;
            for (int i = 0; i < 6; i++) if (_slotSpellIds[i] != null) filled++;
            string editing = _editingDeckId != null ? "edition" : "nouveau";
            SetStatus($"{_currentClassId} · {filled}/6 sorts · {_myDecks.Count}/5 decks · {editing}");
        }

        private void HandleDeckChanged()
        {
            if (_hasFetchedOnce) FetchDecksAsync().Forget();
        }

        private SpellDefinition FindSignatureForClass(string classIdStr)
        {
            if (_spellCatalog == null) return null;
            if (!System.Enum.TryParse(classIdStr, out NymoraClass cls)) return null;
            foreach (var s in _spellCatalog.Spells)
                if (s != null && s.ClassId == cls && s.Category == SpellCategory.Signature) return s;
            return null;
        }

        private void SetStatus(string msg)
        {
            if (_statusLabel != null) _statusLabel.text = msg;
        }

        private static string ResolveDevToken()
        {
            return HubChatClient.Instance?.DevToken;
        }

        private void ClearSpawned(List<GameObject> list)
        {
            foreach (var go in list) if (go != null) Destroy(go);
            list.Clear();
        }

        private static void StretchToParent(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Hover handler proxy : delegue show/hide tooltip au panel parent.</summary>
    internal sealed class TooltipHoverProxy : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        private HubDeckBuilderPanel _panel;
        private string _spellId;

        public void Init(HubDeckBuilderPanel panel, string spellId)
        {
            _panel = panel;
            _spellId = spellId;
        }

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData _) => _panel?.ShowTooltipForSpell(_spellId);
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData _) => _panel?.HideTooltip();
    }
}
