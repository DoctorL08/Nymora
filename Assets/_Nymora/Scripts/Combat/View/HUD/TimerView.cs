using TMPro;
using UnityEngine;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Affichage du timer de tour. Couleur vire au rouge en dessous de 5s pour signaler
    /// l'urgence au joueur actif.
    /// </summary>
    public class TimerView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;
        [SerializeField] private TMP_Text _turnLabel;

        private static readonly Color Safe    = new Color(1.00f, 0.95f, 0.55f, 1f);
        private static readonly Color Warning = new Color(0.95f, 0.40f, 0.25f, 1f);

        // A2 — SFX d'urgence : joué une seule fois quand on passe sous 5s, ré-armé à chaque tour.
        private const float WarningThreshold = 5f;
        private bool _warned;
        private int _lastTurnNumber = -1;

        public void Refresh(float secondsRemaining, int turnNumber)
        {
            if (_label != null)
            {
                _label.text = $"{secondsRemaining:0.0}s";
                _label.color = secondsRemaining < WarningThreshold ? Warning : Safe;
            }
            if (_turnLabel != null)
            {
                _turnLabel.text = $"Tour {turnNumber}";
            }

            // Ré-arme l'alerte à chaque nouveau tour.
            if (turnNumber != _lastTurnNumber)
            {
                _lastTurnNumber = turnNumber;
                _warned = false;
            }
            if (!_warned && secondsRemaining > 0.05f && secondsRemaining < WarningThreshold)
            {
                _warned = true;
                Nymora.Core.Audio.NymoraAudioManager.Instance?.PlaySfx(Nymora.Core.Audio.SoundId.TimerWarning);
            }
        }
    }
}
