using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nymora.UI.Login;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// B6.5 — Injecte le bouton "Entrer dans le hub" dans la scene 00_Login,
    /// le cable au champ _enterHubButton du LoginScreenController, set _hubSceneName,
    /// et garantit que 00_Login + 10_CommunityHub sont dans les Build Settings.
    ///
    /// Idempotent : si le bouton existe deja, il est juste re-style + re-cable
    /// (pas de doublon). Reproduit le style exact de CreateLoginSceneTool.CreateButton,
    /// avec une teinte verte pour signaler l'action principale post-login.
    ///
    /// Menu : Nymora &gt; Setup &gt; Add Enter Hub Button (Login Scene)
    /// </summary>
    public static class AddEnterHubButtonTool
    {
        private const string LoginScenePath = "Assets/_Nymora/Scenes/00_Login.unity";
        private const string HubScenePath = "Assets/_Nymora/Scenes/10_CommunityHub.unity";
        private const string HubSceneName = "10_CommunityHub";
        private const string ButtonName = "EnterHubButton";

        [MenuItem("Nymora/Setup/Add Enter Hub Button (Login Scene)", priority = 33)]
        private static void AddEnterHubButton()
        {
            if (!File.Exists(LoginScenePath))
            {
                EditorUtility.DisplayDialog("Enter Hub Button",
                    $"Scene introuvable : {LoginScenePath}\n\nLance d'abord Nymora > Setup > Create Login Scene.",
                    "OK");
                return;
            }

            // Sauvegarde les scenes modifiees avant d'ouvrir la scene login.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scene = EditorSceneManager.OpenScene(LoginScenePath, OpenSceneMode.Single);

            var controller = Object.FindAnyObjectByType<LoginScreenController>();
            if (controller == null)
            {
                EditorUtility.DisplayDialog("Enter Hub Button",
                    "LoginScreenController introuvable dans la scene 00_Login.",
                    "OK");
                return;
            }

            // Canvas : on prend celui qui heberge deja les boutons login.
            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("Enter Hub Button",
                    "Aucun Canvas trouve dans 00_Login.",
                    "OK");
                return;
            }

            // Cherche un bouton existant (idempotence) sinon le cree.
            var existing = canvas.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == ButtonName);

            Button enterBtn;
            if (existing != null)
            {
                enterBtn = existing.GetComponent<Button>();
                StyleEnterButton(enterBtn, existing.gameObject);
                Debug.Log("[Nymora.Setup] EnterHubButton existant : re-style + re-cable (pas de doublon).");
            }
            else
            {
                enterBtn = CreateButton(canvas.gameObject, ButtonName, "Entrer dans le hub", 320f, -250f, width: 280f);
                StyleEnterButton(enterBtn, enterBtn.gameObject);
                Debug.Log("[Nymora.Setup] EnterHubButton cree.");
            }

            // Cablage de la reference + nom de scene via SerializedObject.
            var so = new SerializedObject(controller);
            so.FindProperty("_enterHubButton").objectReferenceValue = enterBtn;
            var hubNameProp = so.FindProperty("_hubSceneName");
            if (hubNameProp != null) hubNameProp.stringValue = HubSceneName;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Le bouton est cache par defaut (le controller l'active apres login).
            enterBtn.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            EnsureScenesInBuild();

            EditorUtility.DisplayDialog("Enter Hub Button",
                "Bouton 'Entrer dans le hub' injecte + cable au LoginScreenController.\n\n" +
                $"- _enterHubButton : assigne\n" +
                $"- _hubSceneName : {HubSceneName}\n" +
                "- Build Settings : 00_Login + 10_CommunityHub garantis\n\n" +
                "Test : Play sur 00_Login -> Register -> le bouton apparait -> Entrer.",
                "OK");
        }

        private static void StyleEnterButton(Button btn, GameObject go)
        {
            // Teinte verte = action principale (distincte du bleu des autres boutons).
            var img = go.GetComponent<Image>();
            if (img != null) img.color = new Color(0.22f, 0.55f, 0.33f, 1f);
        }

        // --- Helpers (repris a l'identique de CreateLoginSceneTool pour coherence visuelle) ---

        private static GameObject CreateUIChild(GameObject parent, string name, params System.Type[] components)
        {
            var allComponents = new System.Type[components.Length + 1];
            allComponents[0] = typeof(RectTransform);
            for (int i = 0; i < components.Length; i++) allComponents[i + 1] = components[i];

            var go = new GameObject(name, allComponents);
            go.transform.SetParent(parent.transform, worldPositionStays: false);
            return go;
        }

        private static Button CreateButton(GameObject parent, string name, string label, float x, float y, float width = 200f)
        {
            var btnGo = CreateUIChild(parent, name, typeof(Image), typeof(Button));
            var img = btnGo.GetComponent<Image>();
            img.color = new Color(0.25f, 0.35f, 0.55f, 1f);
            PositionAbsolute(btnGo.GetComponent<RectTransform>(), x, y, width, 70f);

            var labelGo = CreateUIChild(btnGo, "Label", typeof(TextMeshProUGUI));
            var labelText = labelGo.GetComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = 28;
            labelText.color = Color.white;
            labelText.alignment = TextAlignmentOptions.Center;
            StretchFull(labelGo.GetComponent<RectTransform>());

            return btnGo.GetComponent<Button>();
        }

        private static void PositionAbsolute(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void EnsureScenesInBuild()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            bool changed = false;

            foreach (var path in new[] { LoginScenePath, HubScenePath })
            {
                if (!File.Exists(path)) continue;
                if (scenes.Any(s => s.path == path)) continue;
                scenes.Add(new EditorBuildSettingsScene(path, true));
                changed = true;
                Debug.Log($"[Nymora.Setup] Scene ajoutee aux Build Settings : {path}");
            }

            if (changed)
            {
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
