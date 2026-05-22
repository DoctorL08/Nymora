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
    /// Brique 5.5.c — Patch scene 10_CommunityHub : panneau Boutique (grille de cosmétiques,
    /// ShopItemCell clonée par item) + bouton hub "Boutique" (haut-gauche, sous Quêtes). Idempotent.
    ///
    /// Menu : Nymora > Setup > Patch Shop Panel.
    /// </summary>
    public static class PatchShopPanelTool
    {
        private const string ScenePath = "Assets/_Nymora/Scenes/10_CommunityHub.unity";
        private const string CosmeticsResDir = "Assets/_Nymora/Resources/cosmetics";

        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.65f);
        private static readonly Color ContainerColor = new Color(0.12f, 0.13f, 0.16f, 0.98f);
        private static readonly Color HeaderColor = new Color(0.18f, 0.20f, 0.25f, 1f);
        private static readonly Color CloseColor = new Color(0.55f, 0.25f, 0.25f, 1f);
        private static readonly Color ViewportColor = new Color(0.08f, 0.09f, 0.11f, 1f);
        private static readonly Color CellColor = new Color(0.16f, 0.17f, 0.21f, 1f);
        private static readonly Color PreviewBgColor = new Color(0.10f, 0.11f, 0.14f, 1f);
        private static readonly Color BuyColor = new Color(0.30f, 0.50f, 0.32f, 1f);
        private static readonly Color ShopButtonColor = new Color(0.45f, 0.38f, 0.20f, 1f);

        private static readonly string[] CosmeticKeys =
            { "ashen_sovereign", "placeholder_skin", "placeholder_banner", "placeholder_title", "placeholder_emote" };

        [MenuItem("Nymora/Setup/Patch Shop Panel", priority = 43)]
        private static void Patch()
        {
            if (Application.isPlaying) { EditorUtility.DisplayDialog("Patch Shop", "Impossible pendant Play Mode.", "OK"); return; }
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.path.EndsWith("10_CommunityHub.unity"))
            {
                if (!EditorUtility.DisplayDialog("Patch Shop",
                    $"Scene active : {scene.path}\nAttendu : {ScenePath}\n\nOuvrir 10_CommunityHub ?", "Ouvrir", "Annuler")) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) { EditorUtility.DisplayDialog("Patch Shop", "Aucun Canvas.", "OK"); return; }

            var actions = new List<string>();
            EnsureSpriteImports(actions);
            EnsureButton(canvas, actions);
            EnsurePanel(canvas, actions);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Nymora.Setup] Patch shop : {string.Join("\n", actions)}");
            EditorUtility.DisplayDialog("Patch Shop", string.Join("\n", actions) + "\n\nN'oublie pas Ctrl+S.", "OK");
        }

        // Les vignettes doivent etre importees en Sprite pour Resources.Load<Sprite>.
        private static void EnsureSpriteImports(List<string> actions)
        {
            int fixedCount = 0;
            foreach (var key in CosmeticKeys)
            {
                string path = $"{CosmeticsResDir}/{key}.png";
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;
                if (imp.textureType != TextureImporterType.Sprite)
                {
                    imp.textureType = TextureImporterType.Sprite;
                    imp.SaveAndReimport();
                    fixedCount++;
                }
            }
            if (fixedCount > 0) actions.Add($"{fixedCount} vignette(s) reimportee(s) en Sprite");
        }

        private static void EnsureButton(Canvas canvas, List<string> actions)
        {
            var existing = canvas.transform.Find("ShopButton");
            GameObject go;
            bool isNew = existing == null;
            if (!isNew) { go = existing.gameObject; actions.Add("ShopButton re-style (position préservée)"); }
            else
            {
                go = new GameObject("ShopButton", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(canvas.transform, false);
                actions.Add("ShopButton créé (haut-gauche, sous Quêtes)");
            }
            var img = go.GetComponent<Image>(); img.color = ShopButtonColor;
            go.GetComponent<Button>().targetGraphic = img;
            if (go.GetComponent<HubShopButton>() == null) go.AddComponent<HubShopButton>();
            if (isNew)
            {
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(20f, -150f); // sous Quêtes (-86, h56)
                rt.sizeDelta = new Vector2(190f, 56f);
            }
            var oldLabel = go.transform.Find("Label");
            if (oldLabel != null) Object.DestroyImmediate(oldLabel.gameObject);
            var labelGo = NewChild("Label", go.transform); StretchToParent(labelGo);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = "Boutique"; tmp.fontSize = 24f; tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center; tmp.fontStyle = FontStyles.Bold;
        }

        private static void EnsurePanel(Canvas canvas, List<string> actions)
        {
            var existing = canvas.transform.Find("ShopPanelHost");
            HubShopPanel panel;
            GameObject hostGo;
            if (existing != null)
            {
                hostGo = existing.gameObject;
                panel = hostGo.GetComponent<HubShopPanel>() ?? hostGo.AddComponent<HubShopPanel>();
                for (int i = hostGo.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(hostGo.transform.GetChild(i).gameObject);
                actions.Add("ShopPanelHost cleanup (rebuild)");
            }
            else
            {
                hostGo = new GameObject("ShopPanelHost", typeof(RectTransform));
                hostGo.transform.SetParent(canvas.transform, false);
                StretchToParent(hostGo);
                panel = hostGo.AddComponent<HubShopPanel>();
                actions.Add("ShopPanelHost créé");
            }

            var panelRoot = NewChild("PanelRoot", hostGo.transform); StretchToParent(panelRoot);
            var backdrop = NewChild("Backdrop", panelRoot.transform); StretchToParent(backdrop);
            backdrop.AddComponent<Image>().color = BackdropColor;

            var container = NewChild("Container", panelRoot.transform);
            var crt = container.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f); crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(760f, 640f);
            container.AddComponent<Image>().color = ContainerColor;

            // Header
            var header = NewChild("Header", container.transform);
            var hRt = header.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0f, 1f); hRt.anchorMax = new Vector2(1f, 1f);
            hRt.pivot = new Vector2(0.5f, 1f); hRt.sizeDelta = new Vector2(0f, 56f);
            header.AddComponent<Image>().color = HeaderColor;
            var headerTextGo = NewChild("HeaderText", header.transform);
            var htRt = headerTextGo.GetComponent<RectTransform>();
            htRt.anchorMin = new Vector2(0f, 0f); htRt.anchorMax = new Vector2(1f, 1f);
            htRt.offsetMin = new Vector2(20f, 0f); htRt.offsetMax = new Vector2(-200f, 0f);
            var headerTmp = headerTextGo.AddComponent<TextMeshProUGUI>();
            headerTmp.text = "Boutique"; headerTmp.fontSize = 24f; headerTmp.color = Color.white;
            headerTmp.alignment = TextAlignmentOptions.Left; headerTmp.fontStyle = FontStyles.Bold;

            // Texte rotation (droite du header, avant le X)
            var rotGo = NewChild("RotationText", header.transform);
            var rRt = rotGo.GetComponent<RectTransform>();
            rRt.anchorMin = new Vector2(1f, 0f); rRt.anchorMax = new Vector2(1f, 1f);
            rRt.pivot = new Vector2(1f, 0.5f); rRt.sizeDelta = new Vector2(300f, 0f);
            rRt.anchoredPosition = new Vector2(-64f, 0f);
            var rotTmp = rotGo.AddComponent<TextMeshProUGUI>();
            rotTmp.text = ""; rotTmp.fontSize = 15f; rotTmp.color = new Color(0.7f, 0.75f, 0.85f);
            rotTmp.alignment = TextAlignmentOptions.Right;

            var closeGo = MakeButton(header.transform, "CloseButton", "X", CloseColor, 44f, 38f);
            var crtClose = closeGo.GetComponent<RectTransform>();
            crtClose.anchorMin = crtClose.anchorMax = new Vector2(1f, 0.5f); crtClose.pivot = new Vector2(1f, 0.5f);
            crtClose.anchoredPosition = new Vector2(-10f, 0f);

            // ScrollRect vertical
            var scrollGo = NewChild("Scroll", container.transform);
            var sRt = scrollGo.GetComponent<RectTransform>();
            sRt.anchorMin = new Vector2(0f, 0f); sRt.anchorMax = new Vector2(1f, 1f);
            sRt.offsetMin = new Vector2(20f, 16f); sRt.offsetMax = new Vector2(-20f, -64f);
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30f;
            var viewport = NewChild("Viewport", scrollGo.transform); StretchToParent(viewport);
            viewport.AddComponent<Image>().color = ViewportColor;
            viewport.AddComponent<RectMask2D>();

            var content = NewChild("Content", viewport.transform);
            var cRt = content.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0f, 1f); cRt.anchorMax = new Vector2(1f, 1f); cRt.pivot = new Vector2(0.5f, 1f);
            // sizeDelta.x DOIT etre 0 (ancrages etires + CSF horizontal Unconstrained) sinon debord.
            cRt.sizeDelta = Vector2.zero; cRt.anchoredPosition = Vector2.zero;
            var grid = content.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(220f, 168f);
            grid.spacing = new Vector2(12f, 12f);
            grid.padding = new RectOffset(14, 14, 12, 12);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = cRt;

            var template = BuildCellTemplate(panelRoot.transform);
            template.SetActive(false);

            var so = new SerializedObject(panel);
            so.FindProperty("_panelRoot").objectReferenceValue = panelRoot;
            so.FindProperty("_closeButton").objectReferenceValue = closeGo.GetComponent<Button>();
            so.FindProperty("_headerText").objectReferenceValue = headerTmp;
            so.FindProperty("_rotationText").objectReferenceValue = rotTmp;
            so.FindProperty("_content").objectReferenceValue = cRt;
            so.FindProperty("_cellTemplate").objectReferenceValue = template.GetComponent<ShopItemCell>();
            var settingsGuids = AssetDatabase.FindAssets("t:NymoraBackendSettings");
            if (settingsGuids.Length > 0)
                so.FindProperty("_backendSettings").objectReferenceValue =
                    AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(settingsGuids[0]));
            so.ApplyModifiedPropertiesWithoutUndo();
            actions.Add("ShopPanel wires (content + cell template + settings)");

            panelRoot.SetActive(false);
        }

        private static GameObject BuildCellTemplate(Transform parent)
        {
            var cell = new GameObject("ShopCellTemplate", typeof(RectTransform), typeof(Image));
            cell.transform.SetParent(parent, false);
            cell.GetComponent<Image>().color = CellColor;
            var comp = cell.AddComponent<ShopItemCell>();

            // Fond vignette (haut, centré) + Image preview par-dessus.
            var prevBg = new GameObject("PreviewBg", typeof(RectTransform), typeof(Image));
            prevBg.transform.SetParent(cell.transform, false);
            var pbRt = prevBg.GetComponent<RectTransform>();
            pbRt.anchorMin = pbRt.anchorMax = new Vector2(0.5f, 1f); pbRt.pivot = new Vector2(0.5f, 1f);
            pbRt.anchoredPosition = new Vector2(0f, -8f); pbRt.sizeDelta = new Vector2(88f, 88f);
            prevBg.GetComponent<Image>().color = PreviewBgColor;

            var prevGo = new GameObject("Preview", typeof(RectTransform), typeof(Image));
            prevGo.transform.SetParent(prevBg.transform, false);
            var pRt = prevGo.GetComponent<RectTransform>();
            pRt.anchorMin = Vector2.zero; pRt.anchorMax = Vector2.one;
            pRt.offsetMin = new Vector2(4f, 4f); pRt.offsetMax = new Vector2(-4f, -4f);
            var prevImg = prevGo.GetComponent<Image>();
            prevImg.preserveAspect = true;

            // Nom (bande basse).
            var nameGo = NewChild("Name", cell.transform);
            var nRt = nameGo.GetComponent<RectTransform>();
            nRt.anchorMin = new Vector2(0f, 0f); nRt.anchorMax = new Vector2(1f, 0f); nRt.pivot = new Vector2(0.5f, 0f);
            nRt.offsetMin = new Vector2(6f, 58f); nRt.offsetMax = new Vector2(-6f, 80f);
            var nameTmp = nameGo.AddComponent<TextMeshProUGUI>();
            nameTmp.text = "Item"; nameTmp.fontSize = 15f; nameTmp.color = Color.white;
            nameTmp.alignment = TextAlignmentOptions.Center; nameTmp.enableWordWrapping = false;
            nameTmp.overflowMode = TextOverflowModes.Ellipsis;

            // Prix.
            var priceGo = NewChild("Price", cell.transform);
            var prRt = priceGo.GetComponent<RectTransform>();
            prRt.anchorMin = new Vector2(0f, 0f); prRt.anchorMax = new Vector2(1f, 0f); prRt.pivot = new Vector2(0.5f, 0f);
            prRt.offsetMin = new Vector2(6f, 34f); prRt.offsetMax = new Vector2(-6f, 56f);
            var priceTmp = priceGo.AddComponent<TextMeshProUGUI>();
            priceTmp.text = ""; priceTmp.fontSize = 16f; priceTmp.color = new Color(0.96f, 0.86f, 0.40f);
            priceTmp.alignment = TextAlignmentOptions.Center; priceTmp.fontStyle = FontStyles.Bold;

            // Bouton Acheter (bas).
            var buyGo = MakeButton(cell.transform, "BuyButton", "Acheter", BuyColor, 0f, 0f);
            var bRt = buyGo.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0f, 0f); bRt.anchorMax = new Vector2(1f, 0f); bRt.pivot = new Vector2(0.5f, 0f);
            bRt.offsetMin = new Vector2(10f, 6f); bRt.offsetMax = new Vector2(-10f, 32f);
            var buyLabel = buyGo.transform.Find("Label")?.GetComponent<TMP_Text>();
            if (buyLabel != null) buyLabel.fontSize = 15f;

            var so = new SerializedObject(comp);
            so.FindProperty("_preview").objectReferenceValue = prevImg;
            so.FindProperty("_nameText").objectReferenceValue = nameTmp;
            so.FindProperty("_priceText").objectReferenceValue = priceTmp;
            so.FindProperty("_buyButton").objectReferenceValue = buyGo.GetComponent<Button>();
            so.FindProperty("_buyLabel").objectReferenceValue = buyLabel;
            so.ApplyModifiedPropertiesWithoutUndo();

            return cell;
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
