using System.Collections.Generic;
using Nymora.Hub;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Brique 4.X — Tool de patch incremental de la scène 10_CommunityHub.
    ///
    /// Contrairement à `Create Community Hub Scene` qui REGEN tout (écrase tweaks Inspector),
    /// ce tool n'agit que sur ce qui est MANQUANT ou NULL :
    ///   - Ajoute un GameObject seulement s'il n'existe pas
    ///   - Wire un SerializedField seulement s'il est null
    /// Aucun tweak existant n'est écrasé.
    ///
    /// Limite : ne créé PAS les éléments UI complexes (chat panel, popup, etc.) — pour ça
    /// faut Create Community Hub Scene (qui écrase). Le tool Patch couvre les ajouts
    /// récents (HubBanEditMode, refs ban list, etc.).
    ///
    /// Menu : Nymora > Setup > Patch Community Hub Scene
    /// </summary>
    public static class PatchCommunityHubSceneTool
    {
        private const string ScenePath = "Assets/_Nymora/Scenes/10_CommunityHub.unity";
        private const string HubGridBanListPath = "Assets/_Nymora/ScriptableObjects/Hub/HubGridBanList.asset";

        [MenuItem("Nymora/Setup/Patch Community Hub Scene", priority = 34)]
        private static void Patch()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Patch Community Hub Scene",
                    "Impossible de patcher pendant Play Mode.\nStoppe Play (Ctrl+P) puis relance ce menu.",
                    "OK");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.path.EndsWith("10_CommunityHub.unity"))
            {
                if (!EditorUtility.DisplayDialog("Patch Community Hub Scene",
                    $"La scène active n'est pas 10_CommunityHub.\n\n" +
                    $"Active : {scene.path}\nAttendu : {ScenePath}\n\n" +
                    "Ouvrir 10_CommunityHub maintenant ?",
                    "Ouvrir", "Annuler"))
                {
                    return;
                }
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var actions = new List<string>();
            var banList = AssetDatabase.LoadAssetAtPath<HubGridBanList>(HubGridBanListPath);
            if (banList == null)
            {
                actions.Add("⚠ HubGridBanList introuvable — Crée-le via Nymora > Hub > Grid Ban Editor");
            }

            // 1. HubBanEditMode : add if missing
            var banEdit = Object.FindFirstObjectByType<HubBanEditMode>();
            if (banEdit == null)
            {
                var cam = Object.FindFirstObjectByType<Camera>();
                var grid = Object.FindFirstObjectByType<HubGridRenderer>();
                var input = Object.FindFirstObjectByType<HubInputController>();

                var go = new GameObject("HubBanEditMode", typeof(HubBanEditMode));
                SceneManager.MoveGameObjectToScene(go, scene);
                banEdit = go.GetComponent<HubBanEditMode>();
                var so = new SerializedObject(banEdit);
                so.FindProperty("_camera").objectReferenceValue = cam;
                so.FindProperty("_grid").objectReferenceValue = grid;
                so.FindProperty("_input").objectReferenceValue = input;
                if (banList != null) so.FindProperty("_banList").objectReferenceValue = banList;
                so.ApplyModifiedPropertiesWithoutUndo();
                actions.Add("+ GO 'HubBanEditMode' ajouté (touche B = mode ban clic)");
            }
            else
            {
                // Wire refs si null sur le composant existant
                var so = new SerializedObject(banEdit);
                if (TryWireIfNull(so, "_camera", Object.FindFirstObjectByType<Camera>())) actions.Add("+ HubBanEditMode._camera wire");
                if (TryWireIfNull(so, "_grid", Object.FindFirstObjectByType<HubGridRenderer>())) actions.Add("+ HubBanEditMode._grid wire");
                if (TryWireIfNull(so, "_input", Object.FindFirstObjectByType<HubInputController>())) actions.Add("+ HubBanEditMode._input wire");
                if (banList != null && TryWireIfNull(so, "_banList", banList)) actions.Add("+ HubBanEditMode._banList wire");
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // 2. HubGridRenderer._banList wire si null
            var hubGrid2 = Object.FindFirstObjectByType<HubGridRenderer>();
            if (hubGrid2 != null && banList != null)
            {
                var so = new SerializedObject(hubGrid2);
                if (TryWireIfNull(so, "_banList", banList)) actions.Add("+ HubGridRenderer._banList wire");
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // 3. HubInputController._banList wire si null
            var hubInput = Object.FindFirstObjectByType<HubInputController>();
            if (hubInput != null && banList != null)
            {
                var so = new SerializedObject(hubInput);
                if (TryWireIfNull(so, "_banList", banList)) actions.Add("+ HubInputController._banList wire");
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // 4. UI indicators ban edit mode (banner + hover coords)
            if (banEdit != null)
            {
                EnsureBanModeUI(scene, banEdit, actions);
            }

            // 5. HubTileHoverView : add if missing
            var hoverView = Object.FindFirstObjectByType<HubTileHoverView>();
            if (hoverView == null)
            {
                var cam = Object.FindFirstObjectByType<Camera>();
                var grid = Object.FindFirstObjectByType<HubGridRenderer>();

                var go = new GameObject("HubTileHoverView", typeof(HubTileHoverView));
                SceneManager.MoveGameObjectToScene(go, scene);
                hoverView = go.GetComponent<HubTileHoverView>();
                var so = new SerializedObject(hoverView);
                so.FindProperty("_camera").objectReferenceValue = cam;
                so.FindProperty("_grid").objectReferenceValue = grid;
                so.ApplyModifiedPropertiesWithoutUndo();
                actions.Add("+ GO 'HubTileHoverView' ajouté (preview tile au survol souris)");
            }
            else
            {
                var so = new SerializedObject(hoverView);
                if (TryWireIfNull(so, "_camera", Object.FindFirstObjectByType<Camera>())) actions.Add("+ HubTileHoverView._camera wire");
                if (TryWireIfNull(so, "_grid", Object.FindFirstObjectByType<HubGridRenderer>())) actions.Add("+ HubTileHoverView._grid wire");
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(scene);

            string summary = actions.Count == 0
                ? "✓ Scène déjà à jour, rien à patcher."
                : "Patch appliqué :\n\n" + string.Join("\n", actions) + "\n\n⚠ N'oublie pas Ctrl+S pour sauvegarder la scène.";
            Debug.Log($"[Nymora.Setup] Patch hub : {actions.Count} action(s)\n{summary}");
            EditorUtility.DisplayDialog("Patch Community Hub Scene", summary, "OK");
        }

        private static bool TryWireIfNull(SerializedObject so, string propName, Object value)
        {
            if (value == null) return false;
            var prop = so.FindProperty(propName);
            if (prop == null) return false;
            if (prop.objectReferenceValue != null) return false;
            prop.objectReferenceValue = value;
            return true;
        }

        private static void EnsureBanModeUI(Scene scene, HubBanEditMode banEdit, List<string> actions)
        {
            // Le Canvas existe forcément (créé par Create Community Hub Scene)
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            // Banner "BAN EDIT MODE"
            var existingBanner = canvas.transform.Find("BanModeIndicator");
            GameObject banner;
            TextMeshProUGUI bannerText;
            if (existingBanner == null)
            {
                banner = new GameObject("BanModeIndicator", typeof(RectTransform), typeof(Image));
                banner.transform.SetParent(canvas.transform, false);
                var img = banner.GetComponent<Image>();
                img.color = new Color(0.7f, 0.2f, 0.2f, 0.85f);
                var rt = banner.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -20f);
                rt.sizeDelta = new Vector2(520f, 50f);

                var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelGo.transform.SetParent(banner.transform, false);
                bannerText = labelGo.GetComponent<TextMeshProUGUI>();
                bannerText.text = "BAN EDIT MODE — clic gauche = toggle ban — touche B pour quitter";
                bannerText.fontSize = 20;
                bannerText.color = Color.white;
                bannerText.alignment = TextAlignmentOptions.Center;
                var labelRt = labelGo.GetComponent<RectTransform>();
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;
                actions.Add("+ UI 'BanModeIndicator' ajouté");
            }
            else
            {
                banner = existingBanner.gameObject;
                bannerText = banner.GetComponentInChildren<TextMeshProUGUI>();
            }

            // Hover coords text
            var existingHover = canvas.transform.Find("BanHoverCoords");
            GameObject hoverGo;
            TextMeshProUGUI hoverText;
            RectTransform hoverRt;
            if (existingHover == null)
            {
                hoverGo = new GameObject("BanHoverCoords", typeof(RectTransform), typeof(TextMeshProUGUI));
                hoverGo.transform.SetParent(canvas.transform, false);
                hoverText = hoverGo.GetComponent<TextMeshProUGUI>();
                hoverText.text = "(0, 0)";
                hoverText.fontSize = 22;
                hoverText.color = new Color(1f, 0.95f, 0.4f);
                hoverText.alignment = TextAlignmentOptions.Left;
                hoverText.fontStyle = FontStyles.Bold;
                hoverRt = hoverGo.GetComponent<RectTransform>();
                hoverRt.anchorMin = Vector2.zero;
                hoverRt.anchorMax = Vector2.zero;
                hoverRt.pivot = new Vector2(0f, 0f);
                hoverRt.sizeDelta = new Vector2(260f, 36f);
                actions.Add("+ UI 'BanHoverCoords' ajouté");
            }
            else
            {
                hoverGo = existingHover.gameObject;
                hoverText = hoverGo.GetComponent<TextMeshProUGUI>();
                hoverRt = hoverGo.GetComponent<RectTransform>();
            }

            // Wire SerializedObject de HubBanEditMode si null
            var so = new SerializedObject(banEdit);
            if (TryWireIfNull(so, "_modeIndicatorRoot", banner)) actions.Add("+ HubBanEditMode._modeIndicatorRoot wire");
            if (TryWireIfNull(so, "_hoverCoordsText", hoverText)) actions.Add("+ HubBanEditMode._hoverCoordsText wire");
            if (TryWireIfNull(so, "_hoverCoordsRect", hoverRt)) actions.Add("+ HubBanEditMode._hoverCoordsRect wire");
            so.ApplyModifiedPropertiesWithoutUndo();

            // Disable au démarrage : le HubBanEditMode.Start() les set actifs uniquement quand mode B
            banner.SetActive(false);
            hoverGo.SetActive(false);
        }
    }
}
