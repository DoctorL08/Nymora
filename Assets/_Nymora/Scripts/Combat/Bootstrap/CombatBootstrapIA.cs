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
    /// Brique 5.4 (18 mai 2026) — Bootstrap offline Quantum pour la scene 30_CombatIA.
    ///
    /// REMPLACE QuantumRunnerLocalDebug (offline default) dans la scene 30_CombatIA pour
    /// pouvoir injecter la classe du joueur depuis DeckBridge (avant fix : CombatantSystem.OnInit
    /// hardcodait P0=Ghostra). Mirror simplifie de CombatBootstrapCasual : pas de Photon
    /// (GameMode.Local), pas de slot remote, on AddPlayer les 2 slots localement.
    ///
    /// Pipeline (au Start) :
    ///   1. Lit DeckBridge.PendingClassId / PendingSpellIds (set par HubArenaPanel)
    ///   2. Quantum SessionRunner.StartAsync en mode Local
    ///   3. Game.AddPlayer(0, lorenzo)  — classe + 6 sorts du DeckBridge
    ///      Game.AddPlayer(1, bot)      — classe configurable (default Soulrender)
    ///   4. CombatantSystem.OnPlayerAdded spawn les 2 entities avec la bonne classe
    ///
    /// Fallback : si DeckBridge vide (Lorenzo lance la scene direct depuis l'Editor sans
    /// passer par le hub), slot 0 = Soulrender + array zeros (gameplay testable malgre tout).
    /// </summary>
    public sealed class CombatBootstrapIA : MonoBehaviour
    {
        [Header("Quantum runtime")]
        [Tooltip("RuntimeConfig de 30_CombatIA (RuntimeConfigCombatIA.asset, IsBotMatch=true).")]
        public RuntimeConfig RuntimeConfig;

        [Tooltip("Session config (asset partage avec 33_CombatCasual). Laisse null pour utiliser le default global.")]
        public QuantumDeterministicSessionConfigAsset SessionConfig;

        [Header("Data")]
        [Tooltip("SpellCatalog asset (Assets/_Nymora/ScriptableObjects/Spells/SpellCatalog.asset). " +
                 "Sert a convertir DeckBridge.PendingSpellIds (string snake_case) en int[] " +
                 "Quantum.SpellId values pour push dans RuntimePlayer.SpellIdValues.")]
        public SpellCatalog SpellCatalog;

        [Header("Bot config")]
        [Tooltip("Classe utilisee pour le slot 1 (bot drive par AISystem). " +
                 "Default Soulrender pour coller au comportement legacy 2.2. " +
                 "Plus tard : sera passe par HubArenaPanel pour choisir l'adversaire IA.")]
        public QuantumNymoraClass BotClass = QuantumNymoraClass.Soulrender;

        [Header("Debug")]
        [Tooltip("Si TRUE, log verbeux pour debug brique 5.4.")]
        public bool VerboseLog = true;

        // Runtime
        public QuantumRunner Runner { get; private set; }

        public static CombatBootstrapIA Instance { get; private set; }

        private CancellationTokenSource _cts;

        private async void Start()
        {
            Instance = this;
            _cts = new CancellationTokenSource();
            try
            {
                await BootstrapAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                Log("Bootstrap annule (probablement OnDestroy / scene quit).");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CombatBootstrapIA] Bootstrap echec : {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async Task BootstrapAsync(CancellationToken ct)
        {
            if (RuntimeConfig == null)
                throw new InvalidOperationException("RuntimeConfig non assigne dans l'Inspector — drag RuntimeConfigCombatIA.asset.");

            // ===== 1. Clone runtime config + bind map from scene QuantumMapData =====
            var runtimeConfig = new QuantumUnityJsonSerializer().CloneConfig(RuntimeConfig);

            var mapData = FindAnyObjectByType<QuantumMapData>();
            if (mapData != null) runtimeConfig.Map = mapData.AssetRef;

            if (runtimeConfig.SimulationConfig.Id.IsValid == false
                && QuantumDefaultConfigs.TryGetGlobal(out var defaultConfigs))
            {
                runtimeConfig.SimulationConfig = defaultConfigs.SimulationConfig;
            }

            // Safety net : force IsBotMatch=true pour cette scene IA, meme si l'asset
            // RuntimeConfig en inspector avait ete laisse a false par erreur.
            runtimeConfig.IsBotMatch = true;

            // ===== 2. Start Quantum session (Local, 2 slots) =====
            Log("Demarrage SessionRunner Quantum (Local, IA mode)...");
            var sessionArgs = new SessionRunner.Arguments
            {
                RunnerFactory = QuantumRunnerUnityFactory.DefaultFactory,
                GameParameters = QuantumRunnerUnityFactory.CreateGameParameters,
                ClientId = "local_ia",
                RuntimeConfig = runtimeConfig,
                SessionConfig = (SessionConfig != null ? SessionConfig.Config : null)
                                ?? QuantumDeterministicSessionConfigAsset.DefaultConfig,
                PlayerCount = 2,
                GameMode = DeterministicGameMode.Local,
                CancellationToken = ct,
                RecordingFlags = RecordingFlags.None,
                InstantReplaySettings = InstantReplaySettings.Default,
                DeltaTimeType = SimulationUpdateTime.EngineDeltaTime,
            };

            Runner = (QuantumRunner)await SessionRunner.StartAsync(sessionArgs);
            Log("SessionRunner started.");

            // ===== 3. Add slot 0 (Lorenzo) — classe + sorts depuis DeckBridge =====
            var lorenzo = new RuntimePlayer
            {
                PlayerNickname = "Lorenzo",
                ClassId = ResolveLorenzoClassId(),
                SpellIdValues = ResolveLorenzoSpellIdValues(),
            };
            Runner.Game.AddPlayer(0, lorenzo);
            Log($"AddPlayer slot 0 (Lorenzo) class={lorenzo.ClassId} deck=[{string.Join(",", lorenzo.SpellIdValues)}]");

            // ===== 4. Add slot 1 (bot) — classe hardcoded configurable, drive par AISystem =====
            var bot = new RuntimePlayer
            {
                PlayerNickname = "Bot",
                ClassId = BotClass,
                SpellIdValues = new int[6], // 6 zeros : l'AI utilise tout le pool de sa classe via SpellRegistry
            };
            Runner.Game.AddPlayer(1, bot);
            Log($"AddPlayer slot 1 (Bot) class={bot.ClassId}");
        }

        // ====== Helpers ======

        /// <summary>
        /// Convertit DeckBridge.PendingClassId (string "Soulrender"/...) vers Quantum.NymoraClass.
        /// Fallback Soulrender si DeckBridge vide / classe inconnue (defensive — permet a
        /// Lorenzo de lancer 30_CombatIA direct depuis l'Editor sans passer par le hub).
        /// </summary>
        private static QuantumNymoraClass ResolveLorenzoClassId()
        {
            if (!DeckBridge.HasPending)
            {
                Debug.LogWarning("[CombatBootstrapIA] DeckBridge vide (scene lancee direct Editor ?) — fallback class Soulrender slot 0.");
                return QuantumNymoraClass.Soulrender;
            }

            if (System.Enum.TryParse<NymoraClassEnum>(DeckBridge.PendingClassId, ignoreCase: true, out var coreCls))
            {
                return (QuantumNymoraClass)(byte)coreCls;
            }
            Debug.LogWarning($"[CombatBootstrapIA] DeckBridge.PendingClassId='{DeckBridge.PendingClassId}' non parsable — fallback Soulrender slot 0.");
            return QuantumNymoraClass.Soulrender;
        }

        /// <summary>
        /// Convertit les 6 SpellIdTech (snake_case ex "soulrender_tranche_ame") en int[]
        /// = (int)Quantum.SpellId. Retourne un array de 6 zeros si DeckBridge vide /
        /// catalog manquant (defensive).
        /// </summary>
        private int[] ResolveLorenzoSpellIdValues()
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
                    Debug.LogWarning($"[CombatBootstrapIA] SpellCatalog.FindBySpellId('{spellIdTech}') retourne null — slot {i} = 0.");
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
                Debug.LogWarning($"[CombatBootstrapIA] Runner shutdown error : {ex.Message}");
            }
        }

        private void Log(string msg)
        {
            if (VerboseLog) Debug.Log($"[CombatBootstrapIA] {msg}");
        }
    }
}
