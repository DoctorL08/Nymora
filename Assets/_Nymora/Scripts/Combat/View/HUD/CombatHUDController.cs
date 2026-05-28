using System;
using Nymora.Core.Data;
using Nymora.Core.ScriptableObjects;
using Quantum;
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
        [SerializeField] private TimerView _timer;
        [SerializeField] private PassivePanelView _passive;
        [SerializeField] private TimelineView _timeline;
        [SerializeField] private SpellSlotView[] _spellSlots = new SpellSlotView[6];
        [SerializeField] private SpellSlotView _signatureSlot;
        [SerializeField] private Button _endTurnButton;
        [SerializeField] private SpellTooltipView _tooltip;
        [SerializeField] private MatchEndOverlay _matchEndOverlay;

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

            // Filter combatants une seule fois ; cache localement P0/P1 et le combatant local.
            Combatant p0 = default, p1 = default;
            bool hasP0 = false, hasP1 = false;
            Combatant local = default;
            bool hasLocal = false;
            var filter = frame.Filter<Combatant>();
            while (filter.Next(out EntityRef _, out Combatant c))
            {
                if (c.PlayerIndex == 0) { p0 = c; hasP0 = true; }
                if (c.PlayerIndex == 1) { p1 = c; hasP1 = true; }
                if (c.PlayerIndex == controlPlayer) { local = c; hasLocal = true; }
            }

            // ResourcePanel
            if (_p0Panel != null) { if (hasP0) _p0Panel.Refresh(p0, activePlayer == 0); else _p0Panel.Clear(); }
            if (_p1Panel != null) { if (hasP1) _p1Panel.Refresh(p1, activePlayer == 1); else _p1Panel.Clear(); }

            // Timer
            if (_timer != null)
            {
                int updateRate = frame.UpdateRate;
                float seconds = updateRate > 0 ? state.TurnTimerTicks / (float)updateRate : 0f;
                _timer.Refresh(seconds, state.TurnNumber);
            }

            // Passif (combattant qu'on controle)
            if (_passive != null) { if (hasLocal) _passive.Refresh(local); else _passive.Clear(); }

            // Timeline (sprites idle animes P0/P1 + highlight actif — refacto 19 mai)
            // + cache des combatants pour le tooltip hover (phase + statuses + marques).
            if (_timeline != null)
            {
                _timeline.RefreshWithCombatants(activePlayer, p0, hasP0, p1, hasP1, state.TurnNumber);
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
            RefreshSlots(hasLocal ? local : default, hasLocal, state.TurnNumber, localTurn);

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

        private void RefreshSlots(in Combatant c, bool valid, int turnNumber, bool localTurn)
        {
            for (int i = 0; i < _spellSlots.Length; i++)
            {
                var slot = _spellSlots[i];
                if (slot == null) continue;
                // J10 — hors du tour du joueur (ex : tour du bot en IA) la barre est grisee.
                var st = localTurn ? ResolveSlotState(slot.Spell, c, valid, turnNumber) : SpellSlotView.SlotState.Disabled;
                slot.SetState(st);
                slot.SetCooldownLabel(ResolveCooldownTurnsLeft(slot.Spell, c, valid, turnNumber));
            }
            if (_signatureSlot != null)
            {
                var sigState = localTurn ? ResolveSlotState(_signatureSlot.Spell, c, valid, turnNumber) : SpellSlotView.SlotState.Disabled;
                _signatureSlot.SetState(sigState);
                _signatureSlot.SetCooldownLabel(ResolveCooldownTurnsLeft(_signatureSlot.Spell, c, valid, turnNumber));
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

        private SpellSlotView.SlotState ResolveSlotState(SpellId spell, in Combatant c, bool valid, int turnNumber)
        {
            if (_armedSpell.HasValue && _armedSpell.Value == spell)
            {
                return SpellSlotView.SlotState.Armed;
            }
            if (!valid || spell == SpellId.None) return SpellSlotView.SlotState.Disabled;
            if (!SpellRegistry.TryGet(spell, out SpellDef def)) return SpellSlotView.SlotState.Disabled;

            // Cout PA effectif approxime (base + RageInsatiable). Le bonus -1 PA du passif
            // Soulrender depend de la cible visee donc on l'ignore pour le grisage initial.
            int paCost = def.PACost;
            if (HasStatus(c, StatusKind.RageInsatiableActive)) paCost += 1;
            if (paCost < 1) paCost = 1;

            if (c.PA < paCost) return SpellSlotView.SlotState.Disabled;
            if (c.Resource < def.HGCostMandatory) return SpellSlotView.SlotState.Disabled;

            // 2.13.c : cooldown (signature Ame Laceree) et 1/match (Pacte de Sang, Dernier Souffle).
            if (ResolveCooldownTurnsLeft(spell, c, valid: true, turnNumber) > 0) return SpellSlotView.SlotState.Disabled;
            if (def.OncePerMatchBit != SpellRegistry.OncePerMatchBitNone
                && (c.OncePerMatchUsedFlags & (1 << def.OncePerMatchBit)) != 0)
            {
                return SpellSlotView.SlotState.Disabled;
            }

            return SpellSlotView.SlotState.Normal;
        }

        /// <summary>
        /// Tours de cooldown restants pour un sort, ou 0 s'il est dispo. Pour 2.13.c,
        /// seule la signature Ame Laceree a un cooldown de 4 tours. Les autres retournent 0.
        /// </summary>
        private static int ResolveCooldownTurnsLeft(SpellId spell, in Combatant c, bool valid, int turnNumber)
        {
            if (!valid || spell != SpellId.SoulrenderAmeLaceree) return 0;
            int sinceLast = turnNumber - c.LastAmeLaceeUsedOnTurn;
            int remaining = SpellRegistry.AmeLaceeCooldownTurns - sinceLast;
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
            if (!_isLocalTurn) return;

            if (_armedSpell.HasValue && _armedSpell.Value == spell)
            {
                Disarm();
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
            if (_tooltip != null) _tooltip.Show(spell, anchor);
        }

        public void HideTooltip()
        {
            if (_tooltip != null) _tooltip.Hide();
        }

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
