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
    /// Brique 4.11 — Panel Clan. 2 modes selon l'etat :
    ///   - Pas de clan : bouton "Creer un clan" + liste des invitations recues
    ///   - Dans un clan : header (nom + banniere couleur) + liste membres avec roles +
    ///     actions selon mon role (chef tout, officier promote/kick Recrue/Membre,
    ///     membre/recrue : leave uniquement). Chef seul peut dissoudre.
    ///
    /// Refresh auto sur events WS (INCOMING_CLAN_INVITE, CLAN_INVITE_RESPONSE,
    /// CLAN_MEMBER_JOINED / ROLE_CHANGED / LEFT, CLAN_DISBANDED).
    /// </summary>
    public sealed class HubClanPanel : MonoBehaviour
    {
        [Header("Backend")]
        [SerializeField] private NymoraBackendSettings _backendSettings;

        [Header("Root")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TextMeshProUGUI _statusText;

        [Header("No-clan mode")]
        [SerializeField] private GameObject _noClanModeRoot;
        [SerializeField] private TMP_InputField _createNameInput;
        [SerializeField] private Button _createClanButton;
        [SerializeField] private RectTransform _invitesContainer;
        [SerializeField] private TextMeshProUGUI _invitesCountLabel;

        [Header("In-clan mode")]
        [SerializeField] private GameObject _inClanModeRoot;
        [SerializeField] private Image _clanBanner;
        [SerializeField] private TextMeshProUGUI _clanNameLabel;
        [SerializeField] private TextMeshProUGUI _clanDescriptionLabel;
        [SerializeField] private RectTransform _membersContainer;
        [SerializeField] private TextMeshProUGUI _membersCountLabel;
        [SerializeField] private GameObject _inviteRowRoot; // contient l'input + bouton "Inviter"
        [SerializeField] private TMP_InputField _inviteInput;
        [SerializeField] private Button _inviteSendButton;
        [SerializeField] private Button _leaveButton;
        [SerializeField] private Button _disbandButton;

        [Header("Item style")]
        [SerializeField] private float _itemHeight = 56f;
        [SerializeField] private Color _itemBgColor = new Color(0.18f, 0.20f, 0.25f, 1f);
        [SerializeField] private Color _btnAcceptColor = new Color(0.25f, 0.55f, 0.35f, 1f);
        [SerializeField] private Color _btnRefuseColor = new Color(0.55f, 0.25f, 0.25f, 1f);
        [SerializeField] private Color _btnPromoteColor = new Color(0.35f, 0.45f, 0.65f, 1f);
        [SerializeField] private Color _btnDemoteColor = new Color(0.5f, 0.4f, 0.3f, 1f);

        public static HubClanPanel Instance { get; private set; }
        public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;
        public int PendingInvitesCount { get; private set; }
        public event Action<int> OnPendingInvitesChanged;

        /// <summary>4.11 polish — true si je suis dans un clan (utilisé par ChallengePopup pour masquer "Inviter dans clan").</summary>
        public bool HasClan => _myClan != null;
        public string MyClanRole => _myClan?.myRole;
        public string MyClanName => _myClan?.name;
        public bool CanInviteToClan => HasClan && (_myClan.myRole == "Leader" || _myClan.myRole == "Officer");
        public event Action OnClanStateChanged;

        private NymoraApiClient _api;
        private ClanDto _myClan;
        private readonly List<GameObject> _spawnedItems = new List<GameObject>();
        private bool _refreshing;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            if (_backendSettings == null)
            {
                Debug.LogError("[ClanPanel] _backendSettings non assigne — panel desactive.");
                enabled = false;
                return;
            }
            _api = new NymoraApiClient(_backendSettings);
            if (_panelRoot != null) _panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (_closeButton != null) _closeButton.onClick.AddListener(Close);
            if (_createClanButton != null) _createClanButton.onClick.AddListener(OnCreateClicked);
            if (_inviteSendButton != null) _inviteSendButton.onClick.AddListener(OnInviteClicked);
            if (_inviteInput != null) _inviteInput.onSubmit.AddListener(OnInviteSubmit);
            if (_leaveButton != null) _leaveButton.onClick.AddListener(OnLeaveClicked);
            if (_disbandButton != null) _disbandButton.onClick.AddListener(OnDisbandClicked);
        }

        private void OnDisable()
        {
            if (_closeButton != null) _closeButton.onClick.RemoveListener(Close);
            if (_createClanButton != null) _createClanButton.onClick.RemoveListener(OnCreateClicked);
            if (_inviteSendButton != null) _inviteSendButton.onClick.RemoveListener(OnInviteClicked);
            if (_inviteInput != null) _inviteInput.onSubmit.RemoveListener(OnInviteSubmit);
            if (_leaveButton != null) _leaveButton.onClick.RemoveListener(OnLeaveClicked);
            if (_disbandButton != null) _disbandButton.onClick.RemoveListener(OnDisbandClicked);
        }

        private void Start()
        {
            if (HubChatClient.Instance != null)
            {
                HubChatClient.Instance.OnWelcome += HandleWelcome;
                HubChatClient.Instance.OnIncomingClanInvite += HandleWsAnyClanEvent6;
                HubChatClient.Instance.OnClanInviteResponse += HandleWsClanInviteResponse;
                HubChatClient.Instance.OnClanMemberJoined += HandleWsAnyClanEvent4;
                HubChatClient.Instance.OnClanMemberRoleChanged += HandleWsAnyClanEvent4;
                HubChatClient.Instance.OnClanMemberLeft += HandleWsClanMemberLeft;
                HubChatClient.Instance.OnClanDisbanded += HandleWsClanDisbanded;
            }
            RefreshPendingCountAsync().Forget();
            // D1 (22 mai, test designer) — fetch l'etat clan des le Start. HubChatClient est
            // DontDestroyOnLoad : au RETOUR combat->hub, OnWelcome ne refire PAS (deja connecte),
            // donc RefreshClanStateAsync (branche sur HandleWelcome) ne tournait pas -> le clan
            // ne s'affichait qu'apres ouverture du menu clan. Fetch ici => le tag clan (avatar,
            // via le poll HubAvatar sur HubClanPanel.MyClanName) + l'etat se chargent sans ouvrir
            // le panel. EnsureToken (dans RefreshClanStateAsync) garde le cas non-connecte.
            RefreshClanStateAsync().Forget();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (HubChatClient.Instance != null)
            {
                HubChatClient.Instance.OnWelcome -= HandleWelcome;
                HubChatClient.Instance.OnIncomingClanInvite -= HandleWsAnyClanEvent6;
                HubChatClient.Instance.OnClanInviteResponse -= HandleWsClanInviteResponse;
                HubChatClient.Instance.OnClanMemberJoined -= HandleWsAnyClanEvent4;
                HubChatClient.Instance.OnClanMemberRoleChanged -= HandleWsAnyClanEvent4;
                HubChatClient.Instance.OnClanMemberLeft -= HandleWsClanMemberLeft;
                HubChatClient.Instance.OnClanDisbanded -= HandleWsClanDisbanded;
            }
        }

        private void HandleWelcome(string sub, string email, string displayName)
        {
            // 4.11 polish — fetch l'etat clan initial des qu'on est connecte (token valide).
            // POLISH-7 (20 mai) : signature OnWelcome etendue avec displayName, non utilise ici.
            RefreshClanStateAsync().Forget();
        }

        public void Open()
        {
            if (_panelRoot == null) return;
            _panelRoot.SetActive(true);
            RefreshAsync().Forget();
        }

        public void Close()
        {
            if (_panelRoot != null) _panelRoot.SetActive(false);
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        /// <summary>4.11 — Appelé par ChallengePopup quand on clic "Inviter dans clan" sur un avatar remote.</summary>
        public void InviteByUserIdFromContextMenu(string targetUserId)
        {
            InviteByUserIdAsync(targetUserId).Forget();
        }

        /// <summary>
        /// POLISH-7 polish (20 mai) — Invite par pseudo depuis le menu contextuel chat
        /// (click sur un pseudo dans le chat). Le backend resolve displayName -> userId.
        /// </summary>
        public void InviteByDisplayNameFromContextMenu(string displayName)
        {
            InviteByDisplayNameAsync(displayName).Forget();
        }

        private async UniTask InviteByDisplayNameAsync(string targetDisplayName)
        {
            if (!EnsureToken()) return;
            var res = await _api.InviteToClanByDisplayNameAsync(targetDisplayName);
            if (!res.IsSuccess)
            {
                Debug.LogWarning($"[ClanPanel] Invite by displayName failed ({res.StatusCode}): {res.ErrorMessage}");
                return;
            }
            Debug.Log($"[ClanPanel] Invitation envoyee a {res.Data.toDisplayName}");
        }

        // ====== Event handlers WS ======
        private void HandleWsAnyClanEvent6(string a, string b, string c, string d, string e, string f)
        {
            if (IsOpen) RefreshAsync().Forget();
            else RefreshPendingCountAsync().Forget();
        }

        private void HandleWsClanInviteResponse(string a, bool accepted, string c, string d, string e)
        {
            // 4.11 polish — si j'ai accepte une invite quelque part, mon etat clan a peut-etre change.
            if (accepted) RefreshClanStateAsync().Forget();
            if (IsOpen) RefreshAsync().Forget();
        }

        private void HandleWsAnyClanEvent4(string a, string b, string c, string d)
        {
            if (IsOpen) RefreshAsync().Forget();
        }

        private void HandleWsClanMemberLeft(string clanId, string userId, string displayName, string reason)
        {
            // 4.11 polish — si c'est moi qui pars (leave/kick), reset mon etat clan local.
            if (HubChatClient.Instance != null && userId == HubChatClient.Instance.MyUserId)
            {
                _myClan = null;
                OnClanStateChanged?.Invoke();
            }
            if (IsOpen) RefreshAsync().Forget();
        }

        private void HandleWsClanDisbanded(string clanId)
        {
            if (_myClan != null && _myClan.clanId == clanId)
            {
                _myClan = null;
                OnClanStateChanged?.Invoke();
            }
            if (IsOpen) RefreshAsync().Forget();
        }

        /// <summary>4.11 polish — light fetch /clans/me sans toucher l'UI, pour synchroniser HasClan.</summary>
        private async UniTask RefreshClanStateAsync()
        {
            if (!EnsureToken()) return;
            var res = await _api.GetMyClanAsync();
            bool wasInClan = HasClan;
            _myClan = res.IsSuccess ? res.Data : null;
            if (wasInClan != HasClan)
            {
                OnClanStateChanged?.Invoke();
            }
        }

        // ====== Actions UI ======
        private void OnCreateClicked() => CreateClanAsync().Forget();
        private void OnInviteClicked() => InviteFromInputAsync().Forget();
        private void OnInviteSubmit(string _) => InviteFromInputAsync().Forget();
        private void OnLeaveClicked() => LeaveAsync().Forget();
        private void OnDisbandClicked() => DisbandAsync().Forget();

        private async UniTask CreateClanAsync()
        {
            if (!EnsureToken()) { SetStatus("Pas de JWT"); return; }
            string name = _createNameInput != null ? _createNameInput.text?.Trim() : "";
            if (string.IsNullOrEmpty(name) || name.Length < 3 || name.Length > 32)
            {
                SetStatus("Nom : 3-32 caracteres");
                return;
            }
            SetStatus($"Creation du clan {name}...");
            var res = await _api.CreateClanAsync(name);
            if (!res.IsSuccess)
            {
                SetStatus($"Erreur creation ({res.StatusCode}): {res.ErrorMessage}");
                return;
            }
            if (_createNameInput != null) _createNameInput.text = "";
            SetStatus($"Clan {res.Data.name} cree !");
            await RefreshAsync();
        }

        private async UniTask InviteFromInputAsync()
        {
            if (!EnsureToken()) { SetStatus("Pas de JWT"); return; }
            string target = _inviteInput != null ? _inviteInput.text?.Trim() : "";
            if (string.IsNullOrEmpty(target))
            {
                SetStatus("Saisis un pseudo");
                return;
            }
            SetStatus($"Invitation a {target}...");
            var res = await _api.InviteToClanByDisplayNameAsync(target);
            if (!res.IsSuccess)
            {
                SetStatus($"Echec ({res.StatusCode}): {res.ErrorMessage}");
                return;
            }
            if (_inviteInput != null) _inviteInput.text = "";
            SetStatus($"Invitation envoyee a {res.Data.toDisplayName}");
        }

        private async UniTask InviteByUserIdAsync(string targetUserId)
        {
            if (!EnsureToken()) return;
            var res = await _api.InviteToClanByUserIdAsync(targetUserId);
            if (!res.IsSuccess)
            {
                Debug.LogWarning($"[ClanPanel] Invite by UUID failed ({res.StatusCode}): {res.ErrorMessage}");
                return;
            }
            Debug.Log($"[ClanPanel] Invitation envoyee a {res.Data.toDisplayName}");
        }

        private async UniTask LeaveAsync()
        {
            if (!EnsureToken()) { SetStatus("Pas de JWT"); return; }
            var res = await _api.LeaveClanAsync();
            if (!res.IsSuccess)
            {
                SetStatus($"Erreur leave ({res.StatusCode}): {res.ErrorMessage}");
                return;
            }
            _myClan = null;
            SetStatus("Clan quitte");
            await RefreshAsync();
        }

        private async UniTask DisbandAsync()
        {
            if (!EnsureToken()) { SetStatus("Pas de JWT"); return; }
            var res = await _api.DisbandClanAsync();
            if (!res.IsSuccess)
            {
                SetStatus($"Erreur dissolution ({res.StatusCode}): {res.ErrorMessage}");
                return;
            }
            _myClan = null;
            SetStatus("Clan dissous");
            await RefreshAsync();
        }

        private async UniTask RespondInviteAsync(string inviteId, bool accepted)
        {
            if (!EnsureToken()) { SetStatus("Pas de JWT"); return; }
            var res = await _api.RespondClanInviteAsync(inviteId, accepted);
            if (!res.IsSuccess)
            {
                SetStatus($"Erreur reponse ({res.StatusCode}): {res.ErrorMessage}");
                return;
            }
            SetStatus(accepted ? "Tu as rejoint le clan" : "Invitation refusee");
            await RefreshAsync();
        }

        private async UniTask PromoteAsync(string targetUserId, string newRole)
        {
            if (!EnsureToken()) { SetStatus("Pas de JWT"); return; }
            var res = await _api.PromoteClanMemberAsync(targetUserId, newRole);
            if (!res.IsSuccess)
            {
                SetStatus($"Erreur promotion ({res.StatusCode}): {res.ErrorMessage}");
                return;
            }
            SetStatus($"Role mis a jour: {newRole}");
            await RefreshAsync();
        }

        private async UniTask KickAsync(string targetUserId)
        {
            if (!EnsureToken()) { SetStatus("Pas de JWT"); return; }
            var res = await _api.KickClanMemberAsync(targetUserId);
            if (!res.IsSuccess)
            {
                SetStatus($"Erreur kick ({res.StatusCode}): {res.ErrorMessage}");
                return;
            }
            SetStatus("Membre exclu");
            await RefreshAsync();
        }

        // ====== Refresh / Render ======

        private async UniTask RefreshPendingCountAsync()
        {
            if (!EnsureToken()) return;
            var res = await _api.GetClanInvitesAsync();
            if (!res.IsSuccess) return;
            int count = res.Data.invites?.Length ?? 0;
            if (count != PendingInvitesCount)
            {
                PendingInvitesCount = count;
                OnPendingInvitesChanged?.Invoke(count);
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

                ClearSpawnedItems();

                // Fetch mon clan + invitations en parallele
                var clanTask = _api.GetMyClanAsync();
                var invitesTask = _api.GetClanInvitesAsync();
                var clanRes = await clanTask;
                var invitesRes = await invitesTask;

                bool hasClan = clanRes.IsSuccess;
                _myClan = hasClan ? clanRes.Data : null;
                var invites = invitesRes.IsSuccess ? (invitesRes.Data.invites ?? new ClanInviteDto[0]) : new ClanInviteDto[0];

                PendingInvitesCount = invites.Length;
                OnPendingInvitesChanged?.Invoke(invites.Length);

                if (hasClan && _myClan != null)
                {
                    SwitchMode(false);
                    RenderInClan(_myClan);
                }
                else
                {
                    SwitchMode(true);
                    RenderNoClan(invites);
                }
                SetStatus("");
            }
            finally
            {
                _refreshing = false;
            }
        }

        private void SwitchMode(bool noClan)
        {
            if (_noClanModeRoot != null) _noClanModeRoot.SetActive(noClan);
            if (_inClanModeRoot != null) _inClanModeRoot.SetActive(!noClan);
        }

        private void RenderNoClan(ClanInviteDto[] invites)
        {
            if (_invitesCountLabel != null) _invitesCountLabel.text = $"Invitations recues ({invites.Length})";
            if (_invitesContainer == null) return;
            foreach (var inv in invites)
            {
                SpawnInviteItem(_invitesContainer, inv);
            }
        }

        private void RenderInClan(ClanDto clan)
        {
            if (_clanNameLabel != null) _clanNameLabel.text = clan.name;
            if (_clanDescriptionLabel != null) _clanDescriptionLabel.text = clan.description ?? "";
            if (_clanBanner != null && !string.IsNullOrEmpty(clan.bannerColor) && ColorUtility.TryParseHtmlString(clan.bannerColor, out var c))
            {
                _clanBanner.color = c;
            }

            int memberCount = clan.members?.Length ?? 0;
            if (_membersCountLabel != null) _membersCountLabel.text = $"Membres ({memberCount})";

            // Actions globales selon role
            string myRole = clan.myRole;
            bool isLeader = myRole == "Leader";
            bool canInvite = isLeader || myRole == "Officer";
            if (_inviteRowRoot != null) _inviteRowRoot.SetActive(canInvite);
            if (_leaveButton != null) _leaveButton.gameObject.SetActive(!isLeader);
            if (_disbandButton != null) _disbandButton.gameObject.SetActive(isLeader);

            if (_membersContainer == null || clan.members == null) return;
            foreach (var m in clan.members)
            {
                SpawnMemberItem(_membersContainer, m, myRole);
            }
        }

        // ====== Spawn items ======

        private void SpawnInviteItem(RectTransform parent, ClanInviteDto inv)
        {
            var item = CreateRowBase(parent, $"<b>{inv.clanName}</b> (par {inv.fromDisplayName})");
            var captured = inv;
            AddRowButton(item.transform, "Accepter", _btnAcceptColor, () => RespondInviteAsync(captured.inviteId, true).Forget());
            AddRowButton(item.transform, "Refuser", _btnRefuseColor, () => RespondInviteAsync(captured.inviteId, false).Forget());
        }

        private void SpawnMemberItem(RectTransform parent, ClanMemberDto member, string myRole)
        {
            var item = CreateRowBase(parent, $"{member.displayName}  <color=#888888>[{member.role}]</color>");
            var captured = member;

            bool isLeader = myRole == "Leader";
            bool isOfficer = myRole == "Officer";
            bool isSelf = HubChatClient.Instance != null && member.userId == HubChatClient.Instance.MyUserId;
            if (isSelf) return; // pas d'action sur soi-meme

            // Chef seul peut promote/demote (Officier <-> Membre, Membre <-> Recrue)
            if (isLeader && member.role != "Leader")
            {
                if (member.role == "Recruit")
                {
                    AddRowButton(item.transform, "↑ Membre", _btnPromoteColor, () => PromoteAsync(captured.userId, "Member").Forget());
                }
                else if (member.role == "Member")
                {
                    AddRowButton(item.transform, "↑ Officier", _btnPromoteColor, () => PromoteAsync(captured.userId, "Officer").Forget());
                    AddRowButton(item.transform, "↓ Recrue", _btnDemoteColor, () => PromoteAsync(captured.userId, "Recruit").Forget());
                }
                else if (member.role == "Officer")
                {
                    AddRowButton(item.transform, "↓ Membre", _btnDemoteColor, () => PromoteAsync(captured.userId, "Member").Forget());
                }
            }
            // Officier peut promote Recrue -> Membre uniquement
            else if (isOfficer && member.role == "Recruit")
            {
                AddRowButton(item.transform, "↑ Membre", _btnPromoteColor, () => PromoteAsync(captured.userId, "Member").Forget());
            }

            // Kick : chef tout sauf Leader ; officier kick Member/Recruit uniquement
            bool canKick = (isLeader && member.role != "Leader") ||
                           (isOfficer && (member.role == "Member" || member.role == "Recruit"));
            if (canKick)
            {
                AddRowButton(item.transform, "Kick", _btnRefuseColor, () => KickAsync(captured.userId).Forget());
            }
        }

        // ====== Helpers UI ======

        private void SetStatus(string msg)
        {
            if (_statusText != null) _statusText.text = msg;
        }

        private bool EnsureToken()
        {
            string token = HubChatClient.Instance?.DevToken;
            if (string.IsNullOrEmpty(token)) return false;
            _api.SetBearerToken(token);
            return true;
        }

        private void ClearSpawnedItems()
        {
            foreach (var go in _spawnedItems)
            {
                if (go != null) Destroy(go);
            }
            _spawnedItems.Clear();
        }

        private GameObject CreateRowBase(RectTransform parent, string mainLabel)
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
            hl.spacing = 6;
            hl.childForceExpandHeight = true;
            hl.childControlHeight = true;
            hl.childControlWidth = true;

            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            nameGo.transform.SetParent(go.transform, false);
            var tmp = nameGo.GetComponent<TextMeshProUGUI>();
            tmp.text = mainLabel;
            tmp.fontSize = 18;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.richText = true;
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
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var tmp = labelGo.GetComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 14;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
        }
    }
}
