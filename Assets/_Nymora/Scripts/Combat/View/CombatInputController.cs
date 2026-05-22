using Nymora.Combat.Grid;
using Nymora.Combat.View.HUD;
using Quantum;
using UnityEngine;

namespace Nymora.Combat.View
{
    /// <summary>
    /// Detecte les clics souris sur la grille de combat et envoie une MoveCommand
    /// ou un CastSpellCommand au runtime Quantum.
    ///
    /// POLISH-4 (clôture Phase 3) : refonte deckbuilder. Tous les binds clavier directs
    /// vers des sorts hardcodes ont ete supprimes (A/Z/E/R/T/Y/U/I/O/P/Q/S/D/F/G/H/J/K/L/M/W/X/C/V/B/N/Space
    /// + shift variantes). Seuls sont conserves :
    ///   - Clic souris : move ou cast (si sort arme via la barre de sort HUD).
    ///   - Alpha1..Alpha6 (touches `&é"'(-`  AZERTY) : arment les 6 slots du deck HUD.
    ///   - Alpha7 (touche `è` AZERTY) : arme le slot SIGNATURE HUD.
    /// Tous les F-keys debug (PlaieOuverte / BleedDoT / SangCoagule / VapeurCarmin / BrumeToxique /
    /// FiletRonces / Mine / ShieldActive / Untargetable / MarqueDeLOmbre / ApplyVenin / SpawnDecoy
    /// + variantes Shift MarkedByCarnage / Traque / Empreinte) ont ete supprimes a la cloture
    /// polish Phase 3 (17 mai 2026).
    ///
    /// En 2.4 : pas de matchmaking ni de vrai local player — par defaut on envoie au
    /// JOUEUR ACTIF (debug mode "all movable") pour permettre de tester P0 puis P1 sans
    /// attendre l'alternance auto. A desactiver en Phase 6 quand on aura un vrai
    /// LocalPlayerIndex defini par le runner Photon.
    /// </summary>
    public class CombatInputController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private GridSettings _gridSettings;
        [SerializeField] private Camera _camera;

        [Tooltip("HUD controller. Si set, un sort 'arme' (clic icone ou touche Alpha1-7) intercepte le clic " +
                 "gauche pour envoyer un Cast a la place du Move (2.13.a, option 2).")]
        [SerializeField] private CombatHUDController _hudController;

        [Header("Local player")]
        [SerializeField] private int _localPlayerIndex = 0;

        [Tooltip("Si vrai (default Phase 2), envoie la command au joueur ACTIF courant. " +
                 "Permet de tester P0 et P1 alternativement sans setup matchmaking. " +
                 "A desactiver en Phase 6 quand on aura un vrai local player.")]
        [SerializeField] private bool _debugAllPlayersMovable = true;

        [Header("Debug — local players")]
        [Tooltip("En 2.4 sans menu/matchmaking, on doit ajouter explicitement des players locaux pour pouvoir envoyer des commands. Sinon Quantum est en mode spectator et SendCommand est rejete.")]
        [SerializeField] private bool _autoAddLocalPlayers = true;
        [SerializeField] private int _autoAddPlayerCount = 2;

        [Header("Debug — targeting preview (brique 2.6)")]
        [Tooltip("Active la preview de targeting. Quand actif, le clic gauche ne deplace plus le combattant (bypass MoveCommand).")]
        [SerializeField] private bool _debugShowTargeting = false;
        [SerializeField] private TargetingShape _debugShape = TargetingShape.SingleTile;
        [SerializeField] private TargetingFilter _debugFilter = TargetingFilter.Enemy;
        [SerializeField] private int _debugRangeMin = 1;
        [SerializeField] private int _debugRangeMax = 4;

        // Expose les valeurs au TargetingPreviewView (read-only).
        public bool DebugShowTargeting => _debugShowTargeting;
        public TargetingShape DebugShape => _debugShape;
        public TargetingFilter DebugFilter => _debugFilter;
        public int DebugRangeMin => _debugRangeMin;
        public int DebugRangeMax => _debugRangeMax;

        private Vector3 _centerOffset;
        private bool _gridReady;

        private void Awake()
        {
            QuantumCallback.Subscribe(this, (CallbackGameStarted c) => OnGameStarted(c.Game));
            if (_camera == null) _camera = Camera.main;
        }

