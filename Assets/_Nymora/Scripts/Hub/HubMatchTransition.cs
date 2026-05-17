using System.Threading.Tasks;
using Fusion;
using Nymora.Core.Data;
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

            // 4.14.e — Verifie deck equipe avant LoadScene. Sans deck, CombatBootstrapCasual
            // spawnerait avec ClassId fallback (Soulrender) + deck 6 sorts = 0 → injouable.
            var dbp = HubDeckBuilderPanel.Instance;
            if (dbp == null)
            {
                Debug.LogError($"[HubMatchTransition] HubDeckBuilderPanel.Instance null — match {matchId} ANNULE.");
                _transitionInProgress = false;
                return;
            }

            // 4.14.e hotfix — Force le DeckBuilder a sync sur la CLASSE SELECTIONNEE
            // (SelectedClassPreferences = classe choisie via Class Selector, visible sur HubAvatar)
            // avant de lire MyDecks. Sans ca, MyDecks[0] pioche le 1er deck de la classe ouverte
            // dans le DeckBuilder (Soulrender default), pas la classe que Lorenzo veut jouer.
            string selectedClass = SelectedClassPreferences.Get();
            if (string.IsNullOrEmpty(selectedClass)) selectedClass = "Soulrender"; // safety net
            try
            {
                await dbp.EnsureClassLoadedAsync(selectedClass);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[HubMatchTransition] EnsureClassLoadedAsync({selectedClass}) echec : {ex.Message} — match {matchId} ANNULE.");
                _transitionInProgress = false;
                return;
            }

            if (dbp.MyDecks == null || dbp.MyDecks.Count == 0)
            {
                Debug.LogError($"[HubMatchTransition] Aucun deck '{selectedClass}' equipe — match {matchId} ANNULE cote local. " +
                               $"Ouvre le Deck Builder et cree au moins 1 deck pour la classe '{selectedClass}'.");
                _transitionInProgress = false;
                // TODO (4.14.f.bis) : POST /matches/{matchId}/cancel au backend pour notifier
                // l'opponent que ce client est out (sinon il timeout 30s sur Photon room).
                return;
            }
            var deck = dbp.MyDecks[0];
            DeckBridge.SetPendingDeck(deck.classId, deck.spellIds, deck.name);

            // 4.14.e — Set MatchBridge avec LOCAL identity (pour CombatBootstrapCasual)
            // + opponent (deja transmis par MATCH_READY backend, sert au retour hub).
            var chat = HubChatClient.Instance;
            string localSub = chat?.MyUserId;
            string localEmail = chat?.MyEmail;
            MatchBridge.SetPendingMatch(matchId, opponentSub, opponentEmail, localSub, localEmail);

            if (_logVerbose) Debug.Log($"[HubMatchTransition] MatchBridge set matchId={matchId} opponent={opponentEmail} " +
                                       $"local={localEmail} deck={deck.classId}/'{deck.name}'. Transition vers '{_combatSceneName}' dans {_transitionDelaySeconds}s.");

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
