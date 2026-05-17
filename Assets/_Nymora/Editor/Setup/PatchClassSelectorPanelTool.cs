using System.Collections.Generic;
using Nymora.Core.Enums;
using Nymora.Core.ScriptableObjects;
using Nymora.Hub;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Brique 5.3.f — Patch scene 10_CommunityHub pour ajouter :
    ///   1. Le bouton "Changer de classe" dans le header du DeckBuilder
    ///   2. Le panel ClassSelectorPanelHost overlay avec 5 cards
    ///
    /// Pre-req : Patch Deck Builder Panel deja execute (DeckBuilderPanelHost doit exister).
    /// Pre-req : Populate Class Definitions deja execute (Lore + IdleAnimator dans les 5 .asset).
    ///
    /// Menu : Nymora > Setup > Patch Class Selector Panel.
    /// </summary>
    public static class PatchClassSelectorPanelTool
    {
        private const string ScenePath = "Assets/_Nymora/Scenes/10_CommunityHub.unity";
        private const string ClassesFolder = "Assets/_Nymora/ScriptableObjects/Classes";

        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.75f);
        private static readonly Color ContainerColor = new Color(0.08f, 0.09f, 0.12f, 0.98f);
        private static readonly Color HeaderColor = new Color(0.18f, 0.20f, 0.25f, 1f);
        private static readonly Color CloseColor = new Color(0.55f, 0.25f, 0.25f, 1f);
        private static readonly Color ChangeClassBtnColor = new Color(0.45f, 0.30f, 0.55f, 1f);

        [MenuItem("Nymora/Setup/Patch Class Selector Panel", priority = 37)]
        private static void Patch()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Patch Class Selector", "Impossible pendant Play Mode.", "OK");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.path.EndsWith("10_CommunityHub.unity"))
            {
                if (!EditorUtility.DisplayDialog("Patch Class Selector",
                    $"Scene active : {scene.path}\nAttendu : {ScenePath}\n\nOuvrir 10_CommunityHub ?", "Ouvrir", "Annuler")) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) { EditorUtility.DisplayDialog("Patch Class Selector", "Aucun Canvas.", "OK"); return; }

            // Charge les 5 ClassDefinition.asset
            var classDefs = new List<NymoraClassDefinition>();
            foreach (NymoraClass cls in System.Enum.GetValues(typeof(NymoraClass)))
            {
                if (cls == NymoraClass.None) continue;
                var def = AssetDatabase.LoadAssetAtPath<NymoraClassDefinition>($"{ClassesFolder}/{cls}.asset");
                if (def != null) classDefs.Add(def);
            }
            if (classDefs.Count != 5)
            {
                EditorUtility.DisplayDialog("Patch Class Selector",
                    $"Attendu 5 ClassDefinition.asset dans {ClassesFolder}, trouve {classDefs.Count}.\nVerifie les fichiers.", "OK");
                return;
            }

            var actions = new List<string>();
            EnsureChangeClassButton(canvas, actions);
            EnsureClassSelectorPanel(canvas, classDefs.ToArray(), actions);

            EditorSceneManager.MarkSceneDirty(scene);
            string summary = actions.Count == 0 ? "OK Scene deja a jour." : "Patch applique :\n\n" + string.Join("\n", actions) + "\n\nN'oublie pas Ctrl+S.";
            Debug.Log($"[Nymora.Setup] Patch class selector : {actions.Count} action(s)\n{summary}");
            EditorUtility.DisplayDialog("Patch Class Selector", summary, "OK");
        }

        // -----------------------------------------------------------------------------
        // Bouton "Changer de classe" inside DeckBuilder header.
        // -----------------------------------------------------------------------------
        private static void EnsureChangeClassButton(Canvas canvas, List<string> actions)
        {
            var deckBuilderHost = canvas.transform.Find("DeckBuilderPanelHost");
            if (deckBuilderHost == null)
            {
                Debug.LogWarning("[PatchClassSelector] DeckBuilderPanelHost introuvable, run Patch Deck Builder Panel d'abord.");
                return;
            }
            var header = deckBuilderHost.Find("PanelRoot/Container/Header");
            if (header == null) return;

            var existing = header.Find("ChangeClassButton");
            GameObject btnGo;
            if (existing != null)
            {
                btnGo = existing.gameObject;
                actions.Add("ChangeClassButton existant : re-style");
            }
            else
            {
                btnGo = new GameObject("ChangeClassButton", typeof(RectTransform), typeof(Image), typeof(Button));
                btnGo.transform.SetParent(header, false);
                actions.Add("ChangeClassButton cree dans header DeckBuilder");
            }

            var rt = btnGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-70f, 0f); // a gauche de CloseButton (-10, 40px wide)
            rt.sizeDelta = new Vector2(220f, 38f);
            var img = btnGo.GetComponent<Image>() ?? btnGo.AddComponent<Image>();
            img.color = ChangeClassBtnColor;
            if (btnGo.GetComponent<Button>() == null) btnGo.AddComponent<Button>();
            btnGo.GetComponent<Button>().targetGraphic = img;

            // Label propre
            var oldLabel = btnGo.transform.Find("Label");
            if (oldLabel != null) Object.DestroyImmediate(oldLabel.gameObject);
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(btnGo.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var tmp = labelGo.GetComponent<TextMeshProUGUI>();
            tmp.text = "Changer de classe";
            tmp.fontSize = 16f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;

            // Wire le bouton dans HubDeckBuilderPanel _changeClassButton
            var deckBuilderPanel = deckBuilderHost.GetComponent<HubDeckBuilderPanel>();
            if (deckBuilderPanel != null)
            {
                var so = new SerializedObject(deckBuilderPanel);
                so.FindProperty("_changeClassButton").objectReferenceValue = btnGo.GetComponent<Button>();
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // -----------------------------------------------------------------------------
        // Panel ClassSelectorPanelHost (overlay full-screen avec 5 cards).
        // -----------------------------------------------------------------------------
        private static void EnsureClassSelectorPanel(Canvas canvas, NymoraClassDefinition[] classDefs, List<string> actions)
        {
            var existing = canvas.transform.Find("ClassSelectorPanelHost");
            HubClassSelectorPanel panel;
            GameObject hostGo;

            if (existing != null)
            {
                hostGo = existing.gameObject;
                panel = hostGo.GetComponent<HubClassSelectorPanel>() ?? hostGo.AddComponent<HubClassSelectorPanel>();
                // Cleanup childs pour rebuild
                for (int i = hostGo.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(hostGo.transform.GetChild(i).gameObject);
                actions.Add("ClassSelectorPanelHost cleanup (rebuild)");
            }
            else
            {
                hostGo = new GameObject("ClassSelectorPanelHost", typeof(RectTransform));
                hostGo.transform.SetParent(canvas.transform, false);
                Stretch(hostGo);
                panel = hostGo.AddComponent<HubClassSelectorPanel>();
                actions.Add("ClassSelectorPanelHost cree");
            }

            // PanelRoot toggleable
            var panelRoot = MakeChild("PanelRoot", hostGo.transform);
            Stretch(panelRoot);

            // Backdrop fullscreen
            var backdrop = MakeChild("Backdrop", panelRoot.transform);
            Stretch(backdrop);
            backdrop.AddComponent<Image>().color = BackdropColor;

            // Container fullscreen
            var container = MakeChild("Container", panelRoot.transform);
            Stretch(container);
            container.AddComponent<Image>().color = ContainerColor;

            // Header bar
            var header = MakeChild("Header", container.transform);
            var hRt = header.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0f, 1f);
            hRt.anchorMax = new Vector2(1f, 1f);
            hRt.pivot = new Vector2(0.5f, 1f);
            hRt.sizeDelta = new Vector2(0f, 60f);
            header.AddComponent<Image>().color = HeaderColor;

            var title = MakeChild("Title", header.transform);
            Stretch(title);
            var titleTmp = title.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "Choisis ta classe";
            titleTmp.fontSize = 26f;
            titleTmp.color = Color.white;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.fontStyle = FontStyles.Bold;

            // Close button (X)
            var closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(header.transform, false);
            var cRt = closeGo.GetComponent<RectTransform>();
            cRt.anchorMin = cRt.anchorMax = new Vector2(1f, 0.5f);
            cRt.pivot = new Vector2(1f, 0.5f);
            cRt.anchoredPosition = new Vector2(-15f, 0f);
            cRt.sizeDelta = new Vector2(44f, 40f);
            closeGo.GetComponent<Image>().color = CloseColor;
            var closeBtn = closeGo.GetComponent<Button>();
            closeBtn.targetGraphic = closeGo.GetComponent<Image>();
            var closeLabel = MakeChild("X", closeGo.transform);
            Stretch(closeLabel);
            var xTmp = closeLabel.AddComponent<TextMeshProUGUI>();
            xTmp.text = "X";
            xTmp.fontSize = 22f;
            xTmp.color = Color.white;
            xTmp.alignment = TextAlignmentOptions.Center;
            xTmp.fontStyle = FontStyles.Bold;

            // Carousel container (zone vide fullscreen sous header, le panel spawn cards + arrows dedans).
            var carousel = MakeChild("CarouselContainer", container.transform);
            var carRt = carousel.GetComponent<RectTransform>();
            carRt.anchorMin = new Vector2(0f, 0f);
            carRt.anchorMax = new Vector2(1f, 1f);
            carRt.offsetMin = new Vector2(0f, 0f);
            carRt.offsetMax = new Vector2(0f, -60f); // laisse 60 pour header

            // Wire panel
            var so = new SerializedObject(panel);
            so.FindProperty("_panelRoot").objectReferenceValue = panelRoot;
            so.FindProperty("_closeButton").objectReferenceValue = closeBtn;
            so.FindProperty("_carouselContainer").objectReferenceValue = carRt;
            var defsProp = so.FindProperty("_classDefinitions");
            defsProp.arraySize = classDefs.Length;
            for (int i = 0; i < classDefs.Length; i++)
                defsProp.GetArrayElementAtIndex(i).objectReferenceValue = classDefs[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            actions.Add("ClassSelectorPanel SerializedFields wires + 5 ClassDefinition.asset bindes");

            panelRoot.SetActive(false);
        }

        // -----------------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------------
        private static GameObject MakeChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Stretch(GameObject go)
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