        private void OnGameStarted(QuantumGame game)
        {
            if (_gridSettings == null)
            {
                Debug.LogError("[Nymora.CombatInput] GridSettings manquant — drag l'asset.", this);
                return;
            }

            var frame = game.Frames.Verified;

            // 4.14.b — En mode PvP (IsBotMatch=false), CombatBootstrapCasual a deja fait
            // Game.AddPlayer(localSlot) avec un RuntimePlayer porteur du deck. L'auto-add
            // ici provoquerait "Failed to add player 0/1" (slots deja occupes par les 2
            // clients PvP). Skip dans ce cas.
            // 5.4 (18 mai 2026) — meme logique en IA : CombatBootstrapIA fait AddPlayer(0)
            // et AddPlayer(1) avec les bonnes classes (Lorenzo via DeckBridge, bot via
            // inspector). Skip si Instance present. Fallback auto-add legacy uniquement si
            // ni Casual ni IA bootstrap n'est en scene (mode dev raw-play 30_CombatIA sans
            // avoir fait la manip Unity de remplacement QuantumRunnerLocalDebug ->
            // CombatBootstrapIA — dans ce cas RuntimePlayer empty -> fallback Soulrender
            // cote CombatantSystem.OnPlayerAdded).
            bool isPvp = frame.RuntimeConfig != null && !frame.RuntimeConfig.IsBotMatch;
            bool hasIABootstrap = Nymora.Combat.Bootstrap.CombatBootstrapIA.Instance != null;
            bool bootstrapHandlesAddPlayer = isPvp || hasIABootstrap;

            if (_autoAddLocalPlayers && !bootstrapHandlesAddPlayer)
            {
                for (int i = 0; i < _autoAddPlayerCount; i++)
                {
                    game.AddPlayer(i, new RuntimePlayer());
                }
                Debug.LogWarning($"[Nymora.CombatInput] Auto-add fallback : {_autoAddPlayerCount} RuntimePlayer empty (pas de CombatBootstrap detecte). " +
                                 "Spawn en Soulrender par defaut. Pour avoir la classe choisie en hub : remplacer QuantumRunnerLocalDebug par CombatBootstrapIA dans la scene 30_CombatIA.");
            }
            else if (hasIABootstrap)
            {
                Debug.Log("[Nymora.CombatInput] Mode IA detecte avec CombatBootstrapIA — auto-add skip. _localPlayerIndex reste 0 (Lorenzo = slot 0 en IA).");
            }
            else if (isPvp)
            {
                Debug.Log("[Nymora.CombatInput] Mode PvP detecte (RuntimeConfig.IsBotMatch=false) — auto-add local skip (CombatBootstrapCasual a la charge des AddPlayer).");

                // 4.14.f hotfix — En PvP, le SendCommand DOIT etre envoye par le slot LOCAL
                // (celui sur lequel ce client a l'autorite). Sans ca, Quantum reject le command
                // avec "Player not found" (Error #19) et disconnect.
                // Bug 19 mai : Quantum dispatch CallbackGameStarted AVANT que CombatBootstrapCasual
                // ait pu resoudre LocalPlayerSlot via GetLocalPlayers. Si pas encore resolu, on
                // s'abonne a l'event LocalPlayerSlotResolved du bootstrap pour retry plus tard.
                var bootstrap = Nymora.Combat.Bootstrap.CombatBootstrapCasual.Instance;
                if (bootstrap == null)
                {
                    Debug.LogError("[Nymora.CombatInput] PvP mais CombatBootstrapCasual.Instance null — input combat va casser. Verifier l'ordre Awake/Start.");
                }
                else if (bootstrap.LocalPlayerSlot >= 0)
                {
                    ApplyLocalPlayerSlot(bootstrap.LocalPlayerSlot);
                }
                else
                {
                    Debug.LogWarning("[Nymora.CombatInput] LocalPlayerSlot pas encore resolu (Quantum CallbackGameStarted dispatche avant AddPlayer/GetLocalPlayers) — attente event LocalPlayerSlotResolved...");
                    bootstrap.LocalPlayerSlotResolved += ApplyLocalPlayerSlot;
                }
            }

            if (!frame.TryGetSingleton<GridSingleton>(out var grid))
            {
                Debug.LogError("[Nymora.CombatInput] GridSingleton introuvable.", this);
                return;
            }

            _centerOffset = _gridSettings.CenterGrid
                ? IsoProjection.CenterOffset(grid.Width, grid.Height, _gridSettings.TileWorldWidth, _gridSettings.TileWorldHeight)
                : Vector3.zero;
            _gridReady = true;
        }

        private void ApplyLocalPlayerSlot(int slot)
        {
            _localPlayerIndex = slot;
            // _debugAllPlayersMovable force false en PvP : sinon le sender devient
            // state.ActivePlayerIndex (slot actif du tour), et les 2 clients enverraient
            // leurs commands au meme slot = un sur deux est rejete "Player not found".
            _debugAllPlayersMovable = false;
            Debug.Log($"[Nymora.CombatInput] PvP: _localPlayerIndex={_localPlayerIndex} (depuis CombatBootstrapCasual.LocalPlayerSlot), _debugAllPlayersMovable=false.");

            var bootstrap = Nymora.Combat.Bootstrap.CombatBootstrapCasual.Instance;
            if (bootstrap != null) bootstrap.LocalPlayerSlotResolved -= ApplyLocalPlayerSlot;
        }

