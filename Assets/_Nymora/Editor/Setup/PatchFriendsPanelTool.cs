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
    /// Brique 4.10 — Patch idempotent de la scène 10_CommunityHub pour ajouter le système amis :
    ///   1. Bouton "Amis" en bas-droite (à gauche de "Mon profil")
    ///   2. GO "FriendsPanelHost" avec hiérarchie complète (HubFriendsPanel + 3 sections + recherche)
    ///   3. GO "IncomingFriendRequestPopupHost" avec popup d'acceptation
    ///   4. Wire tous les SerializedField
    ///
    /// Menu : Nymora > Setup > Patch Friends Panel
    /// </summary>
    public static class PatchFriendsPanelTool
    {
        private const string ScenePath = "Assets/_Nymora/Scenes/10_CommunityHub.unity";
        private const string BackendSettingsPath = "Assets/_Nymora/Settings/NymoraBackendSettings.asset";

        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.65f);
        private static readonly Color ContainerColor = new Color(0.12f, 0.13f, 0.16f, 0.98f);
        private static readonly Color HeaderColor = new Color(0.18f, 0.20f, 0.25f, 1f);
        private static readonly Color CloseButtonColor = new Color(0.55f, 0.25f, 0.25f, 1f);
        private static readonly Color FriendsButtonColor = new Color(0.45f, 0.30f, 0.60f, 1f);
        private static readonly Color SearchButtonColor = new Color(0.25f, 0.55f, 0.35f, 1f);
        private static readonly Color SectionLabelColor = new Color(0.85f, 0.85f, 0.9f, 1f);
        private static readonly Color BadgeColor = new Color(0.85f, 0.25f, 0.25f, 1f);
        private static readonly Color PopupBgColor = new Color(0.14f, 0.14f, 0.17f, 0.98f);
        private static readonly Color AcceptColor = new Color(0.25f, 0.55f, 0.35f, 1f);
        private static readonly Color RefuseColor = new Color(0.55f, 0.25f, 0.25f, 1f);

        [MenuItem("Nymora/Setup/Patch Friends Panel", priority = 36)]
        private static void Patch()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Patch Friends Panel",
                    "Impossible de patcher pendant Play Mode.\nStoppe Play (Ctrl+P) puis relance ce menu.", "OK");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.path.EndsWith("10_CommunityHub.unity"))
            {
                if (!EditorUtility.DisplayDialog("Patch Friends Panel",
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
                EditorUtility.DisplayDialog("Patch Friends Panel",
                    "Aucun Canvas trouvé. Lance d'abord Create Community Hub Scene.", "OK");
                return;
            }

            var settings = AssetDatabase.LoadAssetAtPath<NymoraBackendSettings>(BackendSettingsPath);
            if (settings == null)
            {
                EditorUtility.DisplayDialog("Patch Friends Panel",
                    $"NymoraBackendSettings introuvable à {BackendSettingsPath}.", "OK");
                return;
            }

            var actions = new List<string>();
            EnsureFriendsButton(canvas, actions);
            var panel = EnsureFriendsPanelHost(scene, canvas, settings, actions);
            EnsureIncomingPopup(scene, canvas, actions);

            EditorSceneManager.MarkSceneDirty(scene);

            string summary = actions.Count == 0
                ? "OK Scene déjà à jour, rien à patcher."
                : "Patch appliqué :\n\n" + string.Join("\n", actions) + "\n\nN'oublie pas Ctrl+S pour sauvegarder la scène.";
            Debug.Log($"[Nymora.Setup] Patch friends panel : {actions.Count} action(s)\n{summary}");
            EditorUtility.DisplayDialog("Patch Friends Panel", summary, "OK");
        }

        // -----------------------------------------------------------------------------
        // Section 1 : Bouton "Amis" + badge
        // -----------------------------------------------------------------------------
        private static void EnsureFriendsButton(Canvas canvas, List<string> actions)
        {
            var existing = canvas.transform.Find("FriendsButton");
            if (existing != null)
            {
                if (existing.GetComponent<HubFriendsButton>() == null)
                {
                    existing.gameObject.AddComponent<HubFriendsButton>();
                    actions.Add("+ HubFriendsButton component ajouté");
                }
                return;
            }

            var go = new GameObject("FriendsButton",
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(HubFriendsButton));
            go.transform.SetParent(canvas.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-210f, 20f); // à gauche de MyProfileButton (-20, 20, w=180)
            rt.sizeDelta = new Vector2(180f, 56f);
            go.GetComponent<Image>().color = FriendsButtonColor;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var labelTmp = labelGo.GetComponent<TextMeshProUGUI>();
            labelTmp.text = "Amis";
            labelTmp.fontSize = 26;
            labelTmp.color = Color.white;
            labelTmp.alignment = TextAlignmentOptions.Center;
            labelTmp.fontStyle = FontStyles.Bold;

            // Badge rouge en haut-droite
            var badge = new GameObject("Badge", typeof(RectTransform), typeof(Image));
            badge.transform.SetParent(go.transform, false);
            var badgeRt = badge.GetComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(1f, 1f);
            badgeRt.anchorMax = new Vector2(1f, 1f);
            badgeRt.pivot = new Vector2(1f, 1f);
            badgeRt.anchoredPosition = new Vector2(4f, 4f);
            badgeRt.sizeDelta = new Vector2(28f, 28f);
            badge.GetComponent<Image>().color = BadgeColor;

            var badgeTextGo = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
            badgeTextGo.transform.SetParent(badge.transform, false);
            var badgeTextRt = badgeTextGo.GetComponent<RectTransform>();
            badgeTextRt.anchorMin = Vector2.zero;
            badgeTextRt.anchorMax = Vector2.one;
            badgeTextRt.offsetMin = Vector2.zero;
            badgeTextRt.offsetMax = Vector2.zero;
            var badgeTmp = badgeTextGo.GetComponent<TextMeshProUGUI>();
            badgeTmp.text = "0";
            badgeTmp.fontSize = 18;
            badgeTmp.color = Color.white;
            badgeTmp.alignment = TextAlignmentOptions.Center;
            badgeTmp.fontStyle = FontStyles.Bold;

            badge.SetActive(false);

            // Wire HubFriendsButton refs
            var btnScript = go.GetComponent<HubFriendsButton>();
            var so = new SerializedObject(btnScript);
            so.FindProperty("_badge").objectReferenceValue = badge;
            so.FindProperty("_badgeText").objectReferenceValue = badgeTmp;
            so.ApplyModifiedPropertiesWithoutUndo();

            actions.Add("+ Bouton 'FriendsButton' créé (bas-droite, à gauche de MyProfileButton) + badge");
        }

        // -----------------------------------------------------------------------------
        // Section 2 : FriendsPanelHost
        // -----------------------------------------------------------------------------
        private static HubFriendsPanel EnsureFriendsPanelHost(Scene scene, Canvas canvas, NymoraBackendSettings settings, List<string> actions)
        {
            var existing = canvas.transform.Find("FriendsPanelHost");
            HubFriendsPanel panel;
            GameObject hostGo;
            bool justCreated = false;

            if (existing == null)
            {
                hostGo = new GameObject("FriendsPanelHost", typeof(RectTransform), typeof(HubFriendsPanel));
                hostGo.transform.SetParent(canvas.transform, false);
                var hostRt = hostGo.GetComponent<RectTransform>();
                hostRt.anchorMin = Vector2.zero;
                hostRt.anchorMax = Vector2.one;
                hostRt.offsetMin = Vector2.zero;
                hostRt.offsetMax = Vector2.zero;
                panel = hostGo.GetComponent<HubFriendsPanel>();
                actions.Add("+ GO 'FriendsPanelHost' créé");
                justCreated = true;
            }
            else
            {
                hostGo = existing.gameObject;
                panel = hostGo.GetComponent<HubFriendsPanel>();
                if (panel == null)
                {
                    panel = hostGo.AddComponent<HubFriendsPanel>();
                    actions.Add("+ HubFriendsPanel component ajouté");
                }
            }

            var refs = BuildPanelHierarchy(hostGo, justCreated, actions);

            var so = new SerializedObject(panel);
            TryWire(so, "_backendSettings", settings, actions, "HubFriendsPanel._backendSettings");
            var chatUI = Object.FindFirstObjectByType<HubChatUI>();
            if (chatUI != null) TryWire(so, "_chatUI", chatUI, actions, "HubFriendsPanel._chatUI");
            TryWire(so, "_panelRoot", refs.PanelRoot, actions, "HubFriendsPanel._panelRoot");
            TryWire(so, "_closeButton", refs.CloseButton, actions, "HubFriendsPanel._closeButton");
            TryWire(so, "_searchInput", refs.SearchInput, actions, "HubFriendsPanel._searchInput");
            TryWire(so, "_searchButton", refs.SearchButton, actions, "HubFriendsPanel._searchButton");
            TryWire(so, "_statusText", refs.StatusText, actions, "HubFriendsPanel._statusText");
            TryWire(so, "_friendsContainer", refs.FriendsContainer, actions, "HubFriendsPanel._friendsContainer");
            TryWire(so, "_incomingContainer", refs.IncomingContainer, actions, "HubFriendsPanel._incomingContainer");
            TryWire(so, "_outgoingContainer", refs.OutgoingContainer, actions, "HubFriendsPanel._outgoingContainer");
            TryWire(so, "_friendsCountLabel", refs.FriendsCountLabel, actions, "HubFriendsPanel._friendsCountLabel");
            TryWire(so, "_incomingCountLabel", refs.IncomingCountLabel, actions, "HubFriendsPanel._incomingCountLabel");
            TryWire(so, "_outgoingCountLabel", refs.OutgoingCountLabel, actions, "HubFriendsPanel._outgoingCountLabel");
            so.ApplyModifiedPropertiesWithoutUndo();

            if (justCreated && refs.PanelRoot != null) refs.PanelRoot.SetActive(false);
            return panel;
        }

        private sealed class PanelRefs
        {
            public GameObject PanelRoot;
            public Button CloseButton, SearchButton;
            public TMP_InputField SearchInput;
            public TextMeshProUGUI StatusText;
            public RectTransform FriendsContainer, IncomingContainer, OutgoingContainer;
            public TextMeshProUGUI FriendsCountLabel, IncomingCountLabel, OutgoingCountLabel;
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
                actions.Add("+ FriendsPanel PanelRoot");
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
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(820f, 620f);
                container.GetComponent<Image>().color = ContainerColor;
                actions.Add("+ FriendsPanel Container 820x620");
            }

            // Header
            var header = FindOrCreateChild(container.transform, "Header", out bool createdHeader, typeof(Image));
            if (createdHeader)
            {
                var rt = EnsureRect(header);
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(0f, 64f);
                header.GetComponent<Image>().color = HeaderColor;
            }
            var titleGo = FindOrCreateChild(header.transform, "Title", out bool createdTitle, typeof(TextMeshProUGUI));
            if (createdTitle)
            {
                var rt = EnsureRect(titleGo);
                rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 1f);
                rt.offsetMin = new Vector2(24f, 0f); rt.offsetMax = new Vector2(-84f, 0f);
                var tmp = titleGo.GetComponent<TextMeshProUGUI>();
                tmp.text = "Amis";
                tmp.fontSize = 32; tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                tmp.fontStyle = FontStyles.Bold;
            }
            var closeGo = FindOrCreateChild(header.transform, "CloseButton", out bool createdClose, typeof(Image), typeof(Button));
            refs.CloseButton = closeGo.GetComponent<Button>();
            if (createdClose)
            {
                var rt = EnsureRect(closeGo);
                rt.anchorMin = new Vector2(1f, 0.5f); rt.anchorMax = new Vector2(1f, 0.5f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.anchoredPosition = new Vector2(-16f, 0f);
                rt.sizeDelta = new Vector2(48f, 40f);
                closeGo.GetComponent<Image>().color = CloseButtonColor;

                var lblGo = new GameObject("X", typeof(RectTransform), typeof(TextMeshProUGUI));
                lblGo.transform.SetParent(closeGo.transform, false);
                var lblRt = lblGo.GetComponent<RectTransform>();
                lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
                lblRt.offsetMin = Vector2.zero; lblRt.offsetMax = Vector2.zero;
                var lbl = lblGo.GetComponent<TextMeshProUGUI>();
                lbl.text = "X"; lbl.fontSize = 24; lbl.color = Color.white;
                lbl.alignment = TextAlignmentOptions.Center; lbl.fontStyle = FontStyles.Bold;
            }

            // SearchBar (sous Header)
            var searchBar = FindOrCreateChild(container.transform, "SearchBar", out bool createdSearch);
            if (createdSearch)
            {
                var rt = EnsureRect(searchBar);
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -64f);
                rt.sizeDelta = new Vector2(-32f, 48f);
                actions.Add("+ FriendsPanel SearchBar");
            }
            // Input field
            var inputGo = FindOrCreateChild(searchBar.transform, "Input", out bool createdInput, typeof(Image), typeof(TMP_InputField));
            refs.SearchInput = inputGo.GetComponent<TMP_InputField>();
            if (createdInput)
            {
                var rt = EnsureRect(inputGo);
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.offsetMin = new Vector2(0f, 0f);
                rt.offsetMax = new Vector2(-160f, 0f);
                inputGo.GetComponent<Image>().color = new Color(0.20f, 0.22f, 0.27f, 1f);

                // Text area
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
                textTmp.fontSize = 20;
                textTmp.color = Color.white;
                textTmp.alignment = TextAlignmentOptions.MidlineLeft;

                var placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
                placeholderGo.transform.SetParent(textArea.transform, false);
                var phRt = placeholderGo.GetComponent<RectTransform>();
                phRt.anchorMin = Vector2.zero; phRt.anchorMax = Vector2.one;
                phRt.offsetMin = Vector2.zero; phRt.offsetMax = Vector2.zero;
                var phTmp = placeholderGo.GetComponent<TextMeshProUGUI>();
                phTmp.text = "Pseudo (displayName)";
                phTmp.fontSize = 20;
                phTmp.color = new Color(0.6f, 0.6f, 0.65f, 1f);
                phTmp.alignment = TextAlignmentOptions.MidlineLeft;
                phTmp.fontStyle = FontStyles.Italic;

                var input = inputGo.GetComponent<TMP_InputField>();
                input.textViewport = textAreaRt;
                input.textComponent = textTmp;
                input.placeholder = phTmp;
                input.fontAsset = textTmp.font;
                input.pointSize = 20;
            }
            // Search button
            var searchBtnGo = FindOrCreateChild(searchBar.transform, "SendButton", out bool createdSearchBtn, typeof(Image), typeof(Button));
            refs.SearchButton = searchBtnGo.GetComponent<Button>();
            if (createdSearchBtn)
            {
                var rt = EnsureRect(searchBtnGo);
                rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, 0f);
                rt.sizeDelta = new Vector2(150f, 0f);
                searchBtnGo.GetComponent<Image>().color = SearchButtonColor;

                var lblGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                lblGo.transform.SetParent(searchBtnGo.transform, false);
                var lblRt = lblGo.GetComponent<RectTransform>();
                lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
                lblRt.offsetMin = Vector2.zero; lblRt.offsetMax = Vector2.zero;
                var lbl = lblGo.GetComponent<TextMeshProUGUI>();
                lbl.text = "Envoyer";
                lbl.fontSize = 18; lbl.color = Color.white;
                lbl.alignment = TextAlignmentOptions.Center; lbl.fontStyle = FontStyles.Bold;
            }

            // StatusText
            var statusGo = FindOrCreateChild(container.transform, "StatusText", out bool createdStatus, typeof(TextMeshProUGUI));
            refs.StatusText = statusGo.GetComponent<TextMeshProUGUI>();
            if (createdStatus)
            {
                var rt = EnsureRect(statusGo);
                rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -120f);
                rt.sizeDelta = new Vector2(-32f, 24f);
                refs.StatusText.fontSize = 16;
                refs.StatusText.color = new Color(0.9f, 0.85f, 0.55f, 1f);
                refs.StatusText.alignment = TextAlignmentOptions.MidlineLeft;
                refs.StatusText.fontStyle = FontStyles.Italic;
            }

            // ContentArea (sous status)
            var content = FindOrCreateChild(container.transform, "ContentArea", out bool createdContent, typeof(VerticalLayoutGroup));
            if (createdContent)
            {
                var rt = EnsureRect(content);
                rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 1f);
                rt.offsetMin = new Vector2(16f, 16f);
                rt.offsetMax = new Vector2(-16f, -148f); // header 64 + searchbar 48 + status 24 + marges
                var vl = content.GetComponent<VerticalLayoutGroup>();
                vl.padding = new RectOffset(0, 0, 0, 0);
                vl.spacing = 8;
                vl.childForceExpandWidth = true;
                vl.childForceExpandHeight = false;
                vl.childControlWidth = true;
                vl.childControlHeight = true;
                vl.childAlignment = TextAnchor.UpperLeft;
            }

            // 3 sections : Friends, Incoming, Outgoing
            (refs.FriendsCountLabel, refs.FriendsContainer) = EnsureSection(content.transform, "Friends", "Amis (0)", actions);
            (refs.IncomingCountLabel, refs.IncomingContainer) = EnsureSection(content.transform, "Incoming", "Demandes reçues (0)", actions);
            (refs.OutgoingCountLabel, refs.OutgoingContainer) = EnsureSection(content.transform, "Outgoing", "Demandes envoyées (0)", actions);

            return refs;
        }

        private static (TextMeshProUGUI label, RectTransform container) EnsureSection(Transform parent, string name, string title, List<string> actions)
        {
            var sectionGo = FindOrCreateChild(parent, $"Section_{name}", out bool created, typeof(VerticalLayoutGroup), typeof(LayoutElement));
            if (created)
            {
                var vl = sectionGo.GetComponent<VerticalLayoutGroup>();
                vl.spacing = 4;
                vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
                vl.childControlWidth = true; vl.childControlHeight = true;
                var le = sectionGo.GetComponent<LayoutElement>();
                le.flexibleWidth = 1f;
                actions.Add($"+ Section {name}");
            }

            var labelGo = FindOrCreateChild(sectionGo.transform, "Header", out bool _, typeof(TextMeshProUGUI), typeof(LayoutElement));
            var labelTmp = labelGo.GetComponent<TextMeshProUGUI>();
            labelTmp.text = title;
            labelTmp.fontSize = 20;
            labelTmp.color = SectionLabelColor;
            labelTmp.alignment = TextAlignmentOptions.MidlineLeft;
            labelTmp.fontStyle = FontStyles.Bold;
            labelGo.GetComponent<LayoutElement>().preferredHeight = 28f;

            var containerGo = FindOrCreateChild(sectionGo.transform, "Items", out bool _2, typeof(VerticalLayoutGroup), typeof(LayoutElement));
            var contVl = containerGo.GetComponent<VerticalLayoutGroup>();
            contVl.spacing = 4;
            contVl.childForceExpandWidth = true; contVl.childForceExpandHeight = false;
            contVl.childControlWidth = true; contVl.childControlHeight = true;
            containerGo.GetComponent<LayoutElement>().flexibleWidth = 1f;

            return (labelTmp, containerGo.GetComponent<RectTransform>());
        }

        // -----------------------------------------------------------------------------
        // Section 3 : IncomingFriendRequestPopup
        // -----------------------------------------------------------------------------
        private static void EnsureIncomingPopup(Scene scene, Canvas canvas, List<string> actions)
        {
            var existing = canvas.transform.Find("FriendRequestPopupHost");
            IncomingFriendRequestPopup popup;
            GameObject hostGo;
            bool justCreated = false;

            if (existing == null)
            {
                hostGo = new GameObject("FriendRequestPopupHost",
                    typeof(RectTransform), typeof(IncomingFriendRequestPopup));
                hostGo.transform.SetParent(canvas.transform, false);
                var hostRt = hostGo.GetComponent<RectTransform>();
                hostRt.anchorMin = Vector2.zero; hostRt.anchorMax = Vector2.one;
                hostRt.offsetMin = Vector2.zero; hostRt.offsetMax = Vector2.zero;
                popup = hostGo.GetComponent<IncomingFriendRequestPopup>();
                actions.Add("+ GO 'FriendRequestPopupHost' créé");
                justCreated = true;
            }
            else
            {
                hostGo = existing.gameObject;
                popup = hostGo.GetComponent<IncomingFriendRequestPopup>();
                if (popup == null)
                {
                    popup = hostGo.AddComponent<IncomingFriendRequestPopup>();
                    actions.Add("+ IncomingFriendRequestPopup component ajouté");
                }
            }

            // Panel haut-droite (sous le bouton Amis si possible)
            var panel = FindOrCreateChild(hostGo.transform, "Panel", out bool createdPanel, typeof(Image), typeof(VerticalLayoutGroup));
            if (createdPanel)
            {
                var rt = EnsureRect(panel);
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-20f, -20f);
                rt.sizeDelta = new Vector2(320f, 160f);
                panel.GetComponent<Image>().color = PopupBgColor;
                var vl = panel.GetComponent<VerticalLayoutGroup>();
                vl.padding = new RectOffset(16, 16, 16, 16);
                vl.spacing = 12;
                vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
                vl.childControlWidth = true; vl.childControlHeight = true;
            }

            var labelGo = FindOrCreateChild(panel.transform, "Label", out bool _, typeof(TextMeshProUGUI), typeof(LayoutElement));
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.text = "(en attente)";
            label.fontSize = 18; label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = true;
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
            TryWire(so, "_panel", panel, actions, "IncomingFriendRequestPopup._panel");
            TryWire(so, "_label", label, actions, "IncomingFriendRequestPopup._label");
            TryWire(so, "_acceptButton", acceptBtn, actions, "IncomingFriendRequestPopup._acceptButton");
            TryWire(so, "_refuseButton", refuseBtn, actions, "IncomingFriendRequestPopup._refuseButton");
            so.ApplyModifiedPropertiesWithoutUndo();

            if (justCreated) panel.SetActive(false);
        }

        private static Button EnsureSimpleButton(Transform parent, string name, string label, Color bg)
        {
            var go = FindOrCreateChild(parent, name, out bool created, typeof(Image), typeof(Button), typeof(LayoutElement));
            if (created)
            {
                go.GetComponent<Image>().color = bg;
                go.GetComponent<LayoutElement>().flexibleWidth = 1f;
                var lblGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                lblGo.transform.SetParent(go.transform, false);
                var rt = lblGo.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                var tmp = lblGo.GetComponent<TextMeshProUGUI>();
                tmp.text = label; tmp.fontSize = 18; tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.Center; tmp.fontStyle = FontStyles.Bold;
            }
            return go.GetComponent<Button>();
        }

        // -----------------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------------
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
