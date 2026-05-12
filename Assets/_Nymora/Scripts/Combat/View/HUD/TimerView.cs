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

        public void Refresh(float secondsRemaining, int turnNumber)
        {
            if (_label != null)
            {
                _label.text = $"{secondsRemaining:0.0}s";
                _label.color = secondsRemaining < 5f ? Warning : Safe;
            }
            if (_turnLabel != null)
            {
                _turnLabel.text = $"Tour {turnNumber}";
            }
        }
    }
}
