using System;
using System.Threading;
using System.Threading.Tasks;
using Nymora.Core.Data;
using Nymora.Core.ScriptableObjects;
using Photon.Deterministic;
using Quantum;
using UnityEngine;
using UnityEngine.SceneManagement;
using NymoraClassEnum = Nymora.Core.Enums.NymoraClass;
using QuantumNymoraClass = Quantum.NymoraClass;

namespace Nymora.Combat.Bootstrap
{
    /// <summary>
    /// Brique 5.5a (2v2/3v3) — Bootstrap HOT-SEAT LOCAL pour la scène 41_CombatRanked2v2.
    ///
    /// Premier test du 2v2 AVANT le matchmaking (5.7) : un seul client ajoute les 4 joueurs en
    /// LOCAL (comme CombatBootstrapIA ajoute 2 joueurs), Lorenzo joue chaque tour pour le combattant
    /// dont c'est le tour (l'input hot-seat = brique 5.5b suit l'ActivePlayerIndex).
    ///
    /// Équipes : slots 0,1 = équipe 0 ; slots 2,3 = équipe 1 (TeamId). Rang intra-équipe (TeamOrder)
    /// = 0 puis 1. Classes : slot 0 depuis le deck du hub (DeckBridge), slots 1-3 fixés dans
    /// l'Inspector (classes UNIQUES par équipe). Pose RuntimeConfig.PlayerCount=4 + CombatMap
    /// (forme + spawns de la map authorée en 5.4c) + IsBotMatch=false (pas d'IA en multi).
    ///
    /// Pré-requis scène (5.5c) : une QuantumMap dédiée (ScenePath -> 41_CombatRanked2v2) pour empêcher
    /// Quantum d'auto-load une autre scène combat, et le CombatMap_2v2 asset (NymoraCombatMap).
    /// </summary>
    public sealed class CombatBootstrap2v2 : MonoBehaviour
    {
        [Header("Quantum runtime")]
        [Tooltip("RuntimeConfig de la scène 2v2 (clone d'un RuntimeConfig combat, IsBotMatch sera forcé à false).")]
        public RuntimeConfig RuntimeConfig;

        [Tooltip("Session config (laisse null pour le default global).")]
        public QuantumDeterministicSessionConfigAsset SessionConfig;

        [Tooltip("QuantumMap dédiée à CETTE scène (clone de QuantumMap.asset dont la ScenePath pointe " +
                 "vers 41_CombatRanked2v2). Empêche Quantum d'auto-load une autre scène combat additivement.")]
        public Map TeamQuantumMap;

        [Header("Map de combat (forme + spawns)")]
        [Tooltip("NymoraCombatMap (ex: CombatMap_2v2.asset) : forme irrégulière + points de spawn par " +
                 "équipe/rang, authoré dans Nymora > Combat > Map Editor (5.4c).")]
        public NymoraCombatMap CombatMap;

        [Header("Data")]
        [Tooltip("SpellCatalog asset — convertit le deck du hub (DeckBridge) en SpellId Quantum pour le slot 0.")]
        public SpellCatalog SpellCatalog;

        [Header("Classes (test hot-seat — classes UNIQUES par équipe)")]
        [Tooltip("Slot 0 (équipe 0, rang 0). Si DeckBridge porte un deck du hub, il a priorité ; sinon cette classe.")]
        public QuantumNymoraClass Team0Player0 = QuantumNymoraClass.Soulrender;
        [Tooltip("Slot 1 (équipe 0, rang 1). Différente de Team0Player0.")]
        public QuantumNymoraClass Team0Player1 = QuantumNymoraClass.Nightseer;
        [Tooltip("Slot 2 (équipe 1, rang 0).")]
        public QuantumNymoraClass Team1Player0 = QuantumNymoraClass.Colossar;
        [Tooltip("Slot 3 (équipe 1, rang 1). Différente de Team1Player0.")]
        public QuantumNymoraClass Team1Player1 = QuantumNymoraClass.Necram;

        [Header("Debug")]
        public bool VerboseLog = true;

