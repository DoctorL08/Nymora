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
    ///   5. Game.AddPlayer(localSlot, runtimePlayer) ou localSlot = IsMasterClient ? 0 : 1
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

        // 4.14.f hotfix — singleton pour que CombatInputController + CombatHUDController
        // resolvent leur _localPlayerIndex depuis LocalPlayerSlot (au lieu de 0 hardcoded
        // legacy IA, qui causait "Player not found" en PvP cote slot != 0).
        public static CombatBootstrapCasual Instance { get; private set; }

        private CancellationTokenSource _cts;
        private bool _bootstrapInProgress;

        private async void Start()
        {
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

            // ===== 3. Determine local player slot (master=slot 0, guest=slot 1) =====
            LocalPlayerSlot = Client.LocalPlayer.IsMasterClient ? 0 : 1;
            Log($"LocalPlayerSlot={LocalPlayerSlot}");

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

            // ===== 5. Start Quantum session (Multiplayer) =====
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
                CancellationToken = timeoutCts.Token,
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
            };
            Runner.Game.AddPlayer(LocalPlayerSlot, localPlayer);
            Log($"AddPlayer slot {LocalPlayerSlot} class={localPlayer.ClassId} deck=[{string.Join(",", localPlayer.SpellIdValues)}] (nickname='{playerName}'). Bootstrap online OK.");
        }

        // ====== 4.14.d helpers ======

        /// <summary>
        /// Convertit DeckBridge.PendingClassId (string "Soulrender"/...) vers Quantum.NymoraClass.
        /// Fallback Soulrender si DeckBridge vide / classe inconnue (defensive).
        /// </summary>
        private static QuantumNymoraClass ResolveClassIdForLocalPlayer()
        {
            if (!DeckBridge.HasPending)
            {
                Debug.LogWarning("[CombatBootstrapCasual] DeckBridge vide — fallback class Soulrender.");
                return QuantumNymoraClass.Soulrender;
            }

            // Les 2 enums (Nymora.Core.Enums.NymoraClass et Quantum.NymoraClass) ont les memes
            // valeurs verrouillees par CombatRulesVersion (None=0, Soulrender=1, etc.).
            // On parse depuis le string -> Core enum -> cast byte -> Quantum enum.
            if (System.Enum.TryParse<NymoraClassEnum>(DeckBridge.PendingClassId, ignoreCase: true, out var coreCls))
            {
                return (QuantumNymoraClass)(byte)coreCls;
            }
            Debug.LogWarning($"[CombatBootstrapCasual] DeckBridge.PendingClassId='{DeckBridge.PendingClassId}' non parsable — fallback Soulrender.");
            return QuantumNymoraClass.Soulrender;
        }

        /// <summary>
        /// Convertit les 6 SpellIdTech (snake_case ex "soulrender_tranche_ame") en int[]
        /// = (int)Quantum.SpellId. Le mapping vient de SpellCatalog.QuantumSpellIdValue
        /// (populate via Nymora > Setup > Populate Spell Catalog).
        /// Retourne un array de 6 zeros si DeckBridge vide / catalog manquant (defensive).
        /// </summary>
        private int[] ResolveSpellIdValuesForLocalPlayer()
        {
            var result = new int[6];
            if (!DeckBridge.HasPending || SpellCatalog == null) return result;

            for (int i = 0; i < 6 && i < DeckBridge.PendingSpellIds.Length; i++)
            {
                var spellIdTech = DeckBridge.PendingSpellIds[i];
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
            _cts?.Cancel();
            _ = ShutdownAsync();
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
            SceneManager.LoadScene("10_CommunityHub");
        }

        private void Log(string msg)
        {
            if (VerboseLog) Debug.Log($"[CombatBootstrapCasual] {msg}");
        }
    }
}
