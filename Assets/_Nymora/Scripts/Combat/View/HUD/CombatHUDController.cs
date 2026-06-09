using System;
using Nymora.Core.Data;
using Nymora.Core.ScriptableObjects;
using Quantum;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// HUD combat principal (2.13.a). Orchestre les widgets : ResourcePanel (x2), Timer,
    /// PassivePanel, Timeline, SpellSlots (6 deck + signature), bouton End Turn.
    ///
    /// Source de donnees : frame Quantum verifiee (CallbackUpdateView).
    ///
    /// Mode arme (Option 2 choisie par Lorenzo) : clic icone sort qui necessite une cible
    /// = passe le HUD en "armed", le prochain clic gauche sur la grille envoie le cast
    /// au lieu du mouvement. CombatInputController lit ArmedSpell via ConsumeArmedSpell.
    /// </summary>
    public class CombatHUDController : MonoBehaviour
    {
        [Header("Catalog")]
        [SerializeField] private SpellIconRegistry _iconRegistry;
        [Tooltip("Definitions des 5 classes (drag-drop NymoraClassDefinition assets). Utilise " +
                 "par TimelineView pour afficher les sprites Idle animes des combatants au lieu " +
                 "des portraits statiques.")]
        [SerializeField] private Nymora.Core.ScriptableObjects.NymoraClassDefinition[] _classDefinitions;
        [Tooltip("SpellCatalog.asset (Nymora.Core) — utilise pour mapper SpellIdTech (string) -> " +
                 "Quantum.SpellId (enum) quand DeckBridge a un deck pending depuis le Hub.")]
        [SerializeField] private SpellCatalog _spellCatalog;

        [Header("Local player")]
        [SerializeField] private int _localPlayerIndex = 0;

        [Tooltip("Si vrai (default Phase 2.x) : envoie les commands au joueur ACTIF courant " +
                 "au lieu de _localPlayerIndex. A desactiver en Phase 6 (vrai matchmaking).")]
        [SerializeField] private bool _debugAllPlayersControllable = true;

        [Header("Deck (configuration libre Inspector)")]
        [Tooltip("6 sorts visibles dans la barre de sorts bas-centre.")]
        [SerializeField] private SpellId[] _testDeck = new SpellId[6];

        [Tooltip("Sort de signature occupant le slot dedie a droite de la barre.")]
        [SerializeField] private SpellId _signatureSpell = SpellId.SoulrenderAmeLaceree;

        [Header("Widgets")]
        [SerializeField] private ResourcePanelView _p0Panel;
        [SerializeField] private ResourcePanelView _p1Panel;
        [Tooltip("Patch UI combat 8 juin — les anciens panneaux d'infos haut-gauche/droite sont " +
                 "remplacés par les HP affichés sous les portraits de la timeline + le tooltip au survol. " +
                 "Laisser DÉCOCHÉ. Cocher uniquement pour réafficher les vieux panneaux (debug).")]
        [SerializeField] private bool _showLegacyResourcePanels = false;
        [SerializeField] private TimerView _timer;
        [SerializeField] private PassivePanelView _passive;
        [SerializeField] private TimelineView _timeline;
        [SerializeField] private SpellSlotView[] _spellSlots = new SpellSlotView[6];
        [SerializeField] private SpellSlotView _signatureSlot;
        [SerializeField] private Button _endTurnButton;
        [SerializeField] private SpellTooltipView _tooltip;
        [SerializeField] private MatchEndOverlay _matchEndOverlay;

        [Header("Badges Rubis PA/PM (gauche de la barre de sorts — patch 5 juin)")]
        [Tooltip("Affiche 2 gemmes (PA en haut, PM en bas) du joueur local, juste à gauche de la barre.")]
        [SerializeField] private bool _showResourceGems = true;
        [Tooltip("Écart entre la barre et les gemmes (px). + = plus loin à gauche.")]
        [SerializeField] private float _resourceGemsGap = 18f;
        [Tooltip("Décalage manuel fin des gemmes (tuning visuel dans l'inspector).")]
        [SerializeField] private Vector2 _resourceGemsExtraOffset = Vector2.zero;
        [Tooltip("Taille d'une gemme (px).")]
        [SerializeField] private float _resourceGemSize = 46f;
        [Tooltip("Espace vertical entre la gemme PA et la gemme PM (px).")]
        [SerializeField] private float _resourceGemSpacing = 6f;

        private RectTransform _resourceGemsRoot;
        private TMP_Text _paGemValue;
        private TMP_Text _pmGemValue;
        // PA = rubis rouge (cohérent avec les badges de coût des sorts) ; PM = vert (émeraude).
        private static readonly Color PaGemColor = new Color(0.86f, 0.15f, 0.20f, 1f);
        private static readonly Color PmGemColor = new Color(0.30f, 0.78f, 0.36f, 1f);

        // Etat armed (Option 2). Consume via ConsumeArmedSpell() pour le CombatInputController.
        private SpellId? _armedSpell;
        public SpellId? ArmedSpell => _armedSpell;
        public event Action ArmedSpellChanged;

        // Tuto T5 — Accesseurs lecture seule des RectTransform de widgets HUD, pour que le
        // TutorialDirector positionne ses coach marks (halo) dessus. Null si non câblé en scène.
        public RectTransform EndTurnButtonRect => _endTurnButton != null ? (RectTransform)_endTurnButton.transform : null;
        public RectTransform SpellBarRect =>
            (_spellSlots != null && _spellSlots.Length > 0 && _spellSlots[0] != null)
                ? _spellSlots[0].transform.parent as RectTransform : null;
        public RectTransform LocalResourcePanelRect => _p0Panel != null ? (RectTransform)_p0Panel.transform : null;
        public RectTransform SignatureSlotRect => _signatureSlot != null ? (RectTransform)_signatureSlot.transform : null;
        /// <summary>Tuto : rect du badge rubis (coût PA) du 1er sort de la barre, pour le coach mark
        /// de l'étape « rubis de PA ». Null si le badge n'est pas encore créé (SetPaCost pas appelé).</summary>
        public RectTransform FirstSpellPaGemRect =>
            (_spellSlots != null && _spellSlots.Length > 0 && _spellSlots[0] != null)
                ? _spellSlots[0].PaBadgeRect : null;

        // J10 — true uniquement quand c'est le tour du joueur que CE HUD controle. Sert a bloquer
        // l'armement de sort et a griser la barre pendant le tour adverse (notamment le bot en IA).
        private bool _isLocalTurn;

        // B5 (22 mai) — dernier joueur actif pour lequel on a joue le bandeau de tour anime.
        // -1 = aucun -> declenche le bandeau au 1er tour.
        private int _lastBannerActivePlayer = -1;

        // POLISH-6a (19 mai) — Singleton-like accessor pour que CombatantTooltipView lise
        // l'armed spell sans drag-drop Inspector. Set au Awake, clear au OnDestroy.
        //
        // 19 mai (PvP casual fix) — Self-healing getter. La scene 33_CombatCasual auto-load
        // additivement la scene 30_CombatIA via QuantumMap.ScenePath (cf memoire
        // project_combat_scene_bootstrap_isolation). Pendant ~2 frames, 2 instances de
        // CombatHUDController coexistent (une par scene). Le HUD fantome IA ecrase Instance
        // dans son Awake, puis quand DeferredAdditiveCleanup unload 30_CombatIA, son
        // OnDestroy fait `if (Instance == this) Instance = null` -> Instance devient null
        // malgre le HUD Casual legitime toujours en vie -> CombatantTooltipView lit
        // `hud == null` -> pas de preview damage ligne dans le tooltip.
        // Fix : si _instance est null/destroyed, le getter re-scan via FindAnyObjectByType
        // pour retrouver le HUD encore vivant. Cout = 1 scan par miss (rare).
        private static CombatHUDController _instance;
        public static CombatHUDController Instance
        {
            get
            {
                // Operator overload `bool` UnityEngine.Object : retourne false si l'object
                // a ete destroye (les references C# restent mais l'objet natif est dead).
                if (_instance != null && _instance) return _instance;
                _instance = FindAnyObjectByType<CombatHUDController>(FindObjectsInactive.Exclude);
                return _instance;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Awake()
        {
            _instance = this;
            // 5.3.g — Si on vient du Hub avec un deck equipe via DeckBridge, override _testDeck
            // et _signatureSpell avant BindSlots() pour que le HUD affiche les bons sorts.
            ApplyDeckBridgeIfPending();

            // 4.14.f hotfix — En PvP, _localPlayerIndex doit pointer le slot LOCAL (depuis
            // CombatBootstrapCasual.LocalPlayerSlot). Sans ca, le HUD affiche Victory/Defeat
            // inverse et resolve_local_player_for_sender retourne le mauvais slot.
            QuantumCallback.Subscribe(this, (CallbackGameStarted c) => OnGameStartedResolveLocalSlot(c.Game));

            BindSlots();
            if (_endTurnButton != null)
            {
                _endTurnButton.onClick.RemoveAllListeners();
                _endTurnButton.onClick.AddListener(OnEndTurnClicked);
            }
            if (_passive != null)
            {
                _passive.Init(_iconRegistry);
            }
            // 2.13.e : portrait dans les ResourcePanels.
            if (_p0Panel != null) _p0Panel.Init(_iconRegistry);
            if (_p1Panel != null) _p1Panel.Init(_iconRegistry);
            // Patch UI combat 8 juin — masque les 2 panneaux haut G/D (infos relocalisées dans la
            // timeline : HP sous le portrait + tooltip au survol). Robuste sur les 2 scènes combat
            // sans édition manuelle. Recochable via _showLegacyResourcePanels.
            if (!_showLegacyResourcePanels)
            {
                if (_p0Panel != null) _p0Panel.gameObject.SetActive(false);
                if (_p1Panel != null) _p1Panel.gameObject.SetActive(false);
            }
            // 19 mai : timeline avec sprites idle animes (necessite NymoraClassDefinition).
            if (_timeline != null) _timeline.Init(_classDefinitions);

            // 19 mai POLISH-6g — Auto-attach SignatureSlotEnhancer sur le signature slot pour
            // gerer apparition animee + lueur gold quand la ressource max est atteinte.
            if (_signatureSlot != null)
            {
                _signatureEnhancer = _signatureSlot.gameObject.GetComponent<SignatureSlotEnhancer>();
                if (_signatureEnhancer == null)
                {
                    _signatureEnhancer = _signatureSlot.gameObject.AddComponent<SignatureSlotEnhancer>();
                }
                _signatureEnhancer.Initialize();
            }

            QuantumCallback.Subscribe(this, (CallbackUpdateView c) => OnUpdateView(c.Game));

            // S5 spectateur : masque les éléments INTERACTIFS (barre de sorts, signature, Fin de
            // tour) qui n'ont aucun sens pour un spectateur. On garde les panneaux d'info utiles
            // (HP/PA/PM des 2 joueurs, timeline, passif).
            if (Nymora.Combat.Spectate.LiveSpectateController.LiveSpectateActive)
            {
                HideInteractiveHudForSpectator();
            }
        }

        private void HideInteractiveHudForSpectator()
        {
            if (_spellSlots != null)
                foreach (var s in _spellSlots) if (s != null) s.gameObject.SetActive(false);
            if (_signatureSlot != null) _signatureSlot.gameObject.SetActive(false);
            if (_endTurnButton != null) _endTurnButton.gameObject.SetActive(false);
        }

        private SignatureSlotEnhancer _signatureEnhancer;

        /// <summary>
        /// 5.3.g — Si DeckBridge.HasPending, mappe les 6 SpellIdTech (snake_case) vers
        /// Quantum.SpellId via SpellCatalog.QuantumSpellIdValue, et set le signature
        /// depuis le catalog (premier sort SpellCategory.Signature de la classe pending).
        /// Si aucun deck pending OU pas de SpellCatalog wire : conserve _testDeck Inspector
        /// + _signatureSpell hardcoded (fallback dev / scene Play direct).
        /// </summary>
        private void ApplyDeckBridgeIfPending()
        {
            if (!DeckBridge.HasPending) return;
            if (_spellCatalog == null)
            {
                Debug.LogWarning("[CombatHUDController] DeckBridge a un deck pending mais _spellCatalog non assigne — fallback _testDeck Inspector.");
                return;
            }

            // Mappe les 6 spellIds du deck
            for (int i = 0; i < 6 && i < DeckBridge.PendingSpellIds.Length; i++)
            {
                string spellIdTech = DeckBridge.PendingSpellIds[i];
                var def = _spellCatalog.FindBySpellId(spellIdTech);
                if (def == null)
                {
                    Debug.LogWarning($"[CombatHUDController] SpellCatalog.FindBySpellId('{spellIdTech}') = null, slot {i} reste fallback.");
                    continue;
                }
                if (i >= _testDeck.Length)
                {
                    Array.Resize(ref _testDeck, 6);
                }
                _testDeck[i] = (SpellId)def.QuantumSpellIdValue;
            }

            // Signature : premier sort Signature de la classe pending dans le catalog
            if (Enum.TryParse(DeckBridge.PendingClassId, ignoreCase: false, out Nymora.Core.Enums.NymoraClass cls))
            {
                foreach (var s in _spellCatalog.Spells)
                {
                    if (s == null) continue;
                    if (s.ClassId != cls) continue;
                    if (s.Category != Nymora.Core.Enums.SpellCategory.Signature) continue;
                    _signatureSpell = (SpellId)s.QuantumSpellIdValue;
                    break;
                }
            }

            Debug.Log($"[CombatHUDController] DeckBridge applique : class={DeckBridge.PendingClassId} " +
                      $"deck=[{string.Join(",", _testDeck)}] signature={_signatureSpell} (deckName='{DeckBridge.PendingDeckName}')");

            // 4.14.e — NE PAS clear DeckBridge ici. CombatHUDController.Awake() s'execute AVANT
            // CombatBootstrapCasual.Start(), donc clear ici causerait DeckBridge vide quand
            // le bootstrap PvP tente de set le RuntimePlayer.ClassId + SpellIdValues.
            // Le hub re-set DeckBridge au prochain match (HubMatchTransition / HubArenaPanel),
            // l'absence de Clear ici n'est pas critique.
        }

        /// <summary>
        /// 4.14.f hotfix — En PvP, resolve _localPlayerIndex depuis CombatBootstrapCasual.LocalPlayerSlot.
        /// Sinon le HUD est en mode IA (slot 0 hardcoded) et affiche Victory/Defeat inverse cote slot 1.
        /// Bug 19 mai : Quantum dispatch CallbackGameStarted PENDANT l'await SessionRunner.StartAsync,
        /// donc AVANT que CombatBootstrapCasual ait pu appeler AddPlayer + poll GetLocalPlayers.
        /// Si LocalPlayerSlot pas encore resolu, on s'abonne a l'event LocalPlayerSlotResolved
        /// pour retry quand le poll dans le bootstrap obtient le vrai PlayerRef.
        /// </summary>
        private void OnGameStartedResolveLocalSlot(Quantum.QuantumGame game)
        {
            // Mode spectateur (S4) : pas de joueur local à résoudre (aucun CombatBootstrapCasual).
            // Le QuantumCallback se déclenche même component désactivé → on bail explicitement.
            if (Nymora.Combat.Spectate.LiveSpectateController.LiveSpectateActive) return;

            var frame = game?.Frames?.Verified;
            if (frame == null || frame.RuntimeConfig == null) return;
            bool isPvp = !frame.RuntimeConfig.IsBotMatch;
            if (!isPvp)
            {
                // IA : perspective JOUEUR (slot 0 = humain, slot 1 = bot drive par AISystem).
                // On coupe le "drive both" debug -> la barre de sorts reste celle du joueur et
                // se grise pendant le tour du bot (cf gate _isLocalTurn). Skip si raw-dev sans
                // bootstrap IA (on garde alors le debug pour piloter les 2 cote editeur).
                if (Nymora.Combat.Bootstrap.CombatBootstrapIA.Instance != null)
                {
                    _localPlayerIndex = 0;
                    _debugAllPlayersControllable = false;
                    Debug.Log("[CombatHUDController] Mode IA (CombatBootstrapIA) — perspective joueur : _localPlayerIndex=0, _debugAllPlayersControllable=false.");
                }
                return;
            }

            // 5.5b — HOT-SEAT 2v2/3v3 : tous les slots sont locaux sur ce client. Le HUD suit le
            //   joueur ACTIF (gate « mon tour », bouton Fin de tour, activation de la barre de sorts)
            //   via _debugAllPlayersControllable -> ResolveControlPlayer renvoie ActivePlayerIndex.
            //   Pas de Casual à résoudre. (NB : barre de sorts par-classe + timeline 4 portraits = 5.5d.)
            if (Nymora.Combat.Bootstrap.CombatBootstrap2v2.Instance != null)
            {
                _debugAllPlayersControllable = true;
                Debug.Log("[CombatHUDController] Mode HOT-SEAT 2v2/3v3 (CombatBootstrap2v2) — HUD suit le joueur ACTIF (_debugAllPlayersControllable=true).");
                return;
            }

            var bootstrap = Nymora.Combat.Bootstrap.CombatBootstrapCasual.Instance;
            if (bootstrap == null)
            {
                Debug.LogError("[CombatHUDController] PvP detecte mais CombatBootstrapCasual.Instance null — HUD slot wrong.");
                return;
            }

            if (bootstrap.LocalPlayerSlot >= 0)
            {
                ApplyLocalPlayerSlot(bootstrap.LocalPlayerSlot);
            }
            else
            {
                Debug.LogWarning("[CombatHUDController] LocalPlayerSlot pas encore resolu (Quantum CallbackGameStarted dispatche avant AddPlayer/GetLocalPlayers) — attente event LocalPlayerSlotResolved...");
                bootstrap.LocalPlayerSlotResolved += ApplyLocalPlayerSlot;
            }
        }

        private void ApplyLocalPlayerSlot(int slot)
        {
            _localPlayerIndex = slot;
            // _debugAllPlayersControllable force false en PvP : sinon GetCurrentSenderPlayer
            // retourne state.ActivePlayerIndex meme sur les tours du local, et le HUD peut
            // se trouver desync entre slot affiche et slot reel.
            _debugAllPlayersControllable = false;
            Debug.Log($"[CombatHUDController] PvP: _localPlayerIndex={_localPlayerIndex} (depuis CombatBootstrapCasual.LocalPlayerSlot), _debugAllPlayersControllable=false.");

            var bootstrap = Nymora.Combat.Bootstrap.CombatBootstrapCasual.Instance;
            if (bootstrap != null) bootstrap.LocalPlayerSlotResolved -= ApplyLocalPlayerSlot;
        }

        /// <summary>
        /// Lobby pré-combat (B3) — Ré-applique le deck depuis DeckBridge puis re-bind les slots.
        /// Appelé par CombatBootstrapCasual après la résolution du lobby quand le joueur a choisi
        /// un autre deck que celui du deck builder : la barre de sorts doit afficher les 6 sorts
        /// du deck choisi (le deck est View-only — la sim laisse les 16 sorts de la classe castables).
        /// </summary>
        public void ReapplyDeckFromBridge()
        {
            ApplyDeckBridgeIfPending();
            BindSlots();
        }

        /// <summary>
        /// Lobby pré-combat (B3) — Résout la définition d'une classe parmi celles câblées dans la
        /// scène (sert au portrait idle du lobby quand le joueur n'a pas de skin équipé). Null si absente.
        /// </summary>
        public Nymora.Core.ScriptableObjects.NymoraClassDefinition ResolveClassDefinition(Nymora.Core.Enums.NymoraClass cls)
        {
            if (_classDefinitions == null) return null;
            foreach (var d in _classDefinitions)
                if (d != null && d.ClassId == cls) return d;
            return null;
        }

        private void BindSlots()
        {
            // Deck (1-6)
            if (_spellSlots != null)
            {
                for (int i = 0; i < _spellSlots.Length; i++)
                {
                    var slot = _spellSlots[i];
                    if (slot == null) continue;
                    SpellId spell = i < _testDeck.Length ? _testDeck[i] : SpellId.None;
                    Sprite icon = _iconRegistry != null ? _iconRegistry.GetIcon(spell) : null;
                    slot.Bind(this, spell, icon, (i + 1).ToString());
                }
            }

            // Signature (touche è en AZERTY FR = Alpha7 cf CombatInputController.cs ligne 184).
            if (_signatureSlot != null)
            {
                Sprite sigIcon = _iconRegistry != null ? _iconRegistry.GetIcon(_signatureSpell) : null;
                _signatureSlot.Bind(this, _signatureSpell, sigIcon, "è");
            }
        }

        // 5.5e — Recharge _testDeck + _signatureSpell depuis le RuntimePlayer du joueur `playerIndex`
        //   (deck sync Quantum) + la signature de sa classe, puis re-bind la barre. Utilisé en hot-seat
        //   2v2/3v3 pour que chaque joueur retrouve SON deck quand c'est son tour.
        // 5.5e — remplit le cache de deck du joueur `pi`. Si SpellIdValues est vide (ex : scène
        //   lancée hors hub -> RuntimePlayer slot 0 sans deck), fallback sur le deck PAR DÉFAUT de la
        //   classe (6 premiers sorts non-signature) pour ne jamais afficher une barre vide.
        private void FillDeckCache(int pi, int[] spellIdValues, NymoraClass cls)
        {
            var dest = _deckCache[pi];
            bool anyNonZero = false;
            for (int i = 0; i < 6; i++)
            {
                int v = (spellIdValues != null && i < spellIdValues.Length) ? spellIdValues[i] : 0;
                dest[i] = (SpellId)v;
                if (v != 0) anyNonZero = true;
            }
            if (!anyNonZero) FillClassDefaultDeck(dest, cls);
        }

        // 5.5e — écrit dans `dest` le deck par défaut d'une classe (6 premiers sorts non-signature).
        private void FillClassDefaultDeck(SpellId[] dest, NymoraClass quantumCls)
        {
            for (int i = 0; i < 6; i++) dest[i] = SpellId.None;
            if (_spellCatalog == null || quantumCls == NymoraClass.None) return;
            var coreCls = (Nymora.Core.Enums.NymoraClass)(byte)quantumCls;
            var list = _spellCatalog.FindByClass(coreCls, includeSignature: false);
            for (int i = 0; i < 6 && i < list.Count; i++)
                if (list[i] != null) dest[i] = (SpellId)list[i].QuantumSpellIdValue;
        }

        /// <summary>Rebind la barre depuis le cache de deck du joueur actif (+ signature de sa classe).
        /// False si le deck n'est pas encore caché (l'appelant réessaiera la frame suivante).</summary>
        private bool RebindActivePlayerDeckFromCache(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= _deckCache.Length || !_deckCached[playerIndex]) return false;

            // Hot-seat : localTurn reste vrai en permanence -> le désarmement auto au changement de
            // tour ne s'applique pas. On désarme ici tout sort resté armé par le joueur précédent.
            if (_armedSpell.HasValue) Disarm();

            var src = _deckCache[playerIndex];
            for (int i = 0; i < _testDeck.Length; i++) _testDeck[i] = i < 6 ? src[i] : SpellId.None;

            NymoraClass cls = (playerIndex < _tlPresent.Length && _tlPresent[playerIndex])
                ? _tlByPlayer[playerIndex].Class : NymoraClass.None;
            _signatureSpell = ResolveSignatureFor(cls);

            BindSlots();
            return true;
        }

        // 5.5e — SpellId de la signature d'une classe (catégorie Signature du catalogue). Garde la
        //   valeur courante si catalogue absent / classe inconnue (fallback sûr).
        private SpellId ResolveSignatureFor(NymoraClass quantumCls)
        {
            if (_spellCatalog == null || quantumCls == NymoraClass.None) return _signatureSpell;
            var coreCls = (Nymora.Core.Enums.NymoraClass)(byte)quantumCls;
            var list = _spellCatalog.FindByClass(coreCls, includeSignature: true);
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].Category == Nymora.Core.Enums.SpellCategory.Signature)
                    return (SpellId)list[i].QuantumSpellIdValue;
            return _signatureSpell;
        }

        private void OnUpdateView(QuantumGame game)
        {
            var frame = game.Frames.Verified;
            if (frame == null) return;
            if (!frame.TryGetSingleton<CombatState>(out var state)) return;

            int activePlayer = state.ActivePlayerIndex;
            int controlPlayer = ResolveControlPlayer(activePlayer);

            // J10 — tour du joueur local ? controlPlayer==activePlayer est vrai en debug "drive both"
            // (raw dev), sinon seulement quand le slot local EST le slot actif. Hors tour : on
            // desarme + grise la barre (plus de cast/arme cote bot en IA).
            bool localTurn = state.CurrentPhase == CombatPhase.TurnActive && controlPlayer == activePlayer;
            _isLocalTurn = localTurn;
            if (!localTurn && _armedSpell.HasValue) Disarm();

            // Filter combatants une seule fois ; cache localement P0/P1, le combatant local, et
            // les buffers par PlayerIndex pour la timeline N portraits (5.5d).
            Combatant p0 = default, p1 = default;
            bool hasP0 = false, hasP1 = false;
            Combatant local = default;
            bool hasLocal = false;
            System.Array.Clear(_tlPresent, 0, _tlPresent.Length); // reset présence (slots sans Combatant)
            var filter = frame.Filter<Combatant>();
            while (filter.Next(out EntityRef _, out Combatant c))
            {
                if (c.PlayerIndex == 0) { p0 = c; hasP0 = true; }
                if (c.PlayerIndex == 1) { p1 = c; hasP1 = true; }
                if (c.PlayerIndex == controlPlayer) { local = c; hasLocal = true; }
                if (c.PlayerIndex >= 0 && c.PlayerIndex < _tlByPlayer.Length)
                {
                    _tlByPlayer[c.PlayerIndex] = c;
                    _tlPresent[c.PlayerIndex] = true;
                }
            }

            // Ratio HP de l'ennemi (1v1) — pour le coût PA effectif (bonus Appel du Sang Soulrender
            // = -1 PA si la cible est <70% HP). Caché aussi pour le tooltip (hors OnUpdateView).
            Combatant enemyC = controlPlayer == 0 ? p1 : p0;
            bool hasEnemy = controlPlayer == 0 ? hasP1 : hasP0;
            int enemyHpRatio = (hasEnemy && enemyC.MaxHP > 0) ? enemyC.HP * 100 / enemyC.MaxHP : 100;
            _cachedLocal = local; _hasCachedLocal = hasLocal;
            _cachedEnemyHpRatio = enemyHpRatio; _cachedTurnNumber = state.TurnNumber;

            // ResourcePanel (legacy — masqués par défaut depuis le patch UI combat 8 juin).
            if (_showLegacyResourcePanels)
            {
                if (_p0Panel != null) { if (hasP0) _p0Panel.Refresh(p0, activePlayer == 0); else _p0Panel.Clear(); }
                if (_p1Panel != null) { if (hasP1) _p1Panel.Refresh(p1, activePlayer == 1); else _p1Panel.Clear(); }
            }

            // Timer
            if (_timer != null)
            {
                int updateRate = frame.UpdateRate;
                float seconds = updateRate > 0 ? state.TurnTimerTicks / (float)updateRate : 0f;
                _timer.Refresh(seconds, state.TurnNumber);
            }

            // Passif (combattant qu'on controle)
            if (_passive != null) { if (hasLocal) _passive.Refresh(local); else _passive.Clear(); }

            // 5.5e — HOT-SEAT 2v2/3v3 : la barre de sorts suit le DECK du joueur actif (chaque joueur
            //   son deck + sa signature de classe). Rebind uniquement au changement de joueur actif.
            //   Gate _debugAllPlayersControllable : en IA / 1v1 PvP (=false) la barre reste celle du
            //   joueur local (comportement inchangé, non-régression).
            if (_debugAllPlayersControllable)
            {
                // Remplit le cache de deck de chaque joueur présent dès qu'il est lisible (sur
                // n'importe quelle frame). Découple le rebind du timing de GetPlayerData.
                for (int pi = 0; pi < _deckCache.Length; pi++)
                {
                    if (_deckCached[pi] || !_tlPresent[pi]) continue;
                    var rpd = frame.GetPlayerData((PlayerRef)pi);
                    if (rpd == null) continue;
                    FillDeckCache(pi, rpd.SpellIdValues, _tlByPlayer[pi].Class);
                    _deckCached[pi] = true;
                }

                // Rebind la barre sur le deck (caché) du joueur actif au changement de tour.
                // Ne valide _lastDeckPlayer que si le cache est prêt -> retry sinon.
                if (activePlayer != _lastDeckPlayer && RebindActivePlayerDeckFromCache(activePlayer))
                    _lastDeckPlayer = activePlayer;
            }

            // Timeline N portraits (5.5d) : un slot par PlayerIndex (1v1 = 2, 2v2 = 4, 3v3 = 6),
            // teinte par equipe (TeamId) + highlight actif + HP + chips + tooltip hover.
            // Skin equipe par joueur (RuntimePlayer.SkinId, sync Quantum) -> visuel skinné.
            if (_timeline != null)
            {
                int playerCount = state.PlayerCount > 0 ? state.PlayerCount : _tlByPlayer.Length;
                if (playerCount > _tlByPlayer.Length) playerCount = _tlByPlayer.Length;
                for (int i = 0; i < playerCount; i++)
                    _tlSkin[i] = _tlPresent[i] ? frame.GetPlayerData((PlayerRef)i)?.SkinId : null;

                // Ordre d'affichage. Modes ÉQUIPE (2v2/3v3) : ordre de jeu (TurnOrder A0,B0,A1,B1) ->
                // le highlight progresse de gauche à droite. 1v1 (count<=2) ou ordre pas encore figé :
                // identité PlayerIndex (P0 à gauche) -> rendu 1v1 strictement inchangé (non-régression).
                bool useTurnOrder = playerCount > 2 && state.TurnOrderBuilt != 0;
                if (useTurnOrder)
                    TurnSystem.CopyTurnOrder(ref state, _tlOrder);
                else
                    for (int i = 0; i < _tlOrder.Length; i++) _tlOrder[i] = i;

                _timeline.RefreshTeam(activePlayer, _tlByPlayer, _tlPresent, _tlSkin, _tlOrder, playerCount, state.TurnNumber);
            }

            // B5 (22 mai) — Bandeau de tour TRANSITOIRE anime au changement de tour. Trigger
            // une fois par tour quand le joueur actif change, uniquement en phase de jeu active
            // (pas pendant l'intro pile ou face PreMatch ni a la fin du match).
            // IMPORTANT : on compare au VRAI slot local (LocalPlayerResolver), PAS a
            // controlPlayer — en mode IA debug, controlPlayer == activePlayer toujours (on
            // controle les 2), ce qui affichait "C'EST TON TOUR" en permanence.
            if (state.CurrentPhase == CombatPhase.TurnActive && activePlayer != _lastBannerActivePlayer)
            {
                _lastBannerActivePlayer = activePlayer;
                bool myTurn = activePlayer == LocalPlayerResolver.Resolve();
                var indicator = TurnIndicatorView.Instance;
                if (indicator != null)
                {
                    indicator.PlayTurnBanner(myTurn);
                }
                // A2 — SFX de début de tour (mien / adverse).
                Nymora.Core.Audio.NymoraAudioManager.Instance?.PlaySfx(
                    myTurn ? Nymora.Core.Audio.SoundId.TurnStartMine : Nymora.Core.Audio.SoundId.TurnStartEnemy);
            }

            // Slots : grisage selon PA / HG dispo du combattant qu'on controle, etat armed.
            // 2.13.c : passe aussi le turnNumber pour calcul du cooldown signature.
            RefreshSlots(hasLocal ? local : default, hasLocal, state.TurnNumber, localTurn, enemyHpRatio);

            // Patch 5 juin — badges Rubis PA/PM du joueur local, à gauche de la barre de sorts.
            RefreshResourceGems(hasLocal ? local : default, hasLocal);

            // End Turn : seul le joueur actif peut le presser. (Si _debugAllPlayersControllable
            // est false et qu'on n'est pas le joueur actif, on grise le bouton.)
            if (_endTurnButton != null)
            {
                bool canEnd = state.CurrentPhase == CombatPhase.TurnActive
                              && controlPlayer == activePlayer;
                _endTurnButton.interactable = canEnd;
            }

            // 2.16.c.ii — Overlay Victory/Defeat affiche sur MatchEnd. Polled chaque frame
            // mais Refresh() est idempotent (no-op tant qu'on est deja dans le bon etat).
            if (_matchEndOverlay != null)
            {
                bool isPvpMatch = frame.RuntimeConfig != null && !frame.RuntimeConfig.IsBotMatch;
                _matchEndOverlay.Refresh(state.CurrentPhase, state.WinnerPlayerIndex, _localPlayerIndex, state.TurnNumber, isPvpMatch);
            }
        }

        private void RefreshSlots(in Combatant c, bool valid, int turnNumber, bool localTurn, int enemyHpRatio)
        {
            for (int i = 0; i < _spellSlots.Length; i++)
            {
                var slot = _spellSlots[i];
                if (slot == null) continue;
                // J10 — hors du tour du joueur (ex : tour du bot en IA) la barre est grisee.
                var st = localTurn ? ResolveSlotState(slot.Spell, c, valid, turnNumber, enemyHpRatio) : SpellSlotView.SlotState.Disabled;
                slot.SetState(st);
                slot.SetCooldownLabel(ResolveCooldownTurnsLeft(slot.Spell, c, valid, turnNumber));
                slot.SetPaCost(ResolveBadgePaCost(slot.Spell, c, valid, turnNumber, enemyHpRatio), ResolveBadgeColor(slot.Spell));
            }
            if (_signatureSlot != null)
            {
                var sigState = localTurn ? ResolveSlotState(_signatureSlot.Spell, c, valid, turnNumber, enemyHpRatio) : SpellSlotView.SlotState.Disabled;
                _signatureSlot.SetState(sigState);
                _signatureSlot.SetCooldownLabel(ResolveCooldownTurnsLeft(_signatureSlot.Spell, c, valid, turnNumber));
                _signatureSlot.SetPaCost(ResolveBadgePaCost(_signatureSlot.Spell, c, valid, turnNumber, enemyHpRatio), ResolveBadgeColor(_signatureSlot.Spell));
            }
            // 19 mai POLISH-6g — Signature visible UNIQUEMENT quand la ressource max est atteinte
            // (HG/PR/FD/PT pour 4 classes, ou 3 leurres actifs pour Ghostra). Cast consomme la
            // ressource -> repasse sous max -> SetUnlocked(false) -> slot cache. Recharge ->
            // re-anim apparition automatiquement.
            if (_signatureEnhancer != null)
            {
                _signatureEnhancer.SetUnlocked(valid && localTurn && IsSignatureUnlocked(c));
            }
        }

        // ====================================================================
        // Badges Rubis PA / PM du joueur local (patch 5 juin) — créés au runtime,
        // ancrés juste à GAUCHE de la barre de sorts (LayoutElement ignoreLayout +
        // ancrage relatif à la barre -> robuste quelle que soit la position de la barre).
        // ====================================================================

        private void RefreshResourceGems(in Combatant c, bool valid)
        {
            if (!_showResourceGems) return;
            // Spectateur (B, 8 juin) : pas de gems PA/PM — le spectateur ne doit pas voir les
            // ressources du joueur actif « comme s'il jouait le tour ».
            if (Nymora.Combat.Spectate.LiveSpectateController.LiveSpectateActive)
            {
                if (_resourceGemsRoot != null && _resourceGemsRoot.gameObject.activeSelf)
                    _resourceGemsRoot.gameObject.SetActive(false);
                return;
            }
            EnsureResourceGems();
            if (_resourceGemsRoot == null) return; // barre pas encore prête

            bool show = valid && c.HP > 0;
            if (_resourceGemsRoot.gameObject.activeSelf != show)
            {
                _resourceGemsRoot.gameObject.SetActive(show);
            }
            if (!show) return;

            if (_paGemValue != null) _paGemValue.text = c.PA.ToString();
            if (_pmGemValue != null) _pmGemValue.text = c.PM.ToString();
        }

        private void EnsureResourceGems()
        {
            if (_resourceGemsRoot != null) return;
            RectTransform bar = SpellBarRect;
            if (bar == null) return; // barre de sorts pas encore câblée -> on réessaiera au prochain refresh

            var rootGO = new GameObject("ResourceGems(PA/PM)", typeof(RectTransform));
            rootGO.transform.SetParent(bar, false);
            _resourceGemsRoot = (RectTransform)rootGO.transform;
            // Ignore un éventuel HorizontalLayoutGroup de la barre (sinon les gemmes seraient
            // disposées comme des slots).
            var le = rootGO.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
            // Ancré au bord GAUCHE-centre de la barre, pivot à droite -> les gemmes débordent à gauche.
            _resourceGemsRoot.anchorMin = _resourceGemsRoot.anchorMax = new Vector2(0f, 0.5f);
            _resourceGemsRoot.pivot = new Vector2(1f, 0.5f);
            _resourceGemsRoot.sizeDelta = new Vector2(_resourceGemSize, _resourceGemSize * 2f + _resourceGemSpacing);
            _resourceGemsRoot.anchoredPosition = new Vector2(-_resourceGemsGap, 0f) + _resourceGemsExtraOffset;

            // PA (haut) = rubis rouge ; PM (bas) = vert.
            _paGemValue = CreateResourceGem("PaGem", PaGemColor, top: true);
            _pmGemValue = CreateResourceGem("PmGem", PmGemColor, top: false);
        }

        // Crée une gemme (diamant arrondi + chiffre upright), ancrée en haut ou en bas du root.
        // Même style que le badge de coût des SpellSlotView (CombatUiKit.ApplyRounded + rotation 45°).
        private TMP_Text CreateResourceGem(string name, Color color, bool top)
        {
            var gemGO = new GameObject(name, typeof(RectTransform));
            gemGO.transform.SetParent(_resourceGemsRoot, false);
            var grt = (RectTransform)gemGO.transform;
            grt.sizeDelta = new Vector2(_resourceGemSize, _resourceGemSize);
            grt.anchorMin = grt.anchorMax = new Vector2(0.5f, top ? 1f : 0f);
            grt.pivot = new Vector2(0.5f, top ? 1f : 0f);
            grt.anchoredPosition = Vector2.zero;

            // Diamant = carré arrondi tourné 45°.
            var diamondGO = new GameObject("Gem", typeof(RectTransform));
            diamondGO.transform.SetParent(grt, false);
            var drt = (RectTransform)diamondGO.transform;
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;
            drt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            drt.localScale = Vector3.one * 0.72f;
            var img = diamondGO.AddComponent<Image>();
            CombatUiKit.ApplyRounded(img, 6f);
            img.color = color;
            img.raycastTarget = false;

            // Chiffre (valeur courante), non tourné.
            var txtGO = new GameObject("Value", typeof(RectTransform));
            txtGO.transform.SetParent(grt, false);
            var trt = (RectTransform)txtGO.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var txt = txtGO.AddComponent<TextMeshProUGUI>();
            txt.alignment = TextAlignmentOptions.Center;
            txt.fontSize = 20f;
            txt.fontStyle = FontStyles.Bold;
            txt.color = Color.white;
            txt.raycastTarget = false;
            txt.enableWordWrapping = false;
            return txt;
        }

        /// <summary>
        /// True si le combattant a sa ressource max -> signature debloque.
        /// Ghostra : 3 leurres actifs. Autres : c.Resource >= max via CombatantStats.
        /// </summary>
        private static bool IsSignatureUnlocked(in Combatant c)
        {
            if (c.Class == NymoraClass.Ghostra)
            {
                int active = 0;
                for (int i = 0; i < 3; i++)
                {
                    if (c.Decoys[i].Kind != DecoyKind.None) active++;
                }
                return active >= 3;
            }
            int max = CombatantStats.GetMaxResource(c.Class);
            return max > 0 && c.Resource >= max;
        }

        private SpellSlotView.SlotState ResolveSlotState(SpellId spell, in Combatant c, bool valid, int turnNumber, int enemyHpRatio)
        {
            if (_armedSpell.HasValue && _armedSpell.Value == spell)
            {
                return SpellSlotView.SlotState.Armed;
            }
            if (!valid || spell == SpellId.None) return SpellSlotView.SlotState.Disabled;
            if (!SpellRegistry.TryGet(spell, out SpellDef def)) return SpellSlotView.SlotState.Disabled;

            // Coût PA EFFECTIF (mêmes bonus que la sim : Appel du Sang -1, Permutation Angle 3 = 0,
            // Effondrement -1) → grisage cohérent avec le badge rubis affiché.
            int paCost = ComputeEffectivePaCost(c, def, enemyHpRatio, turnNumber);

            if (c.PA < paCost) return SpellSlotView.SlotState.Disabled;
            if (c.Resource < def.HGCostMandatory) return SpellSlotView.SlotState.Disabled;

            // 2.13.c : cooldown (signatures + relances generiques) et 1/match (Pacte, Dernier Souffle).
            if (ResolveCooldownTurnsLeft(spell, c, valid: true, turnNumber) > 0) return SpellSlotView.SlotState.Disabled;
            if (def.OncePerMatchBit != SpellRegistry.OncePerMatchBitNone
                && (c.OncePerMatchUsedFlags & (1 << def.OncePerMatchBit)) != 0)
            {
                return SpellSlotView.SlotState.Disabled;
            }
            // Refonte 29 mai : cap "Nx/tour" atteint -> grise (plus castable ce tour).
            if (def.MaxUsesPerTurn > 0
                && SpellLimitsHelper.UsesThisTurn(c, spell, turnNumber) >= def.MaxUsesPerTurn)
            {
                return SpellSlotView.SlotState.Disabled;
            }

            return SpellSlotView.SlotState.Normal;
        }

        /// <summary>
        /// Patch UI combat 8 juin (3d) — Si le sort n'est PAS castable maintenant (combattant local,
        /// ce tour), renvoie true + une raison FR lisible pour le chat. Sinon false. Miroir exact des
        /// conditions de grisage de <see cref="ResolveSlotState"/>. Si l'état local n'est pas encore
        /// caché (début de match), renvoie false (on laisse la sim trancher).
        /// </summary>
        private bool TryGetUncastableReason(SpellId spell, out string reason)
        {
            reason = null;
            if (spell == SpellId.None) return false;
            if (!_hasCachedLocal) return false;
            if (!SpellRegistry.TryGet(spell, out SpellDef def)) return false;

            Combatant c = _cachedLocal;
            int turnNumber = _cachedTurnNumber;
            int enemyHpRatio = _cachedEnemyHpRatio;

            // Interdits au tour 1 (miroir ResolveCooldownTurnsLeft).
            if (turnNumber <= 1
                && (spell == SpellId.GhostraPasDansLOmbre
                    || spell == SpellId.NightseerPasFurtif
                    || spell == SpellId.NightseerMarqueDuChasseur))
            {
                reason = "Indisponible au tour 1";
                return true;
            }

            int paCost = ComputeEffectivePaCost(c, def, enemyHpRatio, turnNumber);
            if (c.PA < paCost)
            {
                reason = $"PA insuffisants ({paCost} requis, {c.PA} dispo)";
                return true;
            }

            if (c.Resource < def.HGCostMandatory)
            {
                reason = $"{ResourceFullName(c.Class)} insuffisant ({def.HGCostMandatory} requis)";
                return true;
            }

            int cd = ResolveCooldownTurnsLeft(spell, c, valid: true, turnNumber);
            if (cd > 0)
            {
                reason = $"En relance ({cd} tour{(cd > 1 ? "s" : "")})";
                return true;
            }

            if (def.OncePerMatchBit != SpellRegistry.OncePerMatchBitNone
                && (c.OncePerMatchUsedFlags & (1 << def.OncePerMatchBit)) != 0)
            {
                reason = "Déjà utilisé ce match";
                return true;
            }

            if (def.MaxUsesPerTurn > 0
                && SpellLimitsHelper.UsesThisTurn(c, spell, turnNumber) >= def.MaxUsesPerTurn)
            {
                reason = $"Limite atteinte ce tour ({def.MaxUsesPerTurn}x max)";
                return true;
            }

            return false;
        }

        private static string ResourceFullName(NymoraClass cls)
        {
            switch (cls)
            {
                case NymoraClass.Soulrender: return "Hémoglyphe";
                case NymoraClass.Nightseer:  return "Prescience";
                case NymoraClass.Colossar:   return "Fondation";
                case NymoraClass.Necram:     return "Putréfaction";
                case NymoraClass.Ghostra:    return "Rémanence";
                default: return "Ressource";
            }
        }

        // ---- Coût PA effectif (badge rubis + grisage + tooltip) ----

        // Cache du dernier état (rempli par OnUpdateView) pour calculer le PA effectif au survol
        // du tooltip, qui se déclenche hors OnUpdateView.
        private Combatant _cachedLocal;
        private bool _hasCachedLocal;
        private int _cachedEnemyHpRatio = 100;
        private int _cachedTurnNumber;

        // 5.5d (2v2/3v3) — buffers par PlayerIndex pour la timeline N portraits. Champs (pas de
        // (ré)allocation par frame -> 0 GC dans OnUpdateView). Remplis dans le filter loop.
        private readonly Combatant[] _tlByPlayer = new Combatant[Quantum.TurnConstants.MaxPlayers];
        private readonly bool[] _tlPresent = new bool[Quantum.TurnConstants.MaxPlayers];
        private readonly string[] _tlSkin = new string[Quantum.TurnConstants.MaxPlayers];
        // 5.5d — ordre d'affichage = ordre de jeu (TurnOrder : A0,B0,A1,B1). Rempli depuis le sim.
        private readonly int[] _tlOrder = new int[Quantum.TurnConstants.MaxPlayers];
        // 5.5e — hot-seat : dernier joueur actif pour lequel la barre de sorts a été (re)bindée.
        private int _lastDeckPlayer = -1;
        // 5.5e — cache du deck (6 SpellId) par joueur. Rempli dès que GetPlayerData est lisible
        //   (n'importe quelle frame, pas seulement à la transition de tour où il peut renvoyer null),
        //   puis stable. La barre se re-bind DEPUIS ce cache -> plus de barre vide au round 2.
        private readonly SpellId[][] _deckCache = NewDeckCache();
        private readonly bool[] _deckCached = new bool[Quantum.TurnConstants.MaxPlayers];

        private static SpellId[][] NewDeckCache()
        {
            var a = new SpellId[Quantum.TurnConstants.MaxPlayers][];
            for (int i = 0; i < a.Length; i++) a[i] = new SpellId[6];
            return a;
        }

        /// <summary>
        /// Coût PA EFFECTIF d'un sort, réplique managée de EffectiveStats.GetPACost (sim) :
        ///   - Ghostra : Permutation gratuite (0 PA) à l'Angle 3 (3 leurres actifs).
        ///   - Soulrender : -1 PA sur le 1ER sort du tour si l'ennemi est sous 70% HP (Appel du Sang).
        ///   - Effondrement actif (Colossar) : -1 PA. Min 1 (hors Permutation gratuite).
        /// </summary>
        private int ComputeEffectivePaCost(in Combatant c, in SpellDef def, int enemyHpRatio, int turnNumber)
        {
            // Permutation (Filter TileWithLure) gratuite à l'Angle 3 — court-circuite.
            if (def.Filter == TargetingFilter.TileWithLure && c.Class == NymoraClass.Ghostra)
            {
                int active = 0;
                for (int i = 0; i < 3; i++) if (c.Decoys[i].Kind != DecoyKind.None) active++;
                if (active >= 3) return 0;
            }

            int cost = def.PACost;
            if (c.Class == NymoraClass.Soulrender
                && enemyHpRatio < SpellRegistry.AppelDuSangPalierMarquage
                && SpellLimitsHelper.TotalCastsThisTurn(c, turnNumber) == 0)
            {
                cost -= 1; if (cost < 1) cost = 1;
            }
            if (HasStatus(c, StatusKind.EffondrementActive))
            {
                cost -= 1; if (cost < 1) cost = 1;
            }
            // Provocation (patch 7 juin) : +1 PA sur TOUS les sorts tant que provoqué (miroir
            //   EffectiveStats.GetPACost). Inclus ici -> badge ET grisé reflètent le surcoût.
            if (HasStatus(c, StatusKind.Provoked))
            {
                cost += SpellRegistry.ProvocationCostBump;
            }
            return cost;
        }

        /// <summary>
        /// Coût PA À AFFICHER (badge rubis + tooltip). Patch 7 juin : le surcoût Provocation (+1 PA)
        /// est désormais inclus dans ComputeEffectivePaCost (s'applique à TOUS les sorts du provoqué,
        /// plus d'exception "cible le provocateur") -> badge et grisé cohérents avec la sim.
        /// </summary>
        private int ComputeDisplayPaCost(in Combatant c, in SpellDef def, int enemyHpRatio, int turnNumber)
        {
            return ComputeEffectivePaCost(c, def, enemyHpRatio, turnNumber);
        }

        /// <summary>PA à afficher dans le badge rubis (-1 = pas de badge : slot vide).</summary>
        private int ResolveBadgePaCost(SpellId spell, in Combatant c, bool valid, int turnNumber, int enemyHpRatio)
        {
            if (spell == SpellId.None) return -1;
            if (!SpellRegistry.TryGet(spell, out SpellDef def)) return -1;
            if (!valid) return def.PACost; // pas de combattant résolu → coût de base
            return ComputeDisplayPaCost(c, def, enemyHpRatio, turnNumber);
        }

        // ---- Couleur du losange selon la catégorie du sort (offensif/tactique/survie) ----
        private static readonly Color BadgeColorOffensive = new Color(0.86f, 0.15f, 0.20f, 1f); // rouge
        private static readonly Color BadgeColorTactical  = new Color(0.27f, 0.50f, 0.86f, 1f); // bleu
        private static readonly Color BadgeColorSurvival  = new Color(0.27f, 0.70f, 0.40f, 1f); // vert
        private static readonly Color BadgeColorOther     = new Color(0.80f, 0.65f, 0.25f, 1f); // signature / défaut (ambre)

        // Cache SpellId -> catégorie (depuis le SpellCatalog Core, via QuantumSpellIdValue).
        private System.Collections.Generic.Dictionary<SpellId, Nymora.Core.Enums.SpellCategory> _categoryBySpell;

        private Color ResolveBadgeColor(SpellId spell)
        {
            if (spell == SpellId.None) return BadgeColorOther;
            if (_categoryBySpell == null)
            {
                _categoryBySpell = new System.Collections.Generic.Dictionary<SpellId, Nymora.Core.Enums.SpellCategory>(128);
                if (_spellCatalog != null)
                {
                    foreach (var s in _spellCatalog.Spells)
                        if (s != null) _categoryBySpell[(SpellId)s.QuantumSpellIdValue] = s.Category;
                }
            }
            if (_categoryBySpell.TryGetValue(spell, out var cat))
            {
                switch (cat)
                {
                    case Nymora.Core.Enums.SpellCategory.Offensive: return BadgeColorOffensive;
                    case Nymora.Core.Enums.SpellCategory.Tactical:  return BadgeColorTactical;
                    case Nymora.Core.Enums.SpellCategory.Survival:  return BadgeColorSurvival;
                }
            }
            return BadgeColorOther;
        }

        /// <summary>
        /// Tours de relance restants pour un sort, ou 0 s'il est dispo. Couvre :
        ///   - les SIGNATURES a champ dedie (cooldown 4t) des 5 classes ;
        ///   - les RELANCES GENERIQUES (refonte 29 mai) declarees dans SpellDef.CooldownTurns
        ///     (Rugissement 2t, Peau de Fer 2t, etc.), lues via SpellLimitsHelper.
        /// Pilote le grisage (ResolveSlotState) ET le label "Nt" du slot.
        /// </summary>
        private static int ResolveCooldownTurnsLeft(SpellId spell, in Combatant c, bool valid, int turnNumber)
        {
            if (!valid || spell == SpellId.None) return 0;

            // Patch 7 juin — Pas dans l'Ombre / Pas Furtif / Affût (Marque du Chasseur) INTERDITS au
            //   tour 1 (miroir du gate sim SpellSystem.OnCast). On affiche "1t" + grisé pendant le
            //   round 1 (dispo au tour 2).
            if (turnNumber <= 1
                && (spell == SpellId.GhostraPasDansLOmbre
                    || spell == SpellId.NightseerPasFurtif
                    || spell == SpellId.NightseerMarqueDuChasseur))
            {
                return 1;
            }

            // Signatures a champ dedie (init -1000 -> jamais en cooldown au depart).
            switch (spell)
            {
                case SpellId.SoulrenderAmeLaceree:
                    return RemainingCd(SpellRegistry.AmeLaceeCooldownTurns, turnNumber, c.LastAmeLaceeUsedOnTurn);
                case SpellId.NightseerTraquenard:
                    return RemainingCd(SpellRegistry.TraquenardCooldownTurns, turnNumber, c.LastTraquenardUsedOnTurn);
                case SpellId.ColossarEffondrement:
                    return RemainingCd(SpellRegistry.EffondrementCooldownTurns, turnNumber, c.LastEffondrementUsedOnTurn);
                case SpellId.NecramVirusFatal:
                    return RemainingCd(SpellRegistry.VirusFatalCooldownTurns, turnNumber, c.LastVirusFatalUsedOnTurn);
                case SpellId.GhostraExecutionSpectrale:
                    return RemainingCd(SpellRegistry.ExecutionSpectraleCooldownTurns, turnNumber, c.LastExecutionSpectraleUsedOnTurn);
            }

            // Relances generiques (SpellLimitsHelper + SpellCooldownLastTurn).
            if (SpellRegistry.TryGet(spell, out SpellDef def) && def.CooldownTurns > 0)
            {
                return SpellLimitsHelper.CooldownRemaining(c, spell, def.CooldownTurns, turnNumber);
            }
            return 0;
        }

        /// <summary>Helper : tours restants = cooldown - (tour courant - dernier usage), clamp >= 0.</summary>
        private static int RemainingCd(int cooldownTurns, int turnNumber, int lastUsedTurn)
        {
            int remaining = cooldownTurns - (turnNumber - lastUsedTurn);
            return remaining > 0 ? remaining : 0;
        }

        private static bool HasStatus(in Combatant c, StatusKind kind)
        {
            for (int i = 0; i < 8; i++)
            {
                var s = c.Statuses[i];
                if (s.Kind == kind && s.TurnsLeft > 0) return true;
            }
            return false;
        }

        /// <summary>
        /// Click sur un slot : passe le HUD en mode armed. Le prochain clic grille (gere
        /// par CombatInputController) envoie le CastSpellCommand. Pour les sorts Filter=Self,
        /// l'input controller redirige la cible vers la case du caster, donc Lorenzo peut
        /// cliquer n'importe ou pour confirmer.
        ///
        /// Re-clic sur le sort deja arme = annulation.
        /// </summary>
        public void OnSlotClicked(SpellId spell)
        {
            if (spell == SpellId.None) return;
            // J10 — pas d'armement hors de son tour (ex : on ne pilote pas le sort du bot en IA
            // pendant son tour). La barre est de toute facon grisee, mais on bloque aussi le clic.
            // Patch 3d — on le DIT dans le chat (throttlé) pour les nouveaux joueurs.
            if (!_isLocalTurn)
            {
                CombatCastFeedback.Notify("Ce n'est pas ton tour");
                return;
            }

            if (_armedSpell.HasValue && _armedSpell.Value == spell)
            {
                Disarm();
                return;
            }

            // Patch UI combat 8 juin (3d) — sort non castable : on n'arme pas et on explique pourquoi
            // dans le chat de combat (PA, relance, ressource, 1/match, cap, tour 1).
            if (TryGetUncastableReason(spell, out string reason))
            {
                CombatCastFeedback.Notify(reason);
                return;
            }

            _armedSpell = spell;
            ArmedSpellChanged?.Invoke();
            Debug.Log($"[Nymora.HUD] Armed {spell} (cliquez sur une case pour lancer)");
        }

        /// <summary>
        /// Appele par CombatInputController quand le joueur clique sur la grille.
        /// Si un sort est arme : retourne true + le SpellId, et clear l'etat armed.
        /// Sinon : false (laisser passer le MoveCommand).
        /// </summary>
        public bool ConsumeArmedSpell(out SpellId spell)
        {
            if (_armedSpell.HasValue)
            {
                spell = _armedSpell.Value;
                Disarm();
                return true;
            }
            spell = SpellId.None;
            return false;
        }

        public void Disarm()
        {
            if (_armedSpell.HasValue)
            {
                _armedSpell = null;
                ArmedSpellChanged?.Invoke();
            }
        }

        /// <summary>
        /// POLISH-4 (deckbuilder polish) : arme un sort via son index de slot, equivalent
        /// au clic souris sur SpellSlotView. Appele par CombatInputController quand le joueur
        /// presse les touches 1-7 (Alpha1..Alpha7) : 0..5 = deck slots (sorts equipes du deck
        /// de 6 sorts Bible V7.1), 6 = signature slot.
        ///
        /// Renvoie true si un sort valide a ete arme (ou desarme si re-clic). False si index
        /// invalide ou slot vide (SpellId.None).
        /// </summary>
        public bool TryArmSlotByIndex(int slotIndex)
        {
            SpellId spell = SpellId.None;
            if (slotIndex >= 0 && slotIndex < _testDeck.Length)
            {
                spell = _testDeck[slotIndex];
            }
            else if (slotIndex == _testDeck.Length)
            {
                // slot signature (touche Alpha7 si deck=6)
                spell = _signatureSpell;
            }
            else
            {
                return false;
            }
            if (spell == SpellId.None) return false;
            OnSlotClicked(spell);
            return true;
        }

        // -- Tooltip API (2.13.c) --

        public void ShowTooltip(SpellId spell, RectTransform anchor)
        {
            if (_tooltip == null) return;
            int pa = -1;
            if (_hasCachedLocal && SpellRegistry.TryGet(spell, out SpellDef def))
                pa = ComputeDisplayPaCost(_cachedLocal, def, _cachedEnemyHpRatio, _cachedTurnNumber);
            // Passe le combattant local -> le tooltip résout les valeurs buffées (dégâts/portée en vert).
            _tooltip.Show(spell, anchor, pa, _cachedLocal, _hasCachedLocal);
        }

        public void HideTooltip()
        {
            if (_tooltip != null) _tooltip.Hide();
        }

        /// <summary>#24 (5 juin) — raccourci clavier F1 : passe le tour (même chemin/gardes que le bouton Fin de tour).</summary>
        public void RequestEndTurnHotkey() => OnEndTurnClicked();

        private void OnEndTurnClicked()
        {
            var game = QuantumRunner.Default?.Game;
            if (game == null) return;
            var frame = game.Frames.Verified;
            if (frame == null) return;
            if (!frame.TryGetSingleton<CombatState>(out var state)) return;

            // POLISH-2 : si c'est le tour du bot IA (P1 hardcoded Phase 2-5), ignore le clic.
            // Le bot finit son propre tour via AISystem.EndBotTurn. Spam Espace / clic End Turn
            // pendant le tour bot cassait son pacing ActionIntervalTicks et faisait sauter ses
            // casts/move (le tour passait avant que l'AI ait fini ses actions). Mode prod
            // 1-humain : le bouton ne devrait pas etre cliquable pendant tour IA, mais ce
            // garde-fou ferme aussi le cas spam-clic preventif.
            // 4.14.f hotfix — En PvP (IsBotMatch=false), le slot 1 EST un humain, pas un bot.
            // Le guard doit etre skip sinon dev-2 (slot 1) ne peut JAMAIS passer son tour.
            bool isBotMatch = frame.RuntimeConfig != null && frame.RuntimeConfig.IsBotMatch;
            if (isBotMatch && state.ActivePlayerIndex == AIConstants.BotPlayerIndex)
            {
                Debug.Log($"[Nymora.HUD] EndTurnCommand IGNORE : tour du bot P{state.ActivePlayerIndex} en cours (le bot finit seul via AISystem)");
                return;
            }

            int senderPlayer = ResolveControlPlayer(state.ActivePlayerIndex);
            // ATTENTION : SendCommand attend le splitscreen slot LOCAL (= 0 en prod 1-local-
            // par-client), PAS le PlayerRef GLOBAL Quantum. Bug 19 mai 2026 : on passait
            // senderPlayer (PlayerRef global) -> si Quantum attribuait PlayerRef=1 au client
            // (race AddPlayer ordering PvP, cf project_quantum_playerref_resolution),
            // Quantum cherchait un local player au splitscreen slot 1 introuvable -> plugin
            // renvoie Error #19 "Player not found" et disconnect. Cf rationale identique
            // dans CombatInputController.Update.
            int splitscreenSlot = _debugAllPlayersControllable ? senderPlayer : 0;
            game.SendCommand(splitscreenSlot, new EndTurnCommand());
            Debug.Log($"[Nymora.HUD] EndTurnCommand sent player={senderPlayer} splitscreenSlot={splitscreenSlot}");
            Disarm(); // securite : passer le tour annule un eventuel armement
        }

        private int ResolveControlPlayer(int activePlayer)
        {
            return _debugAllPlayersControllable ? activePlayer : _localPlayerIndex;
        }
    }
}
