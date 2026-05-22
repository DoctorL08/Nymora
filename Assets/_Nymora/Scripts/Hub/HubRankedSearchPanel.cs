using Cysharp.Threading.Tasks;
using Nymora.Core.Data;
using Nymora.Network.Backend;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 6.1 / 6.2.b — Panneau "Recherche de partie classee" (Ranked 1v1).
    ///
    /// Ouvert depuis le menu Arene (HubArenaPanel) quand on clique "Ranked 1v1".
    ///
    /// 6.2.b : "Rechercher" verifie qu'un deck est equipe pour la classe selectionnee,
    /// puis envoie ENQUEUE_RANKED au backend. Le serveur appaire par MMR (fenetre adaptative)
    /// et renvoie RANKED_MATCH_FOUND -> c'est HubMatchTransition qui pilote la transition vers
    /// la scene 40_CombatRanked1v1 (set MatchBridge.IsRanked=true + DeckBridge). Ce panneau
    /// se contente de l'UI de file (statut + bouton Annuler).
    ///
    /// Singleton (pattern miroir HubArenaPanel). Cable via "Nymora > Setup > Patch Ranked Search Panel".
    /// </summary>
    public sealed class HubRankedSearchPanel : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private Button _closeButton;

        [Header("Recherche")]
        [SerializeField] private Button _searchButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private TMP_Text _statusText;

        [Header("Backend (saison)")]
        [SerializeField] private NymoraBackendSettings _backendSettings;

        public static HubRankedSearchPanel Instance { get; private set; }

        public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;

        private bool _searching;
        private NymoraApiClient _api;
        private string _seasonLabel = "";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            if (_backendSettings != null) _api = new NymoraApiClient(_backendSettings);
            if (_panelRoot != null) _panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (_closeButton != null) _closeButton.onClick.AddListener(Close);
            if (_searchButton != null) _searchButton.onClick.AddListener(OnSearchClicked);
            if (_cancelButton != null) _cancelButton.onClick.AddListener(OnCancelClicked);
        }

        private void OnDisable()
        {
            if (_closeButton != null) _closeButton.onClick.RemoveAllListeners();
            if (_searchButton != null) _searchButton.onClick.RemoveAllListeners();
            if (_cancelButton != null) _cancelButton.onClick.RemoveAllListeners();
        }

        private void Start()
        {
            var chat = HubChatClient.Instance;
            if (chat != null)
            {
                chat.OnRankedQueueJoined += HandleQueueJoined;
                chat.OnRankedMatchFound += HandleMatchFound;
                chat.OnRankedQueueLeft += HandleQueueLeft;
            }
        }

        private void OnDestroy()
        {
            var chat = HubChatClient.Instance;
            if (chat != null)
            {
                chat.OnRankedQueueJoined -= HandleQueueJoined;
                chat.OnRankedMatchFound -= HandleMatchFound;
                chat.OnRankedQueueLeft -= HandleQueueLeft;
            }
            if (Instance == this) Instance = null;
        }

        public void Open()
        {
            if (_panelRoot != null) _panelRoot.SetActive(true);
            SetIdleState();
            FetchSeasonAsync().Forget(); // 6.5 — affiche "Saison N — Xj restants"
        }

        private async UniTaskVoid FetchSeasonAsync()
        {
            if (_api == null) return;
            string token = HubChatClient.Instance?.DevToken;
            if (string.IsNullOrEmpty(token)) return;
            _api.SetBearerToken(token);
            var res = await _api.GetSeasonAsync();
            if (!res.IsSuccess) return;
            _seasonLabel = $"Saison {res.Data.number} — {res.Data.daysRemaining}j restants";
            // Rafraichit l'affichage si on est encore a l'etat repos.
            if (!_searching) SetIdleState();
        }

        public void Close()
        {
            // Si on ferme en pleine recherche, on quitte la file backend d'abord.
            if (_searching) OnCancelClicked();
            if (_panelRoot != null) _panelRoot.SetActive(false);
        }

        // ---------- Etats UI ----------

        private void SetIdleState()
        {
            _searching = false;
            string seasonLine = string.IsNullOrEmpty(_seasonLabel) ? "" : $"\n<color=#cdb4ff>{_seasonLabel}</color>";
            SetStatus($"Prêt à chercher une partie classée 1v1.{seasonLine}");
            ShowSearchButton(true);
        }

        private void ShowSearchButton(bool searchVisible)
        {
            if (_searchButton != null) _searchButton.gameObject.SetActive(searchVisible);
            if (_cancelButton != null) _cancelButton.gameObject.SetActive(!searchVisible);
        }

        // ---------- Recherche ----------

        private async void OnSearchClicked()
        {
            if (_searching) return;

            var chat = HubChatClient.Instance;
            if (chat == null || !chat.IsConnected)
            {
                SetStatus("Pas connecté au serveur. Réessaie dans un instant.");
                return;
            }

            // Verifie qu'un deck est equipe pour la classe selectionnee AVANT d'entrer en file
            // (sinon on apparie un adversaire qui poireaute pendant qu'on annule cote local).
            // Meme garde que HubArenaPanel.OnTrainingClicked / HubMatchTransition.
            var dbp = HubDeckBuilderPanel.Instance;
            if (dbp == null)
            {
                SetStatus("Ouvre le Deck Builder une fois avant de chercher une partie.");
                return;
            }

            string selectedClass = SelectedClassPreferences.Get();
            if (string.IsNullOrEmpty(selectedClass)) selectedClass = "Soulrender";

            _searching = true;
            SetStatus($"Vérification du deck {selectedClass}...");
            try
            {
                await dbp.EnsureClassLoadedAsync(selectedClass);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[RankedSearch] EnsureClassLoadedAsync({selectedClass}) echec : {ex.Message}");
                _searching = false;
                SetStatus("Erreur de chargement du deck. Réessaie.");
                return;
            }

            if (dbp.MyDecks == null || dbp.MyDecks.Count == 0 || dbp.SelectedDeck == null)
            {
                _searching = false;
                SetStatus($"Aucun deck '{selectedClass}' équipé.\nCrée un deck dans le Deck Builder d'abord.");
                return;
            }

            // OK -> entre en file. Le backend lit le MMR et renvoie RANKED_QUEUE_JOINED.
            ShowSearchButton(false);
            SetStatus("Connexion à la file classée...");
            chat.SendEnqueueRanked();
        }

        private void OnCancelClicked()
        {
            _searching = false;
            HubChatClient.Instance?.SendDequeueRanked();
            SetIdleState();
        }

        // ---------- Events backend ----------

        private void HandleQueueJoined(int mmr)
        {
            if (!IsOpen) return;
            _searching = true;
            ShowSearchButton(false);
            SetStatus($"Recherche d'un adversaire... (MMR {mmr})\nTon classement s'élargit avec le temps d'attente.");
        }

        private void HandleMatchFound(string matchId, string opponentSub, string opponentDisplayName)
        {
            _searching = false;
            // La transition vers la scene ranked est pilotee par HubMatchTransition.
            SetStatus($"Adversaire trouvé : {opponentDisplayName} !\nLancement du combat...");
        }

        private void HandleQueueLeft()
        {
            if (IsOpen) SetIdleState();
        }

        private void SetStatus(string s)
        {
            if (_statusText != null) _statusText.text = s;
        }
    }
}
