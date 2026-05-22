using System.Collections.Generic;
using Nymora.Hub;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Brique 6.6.b — Patch scene 10_CommunityHub : panneau Classement (leaderboard)
    /// = LeaderboardPanelHost + HubLeaderboardPanel + ScrollRect contenant un TMP scrollable.
    ///
    /// Idempotent (rebuild le host). Ouvert par le bouton "Classement" du panneau de recherche.
    ///
    /// Menu : Nymora > Setup > Patch Leaderboard Panel.
    /// </summary>
    public static class PatchLeaderboardPanelTool
    {
        private const string ScenePath = "Assets/_Nymora/Scenes/10_CommunityHub.unity";

        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.65f);
        private static readonly Color ContainerColor = new Color(0.12f, 0.13f, 0.16f, 0.98f);
        private static readonly Color HeaderColor = new Color(0.18f, 0.20f, 0.25f, 1f);
        private static readonly Color CloseColor = new Color(0.55f, 0.25f, 0.25f, 1f);
        private static readonly Color ViewportColor = new Color(0.08f, 0.09f, 0.11f, 1f);

        [MenuItem("Nymora/Setup/Patch Leaderboard Panel", priority = 40)]
        private static void Patch()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Patch Leaderboard", "Impossible pendant Play Mode.", "OK");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.path.EndsWith("10_CommunityHub.unity"))
            {
                if (!EditorUtility.DisplayDialog("Patch Leaderboard",
                    $"Scene active : {scene.path}\nAttendu : {ScenePath}\n\nOuvrir 10_CommunityHub ?", "Ouvrir", "Annuler")) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) { EditorUtility.DisplayDialog("Patch Leaderboard", "Aucun Canvas.", "OK"); return; }

            var actions = new List<string>();
            EnsurePanel(canvas, actions);

            EditorSceneManager.MarkSceneDirty(scene);
            string summary = "Patch applique :\n\n" + string.Join("\n", actions) + "\n\nN'oublie pas Ctrl+S.";
            Debug.Log($"[Nymora.Setup] Patch leaderboard : {actions.Count} action(s)\n{summary}");
            EditorUtility.DisplayDialog("Patch Leaderboard", summary, "OK");
        }

        private static void EnsurePanel(Canvas canvas, List<string> actions)
        {
            var existing = canvas.transform.Find("LeaderboardPanelHost");
            HubLeaderboardPanel panel;
            GameObject hostGo;
            if (existing != null)
            {
                hostGo = existing.gameObject;
                panel = hostGo.GetComponent<HubLeaderboardPanel>() ?? hostGo.AddComponent<HubLeaderboardPanel>();
                for (int i = hostGo.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(hostGo.transform.GetChild(i).gameObject);
                actions.Add("LeaderboardPanelHost cleanup (rebuild)");
            }
            else
            {
                hostGo = new GameObject("LeaderboardPanelHost", typeof(RectTransform));
                hostGo.transform.SetParent(canvas.transform, false);
                StretchToParent(hostGo);
                panel = hostGo.AddComponent<HubLeaderboardPanel>();
                actions.Add("LeaderboardPanelHost cree");
            }

            var panelRoot = NewChild("PanelRoot", hostGo.transform);
            StretchToParent(panelRoot);

            var backdrop = NewChild("Backdrop", panelRoot.transform);
            StretchToParent(backdrop);
            backdrop.AddComponent<Image>().color = BackdropColor;

            // Container centre 700x680
            var container = NewChild("Container", panelRoot.transform);
            var crt = container.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(700f, 680f);
            container.AddComponent<Image>().color = ContainerColor;

            // Header + titre + close
            var header = NewChild("Header", container.transform);
            var hRt = header.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0f, 1f); hRt.anchorMax = new Vector2(1f, 1f);
            hRt.pivot = new Vector2(0.5f, 1f); hRt.sizeDelta = new Vector2(0f, 56f);
            header.AddComponent<Image>().color = HeaderColor;

            var title = NewChild("Title", header.transform);
            StretchToParent(title);
            var titleTmp = title.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "Classement"; titleTmp.fontSize = 26f; titleTmp.color = Color.white;
            titleTmp.alignment = TextAlignmentOptions.Center; titleTmp.fontStyle = FontStyles.Bold;

            var closeGo = MakeButton(header.transform, "CloseButton", "X", CloseColor, 44f, 40f);
            var crtClose = closeGo.GetComponent<RectTransform>();
            crtClose.anchorMin = crtClose.anchorMax = new Vector2(1f, 0.5f);
            crtClose.pivot = new Vector2(1f, 0.5f);
            crtClose.anchoredPosition = new Vector2(-12f, 0f);

            // ScrollRect : viewport (masque) + content (TMP avec ContentSizeFitter)
            var scrollGo = NewChild("Scroll", container.transform);
            var sRt = scrollGo.GetComponent<RectTransform>();
            sRt.anchorMin = new Vector2(0f, 0f); sRt.anchorMax = new Vector2(1f, 1f);
            sRt.offsetMin = new Vector2(16f, 16f); sRt.offsetMax = new Vector2(-16f, -64f);
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            var viewport = NewChild("Viewport", scrollGo.transform);
            StretchToParent(viewport);
            viewport.AddComponent<Image>().color = ViewportColor;
            viewport.AddComponent<RectMask2D>();

            var content = NewChild("Content", viewport.transform);
            var cRt = content.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0f, 1f); cRt.anchorMax = new Vector2(1f, 1f);
            cRt.pivot = new Vector2(0.5f, 1f); cRt.anchoredPosition = Vector2.zero;
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var listGo = NewChild("ListText", content.transform);
            var lRt = listGo.GetComponent<RectTransform>();
            lRt.anchorMin = new Vector2(0f, 1f); lRt.anchorMax = new Vector2(1f, 1f);
            lRt.pivot = new Vector2(0.5f, 1f); lRt.offsetMin = new Vector2(12f, 0f); lRt.offsetMax = new Vector2(-12f, 0f);
            var listTmp = listGo.AddComponent<TextMeshProUGUI>();
            listTmp.text = "";
            listTmp.fontSize = 20f;
            listTmp.color = new Color(0.92f, 0.92f, 0.95f);
            listTmp.alignment = TextAlignmentOptions.TopLeft;
            listTmp.enableWordWrapping = true;
            // ContentSizeFitter sur Content suit la hauteur preferred du TMP via un LayoutElement.
            var le = listGo.AddComponent<LayoutElement>();
            le.flexibleHeight = 0;
            // La hauteur du Content suit le TMP : on met aussi un fitter sur le TMP lui-meme.
            var listFitter = listGo.AddComponent<ContentSizeFitter>();
            listFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = cRt;

            // Wire panel
            var so = new SerializedObject(panel);
            so.FindProperty("_panelRoot").objectReferenceValue = panelRoot;
            so.FindProperty("_closeButton").objectReferenceValue = closeGo.GetComponent<Button>();
            so.FindProperty("_listText").objectReferenceValue = listTmp;
            var settingsGuids = AssetDatabase.FindAssets("t:NymoraBackendSettings");
            if (settingsGuids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(settingsGuids[0]);
                so.FindProperty("_backendSettings").objectReferenceValue = AssetDatabase.LoadMainAssetAtPath(path);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            actions.Add("LeaderboardPanel wires (panelRoot/close/listText/backendSettings)");

            panelRoot.SetActive(false);
        }

        private static GameObject MakeButton(Transform parent, string name, string label, Color color, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            if (width > 0 && height > 0) rt.sizeDelta = new Vector2(width, height);
            var img = go.GetComponent<Image>();
            img.color = color;
            go.GetComponent<Button>().targetGraphic = img;
            var labelGo = NewChild("Label", go.transform);
            StretchToParent(labelGo);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label; tmp.fontSize = 22f; tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center; tmp.fontStyle = FontStyles.Bold;
            return go;
        }

        private static GameObject NewChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void StretchToParent(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
    }
}
