using Cysharp.Threading.Tasks;
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
        // POLISH-7 (20 mai) : on stocke le displayName (pseudo officiel) au lieu de l'email.
        private string _currentFromDisplayName;

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

        private void HandleIncoming(string challengeId, string fromUserId, string fromDisplayName)
        {
            _currentChallengeId = challengeId;
            _currentFromDisplayName = fromDisplayName;
            if (_label != null) _label.text = $"{fromDisplayName}\nvous défie !";
            if (_panel != null) _panel.SetActive(true);
            // A3 — son de notification de défi entrant.
            Nymora.Core.Audio.NymoraAudioManager.Instance?.PlaySfx(Nymora.Core.Audio.SoundId.ChallengeReceived);
        }

        private async void OnAcceptClicked()
        {
            // Garde deck : impossible d'accepter un défi sans deck équipé. On refuse alors
            // poliment (le challenger est notifié au lieu de timeouter) + pop-up "crée un deck".
            var dbp = HubDeckBuilderPanel.Instance;
            if (dbp == null || !await dbp.HasEquippedDeckForSelectedClassAsync())
            {
                HubNoticePopup.ShowNoDeck();
                Respond(false);
                return;
            }
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
            _currentFromDisplayName = null;
            if (_panel != null) _panel.SetActive(false);
        }
    }
}
