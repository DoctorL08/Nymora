using Nymora.Combat.Replay;
using Nymora.Core.Data;
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

        [Header("Retour Hub (4.14.g — PvP casual + Polish 18 mai — IA)")]
        [Tooltip("Click = LoadScene 10_CommunityHub (+ SetMatchResult uniquement en PvP). " +
                 "Visible en PvP ET en IA depuis polish 18 mai (avant : PvP only). " +
                 "En IA, pas de SetMatchResult — XP MVP differable a Phase 6 ranked.")]
        [SerializeField] private Button _returnToHubButton;

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
        // 4.14.g — Snapshot des params Refresh pour les recuperer dans OnReturnToHubClicked
        // (le bouton n'a pas acces a la frame Quantum a la callback).
        private int _localPlayerIndex;
        private int _winnerPlayerIndex;
        private bool _isPvpMatch;

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
            if (_returnToHubButton != null)
            {
                _returnToHubButton.onClick.RemoveAllListeners();
                _returnToHubButton.onClick.AddListener(OnReturnToHubClicked);
            }
            Hide();
        }

        /// <summary>
        /// Appele par CombatHUDController chaque tick de view update. Si la sim est en
        /// MatchEnd, on affiche l'overlay avec le verdict. Sinon on le cache.
        /// </summary>
        public void Refresh(Quantum.CombatPhase phase, int winnerPlayerIndex, int localPlayerIndex, int turnNumber, bool isPvpMatch = false)
        {
            if (phase != Quantum.CombatPhase.MatchEnd)
            {
                if (_shown) Hide();
                return;
            }

            if (_shown) return; // deja affiche, no-op pour eviter de re-rafraichir chaque frame

            // 4.14.g — Snapshot pour OnReturnToHubClicked.
            _localPlayerIndex = localPlayerIndex;
            _winnerPlayerIndex = winnerPlayerIndex;
            _isPvpMatch = isPvpMatch;

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
            // 3.E.polish : si on est en mode replay (rejeu d'un .nymrep), on cache les
            // boutons "Rejouer" (qui relanceraient un match normal en quittant le replay)
            // et "Sauvegarder le replay" (replay deja existant, recorder force-disabled).
            // Le user reste libre de quitter via le bouton du ReplayControlsPanel.
            // 4.14.g : en mode PvP, cache les boutons Restart IA (pas pertinent online)
            // et affiche le bouton Retour Hub a la place.
            // Polish 18 mai (decision user) : en mode IA aussi, on remplace Restart par
            // Retour Hub. Pour relancer un combat IA, Lorenzo repasse par hub > Arena
            // (laisse le choix de classe + difficulte au lieu de subir le choix actuel).
            // Les fields Restart Easy/Medium restent en place pour eventual rollback.
            bool replayMode = IsInReplayMode();
            bool restartVisible = false;
            bool returnHubVisible = !replayMode;
            if (_restartEasyButton != null) _restartEasyButton.gameObject.SetActive(restartVisible);
            if (_restartMediumButton != null) _restartMediumButton.gameObject.SetActive(restartVisible);
            if (_returnToHubButton != null) _returnToHubButton.gameObject.SetActive(returnHubVisible);
            RefreshSaveReplayButton();
        }

        private static bool IsInReplayMode()
        {
            var c = Object.FindAnyObjectByType<ReplayPlaybackController>();
            return c != null && c.enabled;
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

            // 3.E.polish : en mode replay, le ReplayRecorder est force-disabled donc
            // HasPendingReplay sera false. On hide le bouton entierement pour clarte.
            bool replayMode = IsInReplayMode();
            _saveReplayButton.gameObject.SetActive(!replayMode && _replayRecorder != null);
            // Bouton toujours interactable (sauf si deja sauvegarde) : si la capture a
            // echoue, OnSaveReplayClicked log un warning explicite plutot que de griser
            // silencieusement (debug-friendly).
            _saveReplayButton.interactable = !_replaySavedThisMatch;

            if (_saveReplayLabel != null)
            {
                _saveReplayLabel.text = _replaySavedThisMatch ? _saveReplaySavedText : _saveReplayDefaultText;
            }
        }

        private void OnSaveReplayClicked()
        {
            Debug.Log("[Nymora.HUD] Click Save Replay — recorder=" + (_replayRecorder != null) +
                      " pending=" + (_replayRecorder != null && _replayRecorder.HasPendingReplay));
            if (_replayRecorder == null)
            {
                Debug.LogWarning("[Nymora.HUD] ReplayRecorder ref non assignee dans le MatchEndOverlay Inspector.");
                return;
            }
            if (!_replayRecorder.HasPendingReplay)
            {
                Debug.LogWarning("[Nymora.HUD] Aucun replay en attente — la capture Quantum a peut-etre echoue. " +
                                 "Verifie les logs [Nymora.Replay] precedents.");
                return;
            }
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

        /// <summary>
        /// 4.14.g — Bouton Retour Hub. En PvP : set le resultat dans MatchBridge.LastMatchResult
        /// (consume cote hub par HubMatchResultDisplay au Start = ligne chat + award XP MVP).
        /// En IA (polish 18 mai) : pas de SetMatchResult — XP MVP IA differable a Phase 6 ranked
        /// (cf project-xp-source-ranked-only). Toujours : shutdown Quantum + LoadScene hub.
        ///
        /// Win/Loss/Draw deduit du snapshot Refresh : winner == local -> VICTOIRE,
        /// winner != local && winner >= 0 -> DEFAITE, winner < 0 -> DRAW (double KO).
        /// </summary>
        private void OnReturnToHubClicked()
        {
            if (_isPvpMatch)
            {
                MatchResult result;
                if (_winnerPlayerIndex < 0) result = MatchResult.Draw;
                else if (_winnerPlayerIndex == _localPlayerIndex) result = MatchResult.Victory;
                else result = MatchResult.Defeat;

                // Capture matchId + opponentEmail AVANT que SetMatchResult clear pending.
                string matchId = MatchBridge.PendingMatchId;
                string opponentEmail = MatchBridge.OpponentEmail;
                MatchBridge.SetMatchResult(result, matchId, opponentEmail);
                Debug.Log($"[Nymora.HUD] Retour Hub clique (PvP) — result={result} matchId={matchId} opponent={opponentEmail}");
            }
            else
            {
                Debug.Log("[Nymora.HUD] Retour Hub clique (IA) — pas de SetMatchResult, pas d'XP MVP");
            }

            QuantumRunner.ShutdownAll();
            SceneManager.LoadScene("10_CommunityHub");
        }
    }
}
