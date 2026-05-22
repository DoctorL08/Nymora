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
    /// Brique 6.1 — Patch scene 10_CommunityHub : ajoute le panneau
    /// "Recherche de partie classee" (RankedSearchPanelHost + HubRankedSearchPanel).
    ///
    /// Ouvert par le bouton "Ranked 1v1" du menu Arene (HubArenaPanel.OnRanked1v1Clicked
    /// via HubRankedSearchPanel.Instance.Open()). Pas de reference serialisee croisee :
    /// le lien Arene -> Recherche passe par le singleton.
    ///
    /// Idempotent (rebuild le contenu du host a chaque passage). Rejouable.
    ///
    /// Menu : Nymora > Setup > Patch Ranked Search Panel.
    /// </summary>
    public static class PatchRankedSearchPanelTool
    {
        private const string ScenePath = "Assets/_Nymora/Scenes/10_CommunityHub.unity";

        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.65f);
        private static readonly Color ContainerColor = new Color(0.12f, 0.13f, 0.16f, 0.98f);
        private static readonly Color HeaderColor = new Color(0.18f, 0.20f, 0.25f, 1f);
        private static readonly Color CloseColor = new Color(0.55f, 0.25f, 0.25f, 1f);
        private static readonly Color SearchColor = new Color(0.45f, 0.38f, 0.62f, 1f); // violet "classe"
        private static readonly Color CancelColor = new Color(0.40f, 0.30f, 0.30f, 1f);

        [MenuItem("Nymora/Setup/Patch Ranked Search Panel", priority = 39)]
        private static void Patch()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Patch Ranked Search", "Impossible pendant Play Mode.", "OK");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.path.EndsWith("10_CommunityHub.unity"))
            {
                if (!EditorUtility.DisplayDialog("Patch Ranked Search",
                    $"Scene active : {scene.path}\nAttendu : {ScenePath}\n\nOuvrir 10_CommunityHub ?", "Ouvrir", "Annuler")) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) { EditorUtility.DisplayDialog("Patch Ranked Search", "Aucun Canvas.", "OK"); return; }

            var actions = new List<string>();
            EnsureSearchPanel(canvas, actions);

            EditorSceneManager.MarkSceneDirty(scene);
            string summary = actions.Count == 0 ? "OK Scene deja a jour." : "Patch applique :\n\n" + string.Join("\n", actions) + "\n\nN'oublie pas Ctrl+S.";
            Debug.Log($"[Nymora.Setup] Patch ranked search : {actions.Count} action(s)\n{summary}");
            EditorUtility.DisplayDialog("Patch Ranked Search", summary, "OK");
        }

        private static void EnsureSearchPanel(Canvas canvas, List<string> actions)
        {
            var existing = canvas.transform.Find("RankedSearchPanelHost");
            HubRankedSearchPanel panel;
            GameObject hostGo;

            if (existing != null)
            {
                hostGo = existing.gameObject;
                panel = hostGo.GetComponent<HubRankedSearchPanel>() ?? hostGo.AddComponent<HubRankedSearchPanel>();
                for (int i = hostGo.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(hostGo.transform.GetChild(i).gameObject);
                actions.Add("RankedSearchPanelHost cleanup (rebuild)");
            }
            else
            {
                hostGo = new GameObject("RankedSearchPanelHost", typeof(RectTransform));
                hostGo.transform.SetParent(canvas.transform, false);
                StretchToParent(hostGo);
                panel = hostGo.AddComponent<HubRankedSearchPanel>();
                actions.Add("RankedSearchPanelHost cree");
            }

            // PanelRoot toggleable
            var panelRoot = NewChild("PanelRoot", hostGo.transform);
            StretchToParent(panelRoot);

            // Backdrop fullscreen
            var backdrop = NewChild("Backdrop", panelRoot.transform);
            StretchToParent(backdrop);
            backdrop.AddComponent<Image>().color = BackdropColor;

            // Container modal centre 560x360
            var container = NewChild("Container", panelRoot.transform);
            var crt = container.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(560f, 360f);
            container.AddComponent<Image>().color = ContainerColor;

            // Header bar + titre + close
            var header = NewChild("Header", container.transform);
            var hRt = header.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0f, 1f);
            hRt.anchorMax = new Vector2(1f, 1f);
            hRt.pivot = new Vector2(0.5f, 1f);
            hRt.sizeDelta = new Vector2(0f, 56f);
            header.AddComponent<Image>().color = HeaderColor;

            var title = NewChild("Title", header.transform);
            StretchToParent(title);
            var titleTmp = title.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "Partie classée 1v1";
            titleTmp.fontSize = 26f;
            titleTmp.color = Color.white;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.fontStyle = FontStyles.Bold;

            var closeGo = MakeButton(header.transform, "CloseButton", "X", CloseColor, 44f, 40f);
            var crtClose = closeGo.GetComponent<RectTransform>();
            crtClose.anchorMin = crtClose.anchorMax = new Vector2(1f, 0.5f);
            crtClose.pivot = new Vector2(1f, 0.5f);
            crtClose.anchoredPosition = new Vector2(-12f, 0f);

            // Statut (texte central)
            var statusGo = NewChild("Status", container.transform);
            var sRt = statusGo.GetComponent<RectTransform>();
            sRt.anchorMin = new Vector2(0f, 0f);
            sRt.anchorMax = new Vector2(1f, 1f);
            sRt.offsetMin = new Vector2(30f, 120f);
            sRt.offsetMax = new Vector2(-30f, -76f);
            var statusTmp = statusGo.AddComponent<TextMeshProUGUI>();
            statusTmp.text = "Prêt à chercher une partie classée 1v1.";
            statusTmp.fontSize = 22f;
            statusTmp.color = new Color(0.88f, 0.88f, 0.92f);
            statusTmp.alignment = TextAlignmentOptions.Center;
            statusTmp.enableWordWrapping = true;

            // Bouton Rechercher (visible par defaut)
            var searchGo = MakeButton(container.transform, "SearchButton", "Rechercher", SearchColor, 0f, 0f);
            var searchRt = searchGo.GetComponent<RectTransform>();
            searchRt.anchorMin = new Vector2(0.5f, 0f);
            searchRt.anchorMax = new Vector2(0.5f, 0f);
            searchRt.pivot = new Vector2(0.5f, 0f);
            searchRt.anchoredPosition = new Vector2(0f, 36f);
            searchRt.sizeDelta = new Vector2(320f, 64f);

            // Bouton Annuler (cache par defaut, prend la meme place que Rechercher)
            var cancelGo = MakeButton(container.transform, "CancelButton", "Annuler la recherche", CancelColor, 0f, 0f);
            var cancelRt = cancelGo.GetComponent<RectTransform>();
            cancelRt.anchorMin = new Vector2(0.5f, 0f);
            cancelRt.anchorMax = new Vector2(0.5f, 0f);
            cancelRt.pivot = new Vector2(0.5f, 0f);
            cancelRt.anchoredPosition = new Vector2(0f, 36f);
            cancelRt.sizeDelta = new Vector2(320f, 64f);
            cancelGo.SetActive(false);

            // Wire panel
            var so = new SerializedObject(panel);
            so.FindProperty("_panelRoot").objectReferenceValue = panelRoot;
            so.FindProperty("_closeButton").objectReferenceValue = closeGo.GetComponent<Button>();
            so.FindProperty("_searchButton").objectReferenceValue = searchGo.GetComponent<Button>();
            so.FindProperty("_cancelButton").objectReferenceValue = cancelGo.GetComponent<Button>();
            so.FindProperty("_statusText").objectReferenceValue = statusTmp;
            // 6.5 — wire l'asset NymoraBackendSettings (pour GET /ranked/season).
            var settingsGuids = AssetDatabase.FindAssets("t:NymoraBackendSettings");
            if (settingsGuids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(settingsGuids[0]);
                so.FindProperty("_backendSettings").objectReferenceValue = AssetDatabase.LoadMainAssetAtPath(path);
                actions.Add($"_backendSettings wire ({System.IO.Path.GetFileName(path)})");
            }
            else
            {
                actions.Add("⚠ NymoraBackendSettings introuvable — _backendSettings non wire (saison non affichee)");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            actions.Add("RankedSearchPanel SerializedFields wires");

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
            tmp.text = label;
            tmp.fontSize = 22f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
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
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
