using Quantum;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Overlay UI de fin de match (2.16.c.ii). Affiche VICTOIRE / DEFAITE / MATCH NUL
    /// + bouton Rejouer. Polled par CombatHUDController via Refresh() chaque frame.
    ///
    /// Le root GameObject reste actif en permanence (sinon les bindings sont perdus
    /// au domain reload) ; on toggle juste le panel enfant qui contient les visuels.
    ///
    /// Restart = reload de la scene courante. Quantum reinit OnInit propre. Pas de
    /// state preserve entre les matchs (volontaire en 2.16.c : chaque test est un
    /// match independant).
    /// </summary>
    public class MatchEndOverlay : MonoBehaviour
    {
        [Header("Visuels")]
        [Tooltip("Panel root qui contient le background sombre + textes + bouton.")]
        [SerializeField] private GameObject _panel;

        [Tooltip("Titre central : VICTOIRE / DEFAITE / MATCH NUL.")]
        [SerializeField] private TMP_Text _titleText;

        [Tooltip("Sous-titre : info contextuelle (round, difficulte, etc.).")]
        [SerializeField] private TMP_Text _subtitleText;

        [Header("Bouton")]
        [SerializeField] private Button _restartButton;

        [Header("Couleurs titre")]
        [SerializeField] private Color _victoryColor = new Color(1.00f, 0.83f, 0.30f, 1f); // or
        [SerializeField] private Color _defeatColor = new Color(0.90f, 0.25f, 0.25f, 1f); // rouge
        [SerializeField] private Color _drawColor = new Color(0.75f, 0.75f, 0.75f, 1f); // gris

        private bool _shown;

        private void Awake()
        {
            if (_restartButton != null)
            {
                _restartButton.onClick.RemoveAllListeners();
                _restartButton.onClick.AddListener(OnRestartClicked);
            }
            Hide();
        }

        /// <summary>
        /// Appele par CombatHUDController chaque tick de view update. Si la sim est en
        /// MatchEnd, on affiche l'overlay avec le verdict. Sinon on le cache.
        /// </summary>
        public void Refresh(Quantum.CombatPhase phase, int winnerPlayerIndex, int localPlayerIndex, int turnNumber)
        {
            if (phase != Quantum.CombatPhase.MatchEnd)
            {
                if (_shown) Hide();
                return;
            }

            if (_shown) return; // deja affiche, no-op pour eviter de re-rafraichir chaque frame

            string title;
            Color titleColor;
            if (winnerPlayerIndex < 0)
            {
                title = "MATCH NUL";
                titleColor = _drawColor;
            }
            else if (winnerPlayerIndex == localPlayerIndex)
            {
                title = "VICTOIRE";
                titleColor = _victoryColor;
            }
            else
            {
                title = "DÉFAITE";
                titleColor = _defeatColor;
            }

            if (_titleText != null)
            {
                _titleText.text = title;
                _titleText.color = titleColor;
            }
            if (_subtitleText != null)
            {
                _subtitleText.text = $"Round {turnNumber}";
            }

            Show();
        }

        private void Show()
        {
            if (_panel != null) _panel.SetActive(true);
            _shown = true;
        }

        private void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
            _shown = false;
        }

        private void OnRestartClicked()
        {
            // Quantum installe DontDestroyOnLoad sur ses Singletons (QuantumMapLoader,
            // etc.) et garde le QuantumRunner actif a travers les scene loads. Sans
            // ShutdownAll() avant le reload, le nouveau scene ressort un runner mort
            // et la sim ne s'init pas. C'est le pattern officiel Photon (cf QuantumUnityEditor).
            Debug.Log("[Nymora.HUD] MatchEnd Rejouer cliquee — ShutdownAll + reload scene");
            QuantumRunner.ShutdownAll();
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.name);
        }
    }
}
