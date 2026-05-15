using System.Collections;
using UnityEngine;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 4.9 (stub) — Au Start de la scène hub, consume MatchBridge.LastMatchResult
    /// si présent et l'affiche comme system line dans le chat. Permet de boucler l'expérience
    /// défi → match → retour hub avec feedback visible.
    ///
    /// Le HubChatUI doit être déjà initialisé pour qu'on puisse appeler son AppendSystemLine.
    /// On utilise une coroutine WaitForEndOfFrame pour s'assurer que tous les composants sont
    /// prêts (HubChatClient connecté n'est PAS requis, on injecte direct dans HubChatUI).
    /// </summary>
    public sealed class HubMatchResultDisplay : MonoBehaviour
    {
        [SerializeField] private HubChatUI _chatUI;

        private void Start()
        {
            if (!MatchBridge.HasPendingResult) return;
            StartCoroutine(DisplayAfterEndOfFrame());
        }

        private IEnumerator DisplayAfterEndOfFrame()
        {
            yield return new WaitForEndOfFrame();

            var result = MatchBridge.ConsumeLastResult(out var matchId, out var opponentEmail);
            string color;
            string label;
            switch (result)
            {
                case MatchResult.Victory:
                    color = "#88ff88";
                    label = "VICTOIRE";
                    break;
                case MatchResult.Defeat:
                    color = "#ff8888";
                    label = "DÉFAITE";
                    break;
                case MatchResult.Draw:
                    color = "#cccccc";
                    label = "ÉGALITÉ";
                    break;
                default:
                    yield break;
            }

            var line = $"<color={color}>[MATCH] {label} vs {opponentEmail} (id={SafeShortId(matchId)})</color>";
            Debug.Log($"[HubMatchResultDisplay] {line}");

            if (_chatUI != null)
            {
                _chatUI.AppendSystemLineExternal(line);
            }
        }

        private static string SafeShortId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "?";
            return id.Length >= 8 ? id.Substring(0, 8) : id;
        }
    }
}
