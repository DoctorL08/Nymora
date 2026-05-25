using System;
using System.Collections.Generic;
using Nymora.Network.Backend;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nymora.Hub.Menu
{
    /// <summary>
    /// M4 — Écran Social du nouveau menu hub (onglet "Social" de la barre haute).
    ///
    /// Deux sous-onglets au style du menu (pilules, comme HubMenuCosmetics) :
    ///   - Amis    : barre "Ajouter" (input pseudo) + 3 sections (Mes amis / Demandes reçues /
    ///               Demandes envoyées). Pastille online/offline sur les amis confirmés.
    ///   - Clan    : sans clan -> créer + invitations reçues ; avec clan -> bannière + membres
    ///               + actions selon rôle (promote/kick/leave/disband + inviter).
    ///
    /// Reconstruit la VUE dans le style du menu mais réutilise toute la LOGIQUE backend des
    /// panneaux existants (HubFriendsPanel / HubClanPanel) via NymoraApiClient — aucune
    /// duplication d'endpoint. Le statut online vient du tracking WS de HubFriendsPanel
    /// (snapshot initial via IsFriendOnline) + live via les events HubChatClient tant que
    /// l'écran est ouvert (cleanup auto au Destroy du holder).
    ///
    /// 100% View — pas de bump CombatRulesVersion.
    /// </summary>
    public sealed class HubMenuSocial
    {
        private readonly HubMenuTheme _t;
        private readonly HubMenuUIFactory _f;
        private readonly NymoraApiClient _api;

        private static readonly Color OnlineColor = new Color(0.35f, 0.78f, 0.40f, 1f);
        private static readonly Color OfflineColor = new Color(0.42f, 0.42f, 0.48f, 1f);
        private static readonly Color AcceptColor = new Color(0.27f, 0.55f, 0.36f, 1f);
        private static readonly Color RefuseColor = new Color(0.55f, 0.27f, 0.27f, 1f);
        private static readonly Color PromoteColor = new Color(0.33f, 0.45f, 0.65f, 1f);
        private static readonly Color DemoteColor = new Color(0.5f, 0.4f, 0.3f, 1f);

        private int _tab; // 0 = Amis, 1 = Clan
        private readonly List<(Image bg, TextMeshProUGUI label)> _tabRefs = new List<(Image, TextMeshProUGUI)>();

        private RectTransform _content;     // zone sous les sous-onglets, reconstruite par tab
        private RectTransform _listRoot;    // VerticalLayoutGroup courant
        private TextMeshProUGUI _status;
        private TMP_InputField _addInput;   // amis : ajouter
        private TMP_InputField _clanInput;  // clan : créer / inviter

        // Online (live tant que l'écran est ouvert)
        private readonly Dictionary<string, Image> _onlineDots = new Dictionary<string, Image>();
        private bool _subscribed;
        private bool _busy;

        // Tooltip survol (rond online) — calqué sur HubMenuDeckBuilder
        private GameObject _tooltipGo;
        private TextMeshProUGUI _tooltipText;

        public HubMenuSocial(HubMenuTheme t, HubMenuUIFactory f, NymoraApiClient api)
        {
            _t = t; _f = f; _api = api;
        }

        // ===== Entrée =====

        public void Build(RectTransform parent)
        {
            // Sous-onglets pilule (Amis / Clan), en haut du panneau
            var tabsRow = _f.MakeRect("SocialTabs", parent);
            tabsRow.anchorMin = new Vector2(0.5f, 1f); tabsRow.anchorMax = new Vector2(0.5f, 1f); tabsRow.pivot = new Vector2(0.5f, 1f);
            tabsRow.sizeDelta = new Vector2(360f, 42f); tabsRow.anchoredPosition = new Vector2(0f, -18f);
            var thlg = tabsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            thlg.spacing = 10f; thlg.childAlignment = TextAnchor.MiddleCenter;
            thlg.childControlWidth = true; thlg.childControlHeight = true;
            thlg.childForceExpandWidth = false; thlg.childForceExpandHeight = false;
            SpawnTab(tabsRow, "Amis", 0);
            SpawnTab(tabsRow, "Clan", 1);

            // Statut (sous les onglets)
            _status = _f.MakeText("Status", parent, "", _t.FontSizeSmall, _t.TextMuted, _t.Font, TextAlignmentOptions.Center);
            _status.raycastTarget = false;
            var srt = _status.rectTransform;
            srt.anchorMin = new Vector2(0f, 1f); srt.anchorMax = new Vector2(1f, 1f); srt.pivot = new Vector2(0.5f, 1f);
            srt.sizeDelta = new Vector2(-48f, 22f); srt.anchoredPosition = new Vector2(0f, -66f);

            // Zone de contenu padée dans le panneau (reconstruite à chaque switch de sous-onglet)
            _content = _f.MakeRect("SocialContent", parent);
            _content.anchorMin = new Vector2(0f, 0f); _content.anchorMax = new Vector2(1f, 1f);
            _content.offsetMin = new Vector2(28f, 18f); _content.offsetMax = new Vector2(-28f, -92f);

            // Tooltip survol (hors zone clippée, posé sur le panneau)
            BuildTooltip(parent);

            // Cleanup auto : se désabonne des events online quand le holder est détruit
            var notifier = parent.gameObject.AddComponent<DestroyNotifier>();
            notifier.OnDestroyed = Unsubscribe;
            Subscribe();

            UpdateTabStyles();
            ShowTab(0);
        }

        private void SpawnTab(RectTransform parent, string label, int idx)
        {
            var img = _f.MakeImage("Tab_" + label, parent, _t.ButtonGhostBg);
            var le = img.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 160f; le.preferredHeight = 40f;
            var btn = img.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
            var lbl = _f.MakeText("L", img.rectTransform, label, _t.FontSizeBody, _t.TextSecondary, _t.Font, TextAlignmentOptions.Center);
            HubMenuUIFactory.Stretch(lbl.rectTransform); lbl.raycastTarget = false; lbl.enableWordWrapping = false;
            btn.onClick.AddListener(() => { if (_tab != idx) ShowTab(idx); });
            _tabRefs.Add((img, lbl));
        }

        private void UpdateTabStyles()
        {
            for (int i = 0; i < _tabRefs.Count; i++)
            {
                bool active = i == _tab;
                if (_tabRefs[i].bg != null) _tabRefs[i].bg.color = active ? _t.Accent : _t.ButtonGhostBg;
                if (_tabRefs[i].label != null)
                {
                    _tabRefs[i].label.color = active ? _t.TextOnLight : _t.TextSecondary;
                    _tabRefs[i].label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
                }
            }
        }

        private void ShowTab(int idx)
        {
            _tab = idx;
            UpdateTabStyles();
            SetStatus("");
            _addInput = null; _clanInput = null;
            _onlineDots.Clear();

            // Vide la zone de contenu
            for (int i = _content.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(_content.GetChild(i).gameObject);
            _listRoot = null;

            if (idx == 0) BuildFriendsTab();
            else BuildClanTab();
        }

        // ===== Onglet Amis =====

        private void BuildFriendsTab()
        {
            // Barre "Ajouter" en haut : input + bouton
            var addRow = _f.MakeRect("AddRow", _content);
            addRow.anchorMin = new Vector2(0f, 1f); addRow.anchorMax = new Vector2(1f, 1f); addRow.pivot = new Vector2(0.5f, 1f);
            addRow.sizeDelta = new Vector2(-16f, 46f); addRow.anchoredPosition = new Vector2(0f, -4f);
            var hlg = addRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10f; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            _addInput = MakeInput(addRow, "Pseudo d'un joueur...", out var inputRT);
            var inle = inputRT.gameObject.AddComponent<LayoutElement>(); inle.flexibleWidth = 1f; inle.preferredHeight = 42f;
            _addInput.onSubmit.AddListener(_ => SendFriendRequest());

            var addBtn = _f.MakeButton(addRow, "Ajouter", true, out _);
            addBtn.gameObject.GetComponent<LayoutElement>().preferredWidth = 140f;
            addBtn.onClick.AddListener(SendFriendRequest);

            BuildScroll(_content, 56f);
            LoadFriendsAsync();
        }

        private async void LoadFriendsAsync()
        {
            SetStatus("Chargement...");
            if (!EnsureToken()) { SetStatus("Non connecté."); return; }

            var friendsRes = await _api.GetFriendsAsync();
            var reqRes = await _api.GetFriendRequestsAsync();
            if (_listRoot == null || _tab != 0) return; // écran fermé / changé pendant le fetch
            if (!friendsRes.IsSuccess) { SetStatus($"Erreur amis ({friendsRes.StatusCode})."); return; }

            ClearList();
            _onlineDots.Clear();

            var friends = friendsRes.Data.friends ?? Array.Empty<FriendDto>();
            var incoming = reqRes.IsSuccess ? (reqRes.Data.incoming ?? Array.Empty<IncomingFriendRequestDto>()) : Array.Empty<IncomingFriendRequestDto>();
            var outgoing = reqRes.IsSuccess ? (reqRes.Data.outgoing ?? Array.Empty<OutgoingFriendRequestDto>()) : Array.Empty<OutgoingFriendRequestDto>();

            SpawnSectionHeader($"Mes amis ({friends.Length})");
            if (friends.Length == 0) SpawnInfoRow("<i>Aucun ami pour l'instant. Ajoute un joueur ci-dessus.</i>");
            foreach (var fr in friends) SpawnFriendRow(fr);

            SpawnSectionHeader($"Demandes reçues ({incoming.Length})");
            if (incoming.Length == 0) SpawnInfoRow("<i>Aucune demande reçue.</i>");
            foreach (var r in incoming) SpawnIncomingRow(r);

            SpawnSectionHeader($"Demandes envoyées ({outgoing.Length})");
            if (outgoing.Length == 0) SpawnInfoRow("<i>Aucune demande en attente.</i>");
            foreach (var r in outgoing) SpawnOutgoingRow(r);

            SetStatus("");
        }

        private void SpawnFriendRow(FriendDto friend)
        {
            MakeRow(out var hbox);

            // Pastille online : rond parfait centré dans une cellule de largeur fixe
            // (la cellule absorbe l'étirement vertical du HLG ; le rond garde 14×14).
            var cell = _f.MakeRect("DotCell", hbox);
            cell.gameObject.AddComponent<LayoutElement>().preferredWidth = 20f;
            var dot = _f.MakeImage("Dot", cell, IsOnline(friend.userId) ? OnlineColor : OfflineColor, rounded: false);
            dot.sprite = CircleSprite(); dot.type = Image.Type.Simple; dot.preserveAspect = true;
            dot.raycastTarget = true; // requis pour le survol
            var drt = dot.rectTransform;
            drt.anchorMin = new Vector2(0.5f, 0.5f); drt.anchorMax = new Vector2(0.5f, 0.5f); drt.pivot = new Vector2(0.5f, 0.5f);
            drt.sizeDelta = new Vector2(14f, 14f); drt.anchoredPosition = Vector2.zero;
            dot.gameObject.AddComponent<OnlineDotProxy>().Init(dot, ShowTooltip, HideTooltip);
            if (!string.IsNullOrEmpty(friend.userId)) _onlineDots[friend.userId] = dot;

            SpawnRowName(hbox, friend.displayName);

            var chatUI = UnityEngine.Object.FindObjectOfType<HubChatUI>();
            if (chatUI != null)
            {
                string dn = friend.displayName;
                AddRowButton(hbox, "MP", _t.ButtonGhostBg, () => { chatUI.OpenWhisperToUser(dn); if (HubMenuShell.Instance != null) HubMenuShell.Instance.Close(); });
            }
            string uid = friend.userId;
            AddRowButton(hbox, "Retirer", RefuseColor, () => RemoveFriend(uid));
        }

        private void SpawnIncomingRow(IncomingFriendRequestDto req)
        {
            MakeRow(out var hbox);
            SpawnRowName(hbox, req.fromDisplayName);
            string fid = req.friendshipId;
            AddRowButton(hbox, "Accepter", AcceptColor, () => RespondFriend(fid, true));
            AddRowButton(hbox, "Refuser", RefuseColor, () => RespondFriend(fid, false));
        }

        private void SpawnOutgoingRow(OutgoingFriendRequestDto req)
        {
            MakeRow(out var hbox);
            SpawnRowName(hbox, req.toDisplayName);
            AddRowLabel(hbox, "(en attente)");
        }

        private void SendFriendRequest()
        {
            string target = _addInput != null ? _addInput.text?.Trim() : null;
            if (string.IsNullOrEmpty(target)) { SetStatus("Saisis un pseudo."); return; }
            SendFriendRequestAsync(target);
        }

        private async void SendFriendRequestAsync(string target)
        {
            if (_busy || !EnsureToken()) return;
            _busy = true;
            SetStatus($"Envoi à {target}...");
            var res = await _api.SendFriendRequestAsync(target);
            _busy = false;
            if (!res.IsSuccess) { SetStatus($"Échec ({res.StatusCode}) : {res.ErrorMessage}"); return; }
            if (_addInput != null) _addInput.text = "";
            SetStatus($"Demande envoyée à {res.Data.toDisplayName}.");
            LoadFriendsAsync();
        }

        private async void RespondFriend(string friendshipId, bool accepted)
        {
            if (_busy || !EnsureToken()) return;
            _busy = true;
            var res = await _api.RespondFriendRequestAsync(friendshipId, accepted);
            _busy = false;
            if (!res.IsSuccess) { SetStatus($"Erreur ({res.StatusCode})."); return; }
            SetStatus(accepted ? "Ami ajouté." : "Demande refusée.");
            LoadFriendsAsync();
        }

        private async void RemoveFriend(string friendUserId)
        {
            if (_busy || !EnsureToken()) return;
            _busy = true;
            var res = await _api.RemoveFriendAsync(friendUserId);
            _busy = false;
            if (!res.IsSuccess) { SetStatus($"Erreur retrait ({res.StatusCode})."); return; }
            SetStatus("Ami retiré.");
            LoadFriendsAsync();
        }

        // ===== Onglet Clan =====

        private void BuildClanTab()
        {
            BuildScroll(_content, 4f);
            LoadClanAsync();
        }

        private async void LoadClanAsync()
        {
            SetStatus("Chargement...");
            if (!EnsureToken()) { SetStatus("Non connecté."); return; }

            var clanRes = await _api.GetMyClanAsync();
            var invitesRes = await _api.GetClanInvitesAsync();
            if (_listRoot == null || _tab != 1) return;

            ClearList();
            bool hasClan = clanRes.IsSuccess && clanRes.Data != null;
            if (hasClan) RenderInClan(clanRes.Data);
            else RenderNoClan(invitesRes.IsSuccess ? (invitesRes.Data.invites ?? Array.Empty<ClanInviteDto>()) : Array.Empty<ClanInviteDto>());
            SetStatus("");
        }

        private void RenderNoClan(ClanInviteDto[] invites)
        {
            // Bloc créer un clan
            SpawnSectionHeader("Créer un clan");
            SpawnRowContainer(out var hbox, 52f);
            _clanInput = MakeInput(hbox, "Nom du clan (3-32)...", out var inputRT);
            var inle = inputRT.gameObject.AddComponent<LayoutElement>(); inle.flexibleWidth = 1f; inle.preferredHeight = 42f;
            _clanInput.onSubmit.AddListener(_ => CreateClan());
            var createBtn = _f.MakeButton(hbox, "Créer", true, out _);
            createBtn.gameObject.GetComponent<LayoutElement>().preferredWidth = 130f;
            createBtn.onClick.AddListener(CreateClan);

            SpawnSectionHeader($"Invitations reçues ({invites.Length})");
            if (invites.Length == 0) SpawnInfoRow("<i>Aucune invitation de clan.</i>");
            foreach (var inv in invites) SpawnClanInviteRow(inv);
        }

        private void RenderInClan(ClanDto clan)
        {
            string myRole = clan.myRole;
            bool isLeader = myRole == "Leader";
            bool canInvite = isLeader || myRole == "Officer";

            // Header : pastille couleur bannière + nom + description
            SpawnRowContainer(out var hbox, 56f);
            var banner = _f.MakeImage("Banner", hbox, _t.Accent, rounded: true);
            banner.raycastTarget = false;
            if (!string.IsNullOrEmpty(clan.bannerColor) && ColorUtility.TryParseHtmlString(clan.bannerColor, out var bc)) banner.color = bc;
            var ble = banner.gameObject.AddComponent<LayoutElement>(); ble.preferredWidth = 40f; ble.preferredHeight = 40f;
            var titleText = string.IsNullOrEmpty(clan.description) ? $"<b>{clan.name}</b>" : $"<b>{clan.name}</b>\n<size=72%><color=#9aa0ac>{clan.description}</color></size>";
            var nameTmp = _f.MakeText("ClanName", hbox, titleText, _t.FontSizeBody, _t.TextPrimary, _t.Font, TextAlignmentOptions.MidlineLeft);
            nameTmp.raycastTarget = false; nameTmp.enableWordWrapping = false;
            nameTmp.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            // Inviter (chef/officier)
            if (canInvite)
            {
                SpawnRowContainer(out var ihbox, 52f);
                _clanInput = MakeInput(ihbox, "Inviter un joueur (pseudo)...", out var inputRT);
                var inle = inputRT.gameObject.AddComponent<LayoutElement>(); inle.flexibleWidth = 1f; inle.preferredHeight = 42f;
                _clanInput.onSubmit.AddListener(_ => InviteToClan());
                var invBtn = _f.MakeButton(ihbox, "Inviter", true, out _);
                invBtn.gameObject.GetComponent<LayoutElement>().preferredWidth = 130f;
                invBtn.onClick.AddListener(InviteToClan);
            }

            var members = clan.members ?? Array.Empty<ClanMemberDto>();
            SpawnSectionHeader($"Membres ({members.Length})");
            foreach (var m in members) SpawnMemberRow(m, myRole);

            // Quitter / Dissoudre (en bas)
            SpawnRowContainer(out var fhbox, 52f);
            if (isLeader)
            {
                var disband = _f.MakeButton(fhbox, "Dissoudre le clan", false, out var dl);
                ColorButton(disband, RefuseColor); dl.color = Color.white;
                disband.gameObject.GetComponent<LayoutElement>().preferredWidth = 220f;
                disband.onClick.AddListener(DisbandClan);
            }
            else
            {
                var leave = _f.MakeButton(fhbox, "Quitter le clan", false, out var ll);
                ColorButton(leave, RefuseColor); ll.color = Color.white;
                leave.gameObject.GetComponent<LayoutElement>().preferredWidth = 200f;
                leave.onClick.AddListener(LeaveClan);
            }
        }

        private void SpawnClanInviteRow(ClanInviteDto inv)
        {
            MakeRow(out var hbox);
            SpawnRowName(hbox, $"<b>{inv.clanName}</b>  <size=80%><color=#9aa0ac>par {inv.fromDisplayName}</color></size>");
            string id = inv.inviteId;
            AddRowButton(hbox, "Accepter", AcceptColor, () => RespondClanInvite(id, true));
            AddRowButton(hbox, "Refuser", RefuseColor, () => RespondClanInvite(id, false));
        }

        private void SpawnMemberRow(ClanMemberDto member, string myRole)
        {
            MakeRow(out var hbox);
            SpawnRowName(hbox, $"{member.displayName}  <size=82%><color=#888888>[{RoleLabel(member.role)}]</color></size>");

            bool isLeader = myRole == "Leader";
            bool isOfficer = myRole == "Officer";
            bool isSelf = HubChatClient.Instance != null && member.userId == HubChatClient.Instance.MyUserId;
            if (isSelf) return;

            string uid = member.userId;
            if (isLeader && member.role != "Leader")
            {
                if (member.role == "Recruit")
                    AddRowButton(hbox, "↑ Membre", PromoteColor, () => Promote(uid, "Member"));
                else if (member.role == "Member")
                {
                    AddRowButton(hbox, "↑ Officier", PromoteColor, () => Promote(uid, "Officer"));
                    AddRowButton(hbox, "↓ Recrue", DemoteColor, () => Promote(uid, "Recruit"));
                }
                else if (member.role == "Officer")
                    AddRowButton(hbox, "↓ Membre", DemoteColor, () => Promote(uid, "Member"));
            }
            else if (isOfficer && member.role == "Recruit")
                AddRowButton(hbox, "↑ Membre", PromoteColor, () => Promote(uid, "Member"));

            bool canKick = (isLeader && member.role != "Leader") ||
                           (isOfficer && (member.role == "Member" || member.role == "Recruit"));
            if (canKick) AddRowButton(hbox, "Exclure", RefuseColor, () => Kick(uid));
        }

        private void CreateClan()
        {
            string name = _clanInput != null ? _clanInput.text?.Trim() : null;
            if (string.IsNullOrEmpty(name) || name.Length < 3 || name.Length > 32) { SetStatus("Nom : 3 à 32 caractères."); return; }
            CreateClanAsync(name);
        }

        private async void CreateClanAsync(string name)
        {
            if (_busy || !EnsureToken()) return;
            _busy = true;
            SetStatus($"Création de {name}...");
            var res = await _api.CreateClanAsync(name);
            _busy = false;
            if (!res.IsSuccess) { SetStatus($"Échec ({res.StatusCode}) : {res.ErrorMessage}"); return; }
            SetStatus($"Clan {res.Data.name} créé !");
            LoadClanAsync();
        }

        private void InviteToClan()
        {
            string target = _clanInput != null ? _clanInput.text?.Trim() : null;
            if (string.IsNullOrEmpty(target)) { SetStatus("Saisis un pseudo."); return; }
            InviteToClanAsync(target);
        }

        private async void InviteToClanAsync(string target)
        {
            if (_busy || !EnsureToken()) return;
            _busy = true;
            SetStatus($"Invitation à {target}...");
            var res = await _api.InviteToClanByDisplayNameAsync(target);
            _busy = false;
            if (!res.IsSuccess) { SetStatus($"Échec ({res.StatusCode}) : {res.ErrorMessage}"); return; }
            if (_clanInput != null) _clanInput.text = "";
            SetStatus($"Invitation envoyée à {res.Data.toDisplayName}.");
        }

        private async void RespondClanInvite(string inviteId, bool accepted)
        {
            if (_busy || !EnsureToken()) return;
            _busy = true;
            var res = await _api.RespondClanInviteAsync(inviteId, accepted);
            _busy = false;
            if (!res.IsSuccess) { SetStatus($"Erreur ({res.StatusCode})."); return; }
            SetStatus(accepted ? "Tu as rejoint le clan." : "Invitation refusée.");
            LoadClanAsync();
        }

        private async void Promote(string targetUserId, string newRole)
        {
            if (_busy || !EnsureToken()) return;
            _busy = true;
            var res = await _api.PromoteClanMemberAsync(targetUserId, newRole);
            _busy = false;
            if (!res.IsSuccess) { SetStatus($"Erreur promotion ({res.StatusCode})."); return; }
            SetStatus($"Rôle mis à jour : {RoleLabel(newRole)}.");
            LoadClanAsync();
        }

        private async void Kick(string targetUserId)
        {
            if (_busy || !EnsureToken()) return;
            _busy = true;
            var res = await _api.KickClanMemberAsync(targetUserId);
            _busy = false;
            if (!res.IsSuccess) { SetStatus($"Erreur exclusion ({res.StatusCode})."); return; }
            SetStatus("Membre exclu.");
            LoadClanAsync();
        }

        private async void LeaveClan()
        {
            if (_busy || !EnsureToken()) return;
            _busy = true;
            var res = await _api.LeaveClanAsync();
            _busy = false;
            if (!res.IsSuccess) { SetStatus($"Erreur ({res.StatusCode})."); return; }
            SetStatus("Clan quitté.");
            LoadClanAsync();
        }

        private async void DisbandClan()
        {
            if (_busy || !EnsureToken()) return;
            _busy = true;
            var res = await _api.DisbandClanAsync();
            _busy = false;
            if (!res.IsSuccess) { SetStatus($"Erreur ({res.StatusCode})."); return; }
            SetStatus("Clan dissous.");
            LoadClanAsync();
        }

        // ===== Online (events WS, live tant que l'écran est ouvert) =====

        private bool IsOnline(string userId)
        {
            // Snapshot initial via le tracking persistant de HubFriendsPanel (entretenu même panel fermé).
            return HubFriendsPanel.Instance != null && HubFriendsPanel.Instance.IsFriendOnline(userId);
        }

        private void Subscribe()
        {
            if (_subscribed || HubChatClient.Instance == null) return;
            HubChatClient.Instance.OnFriendsOnlineList += HandleOnlineList;
            HubChatClient.Instance.OnFriendOnline += HandleFriendOnline;
            HubChatClient.Instance.OnFriendOffline += HandleFriendOffline;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || HubChatClient.Instance == null) { _subscribed = false; return; }
            HubChatClient.Instance.OnFriendsOnlineList -= HandleOnlineList;
            HubChatClient.Instance.OnFriendOnline -= HandleFriendOnline;
            HubChatClient.Instance.OnFriendOffline -= HandleFriendOffline;
            _subscribed = false;
        }

        private void HandleOnlineList(string[] ids)
        {
            var set = new HashSet<string>();
            if (ids != null) foreach (var id in ids) if (!string.IsNullOrEmpty(id)) set.Add(id);
            foreach (var kv in _onlineDots) if (kv.Value != null) kv.Value.color = set.Contains(kv.Key) ? OnlineColor : OfflineColor;
        }

        private void HandleFriendOnline(string userId) => SetDot(userId, true);
        private void HandleFriendOffline(string userId) => SetDot(userId, false);

        private void SetDot(string userId, bool online)
        {
            if (!string.IsNullOrEmpty(userId) && _onlineDots.TryGetValue(userId, out var img) && img != null)
                img.color = online ? OnlineColor : OfflineColor;
        }

        // ===== Helpers UI =====

        /// <summary>Scroll vertical + VerticalLayoutGroup auto-dimensionné (stocké dans _listRoot).</summary>
        private void BuildScroll(RectTransform parent, float topInset)
        {
            var vp = _f.MakeRect("Scroll", parent);
            vp.anchorMin = new Vector2(0f, 0f); vp.anchorMax = new Vector2(1f, 1f);
            vp.offsetMin = new Vector2(0f, 8f); vp.offsetMax = new Vector2(0f, -topInset);
            var vpImg = vp.gameObject.AddComponent<Image>(); vpImg.color = new Color(1f, 1f, 1f, 0.02f);
            vp.gameObject.AddComponent<RectMask2D>();
            var sr = vp.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 28f; sr.viewport = vp;

            var content = _f.MakeRect("Content", vp);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
            // sizeDelta par défaut (100×100) sur ancrages étirés -> largeur = parent+100, débord
            // de 50px de chaque côté. On force la largeur exacte du viewport.
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f; vlg.padding = new RectOffset(8, 8, 8, 8); vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize; fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            sr.content = content;
            _listRoot = content;
        }

        private void ClearList()
        {
            if (_listRoot == null) return;
            for (int i = _listRoot.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(_listRoot.GetChild(i).gameObject);
        }

        private void SpawnSectionHeader(string text)
        {
            var tmp = _f.MakeText("Header", _listRoot, text, _t.FontSizeHeader, _t.TextPrimary, _t.FontBold, TextAlignmentOptions.MidlineLeft);
            tmp.raycastTarget = false;
            var le = tmp.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 34f;
        }

        private void SpawnInfoRow(string richText)
        {
            var tmp = _f.MakeText("Info", _listRoot, richText, _t.FontSizeSmall, _t.TextMuted, _t.Font, TextAlignmentOptions.MidlineLeft);
            tmp.raycastTarget = false;
            tmp.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
        }

        /// <summary>Ligne avec fond carte + HorizontalLayoutGroup (hbox renvoyé pour y ajouter les enfants).</summary>
        private Image MakeRow(out RectTransform hbox)
        {
            var row = _f.MakeImage("Row", _listRoot, _t.CardBg);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 58f;
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(14, 12, 8, 8); hlg.spacing = 10f; hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            hbox = row.rectTransform;
            return row;
        }

        /// <summary>Conteneur transparent (sans fond carte) pour barres input/footer dans la liste.</summary>
        private RectTransform SpawnRowContainer(out RectTransform hbox, float height)
        {
            var row = _f.MakeRect("Container", _listRoot);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(2, 2, 4, 4); hlg.spacing = 10f; hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            hbox = row;
            return row;
        }

        private void SpawnRowName(RectTransform hbox, string text)
        {
            var name = _f.MakeText("Name", hbox, text, _t.FontSizeBody, _t.TextPrimary, _t.Font, TextAlignmentOptions.MidlineLeft);
            name.raycastTarget = false; name.enableWordWrapping = false;
            name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        }

        private void AddRowButton(RectTransform hbox, string label, Color bg, Action onClick)
        {
            var btn = _f.MakeButton(hbox, label, false, out var lbl);
            ColorButton(btn, bg);
            lbl.color = Color.white;
            var le = btn.gameObject.GetComponent<LayoutElement>();
            le.preferredWidth = Mathf.Max(96f, label.Length * 9f + 28f);
            le.minWidth = 0f; le.preferredHeight = 38f;
            btn.onClick.AddListener(() => onClick());
        }

        /// <summary>Teinte un bouton via son color block (le ColorTint multiplie l'Image,
        /// donc on met l'Image en blanc et on porte la couleur sur normal/highlight/pressed).</summary>
        private static void ColorButton(Button btn, Color bg)
        {
            var img = btn.GetComponent<Image>(); if (img != null) img.color = Color.white;
            var c = btn.colors;
            c.normalColor = bg;
            c.highlightedColor = Color.Lerp(bg, Color.white, 0.18f);
            c.pressedColor = Color.Lerp(bg, Color.black, 0.12f);
            c.selectedColor = bg;
            c.fadeDuration = 0.1f;
            btn.colors = c;
        }

        private void AddRowLabel(RectTransform hbox, string text)
        {
            var tmp = _f.MakeText("Tag", hbox, text, _t.FontSizeSmall, _t.TextMuted, _t.Font, TextAlignmentOptions.MidlineRight);
            tmp.fontStyle = FontStyles.Italic; tmp.raycastTarget = false; tmp.enableWordWrapping = false;
            tmp.gameObject.AddComponent<LayoutElement>().preferredWidth = 130f;
        }

        /// <summary>TMP_InputField minimal (fond ghost + placeholder), monté entièrement en code.</summary>
        private TMP_InputField MakeInput(RectTransform parent, string placeholder, out RectTransform rt)
        {
            var bg = _f.MakeImage("Input", parent, _t.ButtonGhostBg);
            rt = bg.rectTransform;
            var input = bg.gameObject.AddComponent<TMP_InputField>();
            input.lineType = TMP_InputField.LineType.SingleLine;

            var area = _f.MakeRect("TextArea", rt);
            HubMenuUIFactory.Stretch(area, 14f, 14f, 6f, 6f);
            area.gameObject.AddComponent<RectMask2D>();

            var ph = _f.MakeText("Placeholder", area, placeholder, _t.FontSizeBody, _t.TextMuted, _t.Font, TextAlignmentOptions.MidlineLeft);
            HubMenuUIFactory.Stretch(ph.rectTransform);
            ph.fontStyle = FontStyles.Italic; ph.enableWordWrapping = false;

            var txt = _f.MakeText("Text", area, "", _t.FontSizeBody, _t.TextPrimary, _t.Font, TextAlignmentOptions.MidlineLeft);
            HubMenuUIFactory.Stretch(txt.rectTransform);
            txt.enableWordWrapping = false;

            input.textViewport = area;
            input.textComponent = txt;
            input.placeholder = ph;
            input.fontAsset = txt.font;
            input.pointSize = _t.FontSizeBody;
            return input;
        }

        private static string RoleLabel(string role)
        {
            switch (role)
            {
                case "Leader": return "Chef";
                case "Officer": return "Officier";
                case "Member": return "Membre";
                case "Recruit": return "Recrue";
                default: return role;
            }
        }

        private bool EnsureToken()
        {
            string token = HubChatClient.Instance?.DevToken;
            if (_api == null || string.IsNullOrEmpty(token)) return false;
            _api.SetBearerToken(token);
            return true;
        }

        private void SetStatus(string s) { if (_status != null) _status.text = s; }

        // ===== Tooltip survol (rond online) =====

        private void BuildTooltip(RectTransform parent)
        {
            var panel = _f.MakePanel(parent, new Color(0.05f, 0.05f, 0.07f, 0.98f));
            panel.raycastTarget = false;
            var rt = panel.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(0f, 0f); rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(0f, 0f);
            var cg = panel.gameObject.AddComponent<CanvasGroup>(); cg.blocksRaycasts = false; cg.interactable = false;
            var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 8, 8); vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;
            var fit = panel.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize; fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            _tooltipText = _f.MakeText("TT", panel.rectTransform, "", _t.FontSizeSmall, _t.TextPrimary, _t.Font, TextAlignmentOptions.Center);
            _tooltipText.raycastTarget = false; _tooltipText.enableWordWrapping = false;

            panel.gameObject.SetActive(false);
            _tooltipGo = panel.gameObject;
        }

        private void ShowTooltip(string text)
        {
            if (_tooltipGo == null) return;
            _tooltipText.text = text;
            _tooltipGo.SetActive(true);
            _tooltipGo.transform.SetAsLastSibling();
            var rt = (RectTransform)_tooltipGo.transform;
            Vector3 pos = Input.mousePosition;
            bool flip = pos.x + 160f > Screen.width;
            rt.pivot = flip ? new Vector2(1f, 0f) : new Vector2(0f, 0f);
            float dx = flip ? -14f : 14f;
            _tooltipGo.transform.position = new Vector3(pos.x + dx, pos.y + 14f, 0f);
        }

        private void HideTooltip()
        {
            if (_tooltipGo != null) _tooltipGo.SetActive(false);
        }

        // Sprite cercle plein (blanc, teinté via Image.color), généré une fois et caché.
        private static Sprite _circleSprite;
        private static Sprite CircleSprite()
        {
            if (_circleSprite != null) return _circleSprite;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float c = (size - 1) * 0.5f;
            float radius = size * 0.5f - 1f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(radius - d + 0.5f) * 255f); // AA 1px
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px); tex.Apply();
            _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            _circleSprite.name = "FilledCircle";
            return _circleSprite;
        }

        // Petit composant de cycle de vie : déclenche un callback au Destroy du holder
        // (la zone Social est détruite par HubMenuShell.ShowScreen au changement d'écran).
        private sealed class DestroyNotifier : MonoBehaviour
        {
            public Action OnDestroyed;
            private void OnDestroy() { OnDestroyed?.Invoke(); }
        }

        // Survol du rond online : affiche "Connecté"/"Déconnecté" selon la couleur courante
        // du rond (verte = online), donc le tooltip reflète l'état même après un update live.
        private sealed class OnlineDotProxy : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            private Image _dot;
            private Action<string> _show;
            private Action _hide;

            public void Init(Image dot, Action<string> show, Action hide) { _dot = dot; _show = show; _hide = hide; }

            public void OnPointerEnter(PointerEventData _)
            {
                bool online = _dot != null && _dot.color == OnlineColor;
                _show?.Invoke(online ? "Connecté" : "Déconnecté");
            }

            public void OnPointerExit(PointerEventData _) => _hide?.Invoke();
        }
    }
}
