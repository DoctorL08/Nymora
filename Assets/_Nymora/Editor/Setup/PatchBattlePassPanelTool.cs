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
    /// Brique 5.7.b (refonte Brawl Stars) — Battle Pass HORIZONTAL : scroll/swipe gauche→droite,
    /// paliers en cases (gratuit haut / premium bas), template de colonne cloné au runtime.
    /// + bouton hub "Battle Pass" (haut-gauche). Idempotent.
    ///
    /// Menu : Nymora > Setup > Patch Battle Pass Panel.
    /// </summary>
    public static class PatchBattlePassPanelTool
    {
        private const string ScenePath = "Assets/_Nymora/Scenes/10_CommunityHub.unity";

        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.7f);
        private static readonly Color ContainerColor = new Color(0.11f, 0.12f, 0.15f, 0.99f);
        private static readonly Color HeaderColor = new Color(0.18f, 0.20f, 0.25f, 1f);
        private static readonly Color CloseColor = new Color(0.55f, 0.25f, 0.25f, 1f);
        private static readonly Color ViewportColor = new Color(0.07f, 0.08f, 0.10f, 1f);
        private static readonly Color ArrowColor = new Color(0.30f, 0.32f, 0.42f, 0.92f);
        private static readonly Color BpButtonColor = new Color(0.50f, 0.42f, 0.20f, 1f);
        private static readonly Color CellColor = new Color(0.14f, 0.15f, 0.18f, 1f);
        private static readonly Color BadgeColor = new Color(0.18f, 0.20f, 0.25f, 1f);
        private static readonly Color FreeHintColor = new Color(0.16f, 0.22f, 0.18f, 1f);
        private static readonly Color PremHintColor = new Color(0.20f, 0.17f, 0.28f, 1f);

        [MenuItem("Nymora/Setup/Patch Battle Pass Panel", priority = 41)]
        private static void Patch()
        {
            if (Application.isPlaying) { EditorUtility.DisplayDialog("Patch Battle Pass", "Impossible pendant Play Mode.", "OK"); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.path.EndsWith("10_CommunityHub.unity"))
            {
                if (!EditorUtility.DisplayDialog("Patch Battle Pass",
                    $"Scene active : {scene.path}\nAttendu : {ScenePath}\n\nOuvrir 10_CommunityHub ?", "Ouvrir", "Annuler")) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) { EditorUtility.DisplayDialog("Patch Battle Pass", "Aucun Canvas.", "OK"); return; }

            var actions = new List<string>();
            EnsureButton(canvas, actions);
            EnsurePanel(canvas, actions);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Nymora.Setup] Patch battle pass : {string.Join("\n", actions)}");
            EditorUtility.DisplayDialog("Patch Battle Pass", string.Join("\n", actions) + "\n\nN'oublie pas Ctrl+S.", "OK");
        }

        private static void EnsureButton(Canvas canvas, List<string> actions)
        {
            var existing = canvas.transform.Find("BattlePassButton");
            GameObject go;
            bool isNew = existing == null;
            if (!isNew) { go = existing.gameObject; actions.Add("BattlePassButton re-style (position préservée)"); }
            else
            {
                go = new GameObject("BattlePassButton", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(canvas.transform, false);
                actions.Add("BattlePassButton créé (haut-gauche)");
            }
            var img = go.GetComponent<Image>(); img.color = BpButtonColor;
            go.GetComponent<Button>().targetGraphic = img;
            if (go.GetComponent<HubBattlePassButton>() == null) go.AddComponent<HubBattlePassButton>();
            if (isNew)
            {
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(20f, -20f);
                rt.sizeDelta = new Vector2(190f, 56f);
            }
            var oldLabel = go.transform.Find("Label");
            if (oldLabel != null) Object.DestroyImmediate(oldLabel.gameObject);
            var labelGo = NewChild("Label", go.transform); StretchToParent(labelGo);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = "Battle Pass"; tmp.fontSize = 24f; tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center; tmp.fontStyle = FontStyles.Bold;
        }

        private static void EnsurePanel(Canvas canvas, List<string> actions)
        {
            var existing = canvas.transform.Find("BattlePassPanelHost");
            HubBattlePassPanel panel;
            GameObject hostGo;
            if (existing != null)
            {
                hostGo = existing.gameObject;
                panel = hostGo.GetComponent<HubBattlePassPanel>() ?? hostGo.AddComponent<HubBattlePassPanel>();
                for (int i = hostGo.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(hostGo.transform.GetChild(i).gameObject);
                actions.Add("BattlePassPanelHost cleanup (rebuild)");
            }
            else
            {
                hostGo = new GameObject("BattlePassPanelHost", typeof(RectTransform));
                hostGo.transform.SetParent(canvas.transform, false);
                StretchToParent(hostGo);
                panel = hostGo.AddComponent<HubBattlePassPanel>();
                actions.Add("BattlePassPanelHost créé");
            }

            var panelRoot = NewChild("PanelRoot", hostGo.transform); StretchToParent(panelRoot);
            var backdrop = NewChild("Backdrop", panelRoot.transform); StretchToParent(backdrop);
            backdrop.AddComponent<Image>().color = BackdropColor;

            // Container large 1180x600
            var container = NewChild("Container", panelRoot.transform);
            var crt = container.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(1180f, 600f);
            container.AddComponent<Image>().color = ContainerColor;

            // Header bar + close
            var header = NewChild("Header", container.transform);
            var hRt = header.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0f, 1f); hRt.anchorMax = new Vector2(1f, 1f);
            hRt.pivot = new Vector2(0.5f, 1f); hRt.sizeDelta = new Vector2(0f, 52f);
            header.AddComponent<Image>().color = HeaderColor;
            var title = NewChild("Title", header.transform); StretchToParent(title);
            var titleTmp = title.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "Battle Pass"; titleTmp.fontSize = 24f; titleTmp.color = Color.white;
            titleTmp.alignment = TextAlignmentOptions.Center; titleTmp.fontStyle = FontStyles.Bold;
            var closeGo = MakeButton(header.transform, "CloseButton", "X", CloseColor, 44f, 38f);
            var crtClose = closeGo.GetComponent<RectTransform>();
            crtClose.anchorMin = crtClose.anchorMax = new Vector2(1f, 0.5f); crtClose.pivot = new Vector2(1f, 0.5f);
            crtClose.anchoredPosition = new Vector2(-10f, 0f);

            // HeaderText progression
            var headerTextGo = NewChild("HeaderText", container.transform);
            var htRt = headerTextGo.GetComponent<RectTransform>();
            htRt.anchorMin = new Vector2(0f, 1f); htRt.anchorMax = new Vector2(1f, 1f);
            htRt.pivot = new Vector2(0.5f, 1f); htRt.sizeDelta = new Vector2(-40f, 46f);
            htRt.anchoredPosition = new Vector2(0f, -56f);
            var headerTmp = headerTextGo.AddComponent<TextMeshProUGUI>();
            headerTmp.text = ""; headerTmp.fontSize = 20f; headerTmp.color = new Color(0.92f, 0.92f, 0.95f);
            headerTmp.alignment = TextAlignmentOptions.Center;

            // Bandeaux "GRATUIT" / "PREMIUM" a gauche (libelles de piste)
            MakeTrackLabel(container.transform, "GRATUIT", 150f, new Color(0.6f, 0.85f, 0.6f));
            MakeTrackLabel(container.transform, "PREMIUM", -150f, new Color(0.8f, 0.7f, 1f));

            // ScrollRect horizontal
            var scrollGo = NewChild("Scroll", container.transform);
            var sRt = scrollGo.GetComponent<RectTransform>();
            sRt.anchorMin = new Vector2(0f, 0f); sRt.anchorMax = new Vector2(1f, 1f);
            sRt.offsetMin = new Vector2(110f, 24f); sRt.offsetMax = new Vector2(-24f, -106f);
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = true; scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 35f;
            var viewport = NewChild("Viewport", scrollGo.transform); StretchToParent(viewport);
            viewport.AddComponent<Image>().color = ViewportColor;
            viewport.AddComponent<RectMask2D>();
            var content = NewChild("Content", viewport.transform);
            var cRt = content.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0f, 0f); cRt.anchorMax = new Vector2(0f, 1f);
            cRt.pivot = new Vector2(0f, 0.5f); cRt.anchoredPosition = Vector2.zero;
            var hlg = content.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f; hlg.padding = new RectOffset(8, 8, 8, 8);
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            var cFit = content.AddComponent<ContentSizeFitter>();
            cFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = cRt;

            // Fleches prev/next (overlay sur les bords du scroll)
            var prevGo = MakeButton(container.transform, "PrevButton", "◀", ArrowColor, 46f, 90f);
            var pRt = prevGo.GetComponent<RectTransform>();
            pRt.anchorMin = pRt.anchorMax = new Vector2(0f, 0.5f); pRt.pivot = new Vector2(0f, 0.5f);
            pRt.anchoredPosition = new Vector2(112f, -26f);
            var nextGo = MakeButton(container.transform, "NextButton", "▶", ArrowColor, 46f, 90f);
            var nRt = nextGo.GetComponent<RectTransform>();
            nRt.anchorMin = nRt.anchorMax = new Vector2(1f, 0.5f); nRt.pivot = new Vector2(1f, 0.5f);
            nRt.anchoredPosition = new Vector2(-26f, -26f);

            // Template de colonne (inactif, hors content)
            var template = BuildColumnTemplate(panelRoot.transform);
            template.SetActive(false);

            // Wire panel
            var so = new SerializedObject(panel);
            so.FindProperty("_panelRoot").objectReferenceValue = panelRoot;
            so.FindProperty("_closeButton").objectReferenceValue = closeGo.GetComponent<Button>();
            so.FindProperty("_headerText").objectReferenceValue = headerTmp;
            so.FindProperty("_scrollRect").objectReferenceValue = scroll;
            so.FindProperty("_content").objectReferenceValue = cRt;
            so.FindProperty("_columnTemplate").objectReferenceValue = template.GetComponent<BattlePassTierColumn>();
            so.FindProperty("_prevButton").objectReferenceValue = prevGo.GetComponent<Button>();
            so.FindProperty("_nextButton").objectReferenceValue = nextGo.GetComponent<Button>();
            var settingsGuids = AssetDatabase.FindAssets("t:NymoraBackendSettings");
            if (settingsGuids.Length > 0)
                so.FindProperty("_backendSettings").objectReferenceValue =
                    AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(settingsGuids[0]));
            so.ApplyModifiedPropertiesWithoutUndo();
            actions.Add("BattlePassPanel wires (scroll horizontal + template colonne + flèches)");

            panelRoot.SetActive(false);
        }

        /// <summary>Colonne : case gratuite (haut) + badge palier + case premium (bas).</summary>
        private static GameObject BuildColumnTemplate(Transform parent)
        {
            var col = NewChild("TierColumnTemplate", parent);
            var le = col.AddComponent<LayoutElement>(); le.preferredWidth = 132f; le.minWidth = 132f;
            var vlg = col.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4f; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true; vlg.childControlHeight = true;

            var comp = col.AddComponent<BattlePassTierColumn>();

            // Case gratuite
            var (freeCell, freeImg, freeBtn, freeLabel, freeStatus) = BuildCell(col.transform, "FreeCell", 158f, FreeHintColor);
            // Badge palier
            var badge = NewChild("TierBadge", col.transform);
            var badgeLe = badge.AddComponent<LayoutElement>(); badgeLe.preferredHeight = 40f;
            var badgeImg = badge.AddComponent<Image>(); badgeImg.color = BadgeColor;
            var tierLabelGo = NewChild("TierLabel", badge.transform); StretchToParent(tierLabelGo);
            var tierLabel = tierLabelGo.AddComponent<TextMeshProUGUI>();
            tierLabel.text = "0"; tierLabel.fontSize = 22f; tierLabel.color = Color.white;
            tierLabel.alignment = TextAlignmentOptions.Center; tierLabel.fontStyle = FontStyles.Bold;
            // Case premium
            var (premCell, premImg, premBtn, premLabel, premStatus) = BuildCell(col.transform, "PremiumCell", 158f, PremHintColor);

            var so = new SerializedObject(comp);
            so.FindProperty("_freeButton").objectReferenceValue = freeBtn;
            so.FindProperty("_freeImage").objectReferenceValue = freeImg;
            so.FindProperty("_freeLabel").objectReferenceValue = freeLabel;
            so.FindProperty("_freeStatus").objectReferenceValue = freeStatus;
            so.FindProperty("_tierLabel").objectReferenceValue = tierLabel;
            so.FindProperty("_tierBadge").objectReferenceValue = badgeImg;
            so.FindProperty("_premiumButton").objectReferenceValue = premBtn;
            so.FindProperty("_premiumImage").objectReferenceValue = premImg;
            so.FindProperty("_premiumLabel").objectReferenceValue = premLabel;
            so.FindProperty("_premiumStatus").objectReferenceValue = premStatus;
            so.ApplyModifiedPropertiesWithoutUndo();

            return col;
        }

        private static (GameObject cell, Image img, Button btn, TMP_Text label, TMP_Text status)
            BuildCell(Transform parent, string name, float height, Color baseColor)
        {
            var cell = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            cell.transform.SetParent(parent, false);
            var le = cell.AddComponent<LayoutElement>(); le.preferredHeight = height;
            var img = cell.GetComponent<Image>(); img.color = baseColor;
            var btn = cell.GetComponent<Button>(); btn.targetGraphic = img;

            // Label (nom de la récompense), portion haute.
            var labelGo = NewChild("Label", cell.transform);
            var lRt = labelGo.GetComponent<RectTransform>();
            lRt.anchorMin = new Vector2(0f, 0.28f); lRt.anchorMax = new Vector2(1f, 1f);
            lRt.offsetMin = new Vector2(6f, 2f); lRt.offsetMax = new Vector2(-6f, -6f);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = ""; label.fontSize = 15f; label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center; label.enableWordWrapping = true;

            // Statut (verrouillé / RÉCLAMER / ✓), bandeau bas.
            var statusGo = NewChild("Status", cell.transform);
            var stRt = statusGo.GetComponent<RectTransform>();
            stRt.anchorMin = new Vector2(0f, 0f); stRt.anchorMax = new Vector2(1f, 0.28f);
            stRt.offsetMin = Vector2.zero; stRt.offsetMax = Vector2.zero;
            var status = statusGo.AddComponent<TextMeshProUGUI>();
            status.text = ""; status.fontSize = 14f; status.color = Color.white;
            status.alignment = TextAlignmentOptions.Center; status.fontStyle = FontStyles.Bold;

            return (cell, img, btn, label, status);
        }

        private static void MakeTrackLabel(Transform parent, string text, float y, Color color)
        {
            var go = NewChild(text + "Track", parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f); rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(10f, y - 26f); rt.sizeDelta = new Vector2(96f, 40f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = 16f; tmp.color = color; tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
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
            tmp.text = label; tmp.fontSize = 24f; tmp.color = Color.white;
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
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
    }
}
