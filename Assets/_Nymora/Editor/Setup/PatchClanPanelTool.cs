using System.Collections.Generic;
using Nymora.Hub;
using Nymora.Network.Backend;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Brique 4.11 — Patch idempotent de 10_CommunityHub pour le systeme clans :
    ///   1. Bouton "Clan" en bas-droite (a gauche d'Amis, anchored -400)
    ///   2. ClanPanelHost avec hierarchie (NoClanMode + InClanMode)
    ///   3. ClanInvitePopupHost avec popup d'acceptation
    ///   4. Wire de toutes les refs SerializedField
    ///
    /// Menu : Nymora > Setup > Patch Clan Panel
    /// </summary>
    public static class PatchClanPanelTool
    {
        private const string ScenePath = "Assets/_Nymora/Scenes/10_CommunityHub.unity";
        private const string BackendSettingsPath = "Assets/_Nymora/Settings/NymoraBackendSettings.asset";

        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.65f);
        private static readonly Color ContainerColor = new Color(0.12f, 0.13f, 0.16f, 0.98f);
        private static readonly Color HeaderColor = new Color(0.18f, 0.20f, 0.25f, 1f);
        private static readonly Color CloseButtonColor = new Color(0.55f, 0.25f, 0.25f, 1f);
        private static readonly Color ClanButtonColor = new Color(0.25f, 0.35f, 0.55f, 1f);
        private static readonly Color CreateButtonColor = new Color(0.25f, 0.55f, 0.35f, 1f);
        private static readonly Color SendButtonColor = new Color(0.30f, 0.45f, 0.30f, 1f);
        private static readonly Color LeaveButtonColor = new Color(0.50f, 0.30f, 0.30f, 1f);
        private static readonly Color DisbandButtonColor = new Color(0.65f, 0.20f, 0.20f, 1f);
        private static readonly Color BadgeColor = new Color(0.85f, 0.25f, 0.25f, 1f);
        private static readonly Color BannerDefaultColor = new Color(0.36f, 0.48f, 0.65f, 1f);
        private static readonly Color SectionLabelColor = new Color(0.85f, 0.85f, 0.9f, 1f);
        private static readonly Color InputBgColor = new Color(0.20f, 0.22f, 0.27f, 1f);
        private static readonly Color PopupBgColor = new Color(0.14f, 0.14f, 0.17f, 0.98f);
        private static readonly Color AcceptColor = new Color(0.25f, 0.55f, 0.35f, 1f);
        private static readonly Color RefuseColor = new Color(0.55f, 0.25f, 0.25f, 1f);

        [MenuItem("Nymora/Setup/Patch Clan Panel", priority = 37)]
        private static void Patch()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Patch Clan Panel",
                    "Impossible de patcher pendant Play Mode.", "OK");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.path.EndsWith("10_CommunityHub.unity"))
            {
                if (!EditorUtility.DisplayDialog("Patch Clan Panel",
                    $"Scene active : {scene.path}\nAttendu : {ScenePath}\n\nOuvrir 10_CommunityHub ?",
                    "Ouvrir", "Annuler"))
                {
                    return;
                }
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("Patch Clan Panel", "Aucun Canvas trouvé.", "OK");
                return;
            }

            var settings = AssetDatabase.LoadAssetAtPath<NymoraBackendSettings>(BackendSettingsPath);
            if (settings == null)
            {
                EditorUtility.DisplayDialog("Patch Clan Panel",
                    $"NymoraBackendSettings introuvable à {BackendSettingsPath}.", "OK");
                return;
            }

            var actions = new List<string>();
            EnsureClanButton(canvas, actions);
            EnsureClanPanelHost(scene, canvas, settings, actions);
            EnsureIncomingPopup(scene, canvas, settings, actions);

            EditorSceneManager.MarkSceneDirty(scene);

            string summary = actions.Count == 0
                ? "OK Scene déjà à jour."
                : "Patch appliqué :\n\n" + string.Join("\n", actions) + "\n\nN'oublie pas Ctrl+S.";
            Debug.Log($"[Nymora.Setup] Patch clan panel : {actions.Count} action(s)\n{summary}");
            EditorUtility.DisplayDialog("Patch Clan Panel", summary, "OK");
        }

        // -----------------------------------------------------------------------------
        // Section 1 : Bouton "Clan" (anchored -400, 20)
        // -----------------------------------------------------------------------------
        private static void EnsureClanButton(Canvas canvas, List<string> actions)
        {
            var existing = canvas.transform.Find("ClanButton");
            if (existing != null)
            {
                if (existing.GetComponent<HubClanButton>() == null)
                {
                    existing.gameObject.AddComponent<HubClanButton>();
                    actions.Add("+ HubClanButton component ajouté");
                }
                return;
            }

            var go = new GameObject("ClanButton",
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(HubClanButton));
            go.transform.SetParent(canvas.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-400f, 20f);
            rt.sizeDelta = new Vector2(180f, 56f);
            go.GetComponent<Image>().color = ClanButtonColor;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero; labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero; labelRt.offsetMax = Vector2.zero;
            var labelTmp = labelGo.GetComponent<TextMeshProUGUI>();
            labelTmp.text = "Clan";
            labelTmp.fontSize = 26;
            labelTmp.color = Color.white;
            labelTmp.alignment = TextAlignmentOptions.Center;
            labelTmp.fontStyle = FontStyles.Bold;

            // Badge
            var badge = new GameObject("Badge", typeof(RectTransform), typeof(Image));
            badge.transform.SetParent(go.transform, false);
            var bRt = badge.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(1f, 1f); bRt.anchorMax = new Vector2(1f, 1f);
            bRt.pivot = new Vector2(1f, 1f);
            bRt.anchoredPosition = new Vector2(4f, 4f);
            bRt.sizeDelta = new Vector2(28f, 28f);
            badge.GetComponent<Image>().color = BadgeColor;

            var badgeTextGo = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
            badgeTextGo.transform.SetParent(badge.transform, false);
            var btRt = badgeTextGo.GetComponent<RectTransform>();
            btRt.anchorMin = Vector2.zero; btRt.anchorMax = Vector2.one;
            btRt.offsetMin = Vector2.zero; btRt.offsetMax = Vector2.zero;
            var btTmp = badgeTextGo.GetComponent<TextMeshProUGUI>();
            btTmp.text = "0"; btTmp.fontSize = 18; btTmp.color = Color.white;
            btTmp.alignment = TextAlignmentOptions.Center; btTmp.fontStyle = FontStyles.Bold;

            badge.SetActive(false);

            var btnScript = go.GetComponent<HubClanButton>();
            var so = new SerializedObject(btnScript);
            so.FindProperty("_badge").objectReferenceValue = badge;
            so.FindProperty("_badgeText").objectReferenceValue = btTmp;
            so.ApplyModifiedPropertiesWithoutUndo();

            actions.Add("+ Bouton 'ClanButton' créé (bas-droite, anchor -400)");
        }

        // -----------------------------------------------------------------------------
        // Section 2 : ClanPanelHost
        // -----------------------------------------------------------------------------
        private static void EnsureClanPanelHost(Scene scene, Canvas canvas, NymoraBackendSettings settings, List<string> actions)
        {
            var existing = canvas.transform.Find("ClanPanelHost");
            HubClanPanel panel;
            GameObject hostGo;
            bool justCreated = false;

            if (existing == null)
            {
                hostGo = new GameObject("ClanPanelHost", typeof(RectTransform), typeof(HubClanPanel));
                hostGo.transform.SetParent(canvas.transform, false);
                var hostRt = hostGo.GetComponent<RectTransform>();
                hostRt.anchorMin = Vector2.zero; hostRt.anchorMax = Vector2.one;
                hostRt.offsetMin = Vector2.zero; hostRt.offsetMax = Vector2.zero;
                panel = hostGo.GetComponent<HubClanPanel>();
                actions.Add("+ GO 'ClanPanelHost' créé");
                justCreated = true;
            }
            else
            {
                hostGo = existing.gameObject;
                panel = hostGo.GetComponent<HubClanPanel>();
                if (panel == null)
                {
                    panel = hostGo.AddComponent<HubClanPanel>();
                    actions.Add("+ HubClanPanel component ajouté");
                }
            }

            var refs = BuildPanelHierarchy(hostGo, justCreated, actions);

            var so = new SerializedObject(panel);
            TryWire(so, "_backendSettings", settings, actions, "HubClanPanel._backendSettings");
            TryWire(so, "_panelRoot", refs.PanelRoot, actions, "HubClanPanel._panelRoot");
            TryWire(so, "_closeButton", refs.CloseButton, actions, "HubClanPanel._closeButton");
            TryWire(so, "_statusText", refs.StatusText, actions, "HubClanPanel._statusText");
            TryWire(so, "_noClanModeRoot", refs.NoClanModeRoot, actions, "HubClanPanel._noClanModeRoot");
            TryWire(so, "_createNameInput", refs.CreateNameInput, actions, "HubClanPanel._createNameInput");
            TryWire(so, "_createClanButton", refs.CreateClanButton, actions, "HubClanPanel._createClanButton");
            TryWire(so, "_invitesContainer", refs.InvitesContainer, actions, "HubClanPanel._invitesContainer");
            TryWire(so, "_invitesCountLabel", refs.InvitesCountLabel, actions, "HubClanPanel._invitesCountLabel");
            TryWire(so, "_inClanModeRoot", refs.InClanModeRoot, actions, "HubClanPanel._inClanModeRoot");
            TryWire(so, "_clanBanner", refs.ClanBanner, actions, "HubClanPanel._clanBanner");
            TryWire(so, "_clanNameLabel", refs.ClanNameLabel, actions, "HubClanPanel._clanNameLabel");
            TryWire(so, "_clanDescriptionLabel", refs.ClanDescriptionLabel, actions, "HubClanPanel._clanDescriptionLabel");
            TryWire(so, "_membersContainer", refs.MembersContainer, actions, "HubClanPanel._membersContainer");
            TryWire(so, "_membersCountLabel", refs.MembersCountLabel, actions, "HubClanPanel._membersCountLabel");
            TryWire(so, "_inviteRowRoot", refs.InviteRowRoot, actions, "HubClanPanel._inviteRowRoot");
            TryWire(so, "_inviteInput", refs.InviteInput, actions, "HubClanPanel._inviteInput");
            TryWire(so, "_inviteSendButton", refs.InviteSendButton, actions, "HubClanPanel._inviteSendButton");
            TryWire(so, "_leaveButton", refs.LeaveButton, actions, "HubClanPanel._leaveButton");
            TryWire(so, "_disbandButton", refs.DisbandButton, actions, "HubClanPanel._disbandButton");
            so.ApplyModifiedPropertiesWithoutUndo();

            if (justCreated && refs.PanelRoot != null) refs.PanelRoot.SetActive(false);
        }

        private sealed class PanelRefs
        {
            public GameObject PanelRoot;
            public Button CloseButton;
            public TextMeshProUGUI StatusText;
            public GameObject NoClanModeRoot;
            public TMP_InputField CreateNameInput;
            public Button CreateClanButton;
            public RectTransform InvitesContainer;
            public TextMeshProUGUI InvitesCountLabel;
            public GameObject InClanModeRoot;
            public Image ClanBanner;
            public TextMeshProUGUI ClanNameLabel, ClanDescriptionLabel;
            public RectTransform MembersContainer;
            public TextMeshProUGUI MembersCountLabel;
            public GameObject InviteRowRoot;
            public TMP_InputField InviteInput;
            public Button InviteSendButton, LeaveButton, DisbandButton;
        }

        private static PanelRefs BuildPanelHierarchy(GameObject host, bool fresh, List<string> actions)
        {
            var refs = new PanelRefs();

            var panelRoot = FindOrCreateChild(host.transform, "PanelRoot", out bool createdRoot);
            refs.PanelRoot = panelRoot;
            if (createdRoot)
            {
                var rt = EnsureRect(panelRoot);
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            }

            var backdrop = FindOrCreateChild(panelRoot.transform, "Backdrop", out bool createdBackdrop, typeof(Image));
            if (createdBackdrop)
            {
                var rt = EnsureRect(backdrop);
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                backdrop.GetComponent<Image>().color = BackdropColor;
            }

            var container = FindOrCreateChild(panelRoot.transform, "Container", out bool createdContainer, typeof(Image));
            if (createdContainer)
            {
                var rt = EnsureRect(container);
                rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(820f, 620f);
                container.GetComponent<Image>().color = ContainerColor;
            }

            // Header
            var header = FindOrCreateChild(container.transform, "Header", out bool createdHeader, typeof(Image));
            if (createdHeader)
            {
                var rt = EnsureRect(header);
                rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(0f, 64f);
                header.GetComponent<Image>().color = HeaderColor;
            }
            BuildHeaderTitleAndClose(header, "Clan", out var closeBtn);
            refs.CloseButton = closeBtn;

            // StatusText sous header
            var statusGo = FindOrCreateChild(container.transform, "StatusText", out bool createdStatus, typeof(TextMeshProUGUI));
            refs.StatusText = statusGo.GetComponent<TextMeshProUGUI>();
            if (createdStatus)
            {
                var rt = EnsureRect(statusGo);
                rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -68f);
                rt.sizeDelta = new Vector2(-32f, 24f);
                refs.StatusText.fontSize = 16;
                refs.StatusText.color = new Color(0.9f, 0.85f, 0.55f, 1f);
                refs.StatusText.alignment = TextAlignmentOptions.MidlineLeft;
                refs.StatusText.fontStyle = FontStyles.Italic;
            }

            // ContentArea
            var content = FindOrCreateChild(container.transform, "ContentArea", out bool createdContent);
            if (createdContent)
            {
                var rt = EnsureRect(content);
                rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 1f);
                rt.offsetMin = new Vector2(16f, 16f);
                rt.offsetMax = new Vector2(-16f, -96f);
            }

            // NoClan mode
            refs.NoClanModeRoot = FindOrCreateChild(content.transform, "NoClanMode", out bool createdNoClan, typeof(VerticalLayoutGroup));
            if (createdNoClan)
            {
                var rt = EnsureRect(refs.NoClanModeRoot);
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                var vl = refs.NoClanModeRoot.GetComponent<VerticalLayoutGroup>();
                vl.spacing = 12;
                vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
                vl.childControlWidth = true; vl.childControlHeight = true;
            }
            BuildNoClanMode(refs.NoClanModeRoot.transform, refs);

            // InClan mode
            refs.InClanModeRoot = FindOrCreateChild(content.transform, "InClanMode", out bool createdInClan, typeof(VerticalLayoutGroup));
            if (createdInClan)
            {
                var rt = EnsureRect(refs.InClanModeRoot);
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                var vl = refs.InClanModeRoot.GetComponent<VerticalLayoutGroup>();
                vl.spacing = 10;
                vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
                vl.childControlWidth = true; vl.childControlHeight = true;
            }
            BuildInClanMode(refs.InClanModeRoot.transform, refs);

            if (fresh)
            {
                refs.NoClanModeRoot.SetActive(true);
                refs.InClanModeRoot.SetActive(false);
            }

            return refs;
        }

        private static void BuildNoClanMode(Transform parent, PanelRefs refs)
        {
            // Section "Creer un clan"
            var createSection = FindOrCreateChild(parent, "CreateSection", out bool _, typeof(LayoutElement));
            createSection.GetComponent<LayoutElement>().preferredHeight = 56f;
            var createSectionRt = EnsureRect(createSection);

            var createLabel = FindOrCreateChild(createSection.transform, "Label", out bool _2, typeof(TextMeshProUGUI));
            var labelRt = EnsureRect(createLabel);
            labelRt.anchorMin = new Vector2(0f, 0f); labelRt.anchorMax = new Vector2(0f, 1f);
            labelRt.pivot = new Vector2(0f, 0.5f);
            labelRt.anchoredPosition = new Vector2(0f, 0f);
            labelRt.sizeDelta = new Vector2(200f, 0f);
            var labelTmp = createLabel.GetComponent<TextMeshProUGUI>();
            labelTmp.text = "Créer un clan :";
            labelTmp.fontSize = 18; labelTmp.color = Color.white;
            labelTmp.alignment = TextAlignmentOptions.MidlineLeft;

            // Input nom
            var inputGo = FindOrCreateChild(createSection.transform, "NameInput", out bool createdInput, typeof(Image), typeof(TMP_InputField));
            refs.CreateNameInput = inputGo.GetComponent<TMP_InputField>();
            if (createdInput)
            {
                var rt = EnsureRect(inputGo);
                rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.offsetMin = new Vector2(210f, 8f); rt.offsetMax = new Vector2(-160f, -8f);
                inputGo.GetComponent<Image>().color = InputBgColor;
                BuildInputField(inputGo, refs.CreateNameInput, "Nom du clan (3-32)");
            }

            // Bouton Créer
            var createBtnGo = FindOrCreateChild(createSection.transform, "CreateButton", out bool createdBtn, typeof(Image), typeof(Button));
            refs.CreateClanButton = createBtnGo.GetComponent<Button>();
            if (createdBtn)
            {
                var rt = EnsureRect(createBtnGo);
                rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, 0f);
                rt.sizeDelta = new Vector2(150f, -16f);
                createBtnGo.GetComponent<Image>().color = CreateButtonColor;
                AddSimpleLabel(createBtnGo.transform, "Créer", 18, Color.white);
            }

            // Section invitations
            var invLabelGo = FindOrCreateChild(parent, "InvitesLabel", out bool _3, typeof(TextMeshProUGUI), typeof(LayoutElement));
            refs.InvitesCountLabel = invLabelGo.GetComponent<TextMeshProUGUI>();
            refs.InvitesCountLabel.text = "Invitations reçues (0)";
            refs.InvitesCountLabel.fontSize = 20; refs.InvitesCountLabel.color = SectionLabelColor;
            refs.InvitesCountLabel.alignment = TextAlignmentOptions.MidlineLeft;
            refs.InvitesCountLabel.fontStyle = FontStyles.Bold;
            invLabelGo.GetComponent<LayoutElement>().preferredHeight = 28f;

            var invContainer = FindOrCreateChild(parent, "InvitesContainer", out bool _4, typeof(VerticalLayoutGroup), typeof(LayoutElement));
            refs.InvitesContainer = invContainer.GetComponent<RectTransform>();
            var invVl = invContainer.GetComponent<VerticalLayoutGroup>();
            invVl.spacing = 4;
            invVl.childForceExpandWidth = true; invVl.childForceExpandHeight = false;
            invVl.childControlWidth = true; invVl.childControlHeight = true;
            invContainer.GetComponent<LayoutElement>().flexibleWidth = 1f;
        }

        private static void BuildInClanMode(Transform parent, PanelRefs refs)
        {
            // Banner (color rect)
            var bannerGo = FindOrCreateChild(parent, "Banner", out bool createdBanner, typeof(Image), typeof(LayoutElement));
            refs.ClanBanner = bannerGo.GetComponent<Image>();
            if (createdBanner)
            {
                refs.ClanBanner.color = BannerDefaultColor;
                bannerGo.GetComponent<LayoutElement>().preferredHeight = 36f;
            }

            // Nom du clan
            var nameGo = FindOrCreateChild(parent, "ClanName", out bool _, typeof(TextMeshProUGUI), typeof(LayoutElement));
            refs.ClanNameLabel = nameGo.GetComponent<TextMeshProUGUI>();
            refs.ClanNameLabel.text = "Nom du clan";
            refs.ClanNameLabel.fontSize = 28; refs.ClanNameLabel.color = Color.white;
            refs.ClanNameLabel.alignment = TextAlignmentOptions.MidlineLeft;
            refs.ClanNameLabel.fontStyle = FontStyles.Bold;
            nameGo.GetComponent<LayoutElement>().preferredHeight = 40f;

            // Description
            var descGo = FindOrCreateChild(parent, "ClanDesc", out bool _1, typeof(TextMeshProUGUI), typeof(LayoutElement));
            refs.ClanDescriptionLabel = descGo.GetComponent<TextMeshProUGUI>();
            refs.ClanDescriptionLabel.fontSize = 16; refs.ClanDescriptionLabel.color = new Color(0.75f, 0.75f, 0.8f);
            refs.ClanDescriptionLabel.alignment = TextAlignmentOptions.MidlineLeft;
            refs.ClanDescriptionLabel.fontStyle = FontStyles.Italic;
            descGo.GetComponent<LayoutElement>().preferredHeight = 24f;

            // Invite row (chef/officier seulement)
            var inviteRow = FindOrCreateChild(parent, "InviteRow", out bool _2, typeof(LayoutElement));
            inviteRow.GetComponent<LayoutElement>().preferredHeight = 48f;
            refs.InviteRowRoot = inviteRow;
            var inviteRowRt = EnsureRect(inviteRow);

            var inviteInputGo = FindOrCreateChild(inviteRow.transform, "InviteInput", out bool createdInviteInput, typeof(Image), typeof(TMP_InputField));
            refs.InviteInput = inviteInputGo.GetComponent<TMP_InputField>();
            if (createdInviteInput)
            {
                var rt = EnsureRect(inviteInputGo);
                rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.offsetMin = new Vector2(0f, 4f); rt.offsetMax = new Vector2(-160f, -4f);
                inviteInputGo.GetComponent<Image>().color = InputBgColor;
                BuildInputField(inviteInputGo, refs.InviteInput, "Pseudo à inviter");
            }

            var inviteSendGo = FindOrCreateChild(inviteRow.transform, "InviteSend", out bool createdInviteSend, typeof(Image), typeof(Button));
            refs.InviteSendButton = inviteSendGo.GetComponent<Button>();
            if (createdInviteSend)
            {
                var rt = EnsureRect(inviteSendGo);
                rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, 0f);
                rt.sizeDelta = new Vector2(150f, -8f);
                inviteSendGo.GetComponent<Image>().color = SendButtonColor;
                AddSimpleLabel(inviteSendGo.transform, "Inviter", 16, Color.white);
            }

            // Members section
            var memberLabelGo = FindOrCreateChild(parent, "MembersLabel", out bool _3, typeof(TextMeshProUGUI), typeof(LayoutElement));
            refs.MembersCountLabel = memberLabelGo.GetComponent<TextMeshProUGUI>();
            refs.MembersCountLabel.text = "Membres (0)";
            refs.MembersCountLabel.fontSize = 20; refs.MembersCountLabel.color = SectionLabelColor;
            refs.MembersCountLabel.alignment = TextAlignmentOptions.MidlineLeft;
            refs.MembersCountLabel.fontStyle = FontStyles.Bold;
            memberLabelGo.GetComponent<LayoutElement>().preferredHeight = 28f;

            var membersContainer = FindOrCreateChild(parent, "MembersContainer", out bool _4, typeof(VerticalLayoutGroup), typeof(LayoutElement));
            refs.MembersContainer = membersContainer.GetComponent<RectTransform>();
            var memVl = membersContainer.GetComponent<VerticalLayoutGroup>();
            memVl.spacing = 4;
            memVl.childForceExpandWidth = true; memVl.childForceExpandHeight = false;
            memVl.childControlWidth = true; memVl.childControlHeight = true;
            membersContainer.GetComponent<LayoutElement>().flexibleWidth = 1f;

            // Bottom buttons row
            var bottomRow = FindOrCreateChild(parent, "BottomRow", out bool createdBottom, typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            if (createdBottom)
            {
                var hl = bottomRow.GetComponent<HorizontalLayoutGroup>();
                hl.spacing = 8;
                hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;
                hl.childControlWidth = true; hl.childControlHeight = true;
                hl.childAlignment = TextAnchor.MiddleRight;
                bottomRow.GetComponent<LayoutElement>().preferredHeight = 48f;
            }

            var leaveGo = FindOrCreateChild(bottomRow.transform, "LeaveButton", out bool createdLeave, typeof(Image), typeof(Button), typeof(LayoutElement));
            refs.LeaveButton = leaveGo.GetComponent<Button>();
            if (createdLeave)
            {
                leaveGo.GetComponent<Image>().color = LeaveButtonColor;
                leaveGo.GetComponent<LayoutElement>().preferredWidth = 160f;
                AddSimpleLabel(leaveGo.transform, "Quitter", 16, Color.white);
            }

            var disbandGo = FindOrCreateChild(bottomRow.transform, "DisbandButton", out bool createdDisband, typeof(Image), typeof(Button), typeof(LayoutElement));
            refs.DisbandButton = disbandGo.GetComponent<Button>();
            if (createdDisband)
            {
                disbandGo.GetComponent<Image>().color = DisbandButtonColor;
                disbandGo.GetComponent<LayoutElement>().preferredWidth = 200f;
                AddSimpleLabel(disbandGo.transform, "Dissoudre le clan", 16, Color.white);
            }
        }

        // -----------------------------------------------------------------------------
        // Section 3 : IncomingClanInvitePopup
        // -----------------------------------------------------------------------------
        private static void EnsureIncomingPopup(Scene scene, Canvas canvas, NymoraBackendSettings settings, List<string> actions)
        {
            var existing = canvas.transform.Find("ClanInvitePopupHost");
            IncomingClanInvitePopup popup;
            GameObject hostGo;

            if (existing == null)
            {
                hostGo = new GameObject("ClanInvitePopupHost",
                    typeof(RectTransform), typeof(IncomingClanInvitePopup));
                hostGo.transform.SetParent(canvas.transform, false);
                var hostRt = hostGo.GetComponent<RectTransform>();
                hostRt.anchorMin = Vector2.zero; hostRt.anchorMax = Vector2.one;
                hostRt.offsetMin = Vector2.zero; hostRt.offsetMax = Vector2.zero;
                popup = hostGo.GetComponent<IncomingClanInvitePopup>();
                actions.Add("+ GO 'ClanInvitePopupHost' créé");
            }
            else
            {
                hostGo = existing.gameObject;
                popup = hostGo.GetComponent<IncomingClanInvitePopup>();
                if (popup == null) popup = hostGo.AddComponent<IncomingClanInvitePopup>();
            }

            var panel = FindOrCreateChild(hostGo.transform, "Panel", out bool createdPanel, typeof(Image), typeof(VerticalLayoutGroup));
            if (createdPanel)
            {
                var rt = EnsureRect(panel);
                rt.anchorMin = new Vector2(1f, 1f); rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-20f, -200f);
                rt.sizeDelta = new Vector2(340f, 200f);
                panel.GetComponent<Image>().color = PopupBgColor;
                var vl = panel.GetComponent<VerticalLayoutGroup>();
                vl.padding = new RectOffset(16, 16, 16, 16);
                vl.spacing = 12;
                vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
                vl.childControlWidth = true; vl.childControlHeight = true;
            }

            var bannerGo = FindOrCreateChild(panel.transform, "BannerPreview", out bool _, typeof(Image), typeof(LayoutElement));
            var bannerImg = bannerGo.GetComponent<Image>();
            bannerImg.color = BannerDefaultColor;
            bannerGo.GetComponent<LayoutElement>().preferredHeight = 24f;

            var labelGo = FindOrCreateChild(panel.transform, "Label", out bool _1, typeof(TextMeshProUGUI), typeof(LayoutElement));
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.text = "(en attente)";
            label.fontSize = 16; label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = true;
            label.richText = true;
            labelGo.GetComponent<LayoutElement>().preferredHeight = 60f;

            var btnRow = FindOrCreateChild(panel.transform, "ButtonRow", out bool _2, typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            var hl = btnRow.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = 8;
            hl.childForceExpandWidth = true; hl.childForceExpandHeight = true;
            hl.childControlWidth = true; hl.childControlHeight = true;
            btnRow.GetComponent<LayoutElement>().preferredHeight = 44f;

            var acceptBtn = EnsureSimpleButton(btnRow.transform, "AcceptButton", "Accepter", AcceptColor);
            var refuseBtn = EnsureSimpleButton(btnRow.transform, "RefuseButton", "Refuser", RefuseColor);

            var so = new SerializedObject(popup);
            TryWire(so, "_backendSettings", settings, actions, "IncomingClanInvitePopup._backendSettings");
            TryWire(so, "_panel", panel, actions, "IncomingClanInvitePopup._panel");
            TryWire(so, "_bannerPreview", bannerImg, actions, "IncomingClanInvitePopup._bannerPreview");
            TryWire(so, "_label", label, actions, "IncomingClanInvitePopup._label");
            TryWire(so, "_acceptButton", acceptBtn, actions, "IncomingClanInvitePopup._acceptButton");
            TryWire(so, "_refuseButton", refuseBtn, actions, "IncomingClanInvitePopup._refuseButton");
            so.ApplyModifiedPropertiesWithoutUndo();

            panel.SetActive(false);
        }

        // -----------------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------------

        private static void BuildHeaderTitleAndClose(GameObject header, string title, out Button closeBtn)
        {
            var titleGo = FindOrCreateChild(header.transform, "Title", out bool _, typeof(TextMeshProUGUI));
            var rt = EnsureRect(titleGo);
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(24f, 0f); rt.offsetMax = new Vector2(-84f, 0f);
            var tmp = titleGo.GetComponent<TextMeshProUGUI>();
            tmp.text = title;
            tmp.fontSize = 32; tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.fontStyle = FontStyles.Bold;

            var closeGo = FindOrCreateChild(header.transform, "CloseButton", out bool createdClose, typeof(Image), typeof(Button));
            closeBtn = closeGo.GetComponent<Button>();
            if (createdClose)
            {
                var crt = EnsureRect(closeGo);
                crt.anchorMin = new Vector2(1f, 0.5f); crt.anchorMax = new Vector2(1f, 0.5f);
                crt.pivot = new Vector2(1f, 0.5f);
                crt.anchoredPosition = new Vector2(-16f, 0f);
                crt.sizeDelta = new Vector2(48f, 40f);
                closeGo.GetComponent<Image>().color = CloseButtonColor;
                AddSimpleLabel(closeGo.transform, "X", 24, Color.white);
            }
        }

        private static void BuildInputField(GameObject inputGo, TMP_InputField input, string placeholderText)
        {
            var textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(inputGo.transform, false);
            var textAreaRt = textArea.GetComponent<RectTransform>();
            textAreaRt.anchorMin = Vector2.zero; textAreaRt.anchorMax = Vector2.one;
            textAreaRt.offsetMin = new Vector2(12f, 4f);
            textAreaRt.offsetMax = new Vector2(-12f, -4f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(textArea.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero; textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero; textRt.offsetMax = Vector2.zero;
            var textTmp = textGo.GetComponent<TextMeshProUGUI>();
            textTmp.fontSize = 18; textTmp.color = Color.white;
            textTmp.alignment = TextAlignmentOptions.MidlineLeft;

            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
            placeholderGo.transform.SetParent(textArea.transform, false);
            var phRt = placeholderGo.GetComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero; phRt.anchorMax = Vector2.one;
            phRt.offsetMin = Vector2.zero; phRt.offsetMax = Vector2.zero;
            var phTmp = placeholderGo.GetComponent<TextMeshProUGUI>();
            phTmp.text = placeholderText;
            phTmp.fontSize = 18; phTmp.color = new Color(0.6f, 0.6f, 0.65f);
            phTmp.alignment = TextAlignmentOptions.MidlineLeft;
            phTmp.fontStyle = FontStyles.Italic;

            input.textViewport = textAreaRt;
            input.textComponent = textTmp;
            input.placeholder = phTmp;
            input.fontAsset = textTmp.font;
            input.pointSize = 18;
        }

        private static void AddSimpleLabel(Transform parent, string text, int fontSize, Color color)
        {
            var lblGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            lblGo.transform.SetParent(parent, false);
            var rt = lblGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var tmp = lblGo.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize; tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
        }

        private static Button EnsureSimpleButton(Transform parent, string name, string label, Color bg)
        {
            var go = FindOrCreateChild(parent, name, out bool created, typeof(Image), typeof(Button), typeof(LayoutElement));
            if (created)
            {
                go.GetComponent<Image>().color = bg;
                go.GetComponent<LayoutElement>().flexibleWidth = 1f;
                AddSimpleLabel(go.transform, label, 18, Color.white);
            }
            return go.GetComponent<Button>();
        }

        private static GameObject FindOrCreateChild(Transform parent, string childName, out bool created, params System.Type[] components)
        {
            var existing = parent.Find(childName);
            if (existing != null) { created = false; return existing.gameObject; }
            var all = new List<System.Type> { typeof(RectTransform) };
            if (components != null) all.AddRange(components);
            var go = new GameObject(childName, all.ToArray());
            go.transform.SetParent(parent, false);
            created = true;
            return go;
        }

        private static RectTransform EnsureRect(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            return rt;
        }

        private static void TryWire(SerializedObject so, string propName, Object value, List<string> actions, string label)
        {
            if (value == null) return;
            var prop = so.FindProperty(propName);
            if (prop == null) return;
            if (prop.objectReferenceValue == value) return;
            if (prop.objectReferenceValue != null) return;
            prop.objectReferenceValue = value;
            actions.Add($"+ {label} wire");
        }
    }
}
