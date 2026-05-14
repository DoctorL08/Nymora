using Nymora.Combat.Replay;
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

        [Header("Boutons Rejouer (2.16.c.iv)")]
        [Tooltip("Click = AIConstants.CurrentDifficulty=Easy + reload scene.")]
        [SerializeField] private Button _restartEasyButton;
        [Tooltip("Click = AIConstants.CurrentDifficulty=Medium + reload scene.")]
        [SerializeField] private Button _restartMediumButton;

        [Header("Replay (Brique 3.E.1)")]
        [Tooltip("ReplayRecorder de la scene. Si laisse vide, le bouton 'Sauvegarder le replay' est masque.")]
        [SerializeField] private ReplayRecorder _replayRecorder;
        [Tooltip("Bouton qui ecrit le replay courant sur disque (Application.persistentDataPath/Replays/).")]
        [SerializeField] private Button _saveReplayButton;
        [Tooltip("Label TMP du bouton — change en 'Replay sauvegarde !' apres click.")]
        [SerializeField] private TMP_Text _saveReplayLabel;
        [SerializeField] private string _saveReplayDefaultText = "Sauvegarder le replay";
        [SerializeField] private string _saveReplaySavedText = "Replay sauvegarde !";

        [Header("Couleurs titre")]
        [SerializeField] private Color _victoryColor = new Color(1.00f, 0.83f, 0.30f, 1f); // or
        [SerializeField] private Color _defeatColor = new Color(0.90f, 0.25f, 0.25f, 1f); // rouge
        [SerializeField] private Color _drawColor = new Color(0.75f, 0.75f, 0.75f, 1f); // gris

        private bool _shown;
        private bool _replaySavedThisMatch;

        private void Awake()
        {
            if (_restartEasyButton != null)
            {
                _restartEasyButton.onClick.RemoveAllListeners();
                _restartEasyButton.onClick.AddListener(() => OnRestartClicked(AIDifficulty.Easy));
            }
            if (_restartMediumButton != null)
            {
                _restartMediumButton.onClick.RemoveAllListeners();
                _restartMediumButton.onClick.AddListener(() => OnRestartClicked(AIDifficulty.Medium));
            }
            if (_saveReplayButton != null)
            {
                _saveReplayButton.onClick.RemoveAllListeners();
                _saveReplayButton.onClick.AddListener(OnSaveReplayClicked);
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
            RefreshSaveReplayButton();
        }

        private void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
            _shown = false;
            _replaySavedThisMatch = false;
        }

        private void RefreshSaveReplayButton()
        {
            if (_saveReplayButton == null) return;

            bool canSave = _replayRecorder != null && _replayRecorder.HasPendingReplay && !_replaySavedThisMatch;
            _saveReplayButton.gameObject.SetActive(_replayRecorder != null);
            _saveReplayButton.interactable = canSave;

            if (_saveReplayLabel != null)
            {
                _saveReplayLabel.text = _replaySavedThisMatch ? _saveReplaySavedText : _saveReplayDefaultText;
            }
        }

        private void OnSaveReplayClicked()
        {
            if (_replayRecorder == null) return;
            string path = _replayRecorder.SaveCurrentReplay();
            if (!string.IsNullOrEmpty(path))
            {
                _replaySavedThisMatch = true;
                RefreshSaveReplayButton();
            }
        }

        private void OnRestartClicked(AIDifficulty difficulty)
        {
            // 2.16.c.iv — set la difficulte avant le reload. Le static field
            // AIConstants.CurrentDifficulty survit aux scene loads (meme domain Unity),
            // donc la nouvelle sim sera init avec la bonne valeur.
            AIConstants.CurrentDifficulty = difficulty;

            // Quantum installe DontDestroyOnLoad sur ses Singletons (QuantumMapLoader,
            // etc.) et garde le QuantumRunner actif a travers les scene loads. Sans
            // ShutdownAll() avant le reload, le nouveau scene ressort un runner mort
            // et la sim ne s'init pas. C'est le pattern officiel Photon (cf QuantumUnityEditor).
            Debug.Log($"[Nymora.HUD] MatchEnd Rejouer ({difficulty}) cliquee — ShutdownAll + reload scene");
            QuantumRunner.ShutdownAll();
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.name);
        }
    }
}
