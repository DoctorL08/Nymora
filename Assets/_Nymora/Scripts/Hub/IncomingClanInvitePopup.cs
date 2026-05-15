using Cysharp.Threading.Tasks;
using Nymora.Network.Backend;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 4.11 — Popup auto-affiché à la réception d'un INCOMING_CLAN_INVITE.
    /// Boutons Accepter / Refuser → POST /clans/invites/:id/respond via NymoraApiClient.
    ///
    /// Le panel d'acceptation cache aussi la bannière couleur du clan pour aperçu.
    /// </summary>
    public sealed class IncomingClanInvitePopup : MonoBehaviour
    {
        [Header("Backend")]
        [SerializeField] private NymoraBackendSettings _backendSettings;

        [Header("Refs")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private Image _bannerPreview;
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private Button _acceptButton;
        [SerializeField] private Button _refuseButton;

        public static IncomingClanInvitePopup Instance { get; private set; }

        private NymoraApiClient _api;
        private string _currentInviteId;
        private string _currentClanId;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            if (_backendSettings == null)
            {
                Debug.LogError("[IncomingClanInvite] _backendSettings non assigné — popup désactivée.");
                enabled = false;
                return;
            }
            _api = new NymoraApiClient(_backendSettings);
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
                HubChatClient.Instance.OnIncomingClanInvite += HandleIncoming;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (HubChatClient.Instance != null)
            {
                HubChatClient.Instance.OnIncomingClanInvite -= HandleIncoming;
            }
        }

        private void HandleIncoming(string inviteId, string clanId, string clanName, string clanBannerColor, string fromUserId, string fromDisplayName)
        {
            _currentInviteId = inviteId;
            _currentClanId = clanId;
            if (_label != null) _label.text = $"{fromDisplayName} t'invite\ndans le clan <b>{clanName}</b>";
            if (_bannerPreview != null && !string.IsNullOrEmpty(clanBannerColor) && ColorUtility.TryParseHtmlString(clanBannerColor, out var c))
            {
                _bannerPreview.color = c;
            }
            if (_panel != null) _panel.SetActive(true);
        }

        private void OnAcceptClicked() { RespondAsync(true).Forget(); }
        private void OnRefuseClicked() { RespondAsync(false).Forget(); }

        private async UniTask RespondAsync(bool accepted)
        {
            if (string.IsNullOrEmpty(_currentInviteId)) { Hide(); return; }
            string token = HubChatClient.Instance?.DevToken;
            if (string.IsNullOrEmpty(token))
            {
                Debug.LogWarning("[IncomingClanInvite] Pas de JWT");
                Hide();
                return;
            }
            _api.SetBearerToken(token);
            var res = await _api.RespondClanInviteAsync(_currentInviteId, accepted);
            if (!res.IsSuccess)
            {
                Debug.LogWarning($"[IncomingClanInvite] Respond failed ({res.StatusCode}): {res.ErrorMessage}");
            }
            Hide();
        }

        public void Hide()
        {
            _currentInviteId = null;
            _currentClanId = null;
            if (_panel != null) _panel.SetActive(false);
        }
    }
}
