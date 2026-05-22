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
        // 6.2.b — scene chargee pour un match CLASSE (ranked 1v1).
        [SerializeField] private string _rankedSceneName = "40_CombatRanked1v1";
        [SerializeField, Range(0f, 10f)] private float _transitionDelaySeconds = 2f;
        [SerializeField] private bool _logVerbose = true;

        private bool _transitionInProgress;

        private void Start()
        {
            if (HubChatClient.Instance != null)
            {
                HubChatClient.Instance.OnMatchReady += HandleMatchReady;
                HubChatClient.Instance.OnRankedMatchFound += HandleRankedMatchFound;
            }
        }

        private void OnDestroy()
        {
            if (HubChatClient.Instance != null)
            {
                HubChatClient.Instance.OnMatchReady -= HandleMatchReady;
                HubChatClient.Instance.OnRankedMatchFound -= HandleRankedMatchFound;
            }
        }

        // Defi casual (4.8.d.i) -> scene casual, non classe.
        private void HandleMatchReady(string matchId, string opponentSub, string opponentDisplayName)
            => BeginMatchTransition(matchId, opponentSub, opponentDisplayName, ranked: false, _combatSceneName);

        // Matchmaking ranked (6.2) -> scene ranked, classe (impacte le MMR en 6.3).
        private void HandleRankedMatchFound(string matchId, string opponentSub, string opponentDisplayName)
            => BeginMatchTransition(matchId, opponentSub, opponentDisplayName, ranked: true, _rankedSceneName);

        private async void BeginMatchTransition(string matchId, string opponentSub, string opponentDisplayName,
                                                bool ranked, string sceneName)
        {
            if (_transitionInProgress)
            {
                if (_logVerbose) Debug.LogWarning($"[HubMatchTransition] Transition déjà en cours, match ignoré (matchId={matchId}, ranked={ranked})");
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
            // Utilise le deck SELECTIONNE par l'utilisateur (fix 18 mai). Fallback MyDecks[0]
            // gere dans SelectedDeck si aucun deck cliqu.
            var deck = dbp.SelectedDeck;
            if (deck == null)
            {
                Debug.LogError($"[HubMatchTransition] SelectedDeck null malgre MyDecks.Count>0 — match {matchId} ANNULE.");
                _transitionInProgress = false;
                return;
            }
            DeckBridge.SetPendingDeck(deck.classId, deck.spellIds, deck.name);

            // 4.14.e — Set MatchBridge avec LOCAL identity (pour CombatBootstrapCasual)
            // + opponent (deja transmis par MATCH_READY backend, sert au retour hub).
            // POLISH-7 (20 mai) : on push aussi les displayName local + opponent pour les
            // affichages combat (tooltip Combatant) + retour hub (system line VICTOIRE vs X).
            var chat = HubChatClient.Instance;
            string localSub = chat?.MyUserId;
            string localEmail = chat?.MyEmail;
            string localDisplayName = chat?.MyDisplayName;
            // opponentEmail n'est plus push directement par OnMatchReady (POLISH-7) -> on
            // garde une chaine vide cote MatchBridge.OpponentEmail (pas besoin pour gameplay,
            // l'identite reseau Photon utilise sub uniquement).
            MatchBridge.SetPendingMatch(matchId, opponentSub, "", localSub, localEmail,
                                         opponentDisplayName, localDisplayName);
            // 6.2.b — marque le match comme classe (ranked) ou non. Lu en 6.3 (MMR).
            MatchBridge.SetRanked(ranked);

            // 19 mai — Push pseudo/clan pour le tooltip combat. POLISH-7 : displayName officiel
            // au lieu d'extract email. Clan opponent inconnu cote client (pas de cache sub->clan
            // en PvP) -> vide pour MVP.
            string localClan = (HubClanPanel.Instance != null && HubClanPanel.Instance.HasClan)
                ? HubClanPanel.Instance.MyClanName : "";
            PlayerProfileBridge.SetLocal(localDisplayName, localClan);
            PlayerProfileBridge.SetOpponent(opponentDisplayName, "");

            if (_logVerbose) Debug.Log($"[HubMatchTransition] MatchBridge set matchId={matchId} ranked={ranked} opponent='{opponentDisplayName}' " +
                                       $"local='{localDisplayName}' deck={deck.classId}/'{deck.name}'. Transition vers '{sceneName}' dans {_transitionDelaySeconds}s.");

            await TransitionAsync(sceneName);
        }

        private async Task TransitionAsync(string sceneName)
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

            if (_logVerbose) Debug.Log($"[HubMatchTransition] LoadScene → {sceneName}");
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
    }
}
