using Nymora.Combat.View.HUD;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;
using Object = UnityEngine.Object;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Brique 3.E.3 — Genere le panel debug overlay (F12) dans la scene active.
    ///
    /// Cree sous le Canvas trouve (priorite CombatHUDCanvas) :
    ///   CombatDebugOverlayPanel (top-right, fond noir 85%)
    ///     |-> ContentText (TMP monospace, color blanc)
    /// Add CombatDebugOverlay component sur le panel + wire la ref content.
    ///
    /// Menu : Nymora > Setup > Build Combat Debug Overlay
    /// Idempotent : detruit l'ancien panel avant de recreer.
    /// </summary>
    public static class BuildCombatDebugOverlayTool
    {
        private const string PanelName = "CombatDebugOverlayPanel";
        private const string CanvasFallbackName = "DebugOverlayCanvas";

        [MenuItem("Nymora/Setup/Build Combat Debug Overlay")]
        public static void Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("Build Combat Debug Overlay", "Aucune scene ouverte.", "OK");
                return;
            }

            Canvas canvas = FindOrCreateCanvas();

            var existing = FindChildByName(canvas.transform, PanelName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            // Panel root (anchor top-right, fixed 480x600).
            var panelGo = new GameObject(PanelName, typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(canvas.transform, false);
            var rt = panelGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-20f, -20f);
            rt.sizeDelta = new Vector2(480f, 600f);
            var bg = panelGo.GetComponent<Image>();
            bg.color = new Color(0.03f, 0.03f, 0.05f, 0.85f);
            bg.raycastTarget = false; // n'intercepte pas les clics combat

            // Content TMP (monospace si dispo, sinon font par defaut TMP).
            var contentGo = new GameObject("ContentText", typeof(RectTransform));
            contentGo.transform.SetParent(panelGo.transform, false);
            var contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.anchorMin = Vector2.zero;
            contentRt.anchorMax = Vector2.one;
            contentRt.offsetMin = new Vector2(10f, 10f);
            contentRt.offsetMax = new Vector2(-10f, -10f);
            var content = contentGo.AddComponent<TextMeshProUGUI>();
            content.fontSize = 13;
            content.color = new Color(0.85f, 0.95f, 0.85f, 1f);
            content.alignment = TextAlignmentOptions.TopLeft;
            content.enableWordWrapping = false;
            content.overflowMode = TextOverflowModes.Truncate;
            content.raycastTarget = false;
            content.text = "(F12 pour afficher / cacher)";

            // Add component + wire.
            var overlay = panelGo.AddComponent<CombatDebugOverlay>();
            var so = new SerializedObject(overlay);
            SetObjectRef(so, "_panel", panelGo);
            SetObjectRef(so, "_content", content);
            so.ApplyModifiedPropertiesWithoutUndo();

            // IMPORTANT : panelGo reste ACTIF au start sinon Awake() n'est jamais appele,
            // donc QuantumCallback.Subscribe n'a pas lieu et la touche de toggle est ignoree.
            // Le component hide visuellement via SetVisible(false) en desactivant les enfants
            // + le background Image (pas le GameObject porteur).

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = panelGo;
            EditorGUIUtility.PingObject(panelGo);

            EditorUtility.DisplayDialog("Build Combat Debug Overlay",
                "Overlay debug genere dans la scene '" + scene.name + "'.\n\n" +
                "Toggle in-game avec la touche F12.\n\n" +
                "N'oublie pas Ctrl+S.",
                "OK");
        }

        private static Canvas FindOrCreateCanvas()
        {
            var combatHud = GameObject.Find("CombatHUDCanvas");
            if (combatHud != null)
            {
                var c = combatHud.GetComponent<Canvas>();
                if (c != null) return c;
            }
            var anyCanvas = Object.FindAnyObjectByType<Canvas>();
            if (anyCanvas != null) return anyCanvas;

            var canvasGo = new GameObject(CanvasFallbackName,
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c.name == name) return c;
            }
            return null;
        }

        private static void SetObjectRef(SerializedObject so, string fieldName, Object value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning("[Nymora.BuildDebugOverlay] Field '" + fieldName + "' introuvable sur " + so.targetObject.GetType().Name);
                return;
            }
            prop.objectReferenceValue = value;
        }
    }
}
