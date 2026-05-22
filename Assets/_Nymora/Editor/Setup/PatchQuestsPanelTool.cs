using System.Collections.Generic;
using Nymora.Hub;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Brique 5.8.b — Patch scene 10_CommunityHub : panneau Quêtes (liste verticale, QuestRow
    /// clonée par quête) + bouton hub "Quêtes" (haut-gauche, sous Battle Pass). Idempotent.
    ///
    /// Menu : Nymora > Setup > Patch Quests Panel.
    /// </summary>
    public static class PatchQuestsPanelTool
    {
        private const string ScenePath = "Assets/_Nymora/Scenes/10_CommunityHub.unity";

        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.65f);
        private static readonly Color ContainerColor = new Color(0.12f, 0.13f, 0.16f, 0.98f);
        private static readonly Color HeaderColor = new Color(0.18f, 0.20f, 0.25f, 1f);
        private static readonly Color CloseColor = new Color(0.55f, 0.25f, 0.25f, 1f);
        private static readonly Color ViewportColor = new Color(0.08f, 0.09f, 0.11f, 1f);
        private static readonly Color RowColor = new Color(0.16f, 0.17f, 0.21f, 1f);
        private static readonly Color BarBgColor = new Color(0.27f, 0.28f, 0.33f, 1f);
        private static readonly Color BarFillColor = new Color(0.45f, 0.78f, 0.48f, 1f);
        private static readonly Color ClaimColor = new Color(0.30f, 0.50f, 0.32f, 1f);
        private static readonly Color QuestsButtonColor = new Color(0.30f, 0.40f, 0.55f, 1f);

        private static Sprite _uiSprite;
        private static Sprite UiSprite => _uiSprite != null
            ? _uiSprite
            : (_uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"));

        [MenuItem("Nymora/Setup/Patch Quests Panel", priority = 42)]
        private static void Patch()
        {
            if (Application.isPlaying) { EditorUtility.DisplayDialog("Patch Quests", "Impossible pendant Play Mode.", "OK"); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.path.EndsWith("10_CommunityHub.unity"))
            {
                if (!EditorUtility.DisplayDialog("Patch Quests",
                    $"Scene active : {scene.path}\nAttendu : {ScenePath}\n\nOuvrir 10_CommunityHub ?", "Ouvrir", "Annuler")) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) { EditorUtility.DisplayDialog("Patch Quests", "Aucun Canvas.", "OK"); return; }

            var actions = new List<string>();
            EnsureButton(canvas, actions);
            EnsurePanel(canvas, actions);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Nymora.Setup] Patch quests : {string.Join("\n", actions)}");
            EditorUtility.DisplayDialog("Patch Quests", string.Join("\n", actions) + "\n\nN'oublie pas Ctrl+S.", "OK");
        }

        private static void EnsureButton(Canvas canvas, List<string> actions)
        {
            var existing = canvas.transform.Find("QuestsButton");
            GameObject go;
            bool isNew = existing == null;
            if (!isNew) { go = existing.gameObject; actions.Add("QuestsButton re-style (position préservée)"); }
            else
            {
                go = new GameObject("QuestsButton", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(canvas.transform, false);
                actions.Add("QuestsButton créé (haut-gauche, sous Battle Pass)");
            }
            var img = go.GetComponent<Image>(); img.color = QuestsButtonColor;
            go.GetComponent<Button>().targetGraphic = img;
            if (go.GetComponent<HubQuestsButton>() == null) go.AddComponent<HubQuestsButton>();
            if (isNew)
            {
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(20f, -86f); // sous le bouton Battle Pass (-20, h56)
                rt.sizeDelta = new Vector2(190f, 56f);
            }
            var oldLabel = go.transform.Find("Label");
            if (oldLabel != null) Object.DestroyImmediate(oldLabel.gameObject);
            var labelGo = NewChild("Label", go.transform); StretchToParent(labelGo);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = "Quêtes"; tmp.fontSize = 24f; tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center; tmp.fontStyle = FontStyles.Bold;
        }

        private static void EnsurePanel(Canvas canvas, List<string> actions)
        {
            var existing = canvas.transform.Find("QuestsPanelHost");
            HubQuestsPanel panel;
            GameObject hostGo;
            if (existing != null)
            {
                hostGo = existing.gameObject;
                panel = hostGo.GetComponent<HubQuestsPanel>() ?? hostGo.AddComponent<HubQuestsPanel>();
                for (int i = hostGo.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(hostGo.transform.GetChild(i).gameObject);
                actions.Add("QuestsPanelHost cleanup (rebuild)");
            }
            else
            {
                hostGo = new GameObject("QuestsPanelHost", typeof(RectTransform));
                hostGo.transform.SetParent(canvas.transform, false);
                StretchToParent(hostGo);
                panel = hostGo.AddComponent<HubQuestsPanel>();
                actions.Add("QuestsPanelHost créé");
            }

            var panelRoot = NewChild("PanelRoot", hostGo.transform); StretchToParent(panelRoot);
            var backdrop = NewChild("Backdrop", panelRoot.transform); StretchToParent(backdrop);
            backdrop.AddComponent<Image>().color = BackdropColor;

            var container = NewChild("Container", panelRoot.transform);
            var crt = container.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f); crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(760f, 640f);
            container.AddComponent<Image>().color = ContainerColor;

            var header = NewChild("Header", container.transform);
            var hRt = header.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0f, 1f); hRt.anchorMax = new Vector2(1f, 1f);
            hRt.pivot = new Vector2(0.5f, 1f); hRt.sizeDelta = new Vector2(0f, 56f);
            header.AddComponent<Image>().color = HeaderColor;
            var headerTextGo = NewChild("HeaderText", header.transform); StretchToParent(headerTextGo);
            var headerTmp = headerTextGo.AddComponent<TextMeshProUGUI>();
            headerTmp.text = "Quêtes"; headerTmp.fontSize = 24f; headerTmp.color = Color.white;
            headerTmp.alignment = TextAlignmentOptions.Center; headerTmp.fontStyle = FontStyles.Bold;
            var closeGo = MakeButton(header.transform, "CloseButton", "X", CloseColor, 44f, 38f);
            var crtClose = closeGo.GetComponent<RectTransform>();
            crtClose.anchorMin = crtClose.anchorMax = new Vector2(1f, 0.5f); crtClose.pivot = new Vector2(1f, 0.5f);
            crtClose.anchoredPosition = new Vector2(-10f, 0f);

            // ScrollRect vertical
            var scrollGo = NewChild("Scroll", container.transform);
            var sRt = scrollGo.GetComponent<RectTransform>();
            sRt.anchorMin = new Vector2(0f, 0f); sRt.anchorMax = new Vector2(1f, 1f);
            sRt.offsetMin = new Vector2(24f, 16f); sRt.offsetMax = new Vector2(-24f, -64f);
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30f;
            var viewport = NewChild("Viewport", scrollGo.transform); StretchToParent(viewport);
            viewport.AddComponent<Image>().color = ViewportColor;
            viewport.AddComponent<RectMask2D>();
            var content = NewChild("Content", viewport.transform);
            var cRt = content.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0f, 1f); cRt.anchorMax = new Vector2(1f, 1f); cRt.pivot = new Vector2(0.5f, 1f);
            // sizeDelta.x DOIT etre 0 : ancrages etires + CSF horizontal Unconstrained => largeur = Viewport.
            // Sinon le defaut (100) rend le Content 100px trop large (50px de debord a gauche) et le titre est rogne.
            cRt.sizeDelta = Vector2.zero; cRt.anchoredPosition = Vector2.zero;
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f; vlg.padding = new RectOffset(16, 16, 10, 10);
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = cRt;

            var template = BuildRowTemplate(panelRoot.transform);
            template.SetActive(false);

            var so = new SerializedObject(panel);
            so.FindProperty("_panelRoot").objectReferenceValue = panelRoot;
            so.FindProperty("_closeButton").objectReferenceValue = closeGo.GetComponent<Button>();
            so.FindProperty("_headerText").objectReferenceValue = headerTmp;
            so.FindProperty("_content").objectReferenceValue = cRt;
            so.FindProperty("_rowTemplate").objectReferenceValue = template.GetComponent<QuestRow>();
            var settingsGuids = AssetDatabase.FindAssets("t:NymoraBackendSettings");
            if (settingsGuids.Length > 0)
                so.FindProperty("_backendSettings").objectReferenceValue =
                    AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(settingsGuids[0]));
            so.ApplyModifiedPropertiesWithoutUndo();
            actions.Add("QuestsPanel wires (content + row template + settings)");

            panelRoot.SetActive(false);
        }

        private static GameObject BuildRowTemplate(Transform parent)
        {
            // Marge droite reservee au bouton Réclamer (largeur 150 + marges) : tout le reste
            // s'ancre de 0 a 1 en s'arretant a -RM, donc rien ne deborde quelle que soit la largeur.
            const float RM = 178f;

            var row = new GameObject("QuestRowTemplate", typeof(RectTransform), typeof(Image));
            row.transform.SetParent(parent, false);
            row.GetComponent<Image>().color = RowColor;
            var le = row.AddComponent<LayoutElement>(); le.preferredHeight = 98f;
            var comp = row.AddComponent<QuestRow>();

            // 3 etages bas->haut : recompense (y 12-34) / barre + X/Y (y 44-60) / titre (haut).

            // Titre (haut) — etire 0..1 avec marge droite, ellipse si trop long.
            var titleGo = NewChild("Title", row.transform);
            var tRt = titleGo.GetComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0f, 1f); tRt.anchorMax = new Vector2(1f, 1f); tRt.pivot = new Vector2(0.5f, 1f);
            tRt.offsetMin = new Vector2(14f, -34f); tRt.offsetMax = new Vector2(-RM, -8f);
            var title = titleGo.AddComponent<TextMeshProUGUI>();
            title.text = "Quête"; title.fontSize = 18f; title.color = Color.white;
            title.alignment = TextAlignmentOptions.Left; title.enableWordWrapping = false;
            title.overflowMode = TextOverflowModes.Ellipsis;

            // Barre de progression (milieu-gauche) — largeur FIXE compacte (ne pousse rien).
            var barBg = new GameObject("ProgressBar", typeof(RectTransform), typeof(Image));
            barBg.transform.SetParent(row.transform, false);
            var bRt = barBg.GetComponent<RectTransform>();
            bRt.anchorMin = bRt.anchorMax = new Vector2(0f, 0f); bRt.pivot = new Vector2(0f, 0f);
            bRt.anchoredPosition = new Vector2(14f, 44f); bRt.sizeDelta = new Vector2(240f, 16f);
            barBg.GetComponent<Image>().color = BarBgColor;
            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(barBg.transform, false);
            StretchToParent(fillGo);
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.color = BarFillColor; fillImg.sprite = UiSprite;
            fillImg.type = Image.Type.Filled; fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left; fillImg.fillAmount = 0.4f;

            // Texte progression (X/Y) — juste a droite de la barre compacte (meme etage).
            var progGo = NewChild("ProgressText", row.transform);
            var pRt = progGo.GetComponent<RectTransform>();
            pRt.anchorMin = pRt.anchorMax = new Vector2(0f, 0f); pRt.pivot = new Vector2(0f, 0f);
            pRt.anchoredPosition = new Vector2(262f, 42f); pRt.sizeDelta = new Vector2(72f, 20f);
            var progText = progGo.AddComponent<TextMeshProUGUI>();
            progText.text = "0/0"; progText.fontSize = 16f; progText.color = new Color(0.85f, 0.85f, 0.9f);
            progText.alignment = TextAlignmentOptions.Left;

            // Récompense (etage du bas, SOUS la barre) — pleine largeur a gauche jusqu'au bouton.
            var rewGo = NewChild("Reward", row.transform);
            var rRt = rewGo.GetComponent<RectTransform>();
            rRt.anchorMin = new Vector2(0f, 0f); rRt.anchorMax = new Vector2(1f, 0f); rRt.pivot = new Vector2(0.5f, 0f);
            rRt.offsetMin = new Vector2(14f, 12f); rRt.offsetMax = new Vector2(-RM, 34f);
            var reward = rewGo.AddComponent<TextMeshProUGUI>();
            reward.text = ""; reward.fontSize = 15f; reward.color = new Color(0.95f, 0.85f, 0.4f);
            reward.alignment = TextAlignmentOptions.Left; reward.enableWordWrapping = false;
            reward.overflowMode = TextOverflowModes.Ellipsis;

            // Bouton Réclamer (droite, centré vertical).
            var claimGo = MakeButton(row.transform, "ClaimButton", "Réclamer", ClaimColor, 150f, 56f);
            var clRt = claimGo.GetComponent<RectTransform>();
            clRt.anchorMin = clRt.anchorMax = new Vector2(1f, 0.5f); clRt.pivot = new Vector2(1f, 0.5f);
            clRt.anchoredPosition = new Vector2(-14f, 0f);
            var claimLabel = claimGo.transform.Find("Label")?.GetComponent<TMP_Text>();
            if (claimLabel != null) claimLabel.fontSize = 18f;

            var so = new SerializedObject(comp);
            so.FindProperty("_title").objectReferenceValue = title;
            so.FindProperty("_progressFill").objectReferenceValue = fillImg;
            so.FindProperty("_progressText").objectReferenceValue = progText;
            so.FindProperty("_rewardText").objectReferenceValue = reward;
            so.FindProperty("_claimButton").objectReferenceValue = claimGo.GetComponent<Button>();
            so.FindProperty("_claimLabel").objectReferenceValue = claimLabel;
            so.ApplyModifiedPropertiesWithoutUndo();

            return row;
        }

        private static GameObject MakeButton(Transform parent, string name, string label, Color color, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            if (width > 0 && height > 0) rt.sizeDelta = new Vector2(width, height);
            var img = go.GetComponent<Image>(); img.color = color;
            go.GetComponent<Button>().targetGraphic = img;
            var labelGo = NewChild("Label", go.transform); StretchToParent(labelGo);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label; tmp.fontSize = 20f; tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center; tmp.fontStyle = FontStyles.Bold;
            return go;
        }

        private static GameObject NewChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void StretchToParent(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
    }
}
