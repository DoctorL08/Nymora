using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nymora.Hub.Menu;
using Nymora.UI.Audio;
using Nymora.UI.Login;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Refonte VISUELLE de 00_Login pour matcher la DA du menu hub « Échap » (monochrome,
    /// police Ari, coins arrondis générés par code) + pose le nouveau décor pixel-art en fond.
    ///
    /// Ce que fait l'outil (100 % View, idempotent, AUCUNE régén de la scène) :
    ///   1. Importe Art/UI/Backgrounds/login_background.png en Sprite pixel-art et le pose
    ///      sur l'Image "Background" plein écran (le logo NYMORA fait déjà partie du décor).
    ///   2. Reconstruit le contenu de "LoginPanel" : carte translucide centrée (pseudo + mdp
    ///      en champs ghost arrondis, bouton Connexion en pilule claire, lien S'inscrire ghost).
    ///   3. Reconstruit "RegisterPanel" : backdrop sombre + carte (email/pseudo/mdp/confirmation
    ///      + Créer le compte + Retour).
    ///   4. Re-thème "UpdateRequiredPanel" (launcher) SANS toucher aux refs du controller :
    ///      backdrop sombre, pilule de téléchargement, barre de progression arrondie, police Ari.
    ///   5. Re-thème StatusText + VersionVerdictText, masque l'ancien "Title" résiduel.
    ///   6. Re-câble navigation Tab + refs du LoginScreenController.
    ///
    /// Prérequis : scène déjà passée par "Upgrade Login to Launcher (L2)" (LoginPanel +
    /// UpdateRequiredPanel présents) et thème HubMenuTheme.asset peuplé (fonts Ari).
    ///
    /// Menu : Nymora &gt; Setup &gt; UI Menu &gt; Restyle Login Scene
    /// </summary>
    public static class RestyleLoginSceneTool
    {
        private const string LoginScenePath = "Assets/_Nymora/Scenes/00_Login.unity";
        private const string ThemePath = "Assets/_Nymora/ScriptableObjects/Settings/HubMenuTheme.asset";
        private const string BackgroundPath = "Assets/_Nymora/Art/UI/Backgrounds/login_background.png";

        [MenuItem("Nymora/Setup/UI Menu/Restyle Login Scene", priority = 36)]
        private static void RestyleLoginScene()
        {
            if (!File.Exists(LoginScenePath))
            {
                EditorUtility.DisplayDialog("Restyle Login", $"Scène introuvable : {LoginScenePath}", "OK");
                return;
            }

            var theme = AssetDatabase.LoadAssetAtPath<HubMenuTheme>(ThemePath);
            if (theme == null)
            {
                EditorUtility.DisplayDialog("Restyle Login",
                    $"Thème introuvable à {ThemePath}.\nLance d'abord 'Create Theme + Style Preview'.", "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(LoginScenePath, OpenSceneMode.Single);

            var controller = Object.FindAnyObjectByType<LoginScreenController>();
            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (controller == null || canvas == null)
            {
                EditorUtility.DisplayDialog("Restyle Login", "LoginScreenController ou Canvas introuvable.", "OK");
                return;
            }

            // --- 1. Décor plein écran ---
            var bgSprite = ImportBackgroundSprite();
            var bgGo = FindChild(canvas.transform, "Background");
            if (bgGo == null)
            {
                bgGo = NewChild(canvas.gameObject, "Background", typeof(Image));
                bgGo.transform.SetAsFirstSibling();
            }
            var bgImg = bgGo.GetComponent<Image>() ?? bgGo.AddComponent<Image>();
            bgImg.sprite = bgSprite;
            bgImg.type = Image.Type.Simple;
            bgImg.preserveAspect = false;
            bgImg.color = Color.white;
            bgImg.raycastTarget = false;
            Stretch(bgImg.rectTransform);
            bgGo.transform.SetAsFirstSibling();

            // Ancien gros titre "Nymora — Login" devenu inutile (logo dans le décor) -> masqué.
            var leftoverTitle = FindChild(canvas.transform, "Title");
            if (leftoverTitle != null && leftoverTitle.transform.parent == canvas.transform)
                leftoverTitle.SetActive(false);

            // --- 2. CONNEXION : carte translucide centrée ---
            var loginPanel = FindChild(canvas.transform, "LoginPanel");
            if (loginPanel == null)
            {
                EditorUtility.DisplayDialog("Restyle Login",
                    "LoginPanel introuvable. Lance d'abord 'Upgrade Login to Launcher (L2)'.", "OK");
                return;
            }
            ClearChildren(loginPanel);

            var loginCard = MakeCard(loginPanel, theme, 0f, -30f, 480f, 472f);
            MakeHeader(loginCard, theme, "CONNEXION", 184f);
            var pseudoInput = MakeInput(loginCard, theme, "PseudoInput", "Pseudo", 0f, 100f, false);
            var passwordInput = MakeInput(loginCard, theme, "PasswordInput", "Mot de passe", 0f, 28f, true);
            var rememberToggle = MakeRememberToggle(loginCard, theme, "Mémoriser mes identifiants", 0f, -36f, 400f);
            var connexionBtn = MakeThemedButton(loginCard, theme, "ConnexionButton", "Connexion", 0f, -108f, 400f, 58f, true);
            var openRegisterBtn = MakeThemedButton(loginCard, theme, "OpenRegisterButton",
                "Pas de compte ? S'inscrire", 0f, -180f, 400f, 46f, false);

            // --- 3. INSCRIPTION : backdrop sombre + carte ---
            var registerPanel = FindChild(canvas.transform, "RegisterPanel");
            if (registerPanel == null)
            {
                registerPanel = NewChild(canvas.gameObject, "RegisterPanel", typeof(Image));
                Stretch(registerPanel.GetComponent<RectTransform>());
            }
            var regBg = registerPanel.GetComponent<Image>() ?? registerPanel.AddComponent<Image>();
            regBg.sprite = null;
            regBg.color = theme.Backdrop; // voile sombre par-dessus le décor
            regBg.raycastTarget = true;
            ClearChildren(registerPanel);

            var regCard = MakeCard(registerPanel, theme, 0f, 0f, 520f, 600f);
            MakeHeader(regCard, theme, "INSCRIPTION", 248f);
            var regEmail = MakeInput(regCard, theme, "RegEmailInput", "Email", 0f, 158f, false);
            var regPseudo = MakeInput(regCard, theme, "RegPseudoInput", "Pseudo (3-20 caractères)", 0f, 80f, false);
            var regPassword = MakeInput(regCard, theme, "RegPasswordInput", "Mot de passe (min 8)", 0f, 2f, true);
            var regConfirm = MakeInput(regCard, theme, "RegConfirmInput", "Confirme le mot de passe", 0f, -76f, true);
            var createBtn = MakeThemedButton(regCard, theme, "CreateAccountButton", "Créer le compte", 0f, -168f, 440f, 58f, true);
            var backBtn = MakeThemedButton(regCard, theme, "BackToLoginButton", "Retour", 0f, -244f, 440f, 46f, false);
            registerPanel.SetActive(false);

            // --- 4. UpdateRequiredPanel : re-thème SANS rebuild (refs du controller préservées) ---
            RethemeUpdatePanel(canvas.transform, theme);

            // --- 5. Status + verdict ---
            RethemeStatusTexts(canvas.transform, theme);

            // --- 5bis. Bouton AUDIO (login-only) : pose une instance theme-aware pour matcher la DA ---
            EnsureAudioPanel(theme);

            // Navigation Tab (1 navigateur par panneau)
            WireTabNavigator(loginPanel, pseudoInput, passwordInput);
            WireTabNavigator(registerPanel, regEmail, regPseudo, regPassword, regConfirm);

            // UpdateRequiredPanel reste rendu en dernier (au-dessus de tout).
            var updatePanel = FindChild(canvas.transform, "UpdateRequiredPanel");
            if (updatePanel != null) updatePanel.transform.SetAsLastSibling();

            // --- 6. Câblage ---
            var so = new SerializedObject(controller);
            SetRef(so, "_loginPanel", loginPanel);
            SetRef(so, "_pseudoInput", pseudoInput);
            SetRef(so, "_passwordInput", passwordInput);
            SetRef(so, "_connexionButton", connexionBtn);
            SetRef(so, "_openRegisterButton", openRegisterBtn);
            SetRef(so, "_rememberToggle", rememberToggle);
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

            Debug.Log("[Nymora.Setup] 00_Login restylée (DA hub) + décor pixel-art posé.");
            EditorUtility.DisplayDialog("Restyle Login",
                "Écran login refondu à la DA du hub.\n\n" +
                "- Décor pixel-art en fond plein écran\n" +
                "- Connexion : carte translucide (pseudo + mdp + Connexion + S'inscrire)\n" +
                "- Inscription : carte sur voile sombre\n" +
                "- Launcher (MaJ requise) re-thémé\n\n" +
                "Test : Play sur 00_Login (serveur à jour) -> écran Connexion stylé.",
                "OK");
        }

        // ===== Décor =====

        private static Sprite ImportBackgroundSprite()
        {
            if (!File.Exists(BackgroundPath))
            {
                Debug.LogWarning($"[Nymora.Setup] Décor introuvable : {BackgroundPath} (fond laissé tel quel).");
                return null;
            }
            var imp = AssetImporter.GetAtPath(BackgroundPath) as TextureImporter;
            if (imp != null)
            {
                bool dirty = false;
                if (imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; dirty = true; }
                if (imp.filterMode != FilterMode.Point) { imp.filterMode = FilterMode.Point; dirty = true; }       // pixel-art net
                if (imp.textureCompression != TextureImporterCompression.Uncompressed)
                    { imp.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
                if (imp.mipmapEnabled) { imp.mipmapEnabled = false; dirty = true; }
                if (imp.maxTextureSize < 2048) { imp.maxTextureSize = 2048; dirty = true; }
                if (dirty) imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
        }

        // ===== Re-thème du launcher (sans rebuild) =====

        private static void RethemeUpdatePanel(Transform canvas, HubMenuTheme t)
        {
            var panel = FindChild(canvas, "UpdateRequiredPanel");
            if (panel == null) return;

            var bg = panel.GetComponent<Image>();
            if (bg != null) { bg.sprite = null; bg.color = new Color(0.04f, 0.04f, 0.05f, 0.97f); }

            ThemeText(FindChild(panel.transform, "Title"), t, t.FontSizeTitle + 6f, t.TextPrimary, t.FontBold);
            ThemeText(FindChild(panel.transform, "Message"), t, t.FontSizeHeader, t.TextSecondary, t.Font);
            ThemeText(FindChild(panel.transform, "ProgressText"), t, t.FontSizeBody, t.TextSecondary, t.Font);

            // Bouton télécharger -> pilule claire (primaire)
            var dl = FindChild(panel.transform, "DownloadButton");
            if (dl != null)
            {
                var img = dl.GetComponent<Image>();
                if (img != null) { img.sprite = HubMenuUIFactory.RoundedSprite(t.CornerRadius); img.type = Image.Type.Sliced; img.color = t.Accent; }
                ApplyButtonColors(dl.GetComponent<Button>(), img, true, t);
                ThemeText(FindChild(dl.transform, "Label"), t, t.FontSizeBody, t.TextOnLight, t.FontBold);
            }

            // Barre de progression : fond ghost arrondi + remplissage accent
            var barBg = FindChild(panel.transform, "ProgressBarBg");
            if (barBg != null)
            {
                var img = barBg.GetComponent<Image>();
                if (img != null) { img.sprite = HubMenuUIFactory.RoundedSprite(8f); img.type = Image.Type.Sliced; img.color = t.ButtonGhostBg; }
                var fill = FindChild(barBg.transform, "ProgressBarFill");
                var fimg = fill != null ? fill.GetComponent<Image>() : null;
                if (fimg != null)
                {
                    fimg.sprite = HubMenuUIFactory.RoundedSprite(8f);
                    fimg.type = Image.Type.Filled;
                    fimg.color = t.Accent;
                }
            }
        }

        private static void RethemeStatusTexts(Transform canvas, HubMenuTheme t)
        {
            ThemeText(FindChild(canvas, "StatusText"), t, t.FontSizeSmall, t.TextMuted, t.Font);
            var statusGo = FindChild(canvas, "StatusText");
            if (statusGo != null) PositionAbsolute(statusGo.GetComponent<RectTransform>(), 0f, -330f, 1200f, 50f);

            ThemeText(FindChild(canvas, "VersionVerdictText"), t, t.FontSizeSmall, t.TextMuted, t.Font);
            var verdictGo = FindChild(canvas, "VersionVerdictText");
            if (verdictGo != null) PositionAbsolute(verdictGo.GetComponent<RectTransform>(), 0f, -480f, 1200f, 40f);
        }

        /// <summary>Pose (ou re-câble) un AudioSettingsPanel theme-aware dans la scène. À runtime il
        /// gagne le singleton -> l'auto-instance par défaut est neutralisée (DA monochrome + Ari).</summary>
        private static void EnsureAudioPanel(HubMenuTheme t)
        {
            var panel = Object.FindAnyObjectByType<AudioSettingsPanel>();
            if (panel == null)
            {
                var go = new GameObject("AudioSettingsPanel", typeof(AudioSettingsPanel));
                panel = go.GetComponent<AudioSettingsPanel>();
            }
            var so = new SerializedObject(panel);
            var p = so.FindProperty("_theme");
            if (p != null) p.objectReferenceValue = t;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ===== Widgets thémés =====

        /// <summary>Carte translucide arrondie centrée (laisse transparaître le décor).</summary>
        private static GameObject MakeCard(GameObject parent, HubMenuTheme t, float x, float y, float w, float h)
        {
            var go = NewChild(parent, "Card", typeof(Image));
            var img = go.GetComponent<Image>();
            img.sprite = HubMenuUIFactory.RoundedSprite(t.CornerRadius);
            img.type = Image.Type.Sliced;
            img.color = new Color(0.05f, 0.05f, 0.065f, 0.80f); // sombre, semi-transparent
            img.raycastTarget = true;
            PositionAbsolute(go.GetComponent<RectTransform>(), x, y, w, h);
            return go;
        }

        private static void MakeHeader(GameObject card, HubMenuTheme t, string text, float y)
        {
            var go = NewChild(card, "Header", typeof(TextMeshProUGUI));
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.font = t.FontBold != null ? t.FontBold : t.Font;
            tmp.fontSize = t.FontSizeHeader;
            tmp.color = t.TextSecondary;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.characterSpacing = 6f;
            tmp.enableWordWrapping = false;
            tmp.raycastTarget = false;
            PositionAbsolute(go.GetComponent<RectTransform>(), 0f, y, 440f, 40f);
        }

        private static TMP_InputField MakeInput(GameObject parent, HubMenuTheme t, string name,
            string placeholder, float x, float y, bool password)
        {
            var fieldGo = NewChild(parent, name, typeof(Image), typeof(TMP_InputField));
            var bg = fieldGo.GetComponent<Image>();
            bg.sprite = HubMenuUIFactory.RoundedSprite(10f);
            bg.type = Image.Type.Sliced;
            bg.color = t.ButtonGhostBg;
            PositionAbsolute(fieldGo.GetComponent<RectTransform>(), x, y, 400f, 62f);

            var textArea = NewChild(fieldGo, "Text Area", typeof(RectMask2D));
            var taRT = textArea.GetComponent<RectTransform>();
            taRT.anchorMin = Vector2.zero; taRT.anchorMax = Vector2.one;
            taRT.offsetMin = new Vector2(18, 6); taRT.offsetMax = new Vector2(-18, -6);

            var ph = NewChild(textArea, "Placeholder", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            ph.text = placeholder; ph.font = t.Font; ph.fontSize = t.FontSizeBody;
            ph.color = t.TextMuted; ph.alignment = TextAlignmentOptions.Left;
            ph.enableWordWrapping = false;
            Stretch(ph.rectTransform);

            var realText = NewChild(textArea, "Text", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            realText.font = t.Font; realText.fontSize = t.FontSizeBody;
            realText.color = t.TextPrimary; realText.alignment = TextAlignmentOptions.Left;
            realText.enableWordWrapping = false;
            Stretch(realText.rectTransform);

            var input = fieldGo.GetComponent<TMP_InputField>();
            input.textViewport = taRT;
            input.textComponent = realText;
            input.placeholder = ph;
            input.fontAsset = t.Font;
            input.pointSize = t.FontSizeBody;
            input.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.caretColor = t.TextPrimary;
            input.customCaretColor = true;
            input.selectionColor = new Color(t.Accent.r, t.Accent.g, t.Accent.b, 0.25f);
            return input;
        }

        private static Button MakeThemedButton(GameObject parent, HubMenuTheme t, string name, string label,
            float x, float y, float w, float h, bool primary)
        {
            var go = NewChild(parent, name, typeof(Image), typeof(Button));
            var img = go.GetComponent<Image>();
            img.sprite = HubMenuUIFactory.RoundedSprite(primary ? t.CornerRadius : 10f);
            img.type = Image.Type.Sliced;
            img.color = primary ? t.Accent : t.ButtonGhostBg;
            PositionAbsolute(go.GetComponent<RectTransform>(), x, y, w, h);

            var btn = go.GetComponent<Button>();
            ApplyButtonColors(btn, img, primary, t);

            var lbl = NewChild(go, "Label", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            lbl.text = label;
            lbl.font = t.FontBold != null ? t.FontBold : t.Font;
            lbl.fontSize = primary ? t.FontSizeBody : t.FontSizeSmall;
            lbl.color = primary ? t.TextOnLight : t.TextSecondary;
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.enableWordWrapping = false;
            lbl.raycastTarget = false;
            Stretch(lbl.rectTransform, 14f, 14f, 0f, 0f);
            return btn;
        }

        /// <summary>Case à cocher monochrome (case arrondie + coche accent + label), comme les toggles du menu.</summary>
        private static Toggle MakeRememberToggle(GameObject parent, HubMenuTheme t, string label,
            float x, float y, float rowWidth)
        {
            var row = NewChild(parent, "RememberToggle", typeof(Toggle));
            PositionAbsolute(row.GetComponent<RectTransform>(), x, y, rowWidth, 30f);
            var toggle = row.GetComponent<Toggle>();

            // Case (cible cliquable, ancrée à gauche de la ligne)
            var box = NewChild(row, "Box", typeof(Image));
            var boxImg = box.GetComponent<Image>();
            boxImg.sprite = HubMenuUIFactory.RoundedSprite(6f);
            boxImg.type = Image.Type.Sliced;
            boxImg.color = t.ButtonGhostBg;
            var boxRt = box.GetComponent<RectTransform>();
            boxRt.anchorMin = new Vector2(0f, 0.5f); boxRt.anchorMax = new Vector2(0f, 0.5f); boxRt.pivot = new Vector2(0f, 0.5f);
            boxRt.anchoredPosition = Vector2.zero;
            boxRt.sizeDelta = new Vector2(26f, 26f);

            // Coche (graphic du Toggle : visible quand coché)
            var check = NewChild(box, "Check", typeof(Image));
            var checkImg = check.GetComponent<Image>();
            checkImg.sprite = HubMenuUIFactory.RoundedSprite(4f);
            checkImg.type = Image.Type.Sliced;
            checkImg.color = t.Accent;
            checkImg.raycastTarget = false;
            Stretch(checkImg.rectTransform, 6f, 6f, 6f, 6f);

            // Label (cliquable lui aussi -> route vers le Toggle parent)
            var lbl = NewChild(row, "Label", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            lbl.text = label;
            lbl.font = t.Font;
            lbl.fontSize = t.FontSizeSmall;
            lbl.color = t.TextSecondary;
            lbl.alignment = TextAlignmentOptions.MidlineLeft;
            lbl.enableWordWrapping = false;
            var lrt = lbl.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = new Vector2(38f, 0f); lrt.offsetMax = Vector2.zero;

            toggle.targetGraphic = boxImg;
            toggle.graphic = checkImg;
            toggle.isOn = false;
            return toggle;
        }

        private static void ApplyButtonColors(Button btn, Image target, bool primary, HubMenuTheme t)
        {
            if (btn == null) return;
            btn.targetGraphic = target;
            var c = btn.colors;
            if (primary)
            {
                c.normalColor = Color.white;
                c.highlightedColor = new Color(1f, 1f, 1f, 1f);
                c.pressedColor = new Color(0.85f, 0.86f, 0.88f);
                c.selectedColor = Color.white;
            }
            else
            {
                // base = ButtonGhostBg ; on joue sur la teinte (multiplicative) au survol
                c.normalColor = Color.white;
                c.highlightedColor = new Color(2f, 2f, 2f, 2f); // éclaircit le ghost
                c.pressedColor = new Color(1.6f, 1.6f, 1.6f, 1.6f);
                c.selectedColor = Color.white;
            }
            c.fadeDuration = 0.1f;
            btn.colors = c;
        }

        // ===== Helpers génériques =====

        private static void ThemeText(GameObject go, HubMenuTheme t, float size, Color color, TMP_FontAsset font)
        {
            if (go == null) return;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (tmp == null) return;
            tmp.font = font != null ? font : t.Font;
            tmp.fontSize = size;
            tmp.color = color;
        }

        private static void WireTabNavigator(GameObject panel, params Selectable[] fields)
        {
            var nav = panel.GetComponent<TabFieldNavigator>();
            if (nav == null) nav = panel.AddComponent<TabFieldNavigator>();
            var soNav = new SerializedObject(nav);
            var arr = soNav.FindProperty("_fields");
            arr.ClearArray();
            arr.arraySize = fields.Length;
            for (int i = 0; i < fields.Length; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = fields[i];
            soNav.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRef(SerializedObject so, string prop, Object value)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.objectReferenceValue = value;
            else Debug.LogWarning($"[Nymora.Setup] Propriété '{prop}' absente du LoginScreenController.");
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

        private static GameObject NewChild(GameObject parent, string name, params System.Type[] components)
        {
            var all = new System.Type[components.Length + 1];
            all[0] = typeof(RectTransform);
            for (int i = 0; i < components.Length; i++) all[i + 1] = components[i];
            var go = new GameObject(name, all);
            go.transform.SetParent(parent.transform, worldPositionStays: false);
            return go;
        }

        private static void PositionAbsolute(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static void Stretch(RectTransform rt, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }
    }
}
