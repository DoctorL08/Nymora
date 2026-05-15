using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 4.8.d.ii — Listen HubChatClient.OnMatchReady → store dans MatchBridge
    /// + transition vers la scène combat après delay (laisse le user voir la system line
    /// [MATCH] avant le fondu de scène).
    ///
    /// 4.8.d.ii.fix — Shutdown propre du NetworkRunner Fusion avant LoadScene pour éviter
    /// les ticks résiduels qui tentent de spawner des avatars dans la scène combat
    /// (warning `HubGridRenderer introuvable`).
    ///
    /// 4.8.d.iii enrichira la scène cible avec le setup Quantum 2 joueurs réels.
    /// </summary>
    public sealed class HubMatchTransition : MonoBehaviour
    {
        [SerializeField] private string _combatSceneName = "33_CombatCasual";
        [SerializeField, Range(0f, 10f)] private float _transitionDelaySeconds = 2f;
        [SerializeField] private bool _logVerbose = true;

        private bool _transitionInProgress;

        private void Start()
        {
            if (HubChatClient.Instance != null)
            {
                HubChatClient.Instance.OnMatchReady += HandleMatchReady;
            }
        }

        private void OnDestroy()
        {
            if (HubChatClient.Instance != null)
            {
                HubChatClient.Instance.OnMatchReady -= HandleMatchReady;
            }
        }

        private async void HandleMatchReady(string matchId, string opponentSub, string opponentEmail)
        {
            if (_transitionInProgress)
            {
                if (_logVerbose) Debug.LogWarning($"[HubMatchTransition] Transition déjà en cours, MATCH_READY ignoré (matchId={matchId})");
                return;
            }
            _transitionInProgress = true;
            MatchBridge.SetPendingMatch(matchId, opponentSub, opponentEmail);
            if (_logVerbose) Debug.Log($"[HubMatchTransition] MatchBridge set matchId={matchId} opponent={opponentEmail}. Transition vers '{_combatSceneName}' dans {_transitionDelaySeconds}s.");

            await TransitionAsync();
        }

        private async Task TransitionAsync()
        {
            await Task.Delay(Mathf.RoundToInt(_transitionDelaySeconds * 1000));

            var runner = FindFirstObjectByType<NetworkRunner>();
            if (runner != null && runner.IsRunning)
            {
                if (_logVerbose) Debug.Log("[HubMatchTransition] Shutdown NetworkRunner Fusion avant LoadScene");
                try
                {
                    await runner.Shutdown();
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[HubMatchTransition] Shutdown a throw : {ex.Message} — on continue le LoadScene quand même.");
                }
            }

            if (_logVerbose) Debug.Log($"[HubMatchTransition] LoadScene → {_combatSceneName}");
            SceneManager.LoadScene(_combatSceneName, LoadSceneMode.Single);
        }
    }
}
