using System.IO;
using Nymora.Network.Backend;
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
    /// Genere la scene Assets/_Nymora/Scenes/00_Login.unity avec :
    ///   - Canvas (Screen Space Overlay) + EventSystem
    ///   - Titre "Nymora — Login"
    ///   - 3 TMP_InputField : email, password (mode password), displayName
    ///   - 3 Button : Login, Register, Logout
    ///   - Texte de statut en bas
    ///   - GameObject "LoginScreenController" avec component LoginScreenController, toutes les refs cablees
    ///
    /// Prerequis : NymoraBackendSettings.asset doit exister (lance "Create Backend Settings" avant).
    ///
    /// Menu : Nymora &gt; Setup &gt; Create Login Scene
    /// </summary>
    public static class CreateLoginSceneTool
    {
        private const string ScenesFolder = "Assets/_Nymora/Scenes";
        private const string ScenePath = ScenesFolder + "/00_Login.unity";

        [MenuItem("Nymora/Setup/Create Login Scene", priority = 32)]
        private static void CreateLoginScene()
        {
            EnsureFolder(ScenesFolder);

            // Prerequis : settings asset existe
            var settings = AssetDatabase.LoadAssetAtPath<NymoraBackendSettings>(CreateBackendSettingsTool.AssetPath);
            if (settings == null)
            {
                EditorUtility.DisplayDialog("Login Scene",
                    "NymoraBackendSettings.asset introuvable.\n\nLance d'abord :\nNymora > Setup > Create Backend Settings",
                    "OK");
                return;
            }

            if (File.Exists(ScenePath))
            {
                if (!EditorUtility.DisplayDialog("Login Scene",
                    "00_Login.unity existe deja. La regenerer (ecrasement) ?",
                    "Regenerer", "Annuler"))
                {
                    return;
                }
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // EventSystem (necessaire pour les clics UI)
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

            // Background panel (fond gris)
            var bgGo = CreateUIChild(canvasGo, "Background", typeof(Image));
            var bgImg = bgGo.GetComponent<Image>();
            bgImg.color = new Color(0.12f, 0.12f, 0.14f, 1f);
            StretchFull(bgGo.GetComponent<RectTransform>());

            // Titre
            var titleGo = CreateUIChild(canvasGo, "Title", typeof(TextMeshProUGUI));
            var titleText = titleGo.GetComponent<TextMeshProUGUI>();
            titleText.text = "Nymora — Login";
            titleText.fontSize = 60;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = new Color(0.92f, 0.92f, 0.95f);
            PositionAbsolute(titleGo.GetComponent<RectTransform>(), 0f, 360f, 800f, 80f);

            // Champs (1 par ligne, espaces de 90)
            var emailInput = CreateInputField(canvasGo, "EmailInput", "email@nymora.local", 0f, 200f);
            var passwordInput = CreateInputField(canvasGo, "PasswordInput", "mot de passe (min 8)", 0f, 90f, password: true);
            var displayNameInput = CreateInputField(canvasGo, "DisplayNameInput", "displayName (3-20 chars)", 0f, -20f);

            // Boutons (3 cote a cote sur une meme ligne, espaces de 220)
            var loginBtn = CreateButton(canvasGo, "LoginButton", "Login", -220f, -160f);
            var registerBtn = CreateButton(canvasGo, "RegisterButton", "Register", 0f, -160f);
            var logoutBtn = CreateButton(canvasGo, "LogoutButton", "Logout", 220f, -160f);

            // 4eme bouton sur une 2eme ligne (Photon Custom Auth test)
            var connectPhotonBtn = CreateButton(canvasGo, "ConnectPhotonButton", "Connect Photon", 0f, -250f, width: 320f);

            // Statut
            var statusGo = CreateUIChild(canvasGo, "StatusText", typeof(TextMeshProUGUI));
            var statusText = statusGo.GetComponent<TextMeshProUGUI>();
            statusText.text = "Initialisation...";
            statusText.fontSize = 28;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.color = new Color(0.78f, 0.85f, 0.95f);
            PositionAbsolute(statusGo.GetComponent<RectTransform>(), 0f, -360f, 1700f, 60f);

            // Controller + PhotonConnectionTester (sur le meme GameObject pour simplicite)
            var controllerGo = new GameObject("LoginScreenController",
                typeof(LoginScreenController),
                typeof(Nymora.Network.Backend.PhotonConnectionTester));
            SceneManager.MoveGameObjectToScene(controllerGo, scene);
            controllerGo.transform.SetSiblingIndex(0);
            var controller = controllerGo.GetComponent<LoginScreenController>();
            var photonTester = controllerGo.GetComponent<Nymora.Network.Backend.PhotonConnectionTester>();

            // Cablage des references via SerializedObject
            var so = new SerializedObject(controller);
            so.FindProperty("_backendSettings").objectReferenceValue = settings;
            so.FindProperty("_emailInput").objectReferenceValue = emailInput;
            so.FindProperty("_passwordInput").objectReferenceValue = passwordInput;
            so.FindProperty("_displayNameInput").objectReferenceValue = displayNameInput;
            so.FindProperty("_loginButton").objectReferenceValue = loginBtn;
            so.FindProperty("_registerButton").objectReferenceValue = registerBtn;
            so.FindProperty("_logoutButton").objectReferenceValue = logoutBtn;
            so.FindProperty("_connectPhotonButton").objectReferenceValue = connectPhotonBtn;
            so.FindProperty("_photonTester").objectReferenceValue = photonTester;
            so.FindProperty("_statusText").objectReferenceValue = statusText;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log($"[Nymora.Setup] Scene Login generee : {ScenePath}");

            EditorUtility.DisplayDialog("Login Scene",
                $"Scene generee : {ScenePath}\n\n" +
                "Pour tester :\n" +
                "1. Lance le backend : depuis backend/, npm run dev\n" +
                "2. Ouvre la scene 00_Login (deja ouverte)\n" +
                "3. Press Play\n" +
                "4. Remplis email/password/displayName -> Register",
                "OK");
        }

        private static GameObject CreateUIChild(GameObject parent, string name, params System.Type[] components)
        {
            var allComponents = new System.Type[components.Length + 1];
            allComponents[0] = typeof(RectTransform);
            for (int i = 0; i < components.Length; i++) allComponents[i + 1] = components[i];

            var go = new GameObject(name, allComponents);
            go.transform.SetParent(parent.transform, worldPositionStays: false);
            return go;
        }

        private static TMP_InputField CreateInputField(GameObject parent, string name, string placeholder, float x, float y, bool password = false)
        {
            var fieldGo = CreateUIChild(parent, name, typeof(Image), typeof(TMP_InputField));
            var img = fieldGo.GetComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.24f, 1f);
            PositionAbsolute(fieldGo.GetComponent<RectTransform>(), x, y, 600f, 70f);

            // Text Area + Text + Placeholder children (TMP_InputField requirements)
            var textArea = CreateUIChild(fieldGo, "Text Area", typeof(RectMask2D));
            var taRT = textArea.GetComponent<RectTransform>();
            taRT.anchorMin = new Vector2(0, 0);
            taRT.anchorMax = new Vector2(1, 1);
            taRT.offsetMin = new Vector2(15, 7);
            taRT.offsetMax = new Vector2(-15, -7);

            var placeholderGo = CreateUIChild(textArea, "Placeholder", typeof(TextMeshProUGUI));
            var placeholderText = placeholderGo.GetComponent<TextMeshProUGUI>();
            placeholderText.text = placeholder;
            placeholderText.fontSize = 28;
            placeholderText.color = new Color(0.6f, 0.6f, 0.65f, 0.7f);
            placeholderText.alignment = TextAlignmentOptions.Left;
            StretchFull(placeholderGo.GetComponent<RectTransform>());

            var textGo = CreateUIChild(textArea, "Text", typeof(TextMeshProUGUI));
            var realText = textGo.GetComponent<TextMeshProUGUI>();
            realText.fontSize = 28;
            realText.color = new Color(0.95f, 0.95f, 0.98f);
            realText.alignment = TextAlignmentOptions.Left;
            StretchFull(textGo.GetComponent<RectTransform>());

            var input = fieldGo.GetComponent<TMP_InputField>();
            input.textViewport = textArea.GetComponent<RectTransform>();
            input.textComponent = realText;
            input.placeholder = placeholderText;
            input.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            input.lineType = TMP_InputField.LineType.SingleLine;

            return input;
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
