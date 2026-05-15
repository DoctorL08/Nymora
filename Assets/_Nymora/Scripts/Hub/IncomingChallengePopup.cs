using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 4.8.c — Popup auto-affiché à la réception d'un INCOMING_CHALLENGE.
    /// Boutons Accepter / Refuser → SendChallengeResponse → backend forward au sender.
    ///
    /// Multiple challenges simultanés : écrase silencieusement (le précédent reste pending
    /// côté backend jusqu'au disconnect cleanup). Queue à ajouter en 4.13 modération si nécessaire.
    /// </summary>
    public sealed class IncomingChallengePopup : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private Button _acceptButton;
        [SerializeField] private Button _refuseButton;

        public static IncomingChallengePopup Instance { get; private set; }

        private string _currentChallengeId;
        private string _currentFromEmail;

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
                HubChatClient.Instance.OnIncomingChallenge += HandleIncoming;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (HubChatClient.Instance != null)
            {
                HubChatClient.Instance.OnIncomingChallenge -= HandleIncoming;
            }
        }

        private void HandleIncoming(string challengeId, string fromUserId, string fromEmail)
        {
            _currentChallengeId = challengeId;
            _currentFromEmail = fromEmail;
            if (_label != null) _label.text = $"{fromEmail}\nvous défie !";
            if (_panel != null) _panel.SetActive(true);
        }

        private void OnAcceptClicked()
        {
            Respond(true);
        }

        private void OnRefuseClicked()
        {
            Respond(false);
        }

        private void Respond(bool accepted)
        {
            if (string.IsNullOrEmpty(_currentChallengeId))
            {
                Hide();
                return;
            }
            if (HubChatClient.Instance == null)
            {
                Debug.LogWarning("[IncomingChallenge] HubChatClient.Instance null");
                Hide();
                return;
            }
            HubChatClient.Instance.SendChallengeResponse(_currentChallengeId, accepted);
            Hide();
        }

        public void Hide()
        {
            _currentChallengeId = null;
            _currentFromEmail = null;
            if (_panel != null) _panel.SetActive(false);
        }
    }
}
