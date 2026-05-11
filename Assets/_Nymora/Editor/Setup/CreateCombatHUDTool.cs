using Nymora.Combat.View;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Cree un Canvas + GameObject HUD dans la scene active avec le component
    /// CombatHUDView pre-cable. HUD placeholder Phase 2 — repolish en Phase 7.
    ///
    /// Menu : Nymora > Setup > Create Combat HUD
    /// Idempotent : si le HUD existe deja dans la scene, le tool le remplace.
    /// </summary>
    public static class CreateCombatHUDTool
    {
        private const string CanvasName = "CombatHUDCanvas";
        private const string HudName = "CombatHUD";

        [MenuItem("Nymora/Setup/Create Combat HUD")]
        public static void Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("Create Combat HUD", "Aucune scene ouverte.", "OK");
                return;
            }

            // Supprime un eventuel ancien Canvas HUD pour repartir propre.
            var existing = GameObject.Find(CanvasName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            // Canvas en ScreenSpaceOverlay (HUD plein ecran).
            var canvasGo = new GameObject(CanvasName,
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // HUD : GameObject avec TMP_Text ancre en haut centre.
            var hudGo = new GameObject(HudName, typeof(RectTransform), typeof(TextMeshProUGUI));
            hudGo.transform.SetParent(canvasGo.transform, false);

            var rect = hudGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -20f);
            rect.sizeDelta = new Vector2(0f, 60f);

            var text = hudGo.GetComponent<TextMeshProUGUI>();
            text.text = "(combat non initialise)";
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 28f;
            text.color = Color.white;
            text.fontStyle = FontStyles.Bold;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;

            var hudView = hudGo.AddComponent<CombatHUDView>();
            var so = new SerializedObject(hudView);
            var labelProp = so.FindProperty("_label");
            if (labelProp != null)
            {
                labelProp.objectReferenceValue = text;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // EventSystem si pas deja present (necessaire pour UI Unity).
            var eventSystem = Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem == null)
            {
                var esGo = new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(esGo, "Create EventSystem");
            }

            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Combat HUD");
            EditorSceneManager.MarkSceneDirty(scene);

            Selection.activeGameObject = hudGo;
            Debug.Log($"[Nymora.CreateCombatHUDTool] HUD genere dans la scene '{scene.name}'. Sauve la scene (Ctrl+S).");
            EditorUtility.DisplayDialog(
                "Create Combat HUD",
                $"Canvas '{CanvasName}' + HUD '{HudName}' crees dans la scene '{scene.name}'.\n\n" +
                "Sauve la scene (Ctrl+S) puis lance Play.",
                "OK");
        }
    }
}
