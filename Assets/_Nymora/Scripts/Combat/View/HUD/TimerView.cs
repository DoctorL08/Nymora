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

        // Patch 7 juin (Lorenzo) — réduire taille + police du timer. Échelle appliquée au conteneur
        //   du timer (dédié : Image-cadre + les 2 labels étirés) -> shrink uniforme (taille ET police)
        //   sur les 3 scènes combat depuis un seul réglage. Ajustable dans l'Inspector.
        [SerializeField, Range(0.3f, 1f)] private float _uiScale = 0.62f;
        // Taille du CADRE (la boîte sombre) : on resserre la boîte (était 320x130) pour réduire le vide.
        //   Les 2 labels sont ancrés en stretch -> ils suivent. 0,0 = ne pas toucher.
        [SerializeField] private Vector2 _frameSize = new Vector2(210f, 92f);
        // Gap : bande réservée EN HAUT pour le label "Tour N" (sinon les chiffres du timer, étirés
        //   plein cadre, le chevauchent). On enfonce le haut du label des chiffres de ce nombre de px.
        [SerializeField] private float _digitsTopInset = 38f;

        private void Awake()
        {
            float s = _uiScale > 0.01f ? _uiScale : 0.62f;
            transform.localScale = new Vector3(s, s, 1f);

            if (_frameSize.x > 1f && _frameSize.y > 1f && transform is RectTransform rt)
            {
                rt.sizeDelta = _frameSize;
            }

            // Réserve l'espace du "Tour N" en haut -> gap, plus de chevauchement.
            if (_label != null && _digitsTopInset > 0f)
            {
                var lrt = _label.rectTransform;
                lrt.offsetMax = new Vector2(lrt.offsetMax.x, -_digitsTopInset);
            }
        }

        // Re-skin DA hub : temps normal = texte clair monochrome ; urgence < 5s = rouge
        // fonctionnel conservé (signal d'urgence, pas décoratif).
        private static readonly Color Safe    = CombatUiKit.TextPrimary;
        private static readonly Color Warning = new Color(0.86f, 0.36f, 0.33f, 1f);

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
