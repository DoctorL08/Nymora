using System;
using System.Threading;
using System.Threading.Tasks;
using Nymora.Core.Data;
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
    /// Brique 5.7 (sous-brique B) — Bootstrap RÉSEAU 2v2 pour la scène 41_CombatRanked2v2.
    ///
    /// Pendant matchmaking 2v2 (Match2v2Bridge rempli par le hub) : connecte la room Photon
    /// (RoomName = matchId, 4 joueurs) et AddPlayer le joueur LOCAL avec son TeamId/TeamOrder +
    /// son deck. Chaque client ne contrôle qu'UN combattant (comme CombatBootstrapCasual en 1v1) —
    /// à l'opposé du hot-seat (CombatBootstrap2v2) qui ajoute les 4 en local.
    ///
    /// Cohabite avec le hot-seat sur la scène 41 : ce bootstrap ne s'active QUE si
    /// Match2v2Bridge.HasPendingMatch ; sinon le hot-seat prend la main (Play direct = test local).
    ///
    /// L'assignation d'équipe est robuste à l'ordre d'arrivée des AddPlayer : la sim lit
    /// RuntimePlayer.TeamId/TeamOrder (5.1/5.2), pas le PlayerRef. Le TeamOrder local = le rang du
    /// joueur dans son équipe d'après l'ordre du roster backend (défaut ; le vote capitaine = C).
    ///
    /// Calqué sur CombatBootstrapCasual (Photon Realtime + Quantum Multiplayer + poll PlayerRef).
    /// </summary>
    public sealed class CombatBootstrapRanked2v2 : MonoBehaviour
    {
        [Header("Quantum runtime")]
        [Tooltip("RuntimeConfig de la scène 2v2 (clone, IsBotMatch forcé à false).")]
        public RuntimeConfig RuntimeConfig;

        [Tooltip("Session config (null = default global).")]
        public QuantumDeterministicSessionConfigAsset SessionConfig;

        [Tooltip("QuantumMap dédiée à la scène 41 (ScenePath -> 41_CombatRanked2v2).")]
        public Map TeamQuantumMap;

        [Header("Map de combat (forme + spawns)")]
        [Tooltip("NymoraCombatMap (CombatMap_2v2.asset) : forme irrégulière + spawns par équipe/rang.")]
        public NymoraCombatMap CombatMap;

        [Header("Data")]
        [Tooltip("SpellCatalog asset — convertit le deck du hub (DeckBridge) en SpellId Quantum.")]
        public SpellCatalog SpellCatalog;

        [Header("Photon")]
        [Tooltip("Server settings asset. Null = resolve le global.")]
        public PhotonServerSettings ServerSettings;
        [Tooltip("Region Photon (ex 'eu'). Vide = auto.")]
        public string FixedRegion = "eu";
        [Tooltip("App version Quantum (doit matcher entre clients).")]
        public string AppVersion = "0.1.0";
        [Tooltip("Timeout connexion Photon + Quantum start (s).")]
        public float ConnectTimeoutSec = 30f;

        [Header("Debug")]
        public bool VerboseLog = true;

        // Runtime (même interface publique que CombatBootstrapCasual pour les Views).
        public RealtimeClient Client { get; private set; }
        public QuantumRunner Runner { get; private set; }
        public int LocalPlayerSlot { get; private set; } = -1;
        public event Action<int> LocalPlayerSlotResolved;
        public static CombatBootstrapRanked2v2 Instance { get; private set; }

        private CancellationTokenSource _cts;
        private const string ExpectedSceneName = "41_CombatRanked2v2";
        private const int TeamPlayerCount = 4;

        private async void Start()
        {
            if (Nymora.Combat.Replay.ReplayPlaybackController.ReplaybackActive) return;
            if (Nymora.Combat.Spectate.LiveSpectateController.LiveSpectateActive) return;
            if (gameObject.scene.name != ExpectedSceneName)
            {
                Log($"Skip : scène '{gameObject.scene.name}' != '{ExpectedSceneName}'.");
                return;
            }
            // Cohabitation hot-seat : sans match appairé, on laisse le hot-seat (CombatBootstrap2v2) jouer.
            if (!Match2v2Bridge.HasPendingMatch)
            {
                Log("Aucun match 2v2 appairé (Match2v2Bridge vide) -> bootstrap réseau skip (hot-seat prend la main).");
                return;
            }

            Instance = this;
            _cts = new CancellationTokenSource();
            try
            {
                await BootstrapAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                Log("Bootstrap annulé (OnDestroy / quit).");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CombatBootstrapRanked2v2] Bootstrap échec : {ex.Message}\n{ex.StackTrace}");
                ReturnToHub("bootstrap_error");
            }
        }

        private async Task BootstrapAsync(CancellationToken ct)
        {
            if (RuntimeConfig == null) throw new InvalidOperationException("RuntimeConfig non assigné.");

            string matchId = Match2v2Bridge.PendingMatchId;
            int localTeam = Match2v2Bridge.LocalTeam;
            // Auth/ClientId Photon = identité technique (email/sub, unique). Le PSEUDO affiché en combat
            //   (PlayerNickname, sync à tous les clients) = le displayName du roster -> chaque combattant
            //   montre son vrai nom au lieu du défaut "Bot" du tooltip (modèle binaire 1v1 inexistant ici).
            string playerName = Match2v2Bridge.LocalEmail ?? Match2v2Bridge.LocalSub ?? "anon";
            string displayName = ResolveLocalDisplayName();
            int localTeamOrder = ResolveLocalTeamOrder(localTeam);

            // Pré-clean (mirror Casual).
            foreach (var dbg in FindObjectsByType<QuantumRunnerLocalDebug>(FindObjectsSortMode.None))
                if (dbg != null && dbg.enabled) dbg.enabled = false;
            QuantumRunner.ShutdownAll();
            await Task.Yield();

            // Resolve Photon settings.
            var serverSettings = ServerSettings;
            if (serverSettings == null) PhotonServerSettings.TryGetGlobal(out serverSettings);
            if (serverSettings == null)
                throw new InvalidOperationException("PhotonServerSettings introuvable (Ctrl+H Quantum Hub).");
            if (string.IsNullOrEmpty(serverSettings.AppSettings.AppIdQuantum))
                throw new InvalidOperationException("AppIdQuantum non set.");

            Log($"Connect Photon room '{matchId}' (team={localTeam} rank={localTeamOrder}, region={FixedRegion})...");

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(ConnectTimeoutSec));

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
                MaxPlayers = TeamPlayerCount, // 2v2 = 4
                RoomName = matchId,
                PluginName = "QuantumPlugin",
                AuthValues = new AuthenticationValues(playerName),
                AsyncConfig = new AsyncConfig
                {
                    TaskFactory = AsyncConfig.CreateUnityTaskFactory(),
                    CancellationToken = timeoutCts.Token,
                },
            };

            Client = await MatchmakingExtensions.ConnectToRoomAsync(matchmakingArgs);
            Log($"Room '{matchId}' connectée. ActorNumber={Client.LocalPlayer.ActorNumber} IsMaster={Client.LocalPlayer.IsMasterClient}");

            // 5.7-C — Lobby CAPITAINE : le capitaine de chaque équipe ordonne son équipe (player
            //   property Photon "to"), chaque client en déduit son rang AVANT AddPlayer (le TeamOrder
            //   fige TurnOrder côté sim). Tourne ICI (room connectée, session Quantum PAS démarrée ->
            //   le lobby pompe Client.Service() lui-même). Anti-hang interne -> rang par défaut.
            localTeamOrder = await RunCaptainOrderLobbyAsync(localTeam, localTeamOrder, ct);
            if (ct.IsCancellationRequested) return;
            Log($"Ordre capitaine résolu : rang local = {localTeamOrder}.");

            // Clone config + map + 4 joueurs.
            var runtimeConfig = new QuantumUnityJsonSerializer().CloneConfig(RuntimeConfig);
            if (TeamQuantumMap != null) runtimeConfig.Map = TeamQuantumMap;
            else Debug.LogWarning("[CombatBootstrapRanked2v2] TeamQuantumMap non assignée.");
            if (runtimeConfig.SimulationConfig.Id.IsValid == false
                && QuantumDefaultConfigs.TryGetGlobal(out var defaultConfigs))
                runtimeConfig.SimulationConfig = defaultConfigs.SimulationConfig;
            runtimeConfig.IsBotMatch = false;
            runtimeConfig.PlayerCount = TeamPlayerCount;
            if (CombatMap != null) runtimeConfig.CombatMap = CombatMap;
            else Debug.LogWarning("[CombatBootstrapRanked2v2] CombatMap non assigné — fallback rectangle.");
            // Seed : le createur de room (master) est l'autoritaire synchronisé -> seed identique
            //   aux 4 clients mais variable par match (mirror Casual).
            if (runtimeConfig.Seed == 0) runtimeConfig.Seed = System.Guid.NewGuid().GetHashCode();

            using var startTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            startTimeoutCts.CancelAfter(TimeSpan.FromSeconds(ConnectTimeoutSec));

            Log("Démarrage SessionRunner Quantum (Multiplayer, 4 joueurs)...");
            var sessionArgs = new SessionRunner.Arguments
            {
                RunnerFactory = QuantumRunnerUnityFactory.DefaultFactory,
                GameParameters = QuantumRunnerUnityFactory.CreateGameParameters,
                ClientId = playerName,
                RuntimeConfig = runtimeConfig,
                SessionConfig = (SessionConfig != null ? SessionConfig.Config : null)
                                ?? QuantumDeterministicSessionConfigAsset.DefaultConfig,
                PlayerCount = TeamPlayerCount,
                GameMode = DeterministicGameMode.Multiplayer,
                Communicator = new QuantumNetworkCommunicator(Client),
                CancellationToken = startTimeoutCts.Token,
                RecordingFlags = RecordingFlags.None,
                InstantReplaySettings = InstantReplaySettings.Default,
                DeltaTimeType = SimulationUpdateTime.EngineDeltaTime,
            };

            Runner = (QuantumRunner)await SessionRunner.StartAsync(sessionArgs);
            Log("SessionRunner started.");

            // AddPlayer LOCAL uniquement (Quantum sync les 4 via réseau). TeamId/TeamOrder portent
            //   l'appartenance ; le PlayerRef global est attribué par Quantum à la réception.
            var localPlayer = new RuntimePlayer
            {
                PlayerNickname = displayName,
                ClassId = ResolveClassIdForLocalPlayer(),
                SpellIdValues = ResolveSpellIdValuesForLocalPlayer(),
                TeamId = localTeam,
                TeamOrder = localTeamOrder,
                SkinId = Nymora.Core.Data.CombatCosmeticsContext.LocalSkinId,
                PetId = Nymora.Core.Data.CombatCosmeticsContext.LocalPetId,
            };
            const int LOCAL_SPLITSCREEN_SLOT = 0;
            Runner.Game.AddPlayer(LOCAL_SPLITSCREEN_SLOT, localPlayer);
            Log($"AddPlayer team={localTeam} rank={localTeamOrder} class={localPlayer.ClassId} deck=[{string.Join(",", localPlayer.SpellIdValues)}]. Awaiting PlayerRef...");

            // Poll GetLocalPlayers pour le vrai PlayerRef (compteur de yields, pas de source temporelle).
            const int maxAttempts = 600;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (_cts.IsCancellationRequested) return;
                var locals = Runner.Game.GetLocalPlayers();
                if (locals != null && locals.Count > 0)
                {
                    LocalPlayerSlot = locals[0];
                    Log($"PlayerRef={LocalPlayerSlot} attribué (IsMaster={Client.LocalPlayer.IsMasterClient}, attempt={attempt}). Bootstrap réseau 2v2 OK.");
                    try { LocalPlayerSlotResolved?.Invoke(LocalPlayerSlot); }
                    catch (Exception ex) { Debug.LogException(ex); }
                    return;
                }
                await Task.Yield();
            }
            Debug.LogError("[CombatBootstrapRanked2v2] TIMEOUT : GetLocalPlayers() vide après AddPlayer. LocalPlayerSlot reste -1.");
        }

        // Pseudo affiché en combat = displayName du joueur local dans le roster (match par Sub).
        //   Fallback email/sub si absent. Sert au tooltip combat (PlayerNickname synchronisé).
        private string ResolveLocalDisplayName()
        {
            var players = Match2v2Bridge.Players;
            string mySub = Match2v2Bridge.LocalSub;
            if (players != null && !string.IsNullOrEmpty(mySub))
                foreach (var p in players)
                    if (p.Sub == mySub && !string.IsNullOrEmpty(p.DisplayName)) return p.DisplayName;
            return Match2v2Bridge.LocalEmail ?? Match2v2Bridge.LocalSub ?? "Joueur";
        }

        // 5.7-C — Lobby capitaine : instancie NetworkTeamOrderLobby (View), lui passe le client Photon
        //   + le roster, et await le rang local résolu (ordre du capitaine, ou defaultRank si timeout/erreur).
        private async Task<int> RunCaptainOrderLobbyAsync(int localTeam, int defaultRank, CancellationToken ct)
        {
            var roster = Match2v2Bridge.Players;
            string localSub = Match2v2Bridge.LocalSub;
            if (roster == null || string.IsNullOrEmpty(localSub) || Client == null) return defaultRank;

            bool isCaptain = false;
            foreach (var p in roster) if (p.Sub == localSub) { isCaptain = p.IsCaptain; break; }

            var go = new GameObject("NetworkTeamOrderLobby");
            var lobby = go.AddComponent<Nymora.Combat.View.PreCombatLobby.NetworkTeamOrderLobby>();
            try
            {
                return await lobby.RunAsync(Client, roster, localTeam, localSub, isCaptain, defaultRank, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CombatBootstrapRanked2v2] Lobby capitaine échec : {ex.Message} -> ordre par défaut.");
                if (go != null) Destroy(go);
                return defaultRank;
            }
        }

        // Rang du joueur local dans son équipe = son index parmi les joueurs de même équipe (ordre roster).
        private int ResolveLocalTeamOrder(int localTeam)
        {
            var players = Match2v2Bridge.Players;
            string mySub = Match2v2Bridge.LocalSub;
            if (players == null || string.IsNullOrEmpty(mySub)) return 0;
            int rank = 0;
            foreach (var p in players)
            {
                if (p.Team != localTeam) continue;
                if (p.Sub == mySub) return rank;
                rank++;
            }
            return 0;
        }

        private QuantumNymoraClass ResolveClassIdForLocalPlayer()
        {
            string classId = DeckBridge.HasPending ? DeckBridge.PendingClassId : null;
            if (string.IsNullOrEmpty(classId)) return QuantumNymoraClass.Soulrender;
            if (System.Enum.TryParse<NymoraClassEnum>(classId, ignoreCase: true, out var coreCls))
                return (QuantumNymoraClass)(byte)coreCls;
            return QuantumNymoraClass.Soulrender;
        }

        private int[] ResolveSpellIdValuesForLocalPlayer()
        {
            var result = new int[6];
            string[] spellIds = DeckBridge.HasPending ? DeckBridge.PendingSpellIds : null;
            if (spellIds == null || SpellCatalog == null) return result;
            for (int i = 0; i < 6 && i < spellIds.Length; i++)
            {
                if (string.IsNullOrEmpty(spellIds[i])) continue;
                var def = SpellCatalog.FindBySpellId(spellIds[i]);
                if (def != null) result[i] = def.QuantumSpellIdValue;
            }
            return result;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            _cts?.Cancel();
            _ = ShutdownAsync();
        }

        private async Task ShutdownAsync()
        {
            try { if (Runner != null) { await Runner.ShutdownAsync(); Runner = null; } }
            catch (Exception ex) { Debug.LogWarning($"[CombatBootstrapRanked2v2] Runner shutdown : {ex.Message}"); }
            try { if (Client != null) { await Client.DisconnectAsync(); Client = null; } }
            catch (Exception ex) { Debug.LogWarning($"[CombatBootstrapRanked2v2] Client disconnect : {ex.Message}"); }
        }

        private void ReturnToHub(string reason)
        {
            Debug.LogWarning($"[CombatBootstrapRanked2v2] ReturnToHub reason='{reason}'.");
            Match2v2Bridge.Reset();
            Nymora.Core.SceneFlow.SceneTransition.Load("10_CommunityHub", waitForReady: true);
        }

        private void Log(string msg) { if (VerboseLog) Debug.Log($"[CombatBootstrapRanked2v2] {msg}"); }
    }
}
