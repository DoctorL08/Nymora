using System.Collections.Generic;
using System.IO;
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
    /// Brique 4.8.d.ii — Genere Assets/_Nymora/Scenes/33_CombatCasual.unity (stub).
    ///
    /// Contenu minimal :
    ///   - Main Camera (orthographique, fond sombre)
    ///   - EventSystem (pour cliquer ulterieurement)
    ///   - Canvas + Text TMP "33_CombatCasual stub"
    ///   - GameObject "MatchTestLogger" qui lit MatchBridge au Start et affiche les infos
    ///
    /// Ajoute aussi la scene aux EditorBuildSettings (sinon SceneManager.LoadScene crash).
    ///
    /// Sera remplace par le vrai bootstrap Quantum 2 joueurs en 4.8.d.iii.
    ///
    /// Menu : Nymora > Setup > Create Combat Casual Scene
    /// </summary>
    public static class CreateCombatCasualSceneTool
    {
        private const string ScenesFolder = "Assets/_Nymora/Scenes";
        private const string ScenePath = ScenesFolder + "/33_CombatCasual.unity";

        [MenuItem("Nymora/Setup/Create Combat Casual Scene", priority = 35)]
        private static void CreateScene()
        {
            EnsureFolder(ScenesFolder);

            if (File.Exists(ScenePath))
            {
                if (!EditorUtility.DisplayDialog("Combat Casual Scene",
                    "33_CombatCasual.unity existe deja. La regenerer (ecrasement) ?",
                    "Regenerer", "Annuler"))
                {
                    return;
                }
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Main Camera
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            SceneManager.MoveGameObjectToScene(camGo, scene);
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.05f, 0.1f, 1f);

            // EventSystem
            var eventSystemGo = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(eventSystemGo, scene);

            // Canvas
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasGo, scene);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Display text centre ecran (haut)
            var textGo = new GameObject("MatchInfoText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(canvasGo.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.5f, 0.7f);
            textRt.anchorMax = new Vector2(0.5f, 0.7f);
            textRt.pivot = new Vector2(0.5f, 0.5f);
            textRt.anchoredPosition = Vector2.zero;
            textRt.sizeDelta = new Vector2(1200f, 400f);
            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.text = "33_CombatCasual\n(en attente d'init MatchBridge...)";
            text.fontSize = 28;
            text.color = new Color(0.92f, 0.92f, 0.95f);
            text.alignment = TextAlignmentOptions.Center;

            // 4.8.d.iii stub : 3 boutons Victoire / Défaite / Égalité en bas
            var victoryBtn = CreateResultButton(canvasGo, "VictoryButton", "Victoire", new Color(0.25f, 0.55f, 0.35f, 1f), -260f);
            var drawBtn = CreateResultButton(canvasGo, "DrawButton", "Égalité", new Color(0.45f, 0.45f, 0.45f, 1f), 0f);
            var defeatBtn = CreateResultButton(canvasGo, "DefeatButton", "Défaite", new Color(0.55f, 0.25f, 0.25f, 1f), 260f);

            // MatchTestLogger GO
            var loggerGo = new GameObject("MatchTestLogger", typeof(MatchTestLogger));
            SceneManager.MoveGameObjectToScene(loggerGo, scene);
            var logger = loggerGo.GetComponent<MatchTestLogger>();
            var loggerSo = new SerializedObject(logger);
            loggerSo.FindProperty("_displayText").objectReferenceValue = text;
            loggerSo.FindProperty("_victoryButton").objectReferenceValue = victoryBtn;
            loggerSo.FindProperty("_defeatButton").objectReferenceValue = defeatBtn;
            loggerSo.FindProperty("_drawButton").objectReferenceValue = drawBtn;
            loggerSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);

            Debug.Log($"[Nymora.Setup] Scene generee : {ScenePath} (+ ajoutee aux BuildSettings)");

            EditorUtility.DisplayDialog("Combat Casual Scene",
                $"Scene generee : {ScenePath}\n\n" +
                "AJOUTEE AUX BUILD SETTINGS : OK\n\n" +
                "Cette scene est un stub 4.8.d.ii. Sera remplacee par le bootstrap Quantum 2 joueurs en 4.8.d.iii.\n\n" +
                "ETAPES SUIVANTES :\n" +
                "1. Verifie que 10_CommunityHub est aussi dans Build Settings\n" +
                "   (File > Build Settings, ajoute si manquant)\n" +
                "2. Regenere la scene Hub : Nymora > Setup > Create Community Hub Scene\n" +
                "3. Recolle le JWT dans HubChatClient._devToken\n" +
                "4. Rebuild standalone + Editor Play -> defie -> accept -> transition auto vers 33_CombatCasual",
                "OK");
        }

        private static Button CreateResultButton(GameObject parent, string name, string label, Color color, float xOffset)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent.transform, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.2f);
            rt.anchorMax = new Vector2(0.5f, 0.2f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(xOffset, 0f);
            rt.sizeDelta = new Vector2(220f, 80f);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var labelText = labelGo.GetComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = 28;
            labelText.color = Color.white;
            labelText.alignment = TextAlignmentOptions.Center;

            return go.GetComponent<Button>();
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes)
            {
                if (s.path == scenePath)
                {
                    s.enabled = true;
                    EditorBuildSettings.scenes = scenes.ToArray();
                    return;
                }
            }
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureFolder(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }
        }
    }
}
