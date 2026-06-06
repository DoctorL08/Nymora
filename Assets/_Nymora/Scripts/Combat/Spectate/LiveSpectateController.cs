using System;
using System.Collections.Generic;
using Nymora.Combat.Bootstrap;
using Nymora.Combat.Replay;
using Nymora.Combat.View;
using Nymora.Core.Data;
using Nymora.Core.SceneFlow;
using Photon.Deterministic;
using Quantum;
using UnityEngine;

namespace Nymora.Combat.Spectate
{
    /// <summary>
    /// Brique S4 (mode spectateur) — Pilote le visionnage LIVE d'un combat dans la scène du mode
    /// (33_CombatCasual / 40_CombatRanked1v1). Calqué sur ReplayPlaybackController, mais alimenté
    /// par le flux réseau (SpectateStreamBus) au lieu d'un fichier .nymrep.
    ///
    /// Pipeline :
    ///   1. Awake : consume LiveSpectateBridge.RequestedMatchId. Absent → self-disable (la scène
    ///      tourne normalement). Présent → LiveSpectateActive=true + désactive les components
    ///      conflictuels (bootstrap, recorder, input, replay) et s'abonne au bus.
    ///   2. Start : SPECTATE_JOIN via le bus → le backend renvoie INIT (header) + chunks.
    ///   3. OnInit : démarre QuantumRunner en GameMode.Replay LOCAL (0 CCU) avec un
    ///      LiveStreamInputProvider alimenté par les chunks.
    ///   4. OnChunk : Append au provider → MaxFrame grandit → la sim avance au bord live.
    ///   5. Update : drive la sim manuellement (la session attend toute seule à MaxFrame).
    ///
    /// DefaultExecutionOrder -1000 : Awake AVANT les autres MonoBehaviour (bootstrap, etc.) pour
    /// pouvoir les désactiver avant leur Start.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class LiveSpectateController : MonoBehaviour
    {
        /// <summary>True dès qu'un visionnage live pilote la scène (set en Awake). Les bootstraps
        /// le lisent pour s'abstenir de démarrer un vrai match.</summary>
        public static bool LiveSpectateActive { get; private set; }

        private string _matchId;
        private bool _started;
        private bool _ended;
        private string _error;
        private QuantumRunner _runner;
        private LiveStreamInputProvider _provider;
        private readonly List<byte[]> _pendingChunks = new List<byte[]>(); // chunks arrivés avant l'INIT

        private void Awake()
        {
            _matchId = LiveSpectateBridge.Consume();
            LiveSpectateActive = !string.IsNullOrEmpty(_matchId);
            if (!LiveSpectateActive)
            {
                enabled = false; // mode normal : aucun visionnage demandé
                return;
            }

            DisableConflictingComponents();

            SpectateStreamBus.OnInit += OnInit;
            SpectateStreamBus.OnChunk += OnChunk;
            SpectateStreamBus.OnEnd += OnEnd;
        }

        private void Start()
        {
            if (!LiveSpectateActive) return;
            Debug.Log($"[LiveSpectate] JOIN match {_matchId}.");
            SpectateStreamBus.RequestJoin(_matchId);
        }

        private void DisableConflictingComponents()
        {
            foreach (var b in FindObjectsByType<CombatBootstrapCasual>(FindObjectsSortMode.None)) b.enabled = false;
            foreach (var r in FindObjectsByType<ReplayRecorder>(FindObjectsSortMode.None)) r.enabled = false;
            foreach (var d in FindObjectsByType<QuantumRunnerLocalDebug>(FindObjectsSortMode.None)) d.enabled = false;
            foreach (var c in FindObjectsByType<CombatInputController>(FindObjectsSortMode.None)) c.enabled = false;
            foreach (var p in FindObjectsByType<ReplayPlaybackController>(FindObjectsSortMode.None)) p.enabled = false;
        }

        private void OnInit(string matchId, string headerJson)
        {
            if (matchId != _matchId || _started) return;
            _started = true;

            SpectateHeader header;
            try { header = JsonUtility.FromJson<SpectateHeader>(headerJson); }
            catch (Exception ex) { _error = "header KO : " + ex.Message; Debug.LogError("[LiveSpectate] " + _error); return; }
            if (header == null) { _error = "header vide"; return; }

            StartSpectate(header);
        }

        private void StartSpectate(SpectateHeader header)
        {
            // Noms par PlayerIndex : le spectateur « joue » P0 (localPlayerIndex=0 via le garde HUD),
            // donc local=P0, opponent=P1. Renseigne PlayerProfileBridge → tooltip + intro pile-ou-face
            // affichent les vrais pseudos au lieu de « Joueur »/« Bot ».
            PlayerProfileBridge.SetLocal(string.IsNullOrEmpty(header.p0Name) ? "Joueur 1" : header.p0Name, "");
            PlayerProfileBridge.SetOpponent(string.IsNullOrEmpty(header.p1Name) ? "Joueur 2" : header.p1Name, "");

            var serializer = new QuantumUnityJsonSerializer();
            RuntimeConfig runtimeConfig;
            try
            {
                runtimeConfig = serializer.ConfigFromByteArray<RuntimeConfig>(
                    Convert.FromBase64String(header.runtimeConfigB64), compressed: true);
            }
            catch (Exception ex) { _error = "RuntimeConfig KO : " + ex.Message; Debug.LogError("[LiveSpectate] " + _error); return; }

            // SessionConfig : on préfère celui du header (exact match) ; fallback sur le défaut global
            // (identique pour tous les matchs : InputDeltaCompression activé).
            DeterministicSessionConfig sessionConfig = null;
            try { sessionConfig = JsonUtility.FromJson<DeterministicSessionConfig>(header.sessionConfigJson); }
            catch { /* fallback ci-dessous */ }
            if (sessionConfig == null || sessionConfig.PlayerCount <= 0)
                sessionConfig = QuantumDeterministicSessionConfigAsset.DefaultConfig;

            byte[] initialFrameData = string.IsNullOrEmpty(header.initialFrameDataB64)
                ? null : Convert.FromBase64String(header.initialFrameDataB64);

            _provider = new LiveStreamInputProvider(header.localActorNumber);
            // Rejoue les chunks arrivés avant l'INIT (rattrapage backend).
            foreach (var c in _pendingChunks) _provider.Append(c);
            _pendingChunks.Clear();

            var args = new SessionRunner.Arguments
            {
                RunnerFactory = QuantumRunnerUnityFactory.DefaultFactory,
                GameParameters = QuantumRunnerUnityFactory.CreateGameParameters,
                RuntimeConfig = runtimeConfig,
                SessionConfig = sessionConfig,
                ReplayProvider = _provider,
                GameMode = DeterministicGameMode.Replay,
                InitialTick = header.initialTick,
                FrameData = initialFrameData,
                RunnerId = "SPECTATE",
                PlayerCount = sessionConfig.PlayerCount,
                DeltaTimeType = SimulationUpdateTime.EngineDeltaTime,
                GameFlags = 0,
                RecordingFlags = RecordingFlags.None,
            };

            try
            {
                _runner = QuantumRunner.StartGame(args);
                Debug.Log($"[LiveSpectate] Session replay live démarrée (initialTick={header.initialTick}, maxFrame={_provider.MaxFrame}).");
            }
            catch (Exception ex) { _error = "StartGame KO : " + ex.Message; Debug.LogError("[LiveSpectate] " + _error); }
        }

        private void OnChunk(string matchId, int seq, string dataB64)
        {
            if (matchId != _matchId || string.IsNullOrEmpty(dataB64)) return;
            byte[] bytes;
            try { bytes = Convert.FromBase64String(dataB64); }
            catch { return; }
            if (_provider != null) _provider.Append(bytes);
            else _pendingChunks.Add(bytes); // pas encore d'INIT
        }

        private void OnEnd(string matchId)
        {
            if (matchId != _matchId) return;
            _ended = true;
            Debug.Log($"[LiveSpectate] Flux terminé (match {_matchId}).");
        }

        private float _joinRetryTimer;

        // Au-delà de ce retard (en frames) on rejoue à 8× pour rattraper le live (~2s à 60 fps).
        private const int CatchUpFrameThreshold = 120;

        private void Update()
        {
            // Tant que l'INIT n'est pas arrivé (header pas encore relayé si on a cliqué très tôt),
            // on relance le JOIN toutes les 2 s.
            if (_runner == null)
            {
                if (!_started && LiveSpectateActive && _error == null)
                {
                    _joinRetryTimer += Time.unscaledDeltaTime;
                    if (_joinRetryTimer >= 2f) { _joinRetryTimer = 0f; SpectateStreamBus.RequestJoin(_matchId); }
                }
                return;
            }
            if (_runner.Session == null || _provider == null) return;
            // Drive manuel : la session avance jusqu'à MaxFrame (CanSimulate) puis attend le live.
            _runner.IsSessionUpdateDisabled = true;

            // Catch-up : si on a rejoint en cours de match, on rejoue vite jusqu'au bord live, puis 1×.
            // Défensif : Game/Frames.Verified peuvent être null quelques frames après StartGame ;
            // surtout, on n'accède PAS à SessionConfig (null transitoire ici) pour ne jamais
            // empêcher l'appel à Service() ci-dessous (sinon la session ne progresse jamais).
            float speed = 1f;
            var game = _runner.Game;
            if (game != null)
            {
                var f = game.Frames.Verified;
                if (f != null && _provider.MaxFrame - f.Number > CatchUpFrameThreshold) speed = 8f;
            }

            _runner.Service(Time.deltaTime * speed);
            QuantumUnityDB.UpdateGlobal();
        }

        private void Quit()
        {
            SpectateStreamBus.RequestLeave(_matchId);
            SceneTransition.Load("10_CommunityHub", waitForReady: true);
        }

        private void OnDestroy()
        {
            SpectateStreamBus.OnInit -= OnInit;
            SpectateStreamBus.OnChunk -= OnChunk;
            SpectateStreamBus.OnEnd -= OnEnd;
            LiveSpectateActive = false;
            if (_runner != null)
            {
                try { QuantumRunner.ShutdownAll(); }
                catch (Exception ex) { Debug.LogWarning("[LiveSpectate] ShutdownAll : " + ex.Message); }
                _runner = null;
            }
        }

        // HUD minimal (stopgap S4 — S5 intègrera un vrai bandeau « EN DIRECT » + retour propre).
        private void OnGUI()
        {
            if (!LiveSpectateActive) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            style.normal.textColor = Color.white;
            string status = _error != null ? "Erreur : " + _error
                : (_runner == null ? "Connexion…" : (_ended ? "● TERMINÉ" : "● EN DIRECT"));
            GUI.Label(new Rect(Screen.width / 2f - 90f, 8f, 220f, 30f), status, style);
            if (GUI.Button(new Rect(Screen.width - 120f, 8f, 110f, 32f), "Quitter")) Quit();
        }
    }
}
