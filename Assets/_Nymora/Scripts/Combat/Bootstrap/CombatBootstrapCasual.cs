using System;
using System.Threading;
using System.Threading.Tasks;
using Nymora.Combat.View.PreCombatLobby;
using Nymora.Core.Data;
using Nymora.Core.SceneFlow;
using Nymora.Core.ScriptableObjects;
using Photon.Deterministic;
using Photon.Realtime;
using Quantum;
using UnityEngine;
using UnityEngine.SceneManagement;
using NymoraClassEnum = Nymora.Core.Enums.NymoraClass;
using QuantumNymoraClass = Quantum.NymoraClass;

namespace Nymora.Combat.Bootstrap
{
    /// <summary>
    /// Brique 4.14.c — Bootstrap online Quantum pour la scene 33_CombatCasual (PvP).
    ///
    /// REMPLACE QuantumRunnerLocalDebug (offline) dans la scene 33_CombatCasual.
    /// La scene 30_CombatIA garde QuantumRunnerLocalDebug pour le mode IA offline.
    ///
    /// Pipeline (au Start) :
    ///   1. Lit MatchBridge.PendingMatchId (set par OnMatchReady cote hub, brique 4.14.e)
    ///   2. Photon Realtime : ConnectToRoomAsync avec RoomName = matchId (max 2 players)
    ///   3. Attend que les 2 actors soient dans la room (timeout 30s)
    ///   4. Quantum SessionRunner.StartAsync en mode Multiplayer
    ///   5. Game.AddPlayer(0, runtimePlayer) puis poll GetLocalPlayers() pour resoudre
    ///      le vrai PlayerRef attribue par Quantum (ordre d'arrivee dans la session).
    ///
    /// Garde-fous :
    ///   - Si MatchBridge.PendingMatchId vide -> LoadScene 10_CommunityHub (retour hub)
    ///   - Timeout connexion -> retour hub
    ///   - OnDestroy -> Disconnect propre Photon + Quantum
    ///
    /// La brique 4.14.d enrichira RuntimePlayer avec ClassId + SpellIds (deck sync).
    /// La brique 4.14.f gerera le disconnect mid-match (forfait).
    /// </summary>
    public sealed class CombatBootstrapCasual : MonoBehaviour
    {
        [Header("Quantum runtime")]
        [Tooltip("RuntimeConfig clone du 30_CombatIA mais avec IsBotMatch=FALSE. Map sera auto-resolu depuis QuantumMapData de la scene si present.")]
        public RuntimeConfig RuntimeConfig;

        [Header("Data (4.14.d)")]
        [Tooltip("SpellCatalog asset (Assets/_Nymora/ScriptableObjects/Spells/SpellCatalog.asset). " +
                 "Sert a convertir DeckBridge.PendingSpellIds (string snake_case) en int[] " +
                 "Quantum.SpellId values pour push dans RuntimePlayer.SpellIdValues.")]
        public SpellCatalog SpellCatalog;

        [Tooltip("Session config (asset partage avec 30_CombatIA). Laisse null pour utiliser le default global.")]
        public QuantumDeterministicSessionConfigAsset SessionConfig;

        [Header("Photon")]
        [Tooltip("Server settings asset. Laisse null pour resolve le global via PhotonServerSettings.TryGetGlobal.")]
        public PhotonServerSettings ServerSettings;

        [Tooltip("Region Photon (ex 'eu'). Vide = auto (best ping).")]
        public string FixedRegion = "eu";

        [Header("Match settings")]
        [Tooltip("App version Quantum (separation des matchmakers entre clients differents). Doit etre identique cote 2 clients.")]
        public string AppVersion = "0.1.0";

        [Tooltip("Timeout en secondes pour la connexion Photon + Quantum start.")]
        public float ConnectTimeoutSec = 30f;

        [Tooltip("Si TRUE, log verbeux pour debug brique 4.14.c.")]
        public bool VerboseLog = true;

        // Runtime
        public RealtimeClient Client { get; private set; }
        public QuantumRunner Runner { get; private set; }
        public int LocalPlayerSlot { get; private set; } = -1;

