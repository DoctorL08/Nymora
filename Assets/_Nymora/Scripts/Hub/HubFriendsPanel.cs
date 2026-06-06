using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Nymora.Network.Backend;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 4.10 — Panel amis : 3 sections (mes amis / demandes reçues / demandes envoyées)
    /// + barre de recherche par displayName.
    ///
    /// Fetch initial via REST (GET /friends + /friends/requests en parallèle).
    /// Refresh auto sur events WS (incoming request, request response, friend removed).
    ///
    /// Online status DIFFÉRÉ à 4.10.f (push WS FRIEND_ONLINE/OFFLINE non implémenté pour 4.10).
    /// </summary>
    public sealed class HubFriendsPanel : MonoBehaviour
    {
        [Header("Backend")]
        [SerializeField] private NymoraBackendSettings _backendSettings;

        [Header("Refs externes")]
        [SerializeField] private HubChatUI _chatUI;

        [Header("Root")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private Button _closeButton;

        [Header("Search bar")]
        [SerializeField] private TMP_InputField _searchInput;
        [SerializeField] private Button _searchButton;
        [SerializeField] private TextMeshProUGUI _statusText;

        [Header("Section containers (VerticalLayoutGroup parent)")]
        [SerializeField] private RectTransform _friendsContainer;
        [SerializeField] private RectTransform _incomingContainer;
        [SerializeField] private RectTransform _outgoingContainer;

        [Header("Section labels")]
        [SerializeField] private TextMeshProUGUI _friendsCountLabel;
        [SerializeField] private TextMeshProUGUI _incomingCountLabel;
        [SerializeField] private TextMeshProUGUI _outgoingCountLabel;

        [Header("Item style")]
        [SerializeField] private float _itemHeight = 56f;
        [SerializeField] private Color _itemBgColor = new Color(0.18f, 0.20f, 0.25f, 1f);
        [SerializeField] private Color _btnAcceptColor = new Color(0.25f, 0.55f, 0.35f, 1f);
        [SerializeField] private Color _btnRefuseColor = new Color(0.55f, 0.25f, 0.25f, 1f);
        [SerializeField] private Color _btnNeutralColor = new Color(0.4f, 0.4f, 0.45f, 1f);
        [SerializeField] private Color _btnWhisperColor = new Color(0.25f, 0.4f, 0.65f, 1f);
        [SerializeField] private Color _onlineColor = new Color(0.35f, 0.75f, 0.35f, 1f);
        [SerializeField] private Color _offlineColor = new Color(0.40f, 0.40f, 0.45f, 1f);

        public static HubFriendsPanel Instance { get; private set; }
        public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;
        public int PendingIncomingCount { get; private set; }
        public event Action<int> OnPendingCountChanged;

        /// <summary>M4 — Snapshot online d'un ami (réutilisé par le nouveau menu hub
        /// HubMenuSocial). Le set est entretenu en continu via les events WS, même panel
        /// fermé, donc l'appel renvoie l'état courant. Renvoie false si userId inconnu/null.</summary>
        public bool IsFriendOnline(string userId)
            => HubChatClient.Instance != null
                ? HubChatClient.Instance.IsFriendOnline(userId)               // 6 juin : source de vérité (anti-timing)
                : (!string.IsNullOrEmpty(userId) && _onlineFriendIds.Contains(userId)); // fallback

        private NymoraApiClient _api;
        private readonly List<GameObject> _spawnedItems = new List<GameObject>();
        private bool _refreshing;
        // 4.10.g — online status
        private readonly HashSet<string> _onlineFriendIds = new HashSet<string>();
        private readonly Dictionary<string, Image> _onlineDots = new Dictionary<string, Image>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            if (_backendSettings == null)
            {
                Debug.LogError("[FriendsPanel] _backendSettings non assigné — panel désactivé.");
                enabled = false;
                return;
            }
            _api = new NymoraApiClient(_backendSettings);
            if (_panelRoot != null) _panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (_closeButton != null) _closeButton.onClick.AddListener(Close);
            if (_searchButton != null) _searchButton.onClick.AddListener(OnSearchClicked);
            if (_searchInput != null) _searchInput.onSubmit.AddListener(OnSearchSubmit);
        }

        private void OnDisable()
        {
            if (_closeButton != null) _closeButton.onClick.RemoveListener(Close);
            if (_searchButton != null) _searchButton.onClick.RemoveListener(OnSearchClicked);
            if (_searchInput != null) _searchInput.onSubmit.RemoveListener(OnSearchSubmit);
        }

        private void Start()
        {
            if (HubChatClient.Instance != null)
            {
                HubChatClient.Instance.OnIncomingFriendRequest += HandleWsRefreshTrigger;
                HubChatClient.Instance.OnFriendRequestSent += HandleWsRefreshTrigger;
                HubChatClient.Instance.OnFriendRequestResponse += HandleWsResponse;
                HubChatClient.Instance.OnFriendRemoved += HandleWsRemoved;
                // 4.10.g — online status
                HubChatClient.Instance.OnFriendsOnlineList += HandleFriendsOnlineList;
                HubChatClient.Instance.OnFriendOnline += HandleFriendOnline;
                HubChatClient.Instance.OnFriendOffline += HandleFriendOffline;
            }
            // Fetch initial des demandes pending pour le badge, même panel fermé.
            RefreshPendingCountAsync().Forget();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (HubChatClient.Instance != null)
            {
                HubChatClient.Instance.OnIncomingFriendRequest -= HandleWsRefreshTrigger;
                HubChatClient.Instance.OnFriendRequestSent -= HandleWsRefreshTrigger;
                HubChatClient.Instance.OnFriendRequestResponse -= HandleWsResponse;
                HubChatClient.Instance.OnFriendRemoved -= HandleWsRemoved;
                HubChatClient.Instance.OnFriendsOnlineList -= HandleFriendsOnlineList;
                HubChatClient.Instance.OnFriendOnline -= HandleFriendOnline;
                HubChatClient.Instance.OnFriendOffline -= HandleFriendOffline;
            }
        }

        public void Open()
        {
            if (_panelRoot == null) return;
            _panelRoot.SetActive(true);
            RefreshAsync().Forget();
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

        // ====== Event handlers WS (refresh) ======
        private void HandleWsRefreshTrigger(string a, string b, string c)
        {
            if (IsOpen) RefreshAsync().Forget();
            else RefreshPendingCountAsync().Forget();
        }

        private void HandleWsResponse(string friendshipId, bool accepted, string fromUserId, string fromDisplayName)
        {
            if (IsOpen) RefreshAsync().Forget();
            else RefreshPendingCountAsync().Forget();
        }

        private void HandleWsRemoved(string friendUserId, string friendDisplayName)
        {
            if (IsOpen) RefreshAsync().Forget();
        }

        // ====== 4.10.g Online status ======
        private void HandleFriendsOnlineList(string[] ids)
        {
            _onlineFriendIds.Clear();
            if (ids != null)
            {
                foreach (var id in ids) if (!string.IsNullOrEmpty(id)) _onlineFriendIds.Add(id);
            }
            RefreshAllDots();
        }

        private void HandleFriendOnline(string friendUserId)
        {
            if (string.IsNullOrEmpty(friendUserId)) return;
            _onlineFriendIds.Add(friendUserId);
            UpdateDotFor(friendUserId);
        }

        private void HandleFriendOffline(string friendUserId)
        {
            if (string.IsNullOrEmpty(friendUserId)) return;
            _onlineFriendIds.Remove(friendUserId);
            UpdateDotFor(friendUserId);
        }

        private void UpdateDotFor(string friendUserId)
        {
            if (_onlineDots.TryGetValue(friendUserId, out var img) && img != null)
            {
                img.color = _onlineFriendIds.Contains(friendUserId) ? _onlineColor : _offlineColor;
            }
        }

        private void RefreshAllDots()
        {
            foreach (var kv in _onlineDots)
            {
                if (kv.Value == null) continue;
                kv.Value.color = _onlineFriendIds.Contains(kv.Key) ? _onlineColor : _offlineColor;
            }
        }

        // ====== Recherche manuelle ======
        private void OnSearchClicked() => SendRequestFromInput();
        private void OnSearchSubmit(string _) => SendRequestFromInput();

        private void SendRequestFromInput()
        {
            if (_searchInput == null) return;
            string target = _searchInput.text?.Trim() ?? "";
            if (string.IsNullOrEmpty(target))
            {
                SetStatus("Saisis un pseudo (displayName)");
                return;
            }
            SendRequestAsync(target).Forget();
            _searchInput.text = "";
        }

        private async UniTask SendRequestAsync(string targetDisplayName)
        {
            if (!EnsureToken())
            {
                SetStatus("Pas de JWT");
                return;
            }
            SetStatus($"Envoi à {targetDisplayName}...");
            var res = await _api.SendFriendRequestAsync(targetDisplayName);
            if (!res.IsSuccess)
            {
                SetStatus($"Échec ({res.StatusCode}): {res.ErrorMessage}");
                return;
            }
            SetStatus($"Demande envoyée à {res.Data.toDisplayName}");
            await RefreshAsync();
        }

        // ====== Refresh ======
        private async UniTask RefreshPendingCountAsync()
        {
            if (!EnsureToken()) return;
            var res = await _api.GetFriendRequestsAsync();
            if (!res.IsSuccess) return;
            int newCount = res.Data.incoming?.Length ?? 0;
            if (newCount != PendingIncomingCount)
            {
                PendingIncomingCount = newCount;
                OnPendingCountChanged?.Invoke(newCount);
            }
        }

        private async UniTask RefreshAsync()
        {
            if (_refreshing) return;
            _refreshing = true;
            try
            {
                if (!EnsureToken())
                {
                    SetStatus("Pas de JWT (HubChatClient._devToken vide)");
                    return;
                }

                var friendsTask = _api.GetFriendsAsync();
                var requestsTask = _api.GetFriendRequestsAsync();
                var friendsRes = await friendsTask;
                var requestsRes = await requestsTask;

                if (!friendsRes.IsSuccess)
                {
                    SetStatus($"Erreur amis {friendsRes.StatusCode}: {friendsRes.ErrorMessage}");
                    return;
                }
                if (!requestsRes.IsSuccess)
                {
                    SetStatus($"Erreur demandes {requestsRes.StatusCode}: {requestsRes.ErrorMessage}");
                    return;
                }

                ClearSpawnedItems();
                RenderFriends(friendsRes.Data.friends);
                RenderIncoming(requestsRes.Data.incoming);
                RenderOutgoing(requestsRes.Data.outgoing);

                int incomingCount = requestsRes.Data.incoming?.Length ?? 0;
                if (incomingCount != PendingIncomingCount)
                {
                    PendingIncomingCount = incomingCount;
                    OnPendingCountChanged?.Invoke(incomingCount);
                }
                SetStatus("");
            }
            finally
            {
                _refreshing = false;
            }
        }

        private bool EnsureToken()
        {
            string token = HubChatClient.Instance?.DevToken;
            if (string.IsNullOrEmpty(token)) return false;
            _api.SetBearerToken(token);
            return true;
        }

        // ====== Render sections ======
        private void RenderFriends(FriendDto[] friends)
        {
            int count = friends?.Length ?? 0;
            if (_friendsCountLabel != null) _friendsCountLabel.text = $"Amis ({count})";
            if (_friendsContainer == null || friends == null) return;
            foreach (var f in friends)
            {
                SpawnFriendItem(_friendsContainer, f);
            }
        }

        private void RenderIncoming(IncomingFriendRequestDto[] incoming)
        {
            int count = incoming?.Length ?? 0;
            if (_incomingCountLabel != null) _incomingCountLabel.text = $"Demandes reçues ({count})";
            if (_incomingContainer == null || incoming == null) return;
            foreach (var r in incoming)
            {
                SpawnIncomingItem(_incomingContainer, r);
            }
        }

        private void RenderOutgoing(OutgoingFriendRequestDto[] outgoing)
        {
            int count = outgoing?.Length ?? 0;
            if (_outgoingCountLabel != null) _outgoingCountLabel.text = $"Demandes envoyées ({count})";
            if (_outgoingContainer == null || outgoing == null) return;
            foreach (var r in outgoing)
            {
                SpawnOutgoingItem(_outgoingContainer, r);
            }
        }

        private void SpawnFriendItem(RectTransform parent, FriendDto friend)
        {
            var item = CreateRowBase(parent, friend.displayName, friend.userId);
            var captured = friend;
            AddRowButton(item.transform, "MP", _btnWhisperColor, () => OpenWhisperToFriend(captured.displayName));
            AddRowButton(item.transform, "Retirer", _btnRefuseColor, () => RemoveFriendAsync(captured.userId).Forget());
        }

        private void OpenWhisperToFriend(string targetDisplayName)
        {
            if (_chatUI == null)
            {
                Debug.LogWarning("[FriendsPanel] _chatUI non assigne — MP impossible.");
                return;
            }
            _chatUI.OpenWhisperToUser(targetDisplayName);
            Close();
        }

        private void SpawnIncomingItem(RectTransform parent, IncomingFriendRequestDto req)
        {
            var item = CreateRowBase(parent, req.fromDisplayName, null);
            var captured = req;
            AddRowButton(item.transform, "Accepter", _btnAcceptColor, () => RespondAsync(captured.friendshipId, true).Forget());
            AddRowButton(item.transform, "Refuser", _btnRefuseColor, () => RespondAsync(captured.friendshipId, false).Forget());
        }

        private void SpawnOutgoingItem(RectTransform parent, OutgoingFriendRequestDto req)
        {
            var item = CreateRowBase(parent, req.toDisplayName, null);
            AddRowLabel(item.transform, "(en attente)", _btnNeutralColor);
        }

        private async UniTask RemoveFriendAsync(string friendUserId)
        {
            if (!EnsureToken()) { SetStatus("Pas de JWT"); return; }
            var res = await _api.RemoveFriendAsync(friendUserId);
            if (!res.IsSuccess)
            {
                SetStatus($"Erreur retrait ({res.StatusCode}): {res.ErrorMessage}");
                return;
            }
            SetStatus("Ami retiré");
            await RefreshAsync();
        }

        private async UniTask RespondAsync(string friendshipId, bool accepted)
        {
            if (!EnsureToken()) { SetStatus("Pas de JWT"); return; }
            var res = await _api.RespondFriendRequestAsync(friendshipId, accepted);
            if (!res.IsSuccess)
            {
                SetStatus($"Erreur réponse ({res.StatusCode}): {res.ErrorMessage}");
                return;
            }
            SetStatus(accepted ? "Ami ajouté" : "Demande refusée");
            await RefreshAsync();
        }

        // ====== Helpers UI ======
        private void SetStatus(string msg)
        {
            if (_statusText != null) _statusText.text = msg;
        }

        private void ClearSpawnedItems()
        {
            foreach (var go in _spawnedItems)
            {
                if (go != null) Destroy(go);
            }
            _spawnedItems.Clear();
            _onlineDots.Clear();
        }

        /// <summary>onlineForUserId = non-null pour activer le dot online/offline ; null pour pas de dot (incoming/outgoing).</summary>
        private GameObject CreateRowBase(RectTransform parent, string mainLabel, string onlineForUserId)
        {
            var go = new GameObject($"Item_{mainLabel}",
                typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = _itemBgColor;
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = _itemHeight;
            le.flexibleWidth = 1f;
            var hl = go.GetComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(12, 12, 6, 6);
            hl.spacing = 8;
            hl.childForceExpandHeight = true;
            hl.childControlHeight = true;
            hl.childControlWidth = true;

            // Online dot (uniquement pour ami confirmé)
            if (!string.IsNullOrEmpty(onlineForUserId))
            {
                var dotGo = new GameObject("OnlineDot", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                dotGo.transform.SetParent(go.transform, false);
                var dotImg = dotGo.GetComponent<Image>();
                bool online = IsFriendOnline(onlineForUserId); // 6 juin : lit le cache autoritatif (HubChatClient)
                dotImg.color = online ? _onlineColor : _offlineColor;
                var dotLe = dotGo.GetComponent<LayoutElement>();
                dotLe.preferredWidth = 14f;
                dotLe.preferredHeight = 14f;
                _onlineDots[onlineForUserId] = dotImg;
            }

            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            nameGo.transform.SetParent(go.transform, false);
            var name = nameGo.GetComponent<TextMeshProUGUI>();
            name.text = mainLabel;
            name.fontSize = 20;
            name.color = Color.white;
            name.alignment = TextAlignmentOptions.MidlineLeft;
            var nameLe = nameGo.GetComponent<LayoutElement>();
            nameLe.flexibleWidth = 1f;

            _spawnedItems.Add(go);
            return go;
        }

        private void AddRowButton(Transform parent, string label, Color bg, Action onClick)
        {
            var go = new GameObject($"Btn_{label}",
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = bg;
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = 100f;
            le.preferredHeight = _itemHeight - 12f;
            go.GetComponent<Button>().onClick.AddListener(() => onClick());

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(go.transform, false);
            var rt = labelGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var tmp = labelGo.GetComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 16;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
        }

        private void AddRowLabel(Transform parent, string text, Color color)
        {
            var go = new GameObject($"Label_{text}",
                typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 16;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.MidlineRight;
            tmp.fontStyle = FontStyles.Italic;
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = 120f;
            le.preferredHeight = _itemHeight - 12f;
        }
    }
}
