using System;
using System.Collections;
using System.Linq;
using Nymora.Combat.View;
using Nymora.Core.Data;
using Photon.Deterministic;
using Quantum;
using UnityEngine;

namespace Nymora.Combat.Replay
{
    /// <summary>
    /// Brique 3.E.2 — Pilote la relecture d'un fichier .nymrep dans la scene
    /// 31_CombatReplay. Pattern inspire de QuantumRunnerLocalReplay (SDK Photon).
    ///
    /// Cycle de vie :
    ///   1. Awake : consume <see cref="ReplayPlaybackBridge.RequestedReplayPath"/>.
    ///      Si absent -> self-disable + message d'erreur (mode "scene ouverte
    ///      manuellement sans replay request").
    ///   2. Disable les components conflictuels (ReplayRecorder, LocalDebug,
    ///      CombatInputController) pour qu'ils n'envoient ni n'enregistrent rien.
    ///   3. Verifie la compatibilite CombatRulesVersion. Refuse si mismatch.
    ///   4. Decode RuntimeConfig + ReplayProvider depuis le QuantumReplayFile.
    ///   5. Demarre QuantumRunner.StartGame en GameMode.Replay.
    ///   6. Update : drive la simu en mode manuel (IsSessionUpdateDisabled=true)
    ///      pour controler vitesse / pause / step.
    ///
    /// <see cref="DefaultExecutionOrder"/> -1000 : garantit que le Awake passe
    /// AVANT les autres MonoBehaviour (LocalDebug etc.), donc on peut les
    /// desactiver avant leur Start.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class ReplayPlaybackController : MonoBehaviour
    {
        private static readonly float[] SpeedSteps = new[] { 0.5f, 1f, 2f, 4f };

        private NymoraReplayFile _file;
        private QuantumReplayFile _quantumReplay;
        private QuantumRunner _runner;
        private int _lastTick;
        private int _speedIndex = 1; // 1× par defaut
        private bool _paused;
        private string _errorMessage;
        private bool _readyToStart;
        private bool _desyncDetected;
        private int _desyncFrameNumber = -1;
        private bool _isSeeking;

        public bool IsRunning { get { return _runner != null && _runner.Session != null; } }
        public bool IsPaused { get { return _paused; } }
        public bool IsReplayFinished
        {
            get { return IsRunning && _runner.Session.IsReplayFinished; }
        }
        public float Speed { get { return SpeedSteps[_speedIndex]; } }
        public int CurrentTick
        {
            get
            {
                if (_runner == null || _runner.Game == null) return 0;
                var f = _runner.Game.Frames.Verified;
                return f != null ? f.Number : 0;
            }
        }
        public int LastTick { get { return _lastTick; } }
        public string ErrorMessage { get { return _errorMessage; } }
        public NymoraReplayMetadata Metadata { get { return _file != null ? _file.Metadata : null; } }
        public bool HasDesync { get { return _desyncDetected; } }
        public int DesyncFrameNumber { get { return _desyncFrameNumber; } }
        public bool IsSeeking { get { return _isSeeking; } }

        private void Awake()
        {
            string path = ReplayPlaybackBridge.Consume();
            if (string.IsNullOrEmpty(path))
            {
                // Mode normal : aucun replay demande, on cede silencieusement la
                // main au QuantumRunnerLocalDebug standard. La scene fonctionne
                // comme un match normal sans aucune trace de l'infra replay.
                enabled = false;
                return;
            }

            DisableConflictingComponents();

            try { _file = NymoraReplayFile.ReadFromDisk(path); }
            catch (Exception ex)
            {
                _errorMessage = "Lecture .nymrep KO : " + ex.Message;
                Debug.LogError("[Nymora.ReplayPlayback] " + _errorMessage, this);
                return;
            }

            if (_file == null || _file.Metadata == null)
            {
                _errorMessage = "Fichier replay vide ou introuvable : " + path;
                Debug.LogError("[Nymora.ReplayPlayback] " + _errorMessage, this);
                return;
            }

            if (_file.Metadata.CombatRulesVersion != GameVersion.CombatRulesVersion)
            {
                _errorMessage = string.Format(
                    "Replay incompatible : enregistre CombatRulesVersion={0}, courant={1}. " +
                    "Les regles de combat ont change depuis — rejeu impossible sans desync.",
                    _file.Metadata.CombatRulesVersion, GameVersion.CombatRulesVersion);
                Debug.LogError("[Nymora.ReplayPlayback] " + _errorMessage, this);
                return;
            }

            _quantumReplay = _file.ToQuantumReplay();
            if (_quantumReplay == null)
            {
                _errorMessage = "Payload Quantum vide dans le .nymrep.";
                Debug.LogError("[Nymora.ReplayPlayback] " + _errorMessage, this);
                return;
            }
            _lastTick = _quantumReplay.LastTick;

            // Init Quantum (QuantumRunnerUnityFactory.DefaultFactory) se fait via
            // [RuntimeInitializeOnLoadMethod] APRES tous les Awake. On retarde donc
            // le demarrage du runner a Start() pour que DefaultFactory soit set.
            _readyToStart = true;
        }

        private void Start()
        {
            if (!_readyToStart) return;
            StartReplay();
        }

        private void DisableConflictingComponents()
        {
            foreach (var rec in FindObjectsByType<ReplayRecorder>(FindObjectsSortMode.None)) rec.enabled = false;
            foreach (var dbg in FindObjectsByType<QuantumRunnerLocalDebug>(FindObjectsSortMode.None)) dbg.enabled = false;
            foreach (var ctl in FindObjectsByType<CombatInputController>(FindObjectsSortMode.None)) ctl.enabled = false;
        }

        private void StartReplay()
        {
            var serializer = new QuantumUnityJsonSerializer();
            RuntimeConfig runtimeConfig;
            try
            {
                runtimeConfig = serializer.ConfigFromByteArray<RuntimeConfig>(
                    _quantumReplay.RuntimeConfigData.Decode(), compressed: true);
            }
            catch (Exception ex)
            {
                _errorMessage = "Decode RuntimeConfig KO : " + ex.Message;
                Debug.LogError("[Nymora.ReplayPlayback] " + _errorMessage, this);
                return;
            }

            var inputProvider = _quantumReplay.CreateInputProvider();
            if (inputProvider == null)
            {
                _errorMessage = "ReplayProvider null — replay corrompu ou InputHistory vide.";
                Debug.LogError("[Nymora.ReplayPlayback] " + _errorMessage, this);
                return;
            }

            var arguments = new SessionRunner.Arguments
            {
                RunnerFactory = QuantumRunnerUnityFactory.DefaultFactory,
                GameParameters = QuantumRunnerUnityFactory.CreateGameParameters,
                RuntimeConfig = runtimeConfig,
                SessionConfig = _quantumReplay.DeterministicConfig,
                ReplayProvider = inputProvider,
                GameMode = DeterministicGameMode.Replay,
                InitialTick = _quantumReplay.InitialTick,
                FrameData = _quantumReplay.InitialFrameData,
                RunnerId = "REPLAY",
                PlayerCount = _quantumReplay.DeterministicConfig.PlayerCount,
                DeltaTimeType = SimulationUpdateTime.EngineDeltaTime,
                GameFlags = 0,
                RecordingFlags = RecordingFlags.None,
            };

            try
            {
                _runner = QuantumRunner.StartGame(arguments);
                Debug.Log(string.Format(
                    "[Nymora.ReplayPlayback] Replay demarre : tick 0 -> {0}, {1} vs {2}.",
                    _lastTick, _file.Metadata.Player0Class, _file.Metadata.Player1Class), this);

                // 3.E.2.c — Detection desync. Si le replay contient des checksums,
                // on demande a Quantum de les verifier tick-par-tick. Tout mismatch
                // declenche CallbackChecksumError que l'on intercepte ci-dessous.
                if (_quantumReplay.Checksums != null && _quantumReplay.Checksums.Checksums != null
                    && _quantumReplay.Checksums.Checksums.Length > 0)
                {
                    _runner.Game.StartVerifyingChecksums(_quantumReplay.Checksums);
                    QuantumCallback.Subscribe(this, (CallbackChecksumError c) => OnChecksumError(c));
                    Debug.Log("[Nymora.ReplayPlayback] Verification checksums activee (" +
                              _quantumReplay.Checksums.Checksums.Length + " entrees).", this);
                }
            }
            catch (Exception ex)
            {
                _errorMessage = "QuantumRunner.StartGame KO : " + ex.Message;
                Debug.LogError("[Nymora.ReplayPlayback] " + _errorMessage, this);
            }
        }

        private void OnChecksumError(CallbackChecksumError c)
        {
            if (_desyncDetected) return; // ne log que la premiere desync (les suivantes en cascade)
            _desyncDetected = true;
            _desyncFrameNumber = c.Error.Tick;
            Debug.LogError(string.Format(
                "[Nymora.ReplayPlayback] DESYNC detectee au frame {0}. " +
                "Le replay ne correspond pas a l'execution courante — probablement un changement " +
                "non-deterministe dans la simulation (Random/Time/DateTime sneake apres l'enregistrement).",
                _desyncFrameNumber), this);
        }

        private void Update()
        {
            if (!IsRunning) return;
            if (_isSeeking) return; // la coroutine pilote le runner pendant le seek

            // Drive manuel en permanence : permet vitesse / pause / step propres.
            _runner.IsSessionUpdateDisabled = true;

            if (_paused) return;
            if (_runner.Session.IsReplayFinished) return;

            _runner.Service(Time.deltaTime * Speed);
            QuantumUnityDB.UpdateGlobal();
        }

        public void Pause() { _paused = true; }
        public void Resume()
        {
            if (IsReplayFinished) return;
            _paused = false;
        }
        public void TogglePause()
        {
            if (_paused) Resume();
            else Pause();
        }

        /// <summary>Avance d'un tick verifie puis met en pause. Cap safety pour eviter spin.</summary>
        public void Step()
        {
            if (!IsRunning || IsReplayFinished) return;
            _paused = true;

            int startTick = CurrentTick;
            float dtPerTick = 1f / Math.Max(1, _runner.Session.SessionConfig.UpdateFPS);

            for (int i = 0; i < 4 && CurrentTick == startTick; i++)
            {
                if (_runner.Session.IsReplayFinished) break;
                _runner.IsSessionUpdateDisabled = true;
                _runner.Service(dtPerTick * 1.1f);
                QuantumUnityDB.UpdateGlobal();
            }
        }

        public void CycleSpeed()
        {
            _speedIndex = (_speedIndex + 1) % SpeedSteps.Length;
        }

        /// <summary>Rewind a tick 0 puis pause. Equivalent de SeekTo(0).</summary>
        public void Restart()
        {
            SeekTo(0);
        }

        /// <summary>
        /// Seek arbitraire (3.E.2.b) : shutdown du runner courant, restart depuis
        /// tick 0 avec un nouvel input provider, fast-forward jusqu'a targetTick.
        /// No-op si une seek est deja en cours ou si pas de replay valide charge.
        /// </summary>
        public void SeekTo(int targetTick)
        {
            if (_quantumReplay == null) return;
            if (_isSeeking) return;
            targetTick = Mathf.Clamp(targetTick, 0, _lastTick);
            StartCoroutine(SeekCoroutine(targetTick));
        }

        private IEnumerator SeekCoroutine(int targetTick)
        {
            _isSeeking = true;
            _paused = true;

            try { QuantumRunner.ShutdownAll(); }
            catch (Exception ex) { Debug.LogWarning("[Nymora.ReplayPlayback] ShutdownAll during seek : " + ex.Message); }
            _runner = null;
            _desyncDetected = false;
            _desyncFrameNumber = -1;

            int waitSafety = 0;
            while (QuantumRunner.ActiveRunners.Any() && waitSafety++ < 120)
            {
                yield return null;
            }

            StartReplay();
            if (_runner == null)
            {
                _isSeeking = false;
                yield break;
            }

            if (targetTick > 0)
            {
                float dtPerTick = 1f / Math.Max(1, _runner.Session.SessionConfig.UpdateFPS);
                int maxIter = Math.Max(200, targetTick * 4);
                int safety = 0;
                while (CurrentTick < targetTick && safety++ < maxIter)
                {
                    if (_runner.Session.IsReplayFinished) break;
                    _runner.IsSessionUpdateDisabled = true;
                    _runner.Service(dtPerTick * 20f);
                    QuantumUnityDB.UpdateGlobal();
                    if ((safety % 50) == 0) yield return null;
                }
            }

            _isSeeking = false;
            _paused = true;
        }

        private void OnDestroy()
        {
            if (_runner != null)
            {
                try { QuantumRunner.ShutdownAll(); }
                catch (Exception ex) { Debug.LogWarning("[Nymora.ReplayPlayback] ShutdownAll : " + ex.Message); }
            }
        }
    }
}
