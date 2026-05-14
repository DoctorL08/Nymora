using Nymora.Combat.Replay;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;
using Object = UnityEngine.Object;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Brique 3.E.2 — Genere automatiquement le panel UI Replay Controls dans la scene
    /// active. A lancer dans 31_CombatReplay apres l'avoir duplique depuis 30_CombatIA.
    ///
    /// Cree (sous le Canvas trouve, sinon un nouveau) :
    ///   - ReplayControlsPanel (anchor top-center, fond semi-transparent)
    ///     - MatchInfoLabel  "Colossar vs Nightseer · 12 round(s)"
    ///     - TickLabel       "Tick 145 / 8520"
    ///     - 3 boutons : Play/Pause, Step, Speed
    ///     - ErrorLabel (rouge, hidden par defaut)
    /// Ajoute ReplayPlaybackController sur Camera.main si absent.
    /// Auto-wire toutes les refs Inspector via SerializedObject.
    ///
    /// Menu : Nymora > Setup > Build Replay Controls Panel
    /// Idempotent : detruit l'ancien panel avant de recreer.
    /// </summary>
    public static class BuildReplayControlsPanelTool
    {
        private const string PanelName = "ReplayControlsPanel";
        private const string CanvasFallbackName = "ReplayCanvas";

        [MenuItem("Nymora/Setup/Build Replay Controls Panel")]
        public static void Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("Build Replay Controls Panel", "Aucune scene ouverte.", "OK");
                return;
            }

            // 1. Cherche un Canvas existant (en priorite CombatHUDCanvas), sinon en cree un.
            Canvas canvas = FindOrCreateCanvas();

            // 2. Idempotence : supprime l'ancien panel.
            var existing = FindChildByName(canvas.transform, PanelName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            // 3. Panel root.
            var panelGo = new GameObject(PanelName, typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(canvas.transform, false);
            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -20f);
            panelRect.sizeDelta = new Vector2(440f, 160f);
            var bg = panelGo.GetComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.08f, 0.85f);
            bg.raycastTarget = true;

            // 4. Labels.
            var matchInfo = BuildLabel(panelRect, "MatchInfoLabel",
                anchoredPos: new Vector2(0f, -10f), size: new Vector2(420f, 24f),
                fontSize: 16, color: new Color(0.85f, 0.85f, 0.95f), align: TextAlignmentOptions.Center,
                text: "(match info)");

            var tick = BuildLabel(panelRect, "TickLabel",
                anchoredPos: new Vector2(0f, -36f), size: new Vector2(420f, 22f),
                fontSize: 14, color: new Color(0.70f, 0.85f, 1f), align: TextAlignmentOptions.Center,
                text: "Tick 0 / 0");

            // 5. Boutons (row centree).
            float btnY = -90f;
            Button playPauseBtn = BuildButton(panelRect, "BtnPlayPause",
                anchoredPos: new Vector2(-130f, btnY), size: new Vector2(110f, 36f),
                label: "Pause", out TMP_Text playPauseLabel);
            Button stepBtn = BuildButton(panelRect, "BtnStep",
                anchoredPos: new Vector2(0f, btnY), size: new Vector2(110f, 36f),
                label: "Step +1", out _);
            Button speedBtn = BuildButton(panelRect, "BtnSpeed",
                anchoredPos: new Vector2(130f, btnY), size: new Vector2(110f, 36f),
                label: "1×", out TMP_Text speedLabel);

            // 6. Error label (en bas, hidden par defaut — sera show par les Controls si erreur).
            var error = BuildLabel(panelRect, "ErrorLabel",
                anchoredPos: new Vector2(0f, -140f), size: new Vector2(420f, 18f),
                fontSize: 12, color: new Color(1f, 0.45f, 0.45f), align: TextAlignmentOptions.Center,
                text: "");
            error.gameObject.SetActive(false);

            // 7. Add ReplayPlaybackControls + wire refs.
            var controls = panelGo.AddComponent<ReplayPlaybackControls>();
            var so = new SerializedObject(controls);
            SetObjectRef(so, "_playPauseButton", playPauseBtn);
            SetObjectRef(so, "_playPauseLabel", playPauseLabel);
            SetObjectRef(so, "_stepButton", stepBtn);
            SetObjectRef(so, "_speedButton", speedBtn);
            SetObjectRef(so, "_speedLabel", speedLabel);
            SetObjectRef(so, "_tickLabel", tick);
            SetObjectRef(so, "_errorLabel", error);
            SetObjectRef(so, "_matchInfoLabel", matchInfo);

            // 8. Trouve / cree le controller sur Camera.main.
            ReplayPlaybackController controller = Object.FindAnyObjectByType<ReplayPlaybackController>();
            if (controller == null)
            {
                Camera cam = Camera.main;
                if (cam == null)
                {
                    Debug.LogWarning("[Nymora.BuildReplayControls] Camera.main introuvable — " +
                                     "tu devras Add Component 'ReplayPlaybackController' manuellement.");
                }
                else
                {
                    controller = Undo.AddComponent<ReplayPlaybackController>(cam.gameObject);
                    Debug.Log("[Nymora.BuildReplayControls] ReplayPlaybackController ajoute sur Main Camera.");
                }
            }
            SetObjectRef(so, "_controller", controller);
            so.ApplyModifiedPropertiesWithoutUndo();

            // 9. EventSystem (necessaire pour UI Unity, pas toujours present dans une scene dupliquee).
            EnsureEventSystem();

            // 10. Marker dirty + selection visuelle.
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = panelGo;
            EditorGUIUtility.PingObject(panelGo);

            EditorUtility.DisplayDialog("Build Replay Controls Panel",
                "Panel ReplayControls genere et wire dans la scene '" + scene.name + "'.\n\n" +
                "Mode single-scene : le panel est cache automatiquement quand un match\n" +
                "normal demarre. Il devient visible uniquement quand tu lances un replay\n" +
                "via 'Nymora > Combat > Replay Library'.\n\n" +
                "N'oublie pas de sauvegarder la scene (Ctrl+S).",
                "OK");
        }

        // ---- Helpers ----

        private static Canvas FindOrCreateCanvas()
        {
            // Priorite 1 : un Canvas qui contient deja le HUD combat (CombatHUDCanvas).
            var combatHudCanvasGo = GameObject.Find("CombatHUDCanvas");
            if (combatHudCanvasGo != null)
            {
                var c = combatHudCanvasGo.GetComponent<Canvas>();
                if (c != null) return c;
            }

            // Priorite 2 : tout Canvas actif present.
            var anyCanvas = Object.FindAnyObjectByType<Canvas>();
            if (anyCanvas != null) return anyCanvas;

            // Sinon, on cree un Canvas dedie au replay overlay.
            var canvasGo = new GameObject(CanvasFallbackName,
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150; // au-dessus du HUD combat (100)
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            Debug.Log("[Nymora.BuildReplayControls] Aucun Canvas trouve, " + CanvasFallbackName + " cree.");
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

        private static TMP_Text BuildLabel(RectTransform parent, string name,
            Vector2 anchoredPos, Vector2 size, int fontSize, Color color,
            TextAlignmentOptions align, string text)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = align;
            tmp.text = text;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Button BuildButton(RectTransform parent, string name,
            Vector2 anchoredPos, Vector2 size, string label, out TMP_Text labelTmp)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = new Color(0.18f, 0.20f, 0.28f, 1f);

            // Hover/pressed via ColorBlock standard.
            var btn = go.GetComponent<Button>();
            var cb = btn.colors;
            cb.normalColor = new Color(0.18f, 0.20f, 0.28f, 1f);
            cb.highlightedColor = new Color(0.30f, 0.34f, 0.46f, 1f);
            cb.pressedColor = new Color(0.12f, 0.14f, 0.20f, 1f);
            cb.disabledColor = new Color(0.18f, 0.20f, 0.28f, 0.4f);
            btn.colors = cb;

            // Label TMP enfant centre.
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
            labelTmp.text = label;
            labelTmp.fontSize = 16;
            labelTmp.color = Color.white;
            labelTmp.alignment = TextAlignmentOptions.Center;
            labelTmp.raycastTarget = false;

            return btn;
        }

        private static void SetObjectRef(SerializedObject so, string fieldName, Object value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning("[Nymora.BuildReplayControls] Field '" + fieldName + "' introuvable sur " + so.targetObject.GetType().Name);
                return;
            }
            prop.objectReferenceValue = value;
        }

        private static void EnsureEventSystem()
        {
            var es = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (es != null) return;
            var go = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
            Debug.Log("[Nymora.BuildReplayControls] EventSystem cree (manquant dans la scene).");
        }
    }
}
