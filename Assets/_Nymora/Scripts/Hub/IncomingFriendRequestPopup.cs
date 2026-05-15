using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 4.10 — Popup auto-affiché à la réception d'un INCOMING_FRIEND_REQUEST.
    /// Boutons Accepter / Refuser → RespondFriendRequest → backend update Friendship.
    ///
    /// Multiples demandes simultanées : écrase silencieusement la précédente (la précédente
    /// reste pending en DB jusqu'à ce que le user la traite via le panel Amis 4.10.e).
    /// </summary>
    public sealed class IncomingFriendRequestPopup : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private Button _acceptButton;
        [SerializeField] private Button _refuseButton;

        public static IncomingFriendRequestPopup Instance { get; private set; }

        private string _currentFriendshipId;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnEnable()
        {
            if (_acceptButton != null) _acceptButton.onClick.AddListener(OnAcceptClicked);
            if (_refuseButton != null) _refuseButton.onClick.AddListener(OnRefuseClicked);
        }

        private void OnDisable()
        {
            if (_acceptButton != null) _acceptButton.onClick.RemoveListener(OnAcceptClicked);
            if (_refuseButton != null) _refuseButton.onClick.RemoveListener(OnRefuseClicked);
        }

        private void Start()
        {
            if (HubChatClient.Instance != null)
            {
                HubChatClient.Instance.OnIncomingFriendRequest += HandleIncoming;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (HubChatClient.Instance != null)
            {
                HubChatClient.Instance.OnIncomingFriendRequest -= HandleIncoming;
            }
        }

        private void HandleIncoming(string friendshipId, string fromUserId, string fromDisplayName)
        {
            _currentFriendshipId = friendshipId;
            if (_label != null) _label.text = $"{fromDisplayName}\nveut t'ajouter en ami";
            if (_panel != null) _panel.SetActive(true);
        }

        private void OnAcceptClicked() { Respond(true); }
        private void OnRefuseClicked() { Respond(false); }

        private void Respond(bool accepted)
        {
            if (string.IsNullOrEmpty(_currentFriendshipId)) { Hide(); return; }
            if (HubChatClient.Instance == null)
            {
                Debug.LogWarning("[IncomingFriendRequest] HubChatClient.Instance null");
                Hide();
                return;
            }
            HubChatClient.Instance.RespondFriendRequest(_currentFriendshipId, accepted);
            Hide();
        }

        public void Hide()
        {
            _currentFriendshipId = null;
            if (_panel != null) _panel.SetActive(false);
        }
    }
}
