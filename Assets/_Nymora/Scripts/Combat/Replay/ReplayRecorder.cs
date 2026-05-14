using System;
using Nymora.Core.Data;
using Quantum;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nymora.Combat.Replay
{
    /// <summary>
    /// Brique 3.E.1 — Capture en memoire des inputs Quantum d'un match Nymora.
    ///
    /// Pipeline :
    ///   1. CallbackGameStarted -> game.StartRecordingInput() + capture timestamp debut + reset state
    ///   2. CallbackUpdateView (poll chaque frame) -> capte les classes des combattants une
    ///      fois, track le round courant, et a la 1ere occurence de CombatPhase.MatchEnd
    ///      snapshot le QuantumReplayFile et garde-le en memoire (pas d'ecriture disque auto).
    ///   3. Le MatchEndOverlay propose un bouton "Sauvegarder le replay" qui appelle
    ///      <see cref="SaveCurrentReplay"/> -> ecriture du fichier .nymrep dans
    ///      <see cref="ReplayPaths.RootFolder"/>.
    ///   4. Si le joueur ne sauvegarde pas, le replay est lost a la prochaine partie
    ///      (reset au prochain CallbackGameStarted).
    ///
    /// Class-agnostic : on capture les inputs natifs Quantum (MoveCommand, CastSpellCommand,
    /// EndTurnCommand, Debug*Command). Toute classe future (Necram, Ghostra) se rejouera
    /// automatiquement sans toucher au recorder.
    /// </summary>
    public class ReplayRecorder : MonoBehaviour
    {
        [Tooltip("Si vrai, log les transitions d'etat dans la console (debug).")]
        [SerializeField] private bool _verboseLogs = false;

        private QuantumGame _currentGame;
        private DateTime _gameStartedAtUtc;
        private string _sceneNameAtStart;
        private bool _classesCaptured;
        private NymoraClass _player0Class;
        private NymoraClass _player1Class;
        private int _maxRoundSeen;
        private bool _matchEndCaptured;
        private NymoraReplayFile _pendingReplay;
        private string _lastSavedPath;

        /// <summary>Vrai si un replay est en attente d'etre sauvegarde sur disque.</summary>
        public bool HasPendingReplay
        {
            get { return _pendingReplay != null && !string.IsNullOrEmpty(_pendingReplay.QuantumReplayJson); }
        }

        /// <summary>Dernier fichier sauvegarde (null tant qu'aucun save n'a eu lieu pour ce match).</summary>
        public string LastSavedPath { get { return _lastSavedPath; } }

        private void Awake()
        {
            QuantumCallback.Subscribe(this, (CallbackGameStarted c) => OnGameStarted(c.Game));
            QuantumCallback.Subscribe(this, (CallbackUpdateView c) => OnUpdateView(c.Game));
            QuantumCallback.Subscribe(this, (CallbackGameDestroyed c) => OnGameDestroyed(c.Game));
        }

        private void OnGameStarted(QuantumGame game)
        {
            _currentGame = game;
            _gameStartedAtUtc = DateTime.UtcNow;
            _sceneNameAtStart = SceneManager.GetActiveScene().name;
            _classesCaptured = false;
            _player0Class = NymoraClass.None;
            _player1Class = NymoraClass.None;
            _maxRoundSeen = 0;
            _matchEndCaptured = false;
            _pendingReplay = null;
            _lastSavedPath = null;

            try
            {
                game.StartRecordingInput();
                if (_verboseLogs) Debug.Log("[Nymora.Replay] Input recording demarre.", this);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Nymora.Replay] StartRecordingInput a leve : " + ex.Message, this);
            }
        }

        private void OnUpdateView(QuantumGame game)
        {
            if (game != _currentGame) return;
            var frame = game.Frames.Verified;
            if (frame == null) return;
            if (!frame.TryGetSingleton<CombatState>(out var state)) return;

            if (state.TurnNumber > _maxRoundSeen) _maxRoundSeen = state.TurnNumber;

            if (!_classesCaptured)
            {
                bool hasP0 = false, hasP1 = false;
                var filter = frame.Filter<Combatant>();
                while (filter.Next(out EntityRef _, out Combatant c))
                {
                    if (c.PlayerIndex == 0) { _player0Class = c.Class; hasP0 = true; }
                    if (c.PlayerIndex == 1) { _player1Class = c.Class; hasP1 = true; }
                }
                if (hasP0 && hasP1) _classesCaptured = true;
            }

            if (!_matchEndCaptured && state.CurrentPhase == CombatPhase.MatchEnd)
            {
                _matchEndCaptured = true;
                CaptureReplaySnapshot(game, state);
            }
        }

        private void OnGameDestroyed(QuantumGame game)
        {
            if (game != _currentGame) return;
            _currentGame = null;
            if (_verboseLogs && _pendingReplay != null && !string.IsNullOrEmpty(_lastSavedPath) == false)
            {
                Debug.Log("[Nymora.Replay] Match termine, replay non sauvegarde (perdu).", this);
            }
        }

        private void CaptureReplaySnapshot(QuantumGame game, CombatState state)
        {
            QuantumReplayFile quantumReplay;
            try
            {
                // includeChecksums=false : on n'a pas appele StartRecordingChecksums(), donc
                // RecordedChecksums est null et passer true ferait throw une NRE interne.
                // Les checksums ne sont pas requis pour rejouer (le determinisme garantit
                // l'identite). Ils servent uniquement a detecter une desync — on les
                // rebranchera en 3.E.2 si besoin via StartRecordingChecksums() en parallele.
                quantumReplay = game.GetRecordedReplay(includeChecksums: false);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Nymora.Replay] GetRecordedReplay a leve : " + ex.Message, this);
                return;
            }
            if (quantumReplay == null)
            {
                Debug.LogWarning("[Nymora.Replay] GetRecordedReplay a retourne null — replay indisponible.", this);
                return;
            }

            int duration = Mathf.Max(0, (int)(DateTime.UtcNow - _gameStartedAtUtc).TotalSeconds);

            var file = new NymoraReplayFile();
            file.Metadata.FormatVersion = 1;
            file.Metadata.CreatedAtUtc = DateTime.UtcNow.ToString("o");
            file.Metadata.DurationSeconds = duration;
            file.Metadata.TotalRounds = _maxRoundSeen;
            file.Metadata.BibleVersion = GameVersion.BibleVersion;
            file.Metadata.CombatRulesVersion = GameVersion.CombatRulesVersion;
            file.Metadata.Player0Class = _player0Class.ToString();
            file.Metadata.Player1Class = _player1Class.ToString();
            file.Metadata.WinnerPlayerIndex = state.WinnerPlayerIndex;
            file.Metadata.SceneName = _sceneNameAtStart;
            file.SetQuantumReplay(quantumReplay);

            _pendingReplay = file;
            if (_verboseLogs)
            {
                Debug.Log(string.Format(
                    "[Nymora.Replay] Replay capture : {0} vs {1}, {2}s, gagnant={3}.",
                    file.Metadata.Player0Class, file.Metadata.Player1Class,
                    file.Metadata.DurationSeconds, file.Metadata.WinnerPlayerIndex), this);
            }
        }

        /// <summary>
        /// Sauvegarde le replay en attente sur disque. Renvoie le chemin complet du
        /// fichier ecrit, ou null si aucun replay n'est en attente.
        /// </summary>
        public string SaveCurrentReplay()
        {
            if (!HasPendingReplay)
            {
                Debug.LogWarning("[Nymora.Replay] Aucun replay en attente — rien a sauvegarder.", this);
                return null;
            }

            ReplayPaths.EnsureFolderExists();
            string filename = ReplayPaths.GenerateFilename(
                _pendingReplay.Metadata.Player0Class,
                _pendingReplay.Metadata.Player1Class,
                DateTime.UtcNow);
            string fullPath = ReplayPaths.CombineWithRoot(filename);

            try
            {
                _pendingReplay.WriteToDisk(fullPath);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Nymora.Replay] Echec ecriture replay : " + ex.Message, this);
                return null;
            }

            _lastSavedPath = fullPath;
            Debug.Log("[Nymora.Replay] Replay sauvegarde -> " + fullPath, this);
            return fullPath;
        }
    }
}
