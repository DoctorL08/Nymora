using System.Threading.Tasks;
using Fusion;
using Nymora.Core.Data;
using Nymora.Core.SceneFlow;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 5.3.f.bis — Panel Arene : choisis le mode de combat.
    ///
    /// 4 modes :
    ///   - Entrainement (vs IA) — actif, charge la scene 30_CombatIA
    ///   - Ranked 1v1 — ACTIF (Phase 6.1) : ouvre le panneau "Recherche de partie classee"
    ///   - Ranked 2v2 — desactive (reporte)
    ///   - Ranked 3v3 — desactive (reporte)
    ///
    /// Transition vers 30_CombatIA : shutdown propre du NetworkRunner Fusion (cf
    /// HubMatchTransition pattern 4.8.d.ii.fix) puis SceneManager.LoadScene.
    /// </summary>
    public sealed class HubArenaPanel : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private Button _closeButton;

        [Header("Boutons modes")]
        [SerializeField] private Button _trainingButton;
        [SerializeField] private Button _ranked1v1Button;
        [SerializeField] private Button _ranked2v2Button;
        [SerializeField] private Button _ranked3v3Button;

        [Header("Scenes")]
        [SerializeField] private string _trainingSceneName = "30_CombatIA";

        public static HubArenaPanel Instance { get; private set; }

        private bool _transitionInProgress;

        public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            if (_panelRoot != null) _panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (_closeButton != null) _closeButton.onClick.AddListener(Close);
            if (_trainingButton != null) _trainingButton.onClick.AddListener(OnTrainingClicked);
            // 6.1 — Ranked 1v1 actif : ouvre le panneau de recherche classee.
            if (_ranked1v1Button != null) _ranked1v1Button.onClick.AddListener(OnRanked1v1Clicked);
            // 2v2 / 3v3 restent desactives (.interactable=false dans le tool) ; pas de listener.
        }

        private void OnDisable()
        {
            if (_closeButton != null) _closeButton.onClick.RemoveAllListeners();
            if (_trainingButton != null) _trainingButton.onClick.RemoveAllListeners();
            if (_ranked1v1Button != null) _ranked1v1Button.onClick.RemoveAllListeners();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Open()
        {
            if (_panelRoot != null) _panelRoot.SetActive(true);
        }

        public void Close()
        {
            UiPanelAnimator.CloseAnimated(_panelRoot);
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        // 6.1 — Ouvre le panneau "Recherche de partie classee" (matchmaking reel en 6.2).
        private void OnRanked1v1Clicked()
        {
            if (_transitionInProgress) return;
            var search = HubRankedSearchPanel.Instance;
            if (search == null)
            {
                Debug.LogWarning("[ArenaPanel] HubRankedSearchPanel introuvable — lance 'Nymora > Setup > Patch Ranked Search Panel' sur la scene hub.");
                return;
            }
            Close();
            search.Open();
        }

        private async void OnTrainingClicked()
        {
            if (_transitionInProgress) return;

            // 5.3.g — Verifie qu'un deck est equipe dans le DeckBuilder pour la classe courante.
            // En MVP, on prend le PREMIER deck de la classe selectionnee dans HubDeckBuilderPanel.
            // Plus tard (5.3.h polish) : ajouter une UI "choisis quel deck" entre Arene et Combat.
            var dbp = HubDeckBuilderPanel.Instance;
            if (dbp == null)
            {
                Debug.LogWarning("[ArenaPanel] HubDeckBuilderPanel introuvable — ouvre le Deck Builder une fois avant.");
                return;
            }

            // Fix 18 mai — Force le DeckBuilder a fetch les decks de la CLASSE SELECTIONNEE
            // (SelectedClassPreferences) AVANT de lire MyDecks. Sans ca, si Lorenzo se
            // connecte puis lance Combat IA direct sans ouvrir le Deck Builder, MyDecks
            // reste vide (panel jamais fetch) -> log "Aucun deck equipe" alors qu'il en a
            // un cote backend. Pattern identique a HubMatchTransition.HandleMatchReady (PvP).
            string selectedClass = SelectedClassPreferences.Get();
            if (string.IsNullOrEmpty(selectedClass)) selectedClass = "Soulrender";
            try
            {
                await dbp.EnsureClassLoadedAsync(selectedClass);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ArenaPanel] EnsureClassLoadedAsync({selectedClass}) echec : {ex.Message} — combat IA annule.");
                return;
            }

            if (dbp.MyDecks == null || dbp.MyDecks.Count == 0)
            {
                Debug.LogWarning($"[ArenaPanel] Aucun deck '{selectedClass}' equipe. Cree un deck dans le Deck Builder avant de lancer un combat.");
                return;
            }
            // Utilise le deck SELECTIONNE par l'utilisateur (clique en dernier dans la liste)
            // au lieu de MyDecks[0] systematique. Fallback MyDecks[0] si aucun deck cliqu.
            var deck = dbp.SelectedDeck;
            if (deck == null)
            {
                Debug.LogWarning($"[ArenaPanel] SelectedDeck null malgre MyDecks.Count>0 — anomalie. Combat annule.");
                return;
            }
            DeckBridge.SetPendingDeck(deck.classId, deck.spellIds, deck.name);
            Debug.Log($"[ArenaPanel] DeckBridge set : classId={deck.classId} deckName='{deck.name}' spellIds=[{string.Join(",", deck.spellIds)}]");

            // 19 mai — Push pseudo/clan local pour le tooltip combatant (Combat tooltip).
            // Opponent = bot IA -> clear (le tooltip affichera "Bot" fallback en interne).
            // POLISH-7 (20 mai) : utilise MyDisplayName (push par WELCOME backend) au lieu
            // d'extract email.
            string localPseudo = HubChatClient.Instance?.MyDisplayName ?? "";
            string localClan = (HubClanPanel.Instance != null && HubClanPanel.Instance.HasClan)
                ? HubClanPanel.Instance.MyClanName : "";
            PlayerProfileBridge.SetLocal(localPseudo, localClan);
            PlayerProfileBridge.ClearOpponent();

            _transitionInProgress = true;
            Debug.Log($"[ArenaPanel] Entrainement (vs IA) selectionne -> shutdown Fusion + LoadScene '{_trainingSceneName}'");
            await GoToTrainingAsync();
        }

        private async Task GoToTrainingAsync()
        {
            // T1 — fondu doux. Le shutdown Fusion se fait sous le voile opaque (hook async),
            // pas pendant le fondu (cf HubMatchTransition pattern).
            await SceneTransition.LoadAsync(_trainingSceneName, async () =>
            {
                var runner = FindFirstObjectByType<NetworkRunner>();
                if (runner != null && runner.IsRunning)
                {
                    try { await runner.Shutdown(); }
                    catch (System.Exception ex) { Debug.LogWarning($"[ArenaPanel] Shutdown a throw : {ex.Message} — on continue."); }
                }
            }, waitForReady: true);
        }
    }
}