        /// <summary>
        /// Fire une seule fois quand Quantum a attribue le PlayerRef global a ce client
        /// (cf etape 7 dans BootstrapAsync). Les Views (CombatHUDController, CombatInputController)
        /// s'y abonnent si elles s'initialisent AVANT que le poll GetLocalPlayers retourne — ce
        /// qui arrive systematiquement car CallbackGameStarted est dispatche par Quantum pendant
        /// l'await SessionRunner.StartAsync, donc avant qu'on ait pu appeler AddPlayer.
        /// </summary>
        public event Action<int> LocalPlayerSlotResolved;

        // 4.14.f hotfix — singleton pour que CombatInputController + CombatHUDController
        // resolvent leur _localPlayerIndex depuis LocalPlayerSlot (au lieu de 0 hardcoded
        // legacy IA, qui causait "Player not found" en PvP cote slot != 0).
        public static CombatBootstrapCasual Instance { get; private set; }

        private CancellationTokenSource _cts;
        private bool _bootstrapInProgress;

        // Lobby pré-combat (B2) — deck résolu par le lobby (sélection joueur ou défaut deck builder).
        // Null si lobby skip (lancement direct sans hub) → les helpers retombent sur DeckBridge.
        private PreCombatDeckInfo _resolvedDeck;

        // Nom de la scene legitime pour CE bootstrap. Garde stricte : si le component
        // se reveille dans une autre scene (chargement additif fantome, multi-scene editing,
        // Quantum auto-load de QuantumMap.Scene), on no-op. Plus robuste que la comparaison
        // active vs gameObject.scene car les deux peuvent avoir le meme nom dans certains
        // cas de chargement additif (cf incident 18 mai bis).
        //
        // 6.2.b — desormais un SerializeField : ce meme bootstrap pilote la scene casual
        // (33_CombatCasual) ET la scene ranked (40_CombatRanked1v1), qui en est un clone.
        // Mettre "40_CombatRanked1v1" sur le bootstrap de la scene ranked (tool dedie).
        [Header("Scene guard")]
        [Tooltip("Nom de la scene ou ce bootstrap doit s'activer : 33_CombatCasual (casual) ou 40_CombatRanked1v1 (ranked).")]
        [SerializeField] private string _expectedSceneName = "33_CombatCasual";

        private async void Start()
        {
            // Garde 0 (replay) : un replay enregistré en PvP peut être rejoué dans 33_CombatCasual.
            // Le ReplayPlaybackController (-1000) a démarré son runner -> le bootstrap s'abstient.
            if (Nymora.Combat.Replay.ReplayPlaybackController.ReplaybackActive)
            {
                Log("Mode replay actif -> bootstrap Casual skip (le ReplayPlaybackController pilote la sim).");
                return;
            }

            // Garde par nom de scene en dur : ce bootstrap n'a de sens QUE dans
            // 33_CombatCasual. Si on est ailleurs (additif fantome ou autre), no-op.
            if (gameObject.scene.name != _expectedSceneName)
            {
                Log($"Bootstrap skip : ce component est dans la scene '{gameObject.scene.name}' mais ne doit s'activer que dans '{_expectedSceneName}'.");
                return;
            }

            // Garde secondaire multi-scene editing : si 33_CombatCasual est chargee
            // mais qu'une AUTRE scene combat (typiquement 30_CombatIA) est active,
            // on no-op aussi pour ne pas interferer avec le combat IA en cours.
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene.name != _expectedSceneName)
            {
                Log($"Bootstrap skip : scene active='{activeScene.name}' != '{_expectedSceneName}' (probable multi-scene editing avec une autre scene combat).");
                return;
            }

            Instance = this;
            if (!MatchBridge.HasPendingMatch)
            {
                Log("Aucun match pending dans MatchBridge -> retour hub.");
                ReturnToHub("no_pending_match");
                return;
            }

