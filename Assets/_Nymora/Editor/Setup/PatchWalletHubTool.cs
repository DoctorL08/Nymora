using System.Collections.Generic;
using Nymora.Hub;
using Nymora.Network.Backend;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Brique 5.4.b — 2 menus idempotents pour le wallet UI dans 10_CommunityHub :
    ///
    ///   1. "Patch Wallet Header Widget" — cree le GO 'WalletWidget' en haut-droite du Canvas
    ///      avec HubWalletWidget + 2 TMP labels (Nymos / Shards). Refresh live via WS.
    ///
    ///   2. "Patch Profile Wallet Tab" — ajoute le 6e onglet 'Portefeuille' au HubProfilePanel
    ///      avec header solde + container pour spawn runtime des 20 dernieres transactions.
    ///
    /// Menu : Nymora > Setup > Patch Wallet Header Widget
    /// Menu : Nymora > Setup > Patch Profile Wallet Tab
    /// </summary>
    public static class PatchWalletHubTool
    {
        private const string ScenePath = "Assets/_Nymora/Scenes/10_CommunityHub.unity";
        private const string BackendSettingsPath = "Assets/_Nymora/Settings/NymoraBackendSettings.asset";

        private static readonly Color WidgetBgColor = new Color(0.10f, 0.11f, 0.14f, 0.92f);
        private static readonly Color NymosColor = new Color(0.96f, 0.86f, 0.40f, 1f);
        private static readonly Color ShardsColor = new Color(0.55f, 0.88f, 1.00f, 1f);
        private static readonly Color TabInactiveColor = new Color(0.2f, 0.2f, 0.24f, 1f);
        private static readonly Color WalletHeaderColor = new Color(0.92f, 0.92f, 0.96f, 1f);

        // =====================================================================
        // Menu 1 — Wallet Header Widget (haut-droite)
        // =====================================================================
        [MenuItem("Nymora/Setup/Patch Wallet Header Widget", priority = 41)]
        private static void PatchWidget()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Patch Wallet Widget", "Stoppe Play Mode d'abord.", "OK");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.path.EndsWith("10_CommunityHub.unity"))
            {
                if (!EditorUtility.DisplayDialog("Patch Wallet Widget",
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
                EditorUtility.DisplayDialog("Patch Wallet Widget",
                    "Aucun Canvas trouve. Lance d'abord Create Community Hub Scene.", "OK");
                return;
            }

            var settings = AssetDatabase.LoadAssetAtPath<NymoraBackendSettings>(BackendSettingsPath);
            if (settings == null)
            {
                EditorUtility.DisplayDialog("Patch Wallet Widget",
                    $"NymoraBackendSettings introuvable a {BackendSettingsPath}.", "OK");
                return;
            }

            var actions = new List<string>();
            BuildWalletWidget(canvas, settings, actions);

            EditorSceneManager.MarkSceneDirty(scene);
            string summary = actions.Count == 0
                ? "OK Scene deja a jour, rien a patcher."
                : "Patch applique :\n\n" + string.Join("\n", actions) + "\n\nN'oublie pas Ctrl+S.";
            Debug.Log($"[Nymora.Setup] Patch wallet widget : {actions.Count} action(s)\n{summary}");
            EditorUtility.DisplayDialog("Patch Wallet Widget", summary, "OK");
        }

        private static void BuildWalletWidget(Canvas canvas, NymoraBackendSettings settings, List<string> actions)
        {
            // GO root : haut-droite, anchor (1,1) pivot (1,1), offset -20/-20
            var existing = canvas.transform.Find("WalletWidget");
            GameObject widgetGo;
            bool created;
            if (existing == null)
            {
                widgetGo = new GameObject("WalletWidget",
                    typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(HubWalletWidget));
                widgetGo.transform.SetParent(canvas.transform, false);
                created = true;
                actions.Add("+ GO 'WalletWidget' cree (haut-droite)");
            }
            else
            {
                widgetGo = existing.gameObject;
                if (widgetGo.GetComponent<HubWalletWidget>() == null)
                {
                    widgetGo.AddComponent<HubWalletWidget>();
                    actions.Add("+ HubWalletWidget component ajoute");
                }
                if (widgetGo.GetComponent<HorizontalLayoutGroup>() == null)
                {
                    widgetGo.AddComponent<HorizontalLayoutGroup>();
                    actions.Add("+ HorizontalLayoutGroup ajoute");
                }
                if (widgetGo.GetComponent<Image>() == null)
                {
                    widgetGo.AddComponent<Image>();
                    actions.Add("+ Image bg ajoute");
                }
                created = false;
            }

            // RectTransform setup
            var rt = widgetGo.GetComponent<RectTransform>();
            if (created)
            {
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-20f, -20f);
                rt.sizeDelta = new Vector2(280f, 44f);
            }

            // Background
            var bg = widgetGo.GetComponent<Image>();
            if (bg != null) bg.color = WidgetBgColor;

            // Layout
            var hl = widgetGo.GetComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(12, 12, 6, 6);
            hl.spacing = 14;
            hl.childForceExpandWidth = true;
            hl.childForceExpandHeight = true;
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childAlignment = TextAnchor.MiddleCenter;

            // Nymos label
            var nymosGo = FindOrCreateChild(widgetGo.transform, "Nymos", out bool createdNymos, typeof(TextMeshProUGUI), typeof(LayoutElement));
            var nymosTmp = nymosGo.GetComponent<TextMeshProUGUI>();
            if (createdNymos)
            {
                nymosTmp.text = "🪙 0";
                nymosTmp.fontSize = 18;
                nymosTmp.color = NymosColor;
                nymosTmp.alignment = TextAlignmentOptions.Center;
                nymosTmp.fontStyle = FontStyles.Bold;
                nymosGo.GetComponent<LayoutElement>().flexibleWidth = 1f;
                actions.Add("+ Nymos label");
            }

            // Shards label
            var shardsGo = FindOrCreateChild(widgetGo.transform, "Shards", out bool createdShards, typeof(TextMeshProUGUI), typeof(LayoutElement));
            var shardsTmp = shardsGo.GetComponent<TextMeshProUGUI>();
            if (createdShards)
            {
                shardsTmp.text = "💎 0";
                shardsTmp.fontSize = 18;
                shardsTmp.color = ShardsColor;
                shardsTmp.alignment = TextAlignmentOptions.Center;
                shardsTmp.fontStyle = FontStyles.Bold;
                shardsGo.GetComponent<LayoutElement>().flexibleWidth = 1f;
                actions.Add("+ Shards label");
            }

            // Wire HubWalletWidget SerializedFields
            var widget = widgetGo.GetComponent<HubWalletWidget>();
            var so = new SerializedObject(widget);
            TryWire(so, "_backendSettings", settings, actions, "HubWalletWidget._backendSettings");
            TryWire(so, "_nymosLabel", nymosTmp, actions, "HubWalletWidget._nymosLabel");
            TryWire(so, "_shardsLabel", shardsTmp, actions, "HubWalletWidget._shardsLabel");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // =====================================================================
        // Menu 2 — Profile Wallet Tab (onglet 'Portefeuille')
        // =====================================================================
        [MenuItem("Nymora/Setup/Patch Profile Wallet Tab", priority = 42)]
        private static void PatchTab()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Patch Profile Wallet Tab", "Stoppe Play Mode d'abord.", "OK");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.path.EndsWith("10_CommunityHub.unity"))
            {
                if (!EditorUtility.DisplayDialog("Patch Profile Wallet Tab",
                    $"Scene active : {scene.path}\nAttendu : {ScenePath}\n\nOuvrir 10_CommunityHub ?",
                    "Ouvrir", "Annuler"))
                {
                    return;
                }
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var panel = Object.FindFirstObjectByType<HubProfilePanel>();
            if (panel == null)
            {
                EditorUtility.DisplayDialog("Patch Profile Wallet Tab",
                    "HubProfilePanel introuvable. Lance d'abord Patch Profile Panel.", "OK");
                return;
            }

            var panelSo = new SerializedObject(panel);
            var actions = new List<string>();

            // 1. Bouton onglet dans TabsBar
            var tabsBar = panel.transform.Find("PanelRoot/Container/TabsBar");
            if (tabsBar == null)
            {
                EditorUtility.DisplayDialog("Patch Profile Wallet Tab",
                    "TabsBar introuvable sous ProfilePanelHost. Relance Patch Profile Panel.", "OK");
                return;
            }
            var tabWallet = EnsureTabButton(tabsBar, "TabWallet", "Portefeuille", TabInactiveColor, actions);

            // 2. Content panel dans ContentArea
            var contentArea = panel.transform.Find("PanelRoot/Container/ContentArea");
            if (contentArea == null)
            {
                EditorUtility.DisplayDialog("Patch Profile Wallet Tab",
                    "ContentArea introuvable. Relance Patch Profile Panel.", "OK");
                return;
            }
            var contentWallet = EnsureContentWallet(contentArea, out var headerTmp, out var transactionsContainer, actions);
            contentWallet.SetActive(false); // pas l'onglet actif au build

            // 3. Wire les SerializedFields
            TryWire(panelSo, "_tabWalletButton", tabWallet, actions, "HubProfilePanel._tabWalletButton");
            TryWire(panelSo, "_contentWallet", contentWallet, actions, "HubProfilePanel._contentWallet");
            TryWire(panelSo, "_walletHeader", headerTmp, actions, "HubProfilePanel._walletHeader");
            TryWire(panelSo, "_walletTransactionsContainer", transactionsContainer, actions, "HubProfilePanel._walletTransactionsContainer");
            panelSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);

            string summary = actions.Count == 0
                ? "OK déjà à jour."
                : "Patch appliqué :\n\n" + string.Join("\n", actions) + "\n\nN'oublie pas Ctrl+S.";
            Debug.Log($"[Nymora.Setup] Patch profile wallet tab : {actions.Count} action(s)\n{summary}");
            EditorUtility.DisplayDialog("Patch Profile Wallet Tab", summary, "OK");
        }

        private static Button EnsureTabButton(Transform parent, string name, string label, Color bg, List<string> actions)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing.GetComponent<Button>();

            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
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

            actions.Add($"+ Tab '{label}' ajouté");
            return go.GetComponent<Button>();
        }

        private static GameObject EnsureContentWallet(
            Transform contentArea, out TextMeshProUGUI headerTmp,
            out RectTransform transactionsContainer, List<string> actions)
        {
            var content = FindOrCreateChild(contentArea, "ContentWallet", out bool createdContent, typeof(VerticalLayoutGroup));
            if (createdContent)
            {
                var rt = content.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                var vl = content.GetComponent<VerticalLayoutGroup>();
                vl.padding = new RectOffset(20, 20, 16, 16);
                vl.spacing = 8;
                vl.childForceExpandWidth = true;
                vl.childForceExpandHeight = false;
                vl.childControlWidth = true;
                vl.childControlHeight = true;
                vl.childAlignment = TextAnchor.UpperLeft;
                actions.Add("+ ContentWallet panel");
            }

            // Header : solde global
            var headerGo = FindOrCreateChild(content.transform, "Header", out bool createdHeader, typeof(TextMeshProUGUI), typeof(LayoutElement));
            headerTmp = headerGo.GetComponent<TextMeshProUGUI>();
            if (createdHeader)
            {
                headerTmp.text = "<color=#f5dc66>🪙 <b>0</b> Nymos</color>   ·   <color=#8be0ff>💎 <b>0</b> Shards</color>";
                headerTmp.fontSize = 22;
                headerTmp.color = WalletHeaderColor;
                headerTmp.alignment = TextAlignmentOptions.Center;
                headerTmp.richText = true;
                headerGo.GetComponent<LayoutElement>().preferredHeight = 36f;
                actions.Add("+ Wallet header");
            }

            // Sous-titre
            var subtitle = FindOrCreateChild(content.transform, "Subtitle", out bool createdSub, typeof(TextMeshProUGUI), typeof(LayoutElement));
            var subTmp = subtitle.GetComponent<TextMeshProUGUI>();
            if (createdSub)
            {
                subTmp.text = "<i>20 dernières transactions</i>";
                subTmp.fontSize = 14;
                subTmp.color = new Color(0.7f, 0.7f, 0.78f);
                subTmp.alignment = TextAlignmentOptions.MidlineLeft;
                subTmp.richText = true;
                subtitle.GetComponent<LayoutElement>().preferredHeight = 22f;
            }

            // Container transactions (spawn runtime)
            var container = FindOrCreateChild(content.transform, "TransactionsContainer", out bool createdCont, typeof(VerticalLayoutGroup), typeof(LayoutElement));
            transactionsContainer = container.GetComponent<RectTransform>();
            if (createdCont)
            {
                var vl = container.GetComponent<VerticalLayoutGroup>();
                vl.spacing = 2;
                vl.childForceExpandWidth = true;
                vl.childForceExpandHeight = false;
                vl.childControlWidth = true;
                vl.childControlHeight = true;
                container.GetComponent<LayoutElement>().flexibleWidth = 1f;
                actions.Add("+ Wallet transactions container");
            }

            return content;
        }

        // =====================================================================
        // Helpers
        // =====================================================================
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
