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
    ///   1. Pre-clean : disable tout QuantumRunnerLocalDebug legacy + ShutdownAll runners zombis
    ///      (5.4.b hardening 18 mai : garantit l'isolation par instance peu importe l'etat
    ///      de la scene ; un pote sur un build ancien qui n'aurait pas eu la manip Unity
    ///      ne risque plus de spectater le combat d'une autre instance via Quantum cloud).
    ///   2. Lit DeckBridge.PendingClassId / PendingSpellIds (set par HubArenaPanel)
    ///   3. Quantum SessionRunner.StartAsync en mode Local (ClientId GUID unique par instance)
    ///   4. Game.AddPlayer(0, lorenzo)  — classe + 6 sorts du DeckBridge
    ///      Game.AddPlayer(1, bot)      — classe configurable (default Soulrender)
    ///   5. CombatantSystem.OnPlayerAdded spawn les 2 entities avec la bonne classe
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

        // Map dediee IA. Necessaire pour empecher Quantum d'auto-load 33_CombatCasual
        // additivement via QuantumMap.ScenePath (incident 19 mai 2026 bis : la map
        // partagee QuantumMap.asset pointe vers Casual, donc en bootstrap IA, Quantum
        // chargeait Casual par-dessus, son CombatBootstrapCasual.Start() voyait pas
        // de match pending et faisait ReturnToHub -> retour hub immediat).
        // Cette map clone pointe vers 30_CombatIA elle-meme : auto-load no-op.
        [Tooltip("QuantumMap_IA.asset (Assets/QuantumUser/Resources/QuantumMap_IA.asset). " +
                 "Clone de QuantumMap.asset dont la ScenePath pointe vers 30_CombatIA au " +
                 "lieu de 33_CombatCasual, empechant Quantum d'auto-load la scene Casual " +
                 "additivement (qui retournerait au hub via son bootstrap sans match pending).")]
        public Map IAMap;

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

        // Mirror de CombatBootstrapCasual.ExpectedSceneName (incident 18 mai bis) :
        // ce bootstrap n'a de sens QUE dans 30_CombatIA. Si le GO survit dans une AUTRE
        // scene combat (typiquement 33_CombatCasual via residu de clone 4.14.b), son
        // ShutdownAll() ligne 119 tue le runner Casual qui vient de demarrer.
        // Fix 19 mai 2026 (brique 5.4.c PvP test).
        private const string ExpectedSceneName = "30_CombatIA";

        private async void Start()
        {
            // Garde 0 (replay) : si un replay est en cours de lecture dans cette scène, le
            // ReplayPlaybackController (-1000) a déjà démarré son runner. Le bootstrap doit
            // s'abstenir (sinon son ShutdownAll pré-clean tuerait le runner replay).
            if (Nymora.Combat.Replay.ReplayPlaybackController.ReplaybackActive)
            {
                Log("Mode replay actif -> bootstrap IA skip (le ReplayPlaybackController pilote la sim).");
                return;
            }

            // Garde 1 (forte) : si un CombatBootstrapCasual est instancie ailleurs dans
            // la session (peu importe la scene de hosting), on est en match PvP en cours.
            // IA skip total -> sinon son QuantumRunner.ShutdownAll() pre-clean ligne ~144
            // tuerait le runner Casual qui vient juste de demarrer la sim PvP.
            //
            // Cas particulier observe 19 mai (brique 5.4.c) : Unity Editor multi-scene
            // editing avec 30_CombatIA chargee additive en plus de 33_CombatCasual.
            // gameObject.scene.name retourne "30_CombatIA" pour ce GO (resident dans la
            // scene IA additive), donc une simple garde scene-check ne suffit pas.
            var casualBootstrap = FindAnyObjectByType<CombatBootstrapCasual>();
            if (casualBootstrap != null)
            {
                Log($"CombatBootstrapCasual detecte (scene='{casualBootstrap.gameObject.scene.name}') -> IA skip (mode PvP en cours).");
                return;
            }

            // Garde 2 (defensive) : ce component n'a de sens que dans 30_CombatIA en
            // mode single. Si on est dans une autre scene SANS Casual non plus, on skip
            // quand meme par precaution.
            if (gameObject.scene.name != ExpectedSceneName)
            {
                Log($"Bootstrap skip : ce component est dans la scene '{gameObject.scene.name}' mais ne doit s'activer que dans '{ExpectedSceneName}'.");
                return;
            }

            // Garde 3 (defensive) : multi-scene editing avec une scene active != IA.
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.name != ExpectedSceneName)
            {
                Log($"Bootstrap skip : scene active='{activeScene.name}' != '{ExpectedSceneName}' (probable multi-scene editing).");
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
                Log("Bootstrap annule (probablement OnDestroy / scene quit).");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CombatBootstrapIA] Bootstrap echec : {ex.Message}\n{ex.StackTrace}");
            }
        }

        private async Task BootstrapAsync(CancellationToken ct)
        {
            // Tuto : en éditeur, applique l'override DEV (PlayerPrefs posé par TutorialDevLauncher)
            // AVANT toute lecture de TutorialContext.Active ci-dessous. No-op en build (Conditional).
            Nymora.Core.Data.TutorialContext.ApplyEditorDevOverride();

            if (RuntimeConfig == null)
                throw new InvalidOperationException("RuntimeConfig non assigne dans l'Inspector — drag RuntimeConfigCombatIA.asset.");

            // ===== 0. Pre-clean : garantir l'isolation par instance (5.4.b hardening) =====
            // (i) Disable tout QuantumRunnerLocalDebug legacy encore actif dans la scene.
            //     Si Lorenzo (ou un pote sur un build moins recent) n'a pas fait la manip
            //     Unity du 18 mai pour disable QuantumRunnerLocalDebug, le composant risque
            //     de lancer un 2e Quantum runner en parallele du notre, avec un comportement
            //     non-deterministe (deux sims en course, l'une ecrase l'autre). On force le
            //     disable pour rendre l'install foolproof.
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
                Debug.LogWarning($"[CombatBootstrapIA] {disabledCount} QuantumRunnerLocalDebug legacy detecte(s) et disabled. " +
                                 "Tu peux faire la manip Unity (Inspector -> uncheck) pour rendre la scene propre.");
            }

            // (ii) Shutdown tout runner Quantum residuel (zombi d'une session precedente :
            //      hub -> combat -> hub -> combat re-entrant, ou crash Editor laissant un
            //      runner DontDestroyOnLoad encore actif). Sans ce ShutdownAll, le nouveau
            //      SessionRunner.StartAsync peut piocher l'ancien runner singleton et ne
            //      reinit pas la sim (cf pattern MatchEndOverlay.OnRestartClicked).
            QuantumRunner.ShutdownAll();
            await Task.Yield(); // laisse Quantum propager le shutdown sur 1 frame

            // Log explicite du mode : aide a debugger si jamais quelqu'un confond
            // 30_CombatIA (offline) avec 33_CombatCasual (Photon Realtime PvP).
            Log("MODE OFFLINE LOCAL — instance isolee, aucune connexion reseau pour la sim.");
            if (Nymora.Core.Data.TutorialContext.Active)
                Log("MODE TUTORIEL ACTIF — slot 0 force Soulrender + deck tuto (DeckBridge ignore).");

            // ===== 1. Clone runtime config + bind map IA dediee =====
            // On NE FAIT PAS FindAnyObjectByType<QuantumMapData>() car ca retomberait
            // sur la QuantumMap.asset globale dont la ScenePath = 33_CombatCasual ->
            // Quantum auto-load Casual en additif -> ReturnToHub immediat.
            // Cf docstring du field IAMap au-dessus.
            var runtimeConfig = new QuantumUnityJsonSerializer().CloneConfig(RuntimeConfig);

            if (IAMap != null)
            {
                runtimeConfig.Map = IAMap;
            }
            else
            {
                Debug.LogWarning("[CombatBootstrapIA] IAMap non assigne dans l'Inspector — " +
                                 "fallback FindAnyObjectByType<QuantumMapData> (risque charger " +
                                 "33_CombatCasual additivement via QuantumMap.ScenePath). " +
                                 "Drag QuantumMap_IA.asset sur le component CombatBootstrapIA.");
                var mapData = FindAnyObjectByType<QuantumMapData>();
                if (mapData != null) runtimeConfig.Map = mapData.AssetRef;
            }

            if (runtimeConfig.SimulationConfig.Id.IsValid == false
                && QuantumDefaultConfigs.TryGetGlobal(out var defaultConfigs))
            {
                runtimeConfig.SimulationConfig = defaultConfigs.SimulationConfig;
            }

            // Safety net : force IsBotMatch=true pour cette scene IA, meme si l'asset
            // RuntimeConfig en inspector avait ete laisse a false par erreur.
            runtimeConfig.IsBotMatch = true;

            // Tuto T2 : mannequin passif. En mode tutoriel le bot prend son tour mais ne fait
            // rien (AISystem.Update lit ce flag). false en match IA normal -> bot agit comme avant.
            runtimeConfig.TutorialPassiveBot = Nymora.Core.Data.TutorialContext.Active;

            // PATCH 22 mai (test designer) — Seed RNG aleatoire par match (meme bug latent qu'en
            // casual : Seed=0 -> initiative toujours P0). Local single-client : Guid suffit.
            if (runtimeConfig.Seed == 0)
            {
                runtimeConfig.Seed = System.Guid.NewGuid().GetHashCode();
            }

            // ===== 2. Start Quantum session (Local, 2 slots) =====
            // ClientId GUID unique par instance (5.4.b hardening) : meme si l'AppIdQuantum
            // active un mode multiplayer accidentel et qu'on partage le meme AppId entre
            // PC, deux instances ne peuvent plus collide sur le meme ClientId.
            string clientId = $"local_ia_{Guid.NewGuid():N}";
            Log($"Demarrage SessionRunner Quantum (Local, IA mode, clientId={clientId})...");
            var sessionArgs = new SessionRunner.Arguments
            {
                RunnerFactory = QuantumRunnerUnityFactory.DefaultFactory,
                GameParameters = QuantumRunnerUnityFactory.CreateGameParameters,
                ClientId = clientId,
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
                // 5.10 (A3) — skin/familier équipé (résolu côté hub). Le bot (slot 1) n'en a pas.
                SkinId = Nymora.Core.Data.CombatCosmeticsContext.LocalSkinId,
                PetId = Nymora.Core.Data.CombatCosmeticsContext.LocalPetId,
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
            // Tuto (T1) : classe Soulrender imposée, on ignore le DeckBridge pour un scénario prévisible.
            if (Nymora.Core.Data.TutorialContext.Active)
                return QuantumNymoraClass.Soulrender;

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
            // Tuto (T1) : deck Soulrender fixe (TutorialContext), on ignore le DeckBridge.
            string[] sourceIds = Nymora.Core.Data.TutorialContext.Active
                ? Nymora.Core.Data.TutorialContext.SoulrenderTutorialDeck
                : (DeckBridge.HasPending ? DeckBridge.PendingSpellIds : null);

            var result = new int[6];
            if (sourceIds == null || SpellCatalog == null) return result;

            for (int i = 0; i < 6 && i < sourceIds.Length; i++)
            {
                var spellIdTech = sourceIds[i];
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
            // Tuto : on quitte le combat -> on désarme le mode tuto pour ne pas contaminer un
            // futur match IA normal de la même session (le déclencheur le re-pose si besoin).
            Nymora.Core.Data.TutorialContext.Reset();
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
