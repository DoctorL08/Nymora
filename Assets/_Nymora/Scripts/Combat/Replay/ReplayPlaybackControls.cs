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
    ///   - Label "Tick X / Y"
    ///   - Label error (visible uniquement si erreur de chargement)
    /// </summary>
    public class ReplayPlaybackControls : MonoBehaviour
    {
        [Tooltip("Reference au controller. Auto-resolu si laisse vide (FindObjectByType).")]
        [SerializeField] private ReplayPlaybackController _controller;

        [Header("Boutons")]
        [SerializeField] private Button _playPauseButton;
        [SerializeField] private TMP_Text _playPauseLabel;
        [SerializeField] private Button _stepButton;
        [SerializeField] private Button _speedButton;
        [SerializeField] private TMP_Text _speedLabel;

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
        }

        private void Update()
        {
            if (_controller == null) return;

            bool hasErr = !string.IsNullOrEmpty(_controller.ErrorMessage);

            if (_errorLabel != null)
            {
                _errorLabel.gameObject.SetActive(hasErr);
                if (hasErr) _errorLabel.text = _controller.ErrorMessage;
            }

            if (hasErr)
            {
                // Quand erreur, on grise les controles pour eviter clics inutiles.
                SetInteractable(false);
                return;
            }
            SetInteractable(true);

            if (_tickLabel != null)
            {
                _tickLabel.text = string.Format("Tick {0} / {1}", _controller.CurrentTick, _controller.LastTick);
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

        private void SetInteractable(bool on)
        {
            if (_playPauseButton != null) _playPauseButton.interactable = on;
            if (_stepButton != null) _stepButton.interactable = on;
            if (_speedButton != null) _speedButton.interactable = on;
        }

        private void OnPlayPauseClicked() { if (_controller != null) _controller.TogglePause(); }
        private void OnStepClicked() { if (_controller != null) _controller.Step(); }
        private void OnSpeedClicked() { if (_controller != null) _controller.CycleSpeed(); }
    }
}