        private void Update()
        {
            if (!_gridReady) return;
            if (_camera == null) return;

            var game = QuantumRunner.Default?.Game;
            if (game == null) return;

            // PATCH 22 mai (test designer) — intro "pile ou face" casual : tant que l'animation
            // de revelation du premier joueur joue, on bloque l'input grille local (les clics UI
            // sont deja bloques par l'overlay plein ecran). Casual uniquement (l'intro ne
            // s'instancie qu'en scene Casual).
            if (Nymora.Combat.View.HUD.CoinFlipIntroView.IsIntroActive) return;

            // Qualifie UnityEngine.Input : Quantum a aussi un type "Input" (struct DSL).
            bool mouseDown = UnityEngine.Input.GetMouseButtonDown(0);

            // 2.13.a fix : si le clic gauche tombe sur un GameObject UI (icone HUD, bouton
            // End Turn, etc.), on l'ignore cote grille. Sinon le meme clic
            //   1) arme un sort via SpellSlotView.OnClick (event UI)
            //   2) ET, dans la meme frame, serait consume comme clic grille -> cast instantane.
            // Les inputs clavier restent traites normalement (UI ne capture pas les keys).
            if (mouseDown
                && UnityEngine.EventSystems.EventSystem.current != null
                && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                mouseDown = false;
            }

            // POLISH-4 — Alpha1..Alpha7 = slots barre de sort HUD.
            //   Alpha1..Alpha6 : 6 slots deck (Bible V7.1 "6 sorts par deck").
            //   Alpha7 : slot SIGNATURE (1 par classe, Bible V7.1).
            // Dispatch via CombatHUDController.TryArmSlotByIndex : equivalent au clic souris
            // sur le slot. Le sort arme attend ensuite un clic souris sur la grille pour
            // designer la cible (cf bloc mouseDown plus bas).
            bool slot1 = UnityEngine.Input.GetKeyDown(KeyCode.Alpha1);
            bool slot2 = UnityEngine.Input.GetKeyDown(KeyCode.Alpha2);
            bool slot3 = UnityEngine.Input.GetKeyDown(KeyCode.Alpha3);
            bool slot4 = UnityEngine.Input.GetKeyDown(KeyCode.Alpha4);
            bool slot5 = UnityEngine.Input.GetKeyDown(KeyCode.Alpha5);
            bool slot6 = UnityEngine.Input.GetKeyDown(KeyCode.Alpha6);
            bool slot7 = UnityEngine.Input.GetKeyDown(KeyCode.Alpha7); // signature

            bool anySlotKey = slot1 || slot2 || slot3 || slot4 || slot5 || slot6 || slot7;

            if (!mouseDown && !anySlotKey) return;

            // Calcule la case sous la souris (partagee entre mvt, cast et debug commands).
            Vector3 mouseWorld = _camera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
            mouseWorld.z = 0f;
            var (gx, gy) = IsoProjection.WorldToGrid(
                mouseWorld,
                _gridSettings.TileWorldWidth,
                _gridSettings.TileWorldHeight,
                _centerOffset);

            // Determine le sender (joueur actif si debug, sinon local).
            //
            // ATTENTION semantique : 2 valeurs distinctes a ne pas confondre.
            //   - senderPlayer = PlayerRef GLOBAL Quantum (0 ou 1). Sert au filtering UI :
            //     "est-ce mon combatant ?", caster cell pour sorts Self, logging.
            //   - splitscreenSlot = index LOCAL cote ce client, passe a Game.SendCommand.
            //     En prod 1 player local par client -> TOUJOURS 0 (cf CombatBootstrapCasual
            //     qui fait AddPlayer(LOCAL_SPLITSCREEN_SLOT=0, ...)). En legacy debug auto-add
            //     (Phase 2 IA, 2 RuntimePlayer ajoutes sur le meme client) -> coincide avec
            //     ActivePlayerIndex car les 2 PlayerRef sont locals.
            //
            // Bug 19 mai 2026 : on passait senderPlayer (= PlayerRef global) a SendCommand.
            // Si Quantum attribuait PlayerRef=1 au client (race AddPlayer ordering en PvP,
            // cf project_quantum_playerref_resolution), Quantum cherchait un local player
            // au splitscreen slot 1 -> introuvable -> plugin renvoie Error #19 "Player not
            // found" et disconnect immediat. Fix : decouple les 2 semantiques.
            int senderPlayer = _localPlayerIndex;
            int splitscreenSlot = 0;
            if (_debugAllPlayersMovable)
            {
                var frame = game.Frames.Verified;
                if (frame.TryGetSingleton<CombatState>(out var state))
                {
                    senderPlayer = state.ActivePlayerIndex;
                    splitscreenSlot = senderPlayer; // legacy 2-locals : slot = PlayerRef
                }
            }

            // POLISH-4 — Alpha1..Alpha7 dispatch vers le HUD pour armer le sort du slot.
            // Equivalent fonctionnel du clic sur l'icone de la barre de sort. Si pas de HUD
            // wired (cas test scene minimale), no-op silencieux.
            if (anySlotKey)
            {
                if (_hudController == null) return;
                int slotIdx = slot1 ? 0 : slot2 ? 1 : slot3 ? 2 : slot4 ? 3 : slot5 ? 4 : slot6 ? 5 : 6;
                bool armed = _hudController.TryArmSlotByIndex(slotIdx);
                if (armed)
                {
                    Debug.Log($"[Nymora.CombatInput] Slot {slotIdx + 1} arme via touche Alpha{slotIdx + 1} (player={senderPlayer}, attend clic grille pour cible)");
                }
                return;
            }

            // Clic gauche : 2 chemins possibles (priorite descendante).
            // 1) Sort arme via le HUD (2.13.a option 2) : on cast au lieu de bouger.
            //    Si Filter=Self : la case cliquee est ignoree, target redirigee vers caster cell.
            //    Sinon : la case cliquee est utilisee telle quelle (Quantum validera la portee).
            // 2) Mouvement classique : MoveCommand.
            if (mouseDown)
            {
                if (_hudController != null && _hudController.ConsumeArmedSpell(out SpellId armedSpell))
                {
                    int tx = gx;
                    int ty = gy;
                    if (Quantum.SpellRegistry.TryGet(armedSpell, out Quantum.SpellDef def)
                        && def.Filter == TargetingFilter.Self
                        && TryGetCasterCell(game, senderPlayer, out int cx, out int cy))
                    {
                        tx = cx;
                        ty = cy;
                    }
                    SendSpellAt(game, splitscreenSlot, senderPlayer, armedSpell, tx, ty, 0);
                    return;
                }

                // Mouvement classique. SendCommand prend le splitscreenSlot (slot LOCAL),
                // pas le PlayerRef global. Cf commentaire au-dessus pour le rationale.
                var moveCmd = new MoveCommand { TargetX = gx, TargetY = gy };
                game.SendCommand(splitscreenSlot, moveCmd);
                Debug.Log($"[Nymora.CombatInput] Sent MoveCommand player={senderPlayer} splitscreenSlot={splitscreenSlot} target=({gx},{gy})");
            }
        }