        public QuantumRunner Runner { get; private set; }
        public static CombatBootstrap2v2 Instance { get; private set; }

        private CancellationTokenSource _cts;
        private const string ExpectedSceneName = "41_CombatRanked2v2";
        private const int TeamPlayerCount = 4; // 2v2

        private async void Start()
        {
            if (Nymora.Combat.Replay.ReplayPlaybackController.ReplaybackActive)
            {
                Log("Mode replay actif -> bootstrap 2v2 skip.");
                return;
            }
            if (gameObject.scene.name != ExpectedSceneName)
            {
                Log($"Skip : component dans '{gameObject.scene.name}' mais réservé à '{ExpectedSceneName}'.");
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
                Log("Bootstrap annulé (OnDestroy / quit scène).");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CombatBootstrap2v2] Bootstrap échec : {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async Task BootstrapAsync(CancellationToken ct)
        {
            if (RuntimeConfig == null)
                throw new InvalidOperationException("RuntimeConfig non assigné dans l'Inspector.");

            // Pré-clean : isolation par instance (cf CombatBootstrapIA).
            foreach (var dbg in FindObjectsByType<QuantumRunnerLocalDebug>(FindObjectsSortMode.None))
                if (dbg != null && dbg.enabled) dbg.enabled = false;
            QuantumRunner.ShutdownAll();
            await Task.Yield();

            Log("MODE HOT-SEAT LOCAL 2v2 — 4 joueurs ajoutés en local sur ce client.");

            // Clone config + bind map dédiée + données de match.
            var runtimeConfig = new QuantumUnityJsonSerializer().CloneConfig(RuntimeConfig);

            if (TeamQuantumMap != null) runtimeConfig.Map = TeamQuantumMap;
            else Debug.LogWarning("[CombatBootstrap2v2] TeamQuantumMap non assignée — risque d'auto-load d'une autre scène combat.");

            if (runtimeConfig.SimulationConfig.Id.IsValid == false
                && QuantumDefaultConfigs.TryGetGlobal(out var defaultConfigs))
                runtimeConfig.SimulationConfig = defaultConfigs.SimulationConfig;

            // 5.5 — mode équipe : 4 joueurs, pas d'IA, map (forme + spawns) authorée.
            runtimeConfig.IsBotMatch = false;
            runtimeConfig.PlayerCount = TeamPlayerCount;
            if (CombatMap != null) runtimeConfig.CombatMap = CombatMap; // AssetObject -> AssetRef (implicite)
            else Debug.LogWarning("[CombatBootstrap2v2] CombatMap non assigné — fallback zone rectangulaire 12x12 + spawns hardcodés.");

            if (runtimeConfig.Seed == 0)
                runtimeConfig.Seed = System.Guid.NewGuid().GetHashCode();

            // Start session Local, 4 slots.
            string clientId = $"local_2v2_{Guid.NewGuid():N}";
            Log($"Démarrage SessionRunner (Local, 2v2, clientId={clientId})...");
            var sessionArgs = new SessionRunner.Arguments
            {
                RunnerFactory = QuantumRunnerUnityFactory.DefaultFactory,
                GameParameters = QuantumRunnerUnityFactory.CreateGameParameters,
                ClientId = clientId,
                RuntimeConfig = runtimeConfig,
                SessionConfig = (SessionConfig != null ? SessionConfig.Config : null)
                                ?? QuantumDeterministicSessionConfigAsset.DefaultConfig,
                PlayerCount = TeamPlayerCount,
                GameMode = DeterministicGameMode.Local,
                CancellationToken = ct,
                RecordingFlags = RecordingFlags.None,
                InstantReplaySettings = InstantReplaySettings.Default,
                DeltaTimeType = SimulationUpdateTime.EngineDeltaTime,
            };

            Runner = (QuantumRunner)await SessionRunner.StartAsync(sessionArgs);
            Log("SessionRunner started.");

            // AddPlayer ×4. Équipe 0 = slots 0,1 / Équipe 1 = slots 2,3. Rang intra-équipe 0 puis 1.
            // Slot 0 = deck du hub (DeckBridge). Slots 1-3 = deck PAR DÉFAUT de leur classe (5.5e) :
            //   6 premiers sorts non-signature du catalogue -> chaque joueur peut caster en hot-seat
            //   (la signature reste gérée par classe côté HUD, hors SpellIdValues).
            AddTeamPlayer(slot: 0, teamId: 0, teamOrder: 0, cls: ResolveSlot0ClassId(), spells: ResolveSlot0Spells(), nick: "P0 (A1)", useLocalCosmetics: true);
            AddTeamPlayer(slot: 1, teamId: 0, teamOrder: 1, cls: Team0Player1, spells: BuildDefaultDeck(Team0Player1), nick: "P1 (A2)", useLocalCosmetics: false);
            AddTeamPlayer(slot: 2, teamId: 1, teamOrder: 0, cls: Team1Player0, spells: BuildDefaultDeck(Team1Player0), nick: "P2 (B1)", useLocalCosmetics: false);
            AddTeamPlayer(slot: 3, teamId: 1, teamOrder: 1, cls: Team1Player1, spells: BuildDefaultDeck(Team1Player1), nick: "P3 (B2)", useLocalCosmetics: false);
        }

        private void AddTeamPlayer(int slot, int teamId, int teamOrder, QuantumNymoraClass cls, int[] spells, string nick, bool useLocalCosmetics)
        {
            var rp = new RuntimePlayer
            {
                PlayerNickname = nick,
                ClassId = cls,
                SpellIdValues = spells,
                TeamId = teamId,
                TeamOrder = teamOrder,
                SkinId = useLocalCosmetics ? CombatCosmeticsContext.LocalSkinId : "",
                PetId = useLocalCosmetics ? CombatCosmeticsContext.LocalPetId : "",
            };
            Runner.Game.AddPlayer(slot, rp);
            Log($"AddPlayer slot {slot} ({nick}) team={teamId} rank={teamOrder} class={cls}");
        }

        // Slot 0 : classe du deck du hub si présent, sinon la classe Inspector Team0Player0.
        private QuantumNymoraClass ResolveSlot0ClassId()
        {
            if (DeckBridge.HasPending
                && System.Enum.TryParse<NymoraClassEnum>(DeckBridge.PendingClassId, ignoreCase: true, out var coreCls))
                return (QuantumNymoraClass)(byte)coreCls;
            return Team0Player0;
        }

        private int[] ResolveSlot0Spells()
        {
            var result = new int[6];
            if (!DeckBridge.HasPending || SpellCatalog == null) return result;
            var ids = DeckBridge.PendingSpellIds;
            for (int i = 0; i < 6 && ids != null && i < ids.Length; i++)
            {
                if (string.IsNullOrEmpty(ids[i])) continue;
                var def = SpellCatalog.FindBySpellId(ids[i]);
                if (def != null) result[i] = def.QuantumSpellIdValue;
            }
            return result;
        }

        // 5.5e — deck par défaut d'une classe (6 premiers sorts NON-signature du catalogue). Sert aux
        //   slots 1-3 du hot-seat (pas de deck hub) pour qu'ils puissent caster. Vide si catalogue absent.
        private int[] BuildDefaultDeck(QuantumNymoraClass cls)
        {
            var result = new int[6];
            if (SpellCatalog == null) return result;
            var coreCls = (NymoraClassEnum)(byte)cls;
            var list = SpellCatalog.FindByClass(coreCls, includeSignature: false);
            for (int i = 0; i < 6 && i < list.Count; i++)
                if (list[i] != null) result[i] = list[i].QuantumSpellIdValue;
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
                if (Runner != null) { await Runner.ShutdownAsync(); Runner = null; }
            }
            catch (Exception ex) { Debug.LogWarning($"[CombatBootstrap2v2] Shutdown error : {ex.Message}"); }
        }

        private void Log(string msg) { if (VerboseLog) Debug.Log($"[CombatBootstrap2v2] {msg}"); }
    }
}
