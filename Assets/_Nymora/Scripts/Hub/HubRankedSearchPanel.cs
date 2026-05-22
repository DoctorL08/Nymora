using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 6.1 — Panneau "Recherche de partie classee" (Ranked 1v1).
    ///
    /// Ouvert depuis le menu Arene (HubArenaPanel) quand on clique "Ranked 1v1".
    ///
    /// EN 6.1 : coquille UI uniquement (bouton Rechercher / Annuler + statut).
    /// Le clic "Rechercher" passe l'UI en etat "recherche en cours" mais NE lance
    /// PAS encore de matchmaking reel : le branchement backend (queue Redis par MMR
    /// + appairage + lancement de la scene 40_CombatRanked1v1) arrive en brique 6.2.
    ///
    /// Singleton (pattern miroir HubArenaPanel / HubDeckBuilderPanel) : HubArenaPanel
    /// l'ouvre via HubRankedSearchPanel.Instance?.Open() sans reference serialisee.
    ///
    /// Cable via "Nymora > Setup > Patch Ranked Search Panel".
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

        public static HubRankedSearchPanel Instance { get; private set; }

        public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;

        // true pendant qu'une recherche est "en cours" (UI). En 6.2 ce sera l'etat
        // reel de la file d'attente matchmaking.
        private bool _searching;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
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

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Open()
        {
            if (_panelRoot != null) _panelRoot.SetActive(true);
            SetIdleState();
        }

        public void Close()
        {
            // Si on ferme en pleine recherche, on annule d'abord (en 6.2 : leave queue backend).
            if (_searching) OnCancelClicked();
            if (_panelRoot != null) _panelRoot.SetActive(false);
        }

        // ---------- Etats UI ----------

        private void SetIdleState()
        {
            _searching = false;
            SetStatus("Prêt à chercher une partie classée 1v1.");
            if (_searchButton != null) _searchButton.gameObject.SetActive(true);
            if (_searchButton != null) _searchButton.interactable = true;
            if (_cancelButton != null) _cancelButton.gameObject.SetActive(false);
        }

        private void OnSearchClicked()
        {
            if (_searching) return;
            _searching = true;

            // 6.1 : on bascule juste l'UI en mode "recherche". Le matchmaking reel
            // (file Redis MMR + appairage + load 40_CombatRanked1v1) = brique 6.2.
            SetStatus("Recherche d'un adversaire en cours...\n(matchmaking branché en brique 6.2)");
            if (_searchButton != null) _searchButton.gameObject.SetActive(false);
            if (_cancelButton != null) _cancelButton.gameObject.SetActive(true);
            if (_cancelButton != null) _cancelButton.interactable = true;
        }

        private void OnCancelClicked()
        {
            // 6.2 : ici on quittera la file matchmaking backend.
            SetIdleState();
        }

        private void SetStatus(string s)
        {
            if (_statusText != null) _statusText.text = s;
        }
    }
}
