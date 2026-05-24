using Nymora.Core.SceneFlow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;

namespace Nymora.Combat.Replay
{
    /// <summary>
    /// Brique 3.E.2 — Overlay UI pour piloter la lecture du replay. Bindings :
    ///   - Play/Pause (toggle)
    ///   - Step (+1 tick puis pause)
    ///   - Speed (cycle 0.5× / 1× / 2× / 4×)
    ///   - Restart (3.E.2.b) : rewind a tick 0 puis pause
    ///   - Seek (3.E.2.b) : InputField tick cible + bouton Go
    ///   - Quit (3.E.polish) : clear flag replay et reload scene
    ///   - Label "Tick X / Y", "Seeking..." pendant un seek
    ///   - Label error / desync warning
    /// </summary>
    public class ReplayPlaybackControls : MonoBehaviour
    {
        [Tooltip("Reference au controller. Auto-resolu si laisse vide (FindObjectByType).")]
        [SerializeField] private ReplayPlaybackController _controller;

        [Header("Boutons playback")]
        [SerializeField] private Button _playPauseButton;
        [SerializeField] private TMP_Text _playPauseLabel;
        [SerializeField] private Button _stepButton;
        [SerializeField] private Button _speedButton;
        [SerializeField] private TMP_Text _speedLabel;

        [Header("Seek (3.E.2.b)")]
        [SerializeField] private Button _restartButton;
        [SerializeField] private TMP_InputField _seekInput;
        [SerializeField] private Button _seekButton;

        [Header("Exit (3.E.polish)")]
        [SerializeField] private Button _quitButton;

        [Header("Labels info")]
        [SerializeField] private TMP_Text _tickLabel;
        [SerializeField] private TMP_Text _errorLabel;
        [SerializeField] private TMP_Text _matchInfoLabel;

        private void Awake()
        {
            if (_controller == null) _controller = FindAnyObjectByType<ReplayPlaybackController>();

            // Single-scene mode : si pas de replay actif, on hide le panel pour ne
            // pas polluer l'UI du match normal. Le controller a deja self-disable
            // dans son Awake (DefaultExecutionOrder=-1000 garantit l'ordre).
            if (_controller == null || !_controller.enabled)
            {
                gameObject.SetActive(false);
                return;
            }

            if (_playPauseButton != null) _playPauseButton.onClick.AddListener(OnPlayPauseClicked);
            if (_stepButton != null) _stepButton.onClick.AddListener(OnStepClicked);
            if (_speedButton != null) _speedButton.onClick.AddListener(OnSpeedClicked);
            if (_restartButton != null) _restartButton.onClick.AddListener(OnRestartClicked);
            if (_seekButton != null) _seekButton.onClick.AddListener(OnSeekClicked);
            if (_quitButton != null) _quitButton.onClick.AddListener(OnQuitClicked);
        }

        private void Update()
        {
            if (_controller == null) return;

            bool hasErr = !string.IsNullOrEmpty(_controller.ErrorMessage);
            bool hasDesync = _controller.HasDesync;
            bool seeking = _controller.IsSeeking;

            if (_errorLabel != null)
            {
                _errorLabel.gameObject.SetActive(hasErr || hasDesync);
                if (hasErr) _errorLabel.text = _controller.ErrorMessage;
                else if (hasDesync) _errorLabel.text = string.Format(
                    "DESYNC au frame {0} — replay diverge de la simulation courante.",
                    _controller.DesyncFrameNumber);
            }

            if (hasErr)
            {
                // Erreur fatale = tout grise sauf Quit (pour pouvoir sortir).
                SetPlaybackInteractable(false);
                SetSeekInteractable(false);
                if (_quitButton != null) _quitButton.interactable = true;
                return;
            }

            // Pendant un seek : grise tout sauf Quit.
            bool playbackOk = !seeking;
            SetPlaybackInteractable(playbackOk);
            SetSeekInteractable(playbackOk);
            if (_quitButton != null) _quitButton.interactable = true;

            if (_tickLabel != null)
            {
                if (seeking)
                {
                    _tickLabel.text = "Seeking...";
                }
                else
                {
                    _tickLabel.text = string.Format("Tick {0} / {1}", _controller.CurrentTick, _controller.LastTick);
                }
            }

            if (_playPauseLabel != null)
            {
                if (_controller.IsReplayFinished) _playPauseLabel.text = "Fin";
                else _playPauseLabel.text = _controller.IsPaused ? "Play" : "Pause";
            }

            if (_speedLabel != null)
            {
                _speedLabel.text = string.Format("{0}×", _controller.Speed);
            }

            if (_matchInfoLabel != null)
            {
                var m = _controller.Metadata;
                if (m != null)
                {
                    _matchInfoLabel.text = string.Format("{0} vs {1} · {2} round(s)",
                        m.Player0Class, m.Player1Class, m.TotalRounds);
                }
                else _matchInfoLabel.text = "";
            }
        }

        private void SetPlaybackInteractable(bool on)
        {
            if (_playPauseButton != null) _playPauseButton.interactable = on;
            if (_stepButton != null) _stepButton.interactable = on;
            if (_speedButton != null) _speedButton.interactable = on;
        }

        private void SetSeekInteractable(bool on)
        {
            if (_restartButton != null) _restartButton.interactable = on;
            if (_seekButton != null) _seekButton.interactable = on;
            if (_seekInput != null) _seekInput.interactable = on;
        }

        private void OnPlayPauseClicked() { if (_controller != null) _controller.TogglePause(); }
        private void OnStepClicked() { if (_controller != null) _controller.Step(); }
        private void OnSpeedClicked() { if (_controller != null) _controller.CycleSpeed(); }
        private void OnRestartClicked() { if (_controller != null) _controller.Restart(); }

        private void OnSeekClicked()
        {
            if (_controller == null || _seekInput == null) return;
            if (int.TryParse(_seekInput.text, out int target))
            {
                _controller.SeekTo(target);
            }
        }

        private void OnQuitClicked()
        {
            // 3.E.polish : sortir du mode replay = clear le flag et reload la scene
            // courante. Le ReplayPlaybackController detectera Consume() = null et
            // self-disable -> retour au mode match normal.
            ReplayPlaybackBridge.RequestedReplayPath = null;
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            SceneTransition.Load(active.name, () =>
            {
                try { Quantum.QuantumRunner.ShutdownAll(); }
                catch { }
            });
        }
    }
}
