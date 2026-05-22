using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nymora.UI.Login;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Refonte de l'ecran de login (epure) : reconstruit le panneau CONNEXION et cree
    /// le panneau INSCRIPTION separe, puis cable le LoginScreenController.
    ///
    /// CONNEXION (dans LoginPanel) : Pseudo + Mot de passe + bouton "Connexion" + lien "S'inscrire".
    /// INSCRIPTION (RegisterPanel, masque) : Email + Pseudo + Mot de passe + Confirmation
    ///   + "Creer le compte" + "Retour".
    ///
    /// Vire les anciens widgets (email login, displayName, Register/Logout/ConnectPhoton/EnterHub).
    /// Conserve le launcher (verdict version + UpdateRequiredPanel + barre de progression).
    /// Rejouable sans doublon (reconstruit le contenu des panneaux a chaque passage).
    ///
    /// Prerequis : avoir lance "Upgrade Login to Launcher (L2)" avant (cree LoginPanel + UpdateRequiredPanel).
    ///
    /// Menu : Nymora &gt; Setup &gt; Refine Login Form
    /// </summary>
    public static class RefineLoginFormTool
    {
        private const string LoginScenePath = "Assets/_Nymora/Scenes/00_Login.unity";

        [MenuItem("Nymora/Setup/Refine Login Form", priority = 35)]
        private static void RefineLoginForm()
        {
            if (!File.Exists(LoginScenePath))
            {
                EditorUtility.DisplayDialog("Refine Login", $"Scene introuvable : {LoginScenePath}", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.OpenScene(LoginScenePath, OpenSceneMode.Single);

            var controller = Object.FindAnyObjectByType<LoginScreenController>();
            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (controller == null || canvas == null)
            {
                EditorUtility.DisplayDialog("Refine Login", "LoginScreenController ou Canvas introuvable.", "OK");
                return;
            }

            var loginPanel = FindChild(canvas.transform, "LoginPanel");
            if (loginPanel == null)
            {
                EditorUtility.DisplayDialog("Refine Login",
                    "LoginPanel introuvable. Lance d'abord 'Upgrade Login to Launcher (L2)'.", "OK");
                return;
            }

            // --- CONNEXION : on vide LoginPanel et on reconstruit l'UI epuree ---
            ClearChildren(loginPanel);

            CreateLabel(loginPanel, "LoginTitle", "Connexion", 0f, 230f, 50f);
            var pseudoInput = CreateInputField(loginPanel, "PseudoInput", "pseudo", 0f, 110f);
            var passwordInput = CreateInputField(loginPanel, "PasswordInput", "mot de passe", 0f, 20f, password: true);
            var connexionBtn = CreateButton(loginPanel, "ConnexionButton", "Connexion", 0f, -80f, 320f,
                new Color(0.22f, 0.55f, 0.33f, 1f));
            var openRegisterBtn = CreateButton(loginPanel, "OpenRegisterButton", "Pas de compte ? S'inscrire", 0f, -170f, 360f,
                new Color(0.28f, 0.30f, 0.40f, 1f));

            // --- INSCRIPTION : panneau separe (cree ou reconstruit) ---
            var registerPanel = FindChild(canvas.transform, "RegisterPanel");
            if (registerPanel == null)
            {
                registerPanel = CreateUIChild(canvas.gameObject, "RegisterPanel", typeof(Image));
                StretchFull(registerPanel.GetComponent<RectTransform>());
            }
            var regBg = registerPanel.GetComponent<Image>();
            if (regBg != null) regBg.color = new Color(0.07f, 0.07f, 0.09f, 0.98f);
            ClearChildren(registerPanel);

            CreateLabel(registerPanel, "RegisterTitle", "Inscription", 0f, 300f, 60f);
            var regEmail = CreateInputField(registerPanel, "RegEmailInput", "email@exemple.com", 0f, 180f);
            var regPseudo = CreateInputField(registerPanel, "RegPseudoInput", "pseudo (3-20 caracteres)", 0f, 90f);
            var regPassword = CreateInputField(registerPanel, "RegPasswordInput", "mot de passe (min 8)", 0f, 0f, password: true);
            var regConfirm = CreateInputField(registerPanel, "RegConfirmInput", "confirme le mot de passe", 0f, -90f, password: true);
            var createBtn = CreateButton(registerPanel, "CreateAccountButton", "Creer le compte", 0f, -190f, 320f,
                new Color(0.22f, 0.55f, 0.33f, 1f));
            var backBtn = CreateButton(registerPanel, "BackToLoginButton", "Retour", 0f, -275f, 220f,
                new Color(0.28f, 0.30f, 0.40f, 1f));

            registerPanel.SetActive(false);

            // Navigation Tab entre les champs (1 navigateur par panneau ; seul le panneau actif reagit).
            WireTabNavigator(loginPanel, pseudoInput, passwordInput);
            WireTabNavigator(registerPanel, regEmail, regPseudo, regPassword, regConfirm);

            // Ordre de rendu : UpdateRequiredPanel doit rester au-dessus de tout.
            var updatePanel = FindChild(canvas.transform, "UpdateRequiredPanel");
            if (updatePanel != null) updatePanel.transform.SetAsLastSibling();

            // --- Cablage ---
            var so = new SerializedObject(controller);
            SetRef(so, "_loginPanel", loginPanel);
            SetRef(so, "_pseudoInput", pseudoInput);
            SetRef(so, "_passwordInput", passwordInput);
            SetRef(so, "_connexionButton", connexionBtn);
            SetRef(so, "_openRegisterButton", openRegisterBtn);
            SetRef(so, "_registerPanel", registerPanel);
            SetRef(so, "_regEmailInput", regEmail);
            SetRef(so, "_regPseudoInput", regPseudo);
            SetRef(so, "_regPasswordInput", regPassword);
            SetRef(so, "_regConfirmInput", regConfirm);
            SetRef(so, "_createAccountButton", createBtn);
            SetRef(so, "_backToLoginButton", backBtn);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[Nymora.Setup] Ecran login epure : panneau Connexion + panneau Inscription cables.");
            EditorUtility.DisplayDialog("Refine Login",
                "Ecran login refondu.\n\n" +
                "- Connexion : Pseudo + Mot de passe + Connexion + lien S'inscrire\n" +
                "- Inscription : Email + Pseudo + Mot de passe + Confirmation\n" +
                "- Anciens boutons dev (Photon/Logout) supprimes\n\n" +
                "Test : Play sur 00_Login (serveur a jour) -> ecran Connexion.",
                "OK");
        }

        // --- Helpers ---

        private static void WireTabNavigator(GameObject panel, params Selectable[] fields)
        {
            var nav = panel.GetComponent<TabFieldNavigator>();
            if (nav == null) nav = panel.AddComponent<TabFieldNavigator>();

            var soNav = new SerializedObject(nav);
            var arr = soNav.FindProperty("_fields");
            arr.ClearArray();
            arr.arraySize = fields.Length;
            for (int i = 0; i < fields.Length; i++)
            {
                arr.GetArrayElementAtIndex(i).objectReferenceValue = fields[i];
            }
            soNav.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRef(SerializedObject so, string prop, Object value)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.objectReferenceValue = value;
            else Debug.LogWarning($"[Nymora.Setup] Propriete '{prop}' absente du LoginScreenController.");
        }

        private static GameObject FindChild(Transform root, string name)
        {
            var t = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);
            return t != null ? t.gameObject : null;
        }

        private static void ClearChildren(GameObject parent)
        {
            var children = new List<GameObject>();
            foreach (Transform c in parent.transform) children.Add(c.gameObject);
            foreach (var c in children) Object.DestroyImmediate(c);
        }

        private static GameObject CreateUIChild(GameObject parent, string name, params System.Type[] components)
        {
            var all = new System.Type[components.Length + 1];
            all[0] = typeof(RectTransform);
            for (int i = 0; i < components.Length; i++) all[i + 1] = components[i];
            var go = new GameObject(name, all);
            go.transform.SetParent(parent.transform, worldPositionStays: false);
            return go;
        }

        private static void CreateLabel(GameObject parent, string name, string text, float x, float y, float fontSize)
        {
            var go = CreateUIChild(parent, name, typeof(TextMeshProUGUI));
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = fontSize;
            t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.92f, 0.92f, 0.95f);
            PositionAbsolute(go.GetComponent<RectTransform>(), x, y, 800f, 90f);
        }

        private static TMP_InputField CreateInputField(GameObject parent, string name, string placeholder,
            float x, float y, bool password = false)
        {
            var fieldGo = CreateUIChild(parent, name, typeof(Image), typeof(TMP_InputField));
            fieldGo.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.24f, 1f);
            PositionAbsolute(fieldGo.GetComponent<RectTransform>(), x, y, 600f, 70f);

            var textArea = CreateUIChild(fieldGo, "Text Area", typeof(RectMask2D));
            var taRT = textArea.GetComponent<RectTransform>();
            taRT.anchorMin = Vector2.zero; taRT.anchorMax = Vector2.one;
            taRT.offsetMin = new Vector2(15, 7); taRT.offsetMax = new Vector2(-15, -7);

            var placeholderGo = CreateUIChild(textArea, "Placeholder", typeof(TextMeshProUGUI));
            var ph = placeholderGo.GetComponent<TextMeshProUGUI>();
            ph.text = placeholder; ph.fontSize = 28;
            ph.color = new Color(0.6f, 0.6f, 0.65f, 0.7f);
            ph.alignment = TextAlignmentOptions.Left;
            StretchFull(placeholderGo.GetComponent<RectTransform>());

            var textGo = CreateUIChild(textArea, "Text", typeof(TextMeshProUGUI));
            var realText = textGo.GetComponent<TextMeshProUGUI>();
            realText.fontSize = 28; realText.color = new Color(0.95f, 0.95f, 0.98f);
            realText.alignment = TextAlignmentOptions.Left;
            StretchFull(textGo.GetComponent<RectTransform>());

            var input = fieldGo.GetComponent<TMP_InputField>();
            input.textViewport = taRT;
            input.textComponent = realText;
            input.placeholder = ph;
            input.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            input.lineType = TMP_InputField.LineType.SingleLine;
            return input;
        }

        private static Button CreateButton(GameObject parent, string name, string label, float x, float y,
            float width, Color color)
        {
            var btnGo = CreateUIChild(parent, name, typeof(Image), typeof(Button));
            btnGo.GetComponent<Image>().color = color;
            PositionAbsolute(btnGo.GetComponent<RectTransform>(), x, y, width, 70f);

            var labelGo = CreateUIChild(btnGo, "Label", typeof(TextMeshProUGUI));
            var t = labelGo.GetComponent<TextMeshProUGUI>();
            t.text = label; t.fontSize = 28; t.color = Color.white;
            t.alignment = TextAlignmentOptions.Center;
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
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
    }
}