            _bootstrapInProgress = true;
            _cts = new CancellationTokenSource();
            try
            {
                await BootstrapAsync(MatchBridge.PendingMatchId, MatchBridge.LocalEmail ?? MatchBridge.LocalSub ?? "anon", _cts.Token);
            }
            catch (OperationCanceledException)
            {
                Log("Bootstrap annule (probablement OnDestroy / scene quit).");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CombatBootstrapCasual] Bootstrap echec : {ex.Message}\n{ex.StackTrace}");
                ReturnToHub("bootstrap_error");
            }
            finally
            {
                _bootstrapInProgress = false;
            }
        }

        private async Task BootstrapAsync(string matchId, string playerName, CancellationToken ct)
        {
            // ===== 0. Pre-clean : meme hardening que CombatBootstrapIA (5.4.b) =====
            // Mirror du fix IA car le bug reproduit ici : Hub -> Combat -> Hub -> Combat
            // re-entrant (ou crash Editor) laisse un QuantumRunner zombi DontDestroyOnLoad
            // actif. Symptomes : 2 audio listeners, 2 event systems, CombatHUDController.Awake
            // declenche 2x. Sans ShutdownAll, le SessionRunner.StartAsync pioche l'ancien
            // runner singleton et ne reinit pas la sim, et la scene Quantum auto-loadee
            // additivement (via AutoLoadSceneFromMap) reste empilee.
            int disabledCount = 0;
            foreach (var dbg in FindObjectsByType<QuantumRunnerLocalDebug>(FindObjectsSortMode.None))
            {
                if (dbg != null && dbg.enabled)
                {
                    dbg.enabled = false;
                    disabledCount++;
                }
            }
            if (disabledCount > 0)
            {
                Debug.LogWarning($"[CombatBootstrapCasual] {disabledCount} QuantumRunnerLocalDebug legacy detecte(s) et disabled.");
            }

            QuantumRunner.ShutdownAll();
            await Task.Yield(); // laisse Quantum propager le shutdown sur 1 frame

            // ===== 0.c Anti additive '30_CombatIA' =====
            // QuantumMap.asset a ScenePath pointant sur 30_CombatIA.unity, donc la sim
            // Quantum auto-load cette scene additivement quand on demarre Casual
            // (SimulationConfig.AutoLoadSceneFromMap = UnloadPreviousSceneThenLoad).
            // Resultat : 2 EventSystem coexistent + 2 AudioListener + 2 jeux de tile
            // highlighters -> "There can be only one active Event System" + impossible
            // de cliquer une tile en spell range (le highlighter dessine sur la scene
            // fantome). On hook sceneLoaded et on unload 30_CombatIA des qu'elle apparait.
            SceneManager.sceneLoaded -= HandleAdditiveSceneLoaded;
            SceneManager.sceneLoaded += HandleAdditiveSceneLoaded;

            // Si elle est deja chargee additivement (resilience cas reentrants), unload immediat.
            var existingIA = SceneManager.GetSceneByName("30_CombatIA");
            if (existingIA.IsValid() && existingIA.isLoaded)
            {
                Debug.LogWarning("[CombatBootstrapCasual] Scene '30_CombatIA' deja chargee additivement avant SessionRunner -> unload preventif.");
                SceneManager.UnloadSceneAsync(existingIA);
            }

            // ===== 1. Resolve Photon server settings =====
            var serverSettings = ServerSettings;
            if (serverSettings == null) PhotonServerSettings.TryGetGlobal(out serverSettings);
            if (serverSettings == null)
                throw new InvalidOperationException("PhotonServerSettings introuvable. Ctrl+H -> Quantum Hub -> Setup.");

            if (string.IsNullOrEmpty(serverSettings.AppSettings.AppIdQuantum))
                throw new InvalidOperationException("AppIdQuantum non set. Ctrl+H -> Quantum Hub -> Create/Set AppId.");

            // ===== 2. Connect Photon room (RoomName = matchId) =====
            Log($"Connect Photon room '{matchId}' (region={FixedRegion}, appVer={AppVersion})...");

            var matchmakingArgs = new MatchmakingArguments
            {
                PhotonSettings = new AppSettings(serverSettings.AppSettings)
                {
                    AppVersion = AppVersion,
                    FixedRegion = string.IsNullOrEmpty(FixedRegion) ? null : FixedRegion,
                },
                EmptyRoomTtlInSeconds = serverSettings.EmptyRoomTtlInSeconds,
                EnableCrc = serverSettings.EnableCrc,
                PlayerTtlInSeconds = serverSettings.PlayerTtlInSeconds,
                // 1v1 only pour MVP — les 2v2/3v3 ranked viendront en Phase 6.
                MaxPlayers = 2,
                RoomName = matchId,
                PluginName = "QuantumPlugin",
                AuthValues = new AuthenticationValues(playerName),
                AsyncConfig = new AsyncConfig
                {
                    TaskFactory = AsyncConfig.CreateUnityTaskFactory(),
                    CancellationToken = ct,
                },
            };

            // Timeout via cts linked
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(ConnectTimeoutSec));
            matchmakingArgs.AsyncConfig = new AsyncConfig
            {
                TaskFactory = AsyncConfig.CreateUnityTaskFactory(),
                CancellationToken = timeoutCts.Token,
            };

            Client = await MatchmakingExtensions.ConnectToRoomAsync(matchmakingArgs);
            Log($"Photon room '{matchId}' connectee. ActorNumber={Client.LocalPlayer.ActorNumber} IsMaster={Client.LocalPlayer.IsMasterClient}");

            // ===== 2.b Lobby pré-combat (B2) =====
            // Les 2 clients sont maintenant dans la room Photon. On échange pseudo/classe/MMR/ready
            // via les player custom properties (hors-sim) et on attend que les 2 soient prêts (ou
            // timeout 30 s). Le deck choisi sort dans _resolvedDeck. On passe `ct` (pas timeoutCts,
            // dont le CancelAfter 30 s expirerait pendant le lobby) ; le lobby a son propre cap 35 s.
            await RunPreCombatLobbyAsync(playerName, ct);

            // ===== 3. LocalPlayerSlot will be resolved AFTER AddPlayer (cf etape 6/7) =====
            // Bug 19 mai 2026 : on basait LocalPlayerSlot sur IsMasterClient (0 si master,
            // 1 si guest). Mais Quantum 3 attribue le PlayerRef GLOBAL selon l'ordre d'arrivee
            // des AddPlayer dans la session reseau, PAS selon le slot Photon ni le hint local
            // qu'on passe a AddPlayer (qui sert au split-screen multi-local). Resultat : quand
            // le guest faisait AddPlayer avant le master (race condition), le master devenait
            // PlayerRef=1 mais croyait etre PlayerRef=0 -> ses commands ciblaient le mauvais
            // combatant -> "[Movement] rejet : ce n'est pas le tour de P1" pendant son propre
            // tour. Fix : on poll Runner.Game.GetLocalPlayers() apres AddPlayer pour recuperer
            // le vrai PlayerRef attribue (cf etape 7 plus bas). LocalPlayerSlot reste a -1 ici.

            // ===== 4. Clone runtime config + bind map from scene QuantumMapData =====
            var runtimeConfig = new QuantumUnityJsonSerializer().CloneConfig(RuntimeConfig);

            var mapData = FindAnyObjectByType<QuantumMapData>();
            if (mapData != null) runtimeConfig.Map = mapData.AssetRef;

            if (runtimeConfig.SimulationConfig.Id.IsValid == false
                && QuantumDefaultConfigs.TryGetGlobal(out var defaultConfigs))
            {
                runtimeConfig.SimulationConfig = defaultConfigs.SimulationConfig;
            }

            // 4.14.b safety net : si l'asset RuntimeConfig en inspector aurait IsBotMatch=true
            // par erreur (clone de 30_CombatIA), on FORCE false pour cette scene PvP.
            runtimeConfig.IsBotMatch = false;

            // PATCH 22 mai (test designer) — Seed RNG aleatoire par match. Sans ca, l'asset
            // RuntimeConfig a Seed=0 -> RNGSession(0) identique a CHAQUE match -> l'initiative
            // (TurnSystem.OnInit : f.RNG->Next(0,2)) tombait TOUJOURS sur le meme joueur (P0 =
            // PILE). Meme pattern que le Photon Menu SDK (QuantumMenuConnectionBehaviourSDK).
            // En online, le RuntimeConfig du createur de room est l'autoritaire synchronise aux
            // 2 clients -> seed IDENTIQUE des deux cotes (deterministe) mais variable par match.
            if (runtimeConfig.Seed == 0)
            {
                runtimeConfig.Seed = System.Guid.NewGuid().GetHashCode();
            }

            // ===== 5. Start Quantum session (Multiplayer) =====
            // Timeout FRAIS pour la phase de start : le `timeoutCts` (CancelAfter 30 s mesuré avant
            // le lobby) peut déjà avoir expiré pendant l'attente du lobby pré-combat (B2). On crée
            // donc un nouveau token avec son propre délai pour ne pas faire échouer StartAsync.
            using var startTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            startTimeoutCts.CancelAfter(TimeSpan.FromSeconds(ConnectTimeoutSec));

            Log("Demarrage SessionRunner Quantum (Multiplayer)...");
            var sessionArgs = new SessionRunner.Arguments
            {
                RunnerFactory = QuantumRunnerUnityFactory.DefaultFactory,
                GameParameters = QuantumRunnerUnityFactory.CreateGameParameters,
                ClientId = playerName,
                RuntimeConfig = runtimeConfig,
                SessionConfig = (SessionConfig != null ? SessionConfig.Config : null)
                                ?? QuantumDeterministicSessionConfigAsset.DefaultConfig,
                PlayerCount = 2,
                GameMode = DeterministicGameMode.Multiplayer,
                Communicator = new QuantumNetworkCommunicator(Client),
                CancellationToken = startTimeoutCts.Token,
                RecordingFlags = RecordingFlags.None,
                InstantReplaySettings = InstantReplaySettings.Default,
                DeltaTimeType = SimulationUpdateTime.EngineDeltaTime,
            };

            Runner = (QuantumRunner)await SessionRunner.StartAsync(sessionArgs);
            Log("SessionRunner started.");

            // ===== 6. Add LOCAL player only (Quantum sync les 2 add via reseau) =====
            // 4.14.d — Deck sync via RuntimePlayer.ClassId + SpellIdValues.
            // CombatantSystem.OnPlayerAdded(slot=0|1) lira ces values et spawnera
            // l'entity Combatant avec la bonne classe.
            var localPlayer = new RuntimePlayer
            {
                PlayerNickname = playerName,
                ClassId = ResolveClassIdForLocalPlayer(),
                SpellIdValues = ResolveSpellIdValuesForLocalPlayer(),
                // 5.10 (A3) — cosmétiques équipés (résolus côté hub), sync à l'adversaire via Quantum.
                SkinId = Nymora.Core.Data.CombatCosmeticsContext.LocalSkinId,
                PetId = Nymora.Core.Data.CombatCosmeticsContext.LocalPetId,
            };
            // Le 1er argument de Game.AddPlayer est le "localPlayerSlot" : index local utilise
            // uniquement quand un meme client controle plusieurs players (split-screen). En 1v1
            // online un seul player local par client, donc on passe 0. Le PlayerRef GLOBAL
            // (celui qui matche slot 0/1 dans CombatantSystem.OnPlayerAdded) est attribue
            // par Quantum a la reception cote serveur.
            const int LOCAL_SPLITSCREEN_SLOT = 0;
            Runner.Game.AddPlayer(LOCAL_SPLITSCREEN_SLOT, localPlayer);
            Log($"AddPlayer class={localPlayer.ClassId} deck=[{string.Join(",", localPlayer.SpellIdValues)}] (nickname='{playerName}'). Awaiting Quantum PlayerRef assignment...");

            // ===== 7. Poll GetLocalPlayers pour recuperer le vrai PlayerRef attribue par Quantum =====
            // Compteur de Task.Yield (et non source de temps non-deterministe) car le HealthCheck
            // interdit les sources temporelles dans Scripts/Combat/. ~600 yields = ~10s a 60fps
            // (un Task.Yield resume au tick suivant du dispatcher Unity).
            const int playerRefResolveMaxAttempts = 600;
            for (int attempt = 0; attempt < playerRefResolveMaxAttempts; attempt++)
            {
                if (_cts.IsCancellationRequested) return;
                var localPlayers = Runner.Game.GetLocalPlayers();
                if (localPlayers != null && localPlayers.Count > 0)
                {
                    LocalPlayerSlot = localPlayers[0];
                    Log($"Quantum a attribue PlayerRef={LocalPlayerSlot} a ce client (IsMaster={Client.LocalPlayer.IsMasterClient}, attempt={attempt}). Bootstrap online OK.");
                    try { LocalPlayerSlotResolved?.Invoke(LocalPlayerSlot); }
                    catch (Exception ex) { Debug.LogException(ex); }
                    return;
                }
                await Task.Yield();
            }

            Debug.LogError($"[CombatBootstrapCasual] TIMEOUT {playerRefResolveMaxAttempts} yields : Quantum.GetLocalPlayers() reste vide apres AddPlayer. LocalPlayerSlot reste -1 -> les Views vont logger erreur et input PvP cassera. Verifier qu'AddPlayer a bien ete acquitte par le serveur Quantum.");
        }

        // ====== Lobby pré-combat (B2) ======

        /// <summary>
        /// Crée et pilote le lobby pré-combat (échange Photon pseudo/classe/MMR/ready + timer 30 s).
        /// Stocke le deck choisi dans <see cref="_resolvedDeck"/>. Si PreCombatBridge est vide
        /// (lancement direct sans hub), on saute le lobby → fallback DeckBridge dans les helpers.
        /// </summary>
        private async Task RunPreCombatLobbyAsync(string playerName, CancellationToken ct)
        {
            if (!PreCombatBridge.HasData)
            {
                Log("PreCombatBridge vide (lancement direct sans hub ?) — lobby pré-combat skip, deck = DeckBridge.");
                return;
            }

            int localClassValue = (int)ResolveClassIdForLocalPlayer();
            string localPseudo = !string.IsNullOrEmpty(MatchBridge.LocalDisplayName)
                ? MatchBridge.LocalDisplayName
                : playerName;

            var go = new GameObject("PreCombatLobby");
            var ctrl = go.AddComponent<PreCombatLobbyController>();
            ctrl.Init(Client, localPseudo, localClassValue, PreCombatBridge.LocalMmr,
                      Nymora.Core.Data.CombatCosmeticsContext.LocalSkinId,
                      PreCombatBridge.AvailableDecks, PreCombatBridge.DefaultDeckId);
            Log($"Lobby pré-combat démarré (pseudo='{localPseudo}' classe={localClassValue} MMR={PreCombatBridge.LocalMmr} " +
                $"decks={PreCombatBridge.AvailableDecks.Count}, timer {PreCombatLobbyController.LobbyDurationSeconds}s).");

            try
            {
                _resolvedDeck = await ctrl.RunAsync(ct);
                Log($"Lobby terminé. Deck résolu='{_resolvedDeck?.Name}' (class={_resolvedDeck?.ClassId}). " +
                    $"Adversaire: présent={ctrl.OpponentPresent} pseudo='{ctrl.OpponentPseudo}' classe={ctrl.OpponentClassValue} " +
                    $"MMR={ctrl.OpponentMmr} prêt={ctrl.OpponentReady}.");

                // 31 mai — capture le MMR adverse (échange P2P du lobby) dans MatchBridge (survit au
                // PreCombatBridge.Clear ci-dessous) pour le preview ELO du menu de fin (ranked only).
                Nymora.Core.Data.MatchBridge.SetRankedOpponentMmr(ctrl.OpponentMmr);

                // B3 — Si le joueur a choisi un deck (potentiellement différent du défaut deck builder),
                // on réaligne DeckBridge + la barre de sorts du HUD pour qu'elle affiche les 6 sorts
                // choisis (le deck est View-only ; le sim laisse les 16 sorts de la classe castables).
                if (_resolvedDeck != null)
                {
                    DeckBridge.SetPendingDeck(_resolvedDeck.ClassId, _resolvedDeck.SpellIds, _resolvedDeck.Name);
                    View.HUD.CombatHUDController.Instance?.ReapplyDeckFromBridge();
                }

                // B4 — Voile de transition (créé AVANT la destruction du lobby → aucun flash) qui
                // masque la grille vide jusqu'au spawn des combattants (CallbackGameStarted).
                View.PreCombatLobby.PreCombatLoadingVeil.Show();
            }
            finally
            {
                // Le lobby n'a plus de raison d'être une fois le deck résolu (la sim va démarrer).
                if (go != null) Destroy(go);
                // Consomme le bridge (évite qu'un relancement direct réutilise une liste périmée).
                PreCombatBridge.Clear();
            }
        }

        // ====== 4.14.d helpers (étendus B2 : préfèrent le deck résolu par le lobby) ======

        /// <summary>
        /// Convertit la classe du joueur local (deck résolu par le lobby, sinon DeckBridge) vers
        /// Quantum.NymoraClass. La classe est figée côté hub (le lobby ne la change pas).
        /// Fallback Soulrender si tout est vide / classe inconnue (defensive).
        /// </summary>
        private QuantumNymoraClass ResolveClassIdForLocalPlayer()
        {
            string classId = _resolvedDeck != null
                ? _resolvedDeck.ClassId
                : (DeckBridge.HasPending ? DeckBridge.PendingClassId : null);

            if (string.IsNullOrEmpty(classId))
            {
                Debug.LogWarning("[CombatBootstrapCasual] Classe introuvable (lobby + DeckBridge vides) — fallback class Soulrender.");
                return QuantumNymoraClass.Soulrender;
            }

            // Les 2 enums (Nymora.Core.Enums.NymoraClass et Quantum.NymoraClass) ont les memes
            // valeurs verrouillees par CombatRulesVersion (None=0, Soulrender=1, etc.).
            // On parse depuis le string -> Core enum -> cast byte -> Quantum enum.
            if (System.Enum.TryParse<NymoraClassEnum>(classId, ignoreCase: true, out var coreCls))
            {
                return (QuantumNymoraClass)(byte)coreCls;
            }
            Debug.LogWarning($"[CombatBootstrapCasual] classId='{classId}' non parsable — fallback Soulrender.");
            return QuantumNymoraClass.Soulrender;
        }

        /// <summary>
        /// Convertit les 6 SpellIdTech (snake_case ex "soulrender_tranche_ame") du deck résolu
        /// (lobby, sinon DeckBridge) en int[] = (int)Quantum.SpellId. Le mapping vient de
        /// SpellCatalog.QuantumSpellIdValue. Retourne 6 zéros si tout vide / catalog manquant.
        /// </summary>
        private int[] ResolveSpellIdValuesForLocalPlayer()
        {
            var result = new int[6];
            string[] spellIds = _resolvedDeck != null
                ? _resolvedDeck.SpellIds
                : (DeckBridge.HasPending ? DeckBridge.PendingSpellIds : null);

            if (spellIds == null || SpellCatalog == null) return result;

            for (int i = 0; i < 6 && i < spellIds.Length; i++)
            {
                var spellIdTech = spellIds[i];
                if (string.IsNullOrEmpty(spellIdTech)) continue;
                var def = SpellCatalog.FindBySpellId(spellIdTech);
                if (def == null)
                {
                    Debug.LogWarning($"[CombatBootstrapCasual] SpellCatalog.FindBySpellId('{spellIdTech}') retourne null — slot {i} = 0.");
                    continue;
                }
                result[i] = def.QuantumSpellIdValue;
            }
            return result;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= HandleAdditiveSceneLoaded;
            _cts?.Cancel();
            _ = ShutdownAsync();
        }

        private static void HandleAdditiveSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Additive) return;
            if (scene.name != "30_CombatIA") return;
            Debug.LogWarning("[CombatBootstrapCasual] Scene additive '30_CombatIA' detectee (auto-load Quantum via QuantumMap.ScenePath) -> cleanup differe d'une frame (laisse Quantum finir son SetActiveScene avant unload).");
            _ = DeferredAdditiveCleanupAsync();
        }

        private static async Task DeferredAdditiveCleanupAsync()
        {
            // Quantum (QuantumCallbackHandler_UnityCallbacks.LoadScene) appelle
            // SceneManager.SetActiveScene(30_CombatIA) juste apres sceneLoaded. Si on
            // UnloadAsync immediatement, son SetActiveScene throw ArgumentException
            // "scene is not loaded". On laisse passer 2 frames, puis on remet
            // 33_CombatCasual comme active et on unload la fantome.
            await Task.Yield();
            await Task.Yield();

            var ghost = SceneManager.GetSceneByName("30_CombatIA");
            if (!ghost.IsValid() || !ghost.isLoaded) return;

            var casual = SceneManager.GetSceneByName("33_CombatCasual");
            if (casual.IsValid() && casual.isLoaded)
            {
                SceneManager.SetActiveScene(casual);
            }
            Debug.LogWarning("[CombatBootstrapCasual] Unload differe de la scene fantome '30_CombatIA'.");
            SceneManager.UnloadSceneAsync(ghost);
        }

        private async Task ShutdownAsync()
        {
            try
            {
                if (Runner != null)
                {
                    await Runner.ShutdownAsync();
                    Runner = null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CombatBootstrapCasual] Runner shutdown error : {ex.Message}");
            }

            try
            {
                if (Client != null)
                {
                    await Client.DisconnectAsync();
                    Client = null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CombatBootstrapCasual] Client disconnect error : {ex.Message}");
            }
        }

        private void ReturnToHub(string reason)
        {
            Debug.LogWarning($"[CombatBootstrapCasual] ReturnToHub reason='{reason}'.");
            // Reset MatchBridge pour eviter une boucle si l'utilisateur clique a nouveau.
            MatchBridge.Reset();
            SceneTransition.Load("10_CommunityHub", waitForReady: true);
        }

        private void Log(string msg)
        {
            if (VerboseLog) Debug.Log($"[CombatBootstrapCasual] {msg}");
        }
    }
}
