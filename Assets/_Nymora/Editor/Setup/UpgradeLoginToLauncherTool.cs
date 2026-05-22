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
    /// Brique L2 — Transforme la scene 00_Login existante en LAUNCHER, EN PLACE (idempotent,
    /// aucune regeneration : on preserve le bouton "Entrer dans le hub" et tout reglage manuel).
    ///
    /// Ce que fait l'outil :
    ///   1. Cree un conteneur "LoginPanel" (plein ecran, transparent) et y deplace tous les
    ///      widgets de login (champs + boutons + EnterHub). Le controller le masque tant que
    ///      le client n'est pas a jour.
    ///   2. Enrichit "UpdateRequiredPanel" : titre "...pour jouer", bouton "Telecharger la mise
    ///      a jour", barre de progression (Image Filled) + texte de pourcentage.
    ///   3. Cable les nouvelles references du LoginScreenController (_loginPanel, _downloadButton,
    ///      _progressBarFill, _progressText).
    ///
    /// Rejouable sans creer de doublon : chaque element est cherche par nom avant creation.
    ///
    /// Menu : Nymora &gt; Setup &gt; Upgrade Login to Launcher (L2)
    /// </summary>
    public static class UpgradeLoginToLauncherTool
    {
        private const string LoginScenePath = "Assets/_Nymora/Scenes/00_Login.unity";

        private const string LoginPanelName = "LoginPanel";
        private const string UpdatePanelName = "UpdateRequiredPanel";

        // Widgets de login deplaces sous LoginPanel (s'ils existent).
        private static readonly string[] LoginWidgetNames =
        {
            "EmailInput", "PasswordInput", "DisplayNameInput",
            "LoginButton", "RegisterButton", "LogoutButton",
            "ConnectPhotonButton", "EnterHubButton",
        };

        [MenuItem("Nymora/Setup/Upgrade Login to Launcher (L2)", priority = 34)]
        private static void UpgradeLoginToLauncher()
        {
            if (!File.Exists(LoginScenePath))
            {
                EditorUtility.DisplayDialog("Launcher L2",
                    $"Scene introuvable : {LoginScenePath}\n\nLance d'abord Nymora > Setup > Create Login Scene.",
                    "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scene = EditorSceneManager.OpenScene(LoginScenePath, OpenSceneMode.Single);

            var controller = Object.FindAnyObjectByType<LoginScreenController>();
            if (controller == null)
            {
                EditorUtility.DisplayDialog("Launcher L2",
                    "LoginScreenController introuvable dans 00_Login.", "OK");
                return;
            }

            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("Launcher L2", "Aucun Canvas trouve dans 00_Login.", "OK");
                return;
            }

            // --- 1. Conteneur LoginPanel + reparent des widgets de login ---
            var loginPanelGo = FindChildByName(canvas.transform, LoginPanelName);
            if (loginPanelGo == null)
            {
                loginPanelGo = new GameObject(LoginPanelName, typeof(RectTransform));
                loginPanelGo.transform.SetParent(canvas.transform, worldPositionStays: false);
                StretchFull(loginPanelGo.GetComponent<RectTransform>());
            }

            foreach (var widgetName in LoginWidgetNames)
            {
                var widget = FindChildByName(canvas.transform, widgetName);
                if (widget == null) continue;
                if (widget.transform.parent == loginPanelGo.transform) continue;

                var rt = widget.GetComponent<RectTransform>();
                var savedPos = rt != null ? rt.anchoredPosition : Vector2.zero;
                widget.transform.SetParent(loginPanelGo.transform, worldPositionStays: false);
                if (rt != null) rt.anchoredPosition = savedPos; // LoginPanel == plein canvas, donc position visuelle preservee
            }

            // --- 1bis. Label de verdict de version (persistant, en haut, hors LoginPanel) ---
            var verdictGo = FindChildByName(canvas.transform, "VersionVerdictText");
            TextMeshProUGUI verdictText;
            if (verdictGo == null)
            {
                verdictGo = CreateUIChild(canvas.gameObject, "VersionVerdictText", typeof(TextMeshProUGUI));
                verdictText = verdictGo.GetComponent<TextMeshProUGUI>();
                verdictText.text = string.Empty;
                verdictText.fontSize = 38;
                verdictText.alignment = TextAlignmentOptions.Center;
                verdictText.color = new Color(0.4f, 0.85f, 0.5f);
                PositionAbsolute(verdictGo.GetComponent<RectTransform>(), 0f, 285f, 1500f, 60f);
            }
            else
            {
                verdictText = verdictGo.GetComponent<TextMeshProUGUI>();
            }

            // --- 2. UpdateRequiredPanel : titre + bouton telecharger + barre de progression ---
            var updatePanelGo = FindChildByName(canvas.transform, UpdatePanelName);
            if (updatePanelGo == null)
            {
                EditorUtility.DisplayDialog("Launcher L2",
                    $"'{UpdatePanelName}' introuvable. Regenere la scene via Create Login Scene puis relance cet outil.",
                    "OK");
                return;
            }

            // Titre du panneau -> "Mise a jour requise pour jouer"
            var panelTitle = FindChildByName(updatePanelGo.transform, "Title");
            if (panelTitle != null)
            {
                var tmp = panelTitle.GetComponent<TextMeshProUGUI>();
                if (tmp != null) tmp.text = "Mise a jour requise pour jouer";
            }

            // Bouton telecharger (vert), sous le message.
            var downloadGo = FindChildByName(updatePanelGo.transform, "DownloadButton");
            Button downloadBtn;
            if (downloadGo == null)
            {
                downloadBtn = CreateButton(updatePanelGo, "DownloadButton", "Telecharger la mise a jour", 0f, -160f, width: 520f);
                var dimg = downloadBtn.GetComponent<Image>();
                if (dimg != null) dimg.color = new Color(0.22f, 0.55f, 0.33f, 1f);
            }
            else
            {
                downloadBtn = downloadGo.GetComponent<Button>();
            }

            // Barre de progression : fond + remplissage (Image Filled Horizontal).
            var barBgGo = FindChildByName(updatePanelGo.transform, "ProgressBarBg");
            if (barBgGo == null)
            {
                barBgGo = CreateUIChild(updatePanelGo, "ProgressBarBg", typeof(Image));
                var bgImg = barBgGo.GetComponent<Image>();
                bgImg.color = new Color(0.18f, 0.18f, 0.22f, 1f);
                PositionAbsolute(barBgGo.GetComponent<RectTransform>(), 0f, -260f, 620f, 34f);
            }

            var fillGo = FindChildByName(barBgGo.transform, "ProgressBarFill");
            Image fillImg;
            if (fillGo == null)
            {
                fillGo = CreateUIChild(barBgGo, "ProgressBarFill", typeof(Image));
                fillImg = fillGo.GetComponent<Image>();
                fillImg.color = new Color(0.30f, 0.75f, 0.45f, 1f);
                fillImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                fillImg.type = Image.Type.Filled;
                fillImg.fillMethod = Image.FillMethod.Horizontal;
                fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
                fillImg.fillAmount = 0f;
                // Le fill occupe tout le fond ; c'est fillAmount qui anime la progression.
                StretchFull(fillGo.GetComponent<RectTransform>());
            }
            else
            {
                fillImg = fillGo.GetComponent<Image>();
            }

            // Texte de pourcentage sous la barre.
            var progressTextGo = FindChildByName(updatePanelGo.transform, "ProgressText");
            TextMeshProUGUI progressText;
            if (progressTextGo == null)
            {
                progressTextGo = CreateUIChild(updatePanelGo, "ProgressText", typeof(TextMeshProUGUI));
                progressText = progressTextGo.GetComponent<TextMeshProUGUI>();
                progressText.text = string.Empty;
                progressText.fontSize = 28;
                progressText.alignment = TextAlignmentOptions.Center;
                progressText.color = new Color(0.85f, 0.9f, 0.95f);
                PositionAbsolute(progressTextGo.GetComponent<RectTransform>(), 0f, -310f, 620f, 50f);
            }
            else
            {
                progressText = progressTextGo.GetComponent<TextMeshProUGUI>();
            }

            // Le panneau MaJ doit rester au-dessus de tout (rendu en dernier).
            updatePanelGo.transform.SetAsLastSibling();

            // --- 3. Cablage des nouvelles references ---
            var so = new SerializedObject(controller);
            SetRef(so, "_loginPanel", loginPanelGo);
            SetRef(so, "_versionVerdictText", verdictText);
            SetRef(so, "_downloadButton", downloadBtn);
            SetRef(so, "_progressBarFill", fillImg);
            SetRef(so, "_progressText", progressText);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[Nymora.Setup] 00_Login upgrade en launcher (L2) : LoginPanel + bouton telecharger + barre de progression cables.");

            EditorUtility.DisplayDialog("Launcher L2",
                "00_Login transformee en launcher.\n\n" +
                "- LoginPanel cree, widgets de login regroupes dedans (masque tant que pas a jour)\n" +
                "- UpdateRequiredPanel : bouton 'Telecharger' + barre de progression ajoutes\n" +
                "- References cablees au LoginScreenController\n\n" +
                "Test : Play sur 00_Login.\n" +
                "  - Serveur a jour -> 'Votre version est a jour' + login visible.\n" +
                "  - Pour voir le panneau MaJ : bumpe CURRENT_CLIENT_VERSION cote serveur (ou je le fais).",
                "OK");
        }

        // --- Helpers ---

        private static void SetRef(SerializedObject so, string prop, Object value)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.objectReferenceValue = value;
            else Debug.LogWarning($"[Nymora.Setup] Propriete '{prop}' introuvable sur LoginScreenController.");
        }

        /// <summary>Recherche recursive (inclus inactifs) d'un enfant par nom exact.</summary>
        private static GameObject FindChildByName(Transform root, string name)
        {
            var found = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == name);
            return found != null ? found.gameObject : null;
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
    }
}