        private static void SendSpellAt(QuantumGame game, int splitscreenSlot, int sender, SpellId spell, int tx, int ty, byte hgSpend)
        {
            // splitscreenSlot = index local cote ce client (0 en prod, ActivePlayerIndex en
            // legacy debug auto-add). sender = PlayerRef global Quantum, sert uniquement au
            // logging ici. Cf rationale dans Update() au-dessus.
            var cmd = new CastSpellCommand { Spell = spell, TargetX = tx, TargetY = ty, HGSpend = hgSpend };
            game.SendCommand(splitscreenSlot, cmd);
            // 19 mai POLISH-6h — Memorise le cast pour permettre au FloatingTextManager de
            // spawner un texte epique (or + scale bounce) si le sort est signature.
            Nymora.Combat.View.HUD.SignatureCastBridge.NotifySpellCast(spell);
            Debug.Log($"[Nymora.CombatInput] Sent Cast {spell} player={sender} splitscreenSlot={splitscreenSlot} target=({tx},{ty}) HGSpend={hgSpend}");
        }

        /// <summary>
        /// Resoud la case du caster (joueur passe en argument) en lisant la Frame verified.
        /// Utilise pour les sorts self-target : on envoie sa propre case comme TargetX/Y.
        /// </summary>
        private static bool TryGetCasterCell(QuantumGame game, int playerIndex, out int x, out int y)
        {
            x = 0; y = 0;
            var frame = game.Frames.Verified;
            var filter = frame.Filter<Quantum.Combatant>();
            while (filter.Next(out Quantum.EntityRef _, out Quantum.Combatant c))
            {
                if (c.PlayerIndex == playerIndex)
                {
                    x = c.GridX;
                    y = c.GridY;
                    return true;
                }
            }
            return false;
        }
    }
}
