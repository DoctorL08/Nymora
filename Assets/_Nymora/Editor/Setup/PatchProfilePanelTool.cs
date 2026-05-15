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
    /// Brique 4.12 — Patch idempotent de la scene 10_CommunityHub pour ajouter le panel profil.
    ///
    /// Actions effectuees (uniquement si manquantes / null) :
    ///   1. Bouge le panel chat en bas-gauche (anchor + position)
    ///   2. Cree le GO "ProfilePanelHost" portant HubProfilePanel + sa hierarchie UI
    ///   3. Cree le bouton "MyProfileButton" en bas-droite (portant HubProfileButton)
    ///   4. Wire tous les SerializedField du HubProfilePanel
    ///
    /// Menu : Nymora > Setup > Patch Profile Panel
    /// </summary>
    public static class PatchProfilePanelTool
    {
        private const string ScenePath = "Assets/_Nymora/Scenes/10_CommunityHub.unity";
        private const string BackendSettingsPath = "Assets/_Nymora/Settings/NymoraBackendSettings.asset";

        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.65f);
        private static readonly Color ContainerColor = new Color(0.12f, 0.13f, 0.16f, 0.98f);
        private static readonly Color HeaderColor = new Color(0.18f, 0.20f, 0.25f, 1f);
        private static readonly Color TabActiveColor = new Color(0.25f, 0.4f, 0.65f, 1f);
        private static readonly Color TabInactiveColor = new Color(0.2f, 0.2f, 0.24f, 1f);
        private static readonly Color CloseButtonColor = new Color(0.55f, 0.25f, 0.25f, 1f);
        private static readonly Color ProfileButtonColor = new Color(0.30f, 0.45f, 0.30f, 1f);
        private static readonly Color ComingSoonColor = new Color(0.65f, 0.65f, 0.70f, 1f);

        [MenuItem("Nymora/Setup/Patch Profile Panel", priority = 35)]
        private static void Patch()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Patch Profile Panel",
                    "Impossible de patcher pendant Play Mode.\nStoppe Play (Ctrl+P) puis relance ce menu.", "OK");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.path.EndsWith("10_CommunityHub.unity"))
            {
                if (!EditorUtility.DisplayDialog("Patch Profile Panel",
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
                EditorUtility.DisplayDialog("Patch Profile Panel",
                    "Aucun Canvas trouve. Lance d'abord Create Community Hub Scene.", "OK");
                return;
            }

            var settings = AssetDatabase.LoadAssetAtPath<NymoraBackendSettings>(BackendSettingsPath);
            if (settings == null)
            {
                EditorUtility.DisplayDialog("Patch Profile Panel",
                    $"NymoraBackendSettings introuvable a {BackendSettingsPath}.\nCree-le via Nymora > Setup > Create Backend Settings.",
                    "OK");
                return;
            }

            var actions = new List<string>();

            MoveChatToBottomLeft(actions);
            var panelHost = EnsureProfilePanelHost(scene, canvas, settings, actions);
            EnsureProfileButton(canvas, actions);

            EditorSceneManager.MarkSceneDirty(scene);

            string summary = actions.Count == 0
                ? "OK Scene deja a jour, rien a patcher."
                : "Patch applique :\n\n" + string.Join("\n", actions) + "\n\nN'oublie pas Ctrl+S pour sauvegarder la scene.";
            Debug.Log($"[Nymora.Setup] Patch profile panel : {actions.Count} action(s)\n{summary}");
            EditorUtility.DisplayDialog("Patch Profile Panel", summary, "OK");
        }

        // -----------------------------------------------------------------------------
        // Section 1 : Bouger le chat en bas-gauche
        // -----------------------------------------------------------------------------
        private static void MoveChatToBottomLeft(List<string> actions)
        {
            var chatUI = Object.FindFirstObjectByType<HubChatUI>();
            if (chatUI == null) return;

            // Le HubChatUI est attache au GameObject panel root (cf CreateCommunityHubSceneTool).
            // Sa RectTransform est ce qu'on veut deplacer.
            var rt = chatUI.GetComponent<RectTransform>();
            if (rt == null) return;

            // Bas-gauche : anchor (0,0)-(0,0), pivot (0,0), offset 20px x 20px
            bool alreadyLeft = rt.anchorMin.x < 0.5f && rt.anchorMax.x < 0.5f;
            if (alreadyLeft) return;

            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(20f, 20f);
            actions.Add("> Chat panel deplace en bas-gauche");
        }

        // -----------------------------------------------------------------------------
        // Section 2 : Bouton "Mon profil" bas-droite
        // -----------------------------------------------------------------------------
        private static void EnsureProfileButton(Canvas canvas, List<string> actions)
        {
            var existing = canvas.transform.Find("MyProfileButton");
            if (existing != null)
            {
                // Wire HubProfileButton si manquant
                if (existing.GetComponent<HubProfileButton>() == null)
                {
                    existing.gameObject.AddComponent<HubProfileButton>();
                    actions.Add("+ HubProfileButton component ajoute sur bouton existant");
                }
                return;
            }

            var go = new GameObject("MyProfileButton",
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(HubProfileButton));
            go.transform.SetParent(canvas.transform, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-20f, 20f);
            rt.sizeDelta = new Vector2(180f, 56f);

            go.GetComponent<Image>().color = ProfileButtonColor;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var labelText = labelGo.GetComponent<TextMeshProUGUI>();
            labelText.text = "Mon profil";
            labelText.fontSize = 26;
            labelText.color = Color.white;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.fontStyle = FontStyles.Bold;

            actions.Add("+ Bouton 'MyProfileButton' cree (bas-droite)");
        }

        // -----------------------------------------------------------------------------
        // Section 3 : Hierarchie ProfilePanel + wiring HubProfilePanel
        // -----------------------------------------------------------------------------
        private static HubProfilePanel EnsureProfilePanelHost(Scene scene, Canvas canvas, NymoraBackendSettings settings, List<string> actions)
        {
            var existing = canvas.transform.Find("ProfilePanelHost");
            HubProfilePanel panel;
            GameObject hostGo;
            bool justCreated = false;

            if (existing == null)
            {
                hostGo = new GameObject("ProfilePanelHost", typeof(RectTransform), typeof(HubProfilePanel));
                hostGo.transform.SetParent(canvas.transform, false);
                var hostRt = hostGo.GetComponent<RectTransform>();
                hostRt.anchorMin = Vector2.zero;
                hostRt.anchorMax = Vector2.one;
                hostRt.offsetMin = Vector2.zero;
                hostRt.offsetMax = Vector2.zero;
                panel = hostGo.GetComponent<HubProfilePanel>();
                actions.Add("+ GO 'ProfilePanelHost' cree");
                justCreated = true;
            }
            else
            {
                hostGo = existing.gameObject;
                panel = hostGo.GetComponent<HubProfilePanel>();
                if (panel == null)
                {
                    panel = hostGo.AddComponent<HubProfilePanel>();
                    actions.Add("+ HubProfilePanel component ajoute");
                }
            }

            // Construit la hierarchie UI sous le host
            var refs = BuildPanelHierarchy(hostGo, justCreated, actions);

            // Wire SerializedFields
            var so = new SerializedObject(panel);
            TryWire(so, "_backendSettings", settings, actions, "HubProfilePanel._backendSettings");
            TryWire(so, "_panelRoot", refs.PanelRoot, actions, "HubProfilePanel._panelRoot");
            TryWire(so, "_closeButton", refs.CloseButton, actions, "HubProfilePanel._closeButton");
            TryWire(so, "_tabViewButton", refs.TabView, actions, "HubProfilePanel._tabViewButton");
            TryWire(so, "_tabStatsButton", refs.TabStats, actions, "HubProfilePanel._tabStatsButton");
            TryWire(so, "_tabClassesButton", refs.TabClasses, actions, "HubProfilePanel._tabClassesButton");
            TryWire(so, "_tabAchievementsButton", refs.TabAchievements, actions, "HubProfilePanel._tabAchievementsButton");
            TryWire(so, "_tabCosmeticsButton", refs.TabCosmetics, actions, "HubProfilePanel._tabCosmeticsButton");
            TryWire(so, "_contentView", refs.ContentView, actions, "HubProfilePanel._contentView");
            TryWire(so, "_contentStats", refs.ContentStats, actions, "HubProfilePanel._contentStats");
            TryWire(so, "_contentClasses", refs.ContentClasses, actions, "HubProfilePanel._contentClasses");
            TryWire(so, "_contentAchievements", refs.ContentAchievements, actions, "HubProfilePanel._contentAchievements");
            TryWire(so, "_contentCosmetics", refs.ContentCosmetics, actions, "HubProfilePanel._contentCosmetics");
            TryWire(so, "_viewDisplayName", refs.ViewDisplayName, actions, "HubProfilePanel._viewDisplayName");
            TryWire(so, "_viewMmr", refs.ViewMmr, actions, "HubProfilePanel._viewMmr");
            TryWire(so, "_viewCreatedAt", refs.ViewCreatedAt, actions, "HubProfilePanel._viewCreatedAt");
            TryWire(so, "_viewLastLoginAt", refs.ViewLastLoginAt, actions, "HubProfilePanel._viewLastLoginAt");
            TryWire(so, "_viewStatusLine", refs.ViewStatusLine, actions, "HubProfilePanel._viewStatusLine");
            so.ApplyModifiedPropertiesWithoutUndo();

            // Panel root caché par défaut au premier build
            if (justCreated && refs.PanelRoot != null) refs.PanelRoot.SetActive(false);

            return panel;
        }

        private sealed class PanelRefs
        {
            public GameObject PanelRoot;
            public Button CloseButton;
            public Button TabView, TabStats, TabClasses, TabAchievements, TabCosmetics;
            public GameObject ContentView, ContentStats, ContentClasses, ContentAchievements, ContentCosmetics;
            public TextMeshProUGUI ViewDisplayName, ViewMmr, ViewCreatedAt, ViewLastLoginAt, ViewStatusLine;
        }

        private static PanelRefs BuildPanelHierarchy(GameObject host, bool freshBuild, List<string> actions)
        {
            var refs = new PanelRefs();

            // Tout est sous host. On utilise des noms determinist pour pouvoir Find si re-run.
            var panelRoot = FindOrCreateChild(host.transform, "PanelRoot", out bool createdPanelRoot);
            refs.PanelRoot = panelRoot;
            if (createdPanelRoot)
            {
                var rt = EnsureRect(panelRoot);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                actions.Add("+ PanelRoot fullscreen");
            }

            // Backdrop semi-transparent (intercepte les clics)
            var backdrop = FindOrCreateChild(panelRoot.transform, "Backdrop", out bool createdBackdrop, typeof(Image));
            if (createdBackdrop)
            {
                var rt = EnsureRect(backdrop);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                backdrop.GetComponent<Image>().color = BackdropColor;
                actions.Add("+ Backdrop semi-transparent");
            }

            // Container centre 800x600
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
                actions.Add("+ Container centre 820x620");
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
                actions.Add("+ Header bar");
            }

            // Titre dans Header
            var titleGo = FindOrCreateChild(header.transform, "Title", out bool createdTitle, typeof(TextMeshProUGUI));
            if (createdTitle)
            {
                var rt = EnsureRect(titleGo);
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.offsetMin = new Vector2(24f, 0f);
                rt.offsetMax = new Vector2(-84f, 0f);
                var tmp = titleGo.GetComponent<TextMeshProUGUI>();
                tmp.text = "Profil";
                tmp.fontSize = 32;
                tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                tmp.fontStyle = FontStyles.Bold;
            }

            // Close button dans Header
            var closeGo = FindOrCreateChild(header.transform, "CloseButton", out bool createdClose, typeof(Image), typeof(Button));
            refs.CloseButton = closeGo.GetComponent<Button>();
            if (createdClose)
            {
                var rt = EnsureRect(closeGo);
                rt.anchorMin = new Vector2(1f, 0.5f);
                rt.anchorMax = new Vector2(1f, 0.5f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.anchoredPosition = new Vector2(-16f, 0f);
                rt.sizeDelta = new Vector2(48f, 40f);
                closeGo.GetComponent<Image>().color = CloseButtonColor;

                var lblGo = new GameObject("X", typeof(RectTransform), typeof(TextMeshProUGUI));
                lblGo.transform.SetParent(closeGo.transform, false);
                var lblRt = lblGo.GetComponent<RectTransform>();
                lblRt.anchorMin = Vector2.zero;
                lblRt.anchorMax = Vector2.one;
                lblRt.offsetMin = Vector2.zero;
                lblRt.offsetMax = Vector2.zero;
                var lbl = lblGo.GetComponent<TextMeshProUGUI>();
                lbl.text = "X";
                lbl.fontSize = 24;
                lbl.color = Color.white;
                lbl.alignment = TextAlignmentOptions.Center;
                lbl.fontStyle = FontStyles.Bold;
            }

            // Tabs bar
            var tabsBar = FindOrCreateChild(container.transform, "TabsBar", out bool createdTabs, typeof(HorizontalLayoutGroup));
            if (createdTabs)
            {
                var rt = EnsureRect(tabsBar);
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -64f);
                rt.sizeDelta = new Vector2(0f, 56f);
                var hl = tabsBar.GetComponent<HorizontalLayoutGroup>();
                hl.padding = new RectOffset(8, 8, 6, 6);
                hl.spacing = 4;
                hl.childForceExpandWidth = true;
                hl.childForceExpandHeight = true;
                hl.childControlWidth = true;
                hl.childControlHeight = true;
                actions.Add("+ TabsBar (5 tabs)");
            }

            refs.TabView = EnsureTabButton(tabsBar.transform, "TabView", "Vue", TabActiveColor);
            refs.TabStats = EnsureTabButton(tabsBar.transform, "TabStats", "Stats", TabInactiveColor);
            refs.TabClasses = EnsureTabButton(tabsBar.transform, "TabClasses", "Classes", TabInactiveColor);
            refs.TabAchievements = EnsureTabButton(tabsBar.transform, "TabAchievements", "Succes", TabInactiveColor);
            refs.TabCosmetics = EnsureTabButton(tabsBar.transform, "TabCosmetics", "Cosmetiques", TabInactiveColor);

            // Content area (sous Tabs bar)
            var contentArea = FindOrCreateChild(container.transform, "ContentArea", out bool createdContent);
            if (createdContent)
            {
                var rt = EnsureRect(contentArea);
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.offsetMin = new Vector2(16f, 16f);
                rt.offsetMax = new Vector2(-16f, -124f); // 64 (header) + 56 (tabs) + 4 marges
            }

            // Onglet Vue : contenu structure
            refs.ContentView = EnsureContentVue(contentArea.transform, refs, actions);

            // 4 onglets placeholder
            refs.ContentStats = EnsureContentPlaceholder(contentArea.transform, "ContentStats",
                "Statistiques", "A venir avec le tracking des matchs PvP (dette 4.8.d.iii Quantum Online).", actions);
            refs.ContentClasses = EnsureContentPlaceholder(contentArea.transform, "ContentClasses",
                "Classes", "A venir avec le systeme de progression et de deblocage des 5 classes.", actions);
            refs.ContentAchievements = EnsureContentPlaceholder(contentArea.transform, "ContentAchievements",
                "Succes", "A venir avec le systeme de succes (Phase 5+).", actions);
            refs.ContentCosmetics = EnsureContentPlaceholder(contentArea.transform, "ContentCosmetics",
                "Cosmetiques", "A venir avec le shop et le battle pass (Phase 5+).", actions);

            // Au premier build, on cache tous les contenus sauf Vue
            if (freshBuild)
            {
                refs.ContentStats.SetActive(false);
                refs.ContentClasses.SetActive(false);
                refs.ContentAchievements.SetActive(false);
                refs.ContentCosmetics.SetActive(false);
            }

            return refs;
        }

        private static GameObject EnsureContentVue(Transform parent, PanelRefs refs, List<string> actions)
        {
            var content = FindOrCreateChild(parent, "ContentView", out bool created, typeof(VerticalLayoutGroup));
            if (created)
            {
                var rt = EnsureRect(content);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                var vl = content.GetComponent<VerticalLayoutGroup>();
                vl.padding = new RectOffset(24, 24, 24, 24);
                vl.spacing = 12;
                vl.childForceExpandWidth = true;
                vl.childForceExpandHeight = false;
                vl.childControlWidth = true;
                vl.childControlHeight = true;
                vl.childAlignment = TextAnchor.UpperLeft;
                actions.Add("+ ContentView (onglet Vue)");
            }

            refs.ViewDisplayName = EnsureTmpLine(content.transform, "DisplayName", "", 36, Color.white, FontStyles.Bold);

            // 4.12 privacy fix : retirer la ligne Email si elle existe d'un patch precedent.
            var legacyEmail = content.transform.Find("Email");
            if (legacyEmail != null)
            {
                Object.DestroyImmediate(legacyEmail.gameObject);
                actions.Add("- Email line removed (privacy)");
            }

            refs.ViewMmr = EnsureTmpLine(content.transform, "Mmr", "MMR : —", 24, Color.white, FontStyles.Normal);
            refs.ViewCreatedAt = EnsureTmpLine(content.transform, "CreatedAt", "Inscrit : —", 18, new Color(0.8f, 0.8f, 0.85f), FontStyles.Normal);
            refs.ViewLastLoginAt = EnsureTmpLine(content.transform, "LastLoginAt", "Derniere connexion : —", 18, new Color(0.8f, 0.8f, 0.85f), FontStyles.Normal);
            refs.ViewStatusLine = EnsureTmpLine(content.transform, "StatusLine", "", 16, ComingSoonColor, FontStyles.Italic);

            return content;
        }

        private static GameObject EnsureContentPlaceholder(Transform parent, string name, string title, string desc, List<string> actions)
        {
            var content = FindOrCreateChild(parent, name, out bool created, typeof(VerticalLayoutGroup));
            if (created)
            {
                var rt = EnsureRect(content);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                var vl = content.GetComponent<VerticalLayoutGroup>();
                vl.padding = new RectOffset(24, 24, 24, 24);
                vl.spacing = 12;
                vl.childForceExpandWidth = true;
                vl.childForceExpandHeight = false;
                vl.childControlWidth = true;
                vl.childControlHeight = true;
                vl.childAlignment = TextAnchor.UpperLeft;
                actions.Add($"+ {name} placeholder");
            }
            EnsureTmpLine(content.transform, "Title", title, 32, Color.white, FontStyles.Bold);
            EnsureTmpLine(content.transform, "ComingSoon", "Coming soon", 22, ComingSoonColor, FontStyles.Italic);
            EnsureTmpLine(content.transform, "Desc", desc, 16, new Color(0.7f, 0.7f, 0.75f), FontStyles.Normal);
            return content;
        }

        private static Button EnsureTabButton(Transform parent, string name, string label, Color bg)
        {
            var go = FindOrCreateChild(parent, name, out bool created, typeof(Image), typeof(Button), typeof(LayoutElement));
            if (created)
            {
                go.GetComponent<Image>().color = bg;
                var le = go.GetComponent<LayoutElement>();
                le.flexibleWidth = 1f;

                var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelGo.transform.SetParent(go.transform, false);
                var labelRt = labelGo.GetComponent<RectTransform>();
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;
                var tmp = labelGo.GetComponent<TextMeshProUGUI>();
                tmp.text = label;
                tmp.fontSize = 20;
                tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontStyle = FontStyles.Bold;
            }
            return go.GetComponent<Button>();
        }

        private static TextMeshProUGUI EnsureTmpLine(Transform parent, string name, string text, int fontSize, Color color, FontStyles style)
        {
            var go = FindOrCreateChild(parent, name, out bool created, typeof(TextMeshProUGUI), typeof(LayoutElement));
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (created)
            {
                tmp.fontSize = fontSize;
                tmp.color = color;
                tmp.alignment = TextAlignmentOptions.TopLeft;
                tmp.fontStyle = style;
                tmp.enableWordWrapping = true;
                tmp.text = text;
                var le = go.GetComponent<LayoutElement>();
                le.preferredHeight = -1; // auto via content size
                le.flexibleWidth = 1f;
            }
            return tmp;
        }

        // -----------------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------------
        private static GameObject FindOrCreateChild(Transform parent, string childName, out bool created, params System.Type[] components)
        {
            var existing = parent.Find(childName);
            if (existing != null)
            {
                created = false;
                return existing.gameObject;
            }
            var allComponents = new List<System.Type> { typeof(RectTransform) };
            if (components != null) allComponents.AddRange(components);
            var go = new GameObject(childName, allComponents.ToArray());
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
            if (prop.objectReferenceValue == value) return; // deja wire correctement
            if (prop.objectReferenceValue != null) return;  // valeur existante a respecter
            prop.objectReferenceValue = value;
            actions.Add($"+ {label} wire");
        }
    }
}
