using Nymora.Core.Data;
using Nymora.Core.SceneFlow;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 4.8.d.ii — Composant attaché dans la scène stub 33_CombatCasual.
    /// Brique 4.8.d.iii (stub) + 4.9 — 3 boutons Victoire/Défaite/Égalité qui :
    ///   - set MatchBridge.LastMatchResult
    ///   - LoadScene("10_CommunityHub") (retour hub)
    ///
    /// Sera remplacé par le vrai bootstrap Quantum 2 joueurs en 4.8.d.iii final.
    /// </summary>
    public sealed class MatchTestLogger : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _displayText;
        [SerializeField] private Button _victoryButton;
        [SerializeField] private Button _defeatButton;
        [SerializeField] private Button _drawButton;
        [SerializeField] private string _hubSceneName = "10_CommunityHub";

        private string _matchId;
        private string _opponentEmail;

        private void OnEnable()
        {
            if (_victoryButton != null) _victoryButton.onClick.AddListener(OnVictoryClicked);
            if (_defeatButton != null) _defeatButton.onClick.AddListener(OnDefeatClicked);
            if (_drawButton != null) _drawButton.onClick.AddListener(OnDrawClicked);
        }

        private void OnDisable()
        {
            if (_victoryButton != null) _victoryButton.onClick.RemoveListener(OnVictoryClicked);
            if (_defeatButton != null) _defeatButton.onClick.RemoveListener(OnDefeatClicked);
            if (_drawButton != null) _drawButton.onClick.RemoveListener(OnDrawClicked);
        }

        private void Start()
        {
            if (!MatchBridge.HasPendingMatch)
            {
                const string msg = "[MatchTestLogger] Aucun match en attente dans MatchBridge — scène chargée directement ?";
                Debug.LogWarning(msg);
                if (_displayText != null) _displayText.text = "Aucun match en attente.\nScène chargée directement ?";
                return;
            }

            _matchId = MatchBridge.PendingMatchId;
            _opponentEmail = MatchBridge.OpponentEmail;

            var line1 = $"matchId = {_matchId}";
            var line2 = $"opponent sub = {MatchBridge.OpponentSub}";
            var line3 = $"opponent email = {_opponentEmail}";
            Debug.Log($"[MatchTestLogger] {line1}\n{line2}\n{line3}");
            if (_displayText != null)
            {
                _displayText.text = $"<b>33_CombatCasual (stub 4.8.d.iii)</b>\n\n{line1}\n{line2}\n{line3}\n\nClique sur un résultat pour retourner au hub.\n\n<size=80%>Le vrai combat Quantum 2 joueurs remplacera ce stub.</size>";
            }
        }

        private void OnVictoryClicked() => EndMatchWith(MatchResult.Victory);
        private void OnDefeatClicked() => EndMatchWith(MatchResult.Defeat);
        private void OnDrawClicked() => EndMatchWith(MatchResult.Draw);

        private void EndMatchWith(MatchResult result)
        {
            Debug.Log($"[MatchTestLogger] Fin de match : {result} (matchId={_matchId}). LoadScene → {_hubSceneName}");
            MatchBridge.SetMatchResult(result, _matchId, _opponentEmail);
            SceneTransition.Load(_hubSceneName);
        }
    }
}
