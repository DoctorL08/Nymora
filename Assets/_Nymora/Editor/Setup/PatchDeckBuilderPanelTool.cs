using System.Collections.Generic;
using Nymora.Core.ScriptableObjects;
using Nymora.Hub;
using Nymora.Network.Backend;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Brique 5.3.e — Patch idempotent de la scene 10_CommunityHub pour le Deck Builder UI complet.
    ///
    /// Layout (Container 1100x700) :
    ///   - Header bar : title "Deck Builder" + close button (X)
    ///   - Left zone : ClassName label + Signature label + SlotsRow (6 slots horizontal) + SpellsGrid (15 sorts)
    ///   - Right sidebar : DecksList (max 5 vertical) + NameInput + Buttons row (New/Save/Delete)
    ///   - Bottom status label
    ///   - Floating TooltipPanel (caché par défaut)
    ///
    /// Menu : Nymora > Setup > Patch Deck Builder Panel (rerunnable / idempotent).
    /// </summary>
    public static class PatchDeckBuilderPanelTool
    {
        private const string ScenePath = "Assets/_Nymora/Scenes/10_CommunityHub.unity";
        private const string BackendSettingsPath = "Assets/_Nymora/Settings/NymoraBackendSettings.asset";
        private const string SpellCatalogPath = "Assets/_Nymora/ScriptableObjects/Spells/SpellCatalog.asset";

        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.65f);
        private static readonly Color ContainerColor = new Color(0.12f, 0.13f, 0.16f, 0.98f);
        private static readonly Color HeaderColor = new Color(0.18f, 0.20f, 0.25f, 1f);
        private static readonly Color SidebarColor = new Color(0.10f, 0.11f, 0.14f, 1f);
        private static readonly Color CloseColor = new Color(0.55f, 0.25f, 0.25f, 1f);
        private static readonly Color NewBtnColor = new Color(0.30f, 0.45f, 0.30f, 1f);
        private static readonly Color SaveBtnColor = new Color(0.25f, 0.40f, 0.55f, 1f);
        private static readonly Color DeleteBtnColor = new Color(0.50f, 0.25f, 0.25f, 1f);
        private static readonly Color DecksButtonColor = new Color(0.30f, 0.40f, 0.55f, 1f);
        private static readonly Color TooltipBg = new Color(0.05f, 0.06f, 0.08f, 0.97f);

        [MenuItem("Nymora/Setup/Patch Deck Builder Panel", priority = 36)]
        private static void Patch()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Patch Deck Builder", "Impossible pendant Play Mode.", "OK");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.path.EndsWith("10_CommunityHub.unity"))
            {
                if (!EditorUtility.DisplayDialog("Patch Deck Builder",
                    $"Scene active : {scene.path}\nAttendu : {ScenePath}\n\nOuvrir 10_CommunityHub ?", "Ouvrir", "Annuler")) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) { EditorUtility.DisplayDialog("Patch Deck Builder", "Aucun Canvas.", "OK"); return; }

            var settings = AssetDatabase.LoadAssetAtPath<NymoraBackendSettings>(BackendSettingsPath);
            if (settings == null) { EditorUtility.DisplayDialog("Patch Deck Builder", $"NymoraBackendSettings introuvable a {BackendSettingsPath}.", "OK"); return; }

            var catalog = AssetDatabase.LoadAssetAtPath<SpellCatalog>(SpellCatalogPath);
            if (catalog == null)
            {
                EditorUtility.DisplayDialog("Patch Deck Builder",
                    $"SpellCatalog.asset introuvable a {SpellCatalogPath}.\nLance d'abord Nymora > Setup > Populate Spell Catalog.", "OK");
                return;
            }

            var actions = new List<string>();
            EnsureDecksButton(canvas, actions);
            EnsureDeckBuilderPanel(canvas, settings, catalog, actions);

            EditorSceneManager.MarkSceneDirty(scene);

            string summary = actions.Count == 0 ? "OK Scene deja a jour." : "Patch applique :\n\n" + string.Join("\n", actions) + "\n\nN'oublie pas Ctrl+S.";
            Debug.Log($"[Nymora.Setup] Patch deck builder : {actions.Count} action(s)\n{summary}");
            EditorUtility.DisplayDialog("Patch Deck Builder", summary, "OK");
        }

        // -----------------------------------------------------------------------------
        // Bouton hub bas-droite (a gauche des autres). Force-update si existe deja
        // (sinon les anciens patchs gardent leur mauvaise position).
        // Position : (-590, 20) — a gauche de Clan (-400) qui est a gauche d'Amis (-210)
        // qui est a gauche de Mon profil (-20). Largeur 180, hauteur 56.
        // -----------------------------------------------------------------------------
        private static void EnsureDecksButton(Canvas canvas, List<string> actions)
        {
            var existing = canvas.transform.Find("DecksButton");
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
                actions.Add("DecksButton existant : re-positionne + restyle");
            }
            else
            {
                go = new GameObject("DecksButton", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(canvas.transform, false);
                actions.Add("DecksButton cree");
            }

            // Force les params (override l'ancien etat)
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-590f, 20f);
            rt.sizeDelta = new Vector2(180f, 56f);

            var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            img.color = DecksButtonColor;

            if (go.GetComponent<Button>() == null) go.AddComponent<Button>();
            go.GetComponent<Button>().targetGraphic = img;

            if (go.GetComponent<HubDeckBuilderButton>() == null) go.AddComponent<HubDeckBuilderButton>();

            // Recree le Label propre (clean ancien)
            var oldLabel = go.transform.Find("Label");
            if (oldLabel != null) Object.DestroyImmediate(oldLabel.gameObject);
            var labelGo = NewChild("Label", go.transform);
            StretchToParent(labelGo);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = "Decks";
            tmp.fontSize = 26f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
        }

        // -----------------------------------------------------------------------------
        // Panel + vraie UI Deck Builder.
        // -----------------------------------------------------------------------------
        private static void EnsureDeckBuilderPanel(Canvas canvas, NymoraBackendSettings settings, SpellCatalog catalog, List<string> actions)
        {
            // Si le host existe déjà avec ancienne hierarchie minimale, on rebuild en repartant de zéro
            // pour éviter l'enfer du diff stateful. On supprime juste les childs UI, on garde le host + script.
            var existingHost = canvas.transform.Find("DeckBuilderPanelHost");
            HubDeckBuilderPanel panel;
            GameObject hostGo;

            if (existingHost != null)
            {
                hostGo = existingHost.gameObject;
                panel = hostGo.GetComponent<HubDeckBuilderPanel>() ?? hostGo.AddComponent<HubDeckBuilderPanel>();
                // Cleanup childs pour rebuild
                for (int i = hostGo.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(hostGo.transform.GetChild(i).gameObject);
                actions.Add("DeckBuilderPanelHost cleanup (rebuild)");
            }
            else
            {
                hostGo = new GameObject("DeckBuilderPanelHost", typeof(RectTransform));
                hostGo.transform.SetParent(canvas.transform, false);
                StretchToParent(hostGo);
                panel = hostGo.AddComponent<HubDeckBuilderPanel>();
                actions.Add("DeckBuilderPanelHost cree");
            }

            // PanelRoot (toggleable)
            var panelRoot = NewChild("PanelRoot", hostGo.transform);
            StretchToParent(panelRoot);

            // Backdrop fullscreen
            var backdrop = NewChild("Backdrop", panelRoot.transform);
            StretchToParent(backdrop);
            backdrop.AddComponent<Image>().color = BackdropColor;

            // Container PLEIN-ECRAN (stretch to parent)
            var container = NewChild("Container", panelRoot.transform);
            StretchToParent(container);
            container.AddComponent<Image>().color = ContainerColor;

            // === Header bar (top) ===
            var header = NewChild("Header", container.transform);
            AnchorTopStretch(header, 50f, 0f);
            header.AddComponent<Image>().color = HeaderColor;

            var title = NewChild("Title", header.transform);
            StretchToParent(title);
            var titleTmp = title.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "Deck Builder";
            titleTmp.fontSize = 24f;
            titleTmp.color = Color.white;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.fontStyle = FontStyles.Bold;

            var closeBtn = MakeButton(header.transform, "CloseButton", "X", CloseColor, 40f, 36f);
            var crtClose = closeBtn.GetComponent<RectTransform>();
            crtClose.anchorMin = crtClose.anchorMax = new Vector2(1f, 0.5f);
            crtClose.pivot = new Vector2(1f, 0.5f);
            crtClose.anchoredPosition = new Vector2(-10f, 0f);

            // === Class header (under top bar) ===
            var classHeader = NewChild("ClassHeader", container.transform);
            AnchorTopStretch(classHeader, 80f, 50f); // top offset 50 (sous header), height 80
            var classNameGo = NewChild("ClassName", classHeader.transform);
            AnchorTopStretch(classNameGo, 40f, 6f);
            var classNameTmp = classNameGo.AddComponent<TextMeshProUGUI>();
            classNameTmp.text = "Soulrender";
            classNameTmp.fontSize = 32f;
            classNameTmp.color = new Color(0.95f, 0.9f, 0.85f);
            classNameTmp.alignment = TextAlignmentOptions.Center;
            classNameTmp.fontStyle = FontStyles.Bold;

            var sigGo = NewChild("Signature", classHeader.transform);
            AnchorTopStretch(sigGo, 26f, 48f);
            var sigTmp = sigGo.AddComponent<TextMeshProUGUI>();
            sigTmp.text = "Signature : —";
            sigTmp.fontSize = 16f;
            sigTmp.color = new Color(0.85f, 0.85f, 0.95f);
            sigTmp.alignment = TextAlignmentOptions.Center;
            sigTmp.richText = true;

            // === SlotsRow (6 slots horizontal, gros pour visibilite) ===
            var slotsRow = NewChild("SlotsRow", container.transform);
            var slotsRt = slotsRow.GetComponent<RectTransform>();
            slotsRt.anchorMin = new Vector2(0f, 1f);
            slotsRt.anchorMax = new Vector2(1f, 1f);
            slotsRt.pivot = new Vector2(0.5f, 1f);
            slotsRt.offsetMin = new Vector2(40f, 0f);
            slotsRt.offsetMax = new Vector2(-360f, 0f);
            slotsRt.anchoredPosition = new Vector2(slotsRt.anchoredPosition.x, -150f);
            slotsRt.sizeDelta = new Vector2(slotsRt.sizeDelta.x, 140f);
            var slotsHlg = slotsRow.AddComponent<HorizontalLayoutGroup>();
            slotsHlg.spacing = 16f;
            slotsHlg.padding = new RectOffset(15, 15, 10, 10);
            slotsHlg.childAlignment = TextAnchor.MiddleCenter;
            slotsHlg.childForceExpandWidth = false;
            slotsHlg.childForceExpandHeight = false;
            slotsHlg.childControlWidth = true;
            slotsHlg.childControlHeight = true;

            // === CategoryTabsRow : 3 onglets cliquables OFFENSIFS / TACTIQUES / SURVIE ===
            var tabsRowGo = NewChild("CategoryTabsRow", container.transform);
            var tabsRt = tabsRowGo.GetComponent<RectTransform>();
            tabsRt.anchorMin = new Vector2(0f, 1f);
            tabsRt.anchorMax = new Vector2(1f, 1f);
            tabsRt.pivot = new Vector2(0.5f, 1f);
            tabsRt.offsetMin = new Vector2(40f, 0f);
            tabsRt.offsetMax = new Vector2(-360f, 0f);
            tabsRt.anchoredPosition = new Vector2(tabsRt.anchoredPosition.x, -300f);
            tabsRt.sizeDelta = new Vector2(tabsRt.sizeDelta.x, 60f);
            var tabsHlg = tabsRowGo.AddComponent<HorizontalLayoutGroup>();
            tabsHlg.spacing = 8f;
            tabsHlg.padding = new RectOffset(0, 0, 0, 0);
            tabsHlg.childAlignment = TextAnchor.MiddleCenter;
            tabsHlg.childForceExpandWidth = true;
            tabsHlg.childForceExpandHeight = true;
            tabsHlg.childControlWidth = true;
            tabsHlg.childControlHeight = true;

            // === SpellsGrid : container horizontal, le panel script y spawn les 5 sorts de l'onglet actif ===
            var spellsGridGo = NewChild("SpellsGrid", container.transform);
            var sgRt = spellsGridGo.GetComponent<RectTransform>();
            sgRt.anchorMin = new Vector2(0f, 0f);
            sgRt.anchorMax = new Vector2(1f, 1f);
            sgRt.offsetMin = new Vector2(40f, 60f);
            sgRt.offsetMax = new Vector2(-360f, -370f);
            var sgHlg = spellsGridGo.AddComponent<HorizontalLayoutGroup>();
            sgHlg.spacing = 18f;
            sgHlg.padding = new RectOffset(15, 15, 20, 20);
            sgHlg.childAlignment = TextAnchor.UpperCenter;
            sgHlg.childForceExpandWidth = false;
            sgHlg.childForceExpandHeight = false;
            sgHlg.childControlWidth = true;
            sgHlg.childControlHeight = true;

            // === Right sidebar (decks list + input + buttons) ===
            var sidebar = NewChild("Sidebar", container.transform);
            var sbRt = sidebar.GetComponent<RectTransform>();
            sbRt.anchorMin = new Vector2(1f, 0f);
            sbRt.anchorMax = new Vector2(1f, 1f);
            sbRt.pivot = new Vector2(1f, 0.5f);
            sbRt.sizeDelta = new Vector2(320f, -130f);
            sbRt.anchoredPosition = new Vector2(-20f, -65f);
            sidebar.AddComponent<Image>().color = SidebarColor;

            var sbVlg = sidebar.AddComponent<VerticalLayoutGroup>();
            sbVlg.spacing = 6f;
            sbVlg.padding = new RectOffset(10, 10, 10, 10);
            sbVlg.childAlignment = TextAnchor.UpperCenter;
            sbVlg.childForceExpandWidth = true;
            sbVlg.childForceExpandHeight = false;
            sbVlg.childControlWidth = true;
            sbVlg.childControlHeight = false;

            var sbTitle = NewChild("SidebarTitle", sidebar.transform);
            var sbTitleLE = sbTitle.AddComponent<LayoutElement>();
            sbTitleLE.preferredHeight = 36f;
            var sbTitleTmp = sbTitle.AddComponent<TextMeshProUGUI>();
            sbTitleTmp.text = "Mes decks";
            sbTitleTmp.fontSize = 24f;
            sbTitleTmp.color = Color.white;
            sbTitleTmp.alignment = TextAlignmentOptions.Center;
            sbTitleTmp.fontStyle = FontStyles.Bold;

            var decksList = NewChild("DecksList", sidebar.transform);
            var dlLE = decksList.AddComponent<LayoutElement>();
            dlLE.preferredHeight = 320f;
            var dlVlg = decksList.AddComponent<VerticalLayoutGroup>();
            dlVlg.spacing = 4f;
            dlVlg.childAlignment = TextAnchor.UpperLeft;
            dlVlg.childForceExpandWidth = true;
            dlVlg.childForceExpandHeight = false;
            dlVlg.childControlWidth = true;
            dlVlg.childControlHeight = false;

            // Input field nom deck
            var nameInputGo = NewChild("DeckNameInput", sidebar.transform);
            nameInputGo.AddComponent<LayoutElement>().preferredHeight = 48f;
            var nameImg = nameInputGo.AddComponent<Image>();
            nameImg.color = new Color(0.20f, 0.22f, 0.26f, 1f);
            var inputField = nameInputGo.AddComponent<TMP_InputField>();

            var textArea = NewChild("Text Area", nameInputGo.transform);
            var taRt = textArea.GetComponent<RectTransform>();
            taRt.anchorMin = Vector2.zero;
            taRt.anchorMax = Vector2.one;
            taRt.offsetMin = new Vector2(12f, 6f);
            taRt.offsetMax = new Vector2(-12f, -6f);
            textArea.AddComponent<RectMask2D>();

            var placeholderGo = NewChild("Placeholder", textArea.transform);
            StretchToParent(placeholderGo);
            var phTmp = placeholderGo.AddComponent<TextMeshProUGUI>();
            phTmp.text = "Nom du deck...";
            phTmp.fontSize = 18f;
            phTmp.color = new Color(0.7f, 0.7f, 0.75f, 0.7f);
            phTmp.alignment = TextAlignmentOptions.MidlineLeft;

            var textGo = NewChild("Text", textArea.transform);
            StretchToParent(textGo);
            var textTmp = textGo.AddComponent<TextMeshProUGUI>();
            textTmp.fontSize = 18f;
            textTmp.color = Color.white;
            textTmp.alignment = TextAlignmentOptions.MidlineLeft;

            inputField.targetGraphic = nameImg;
            inputField.textViewport = taRt;
            inputField.textComponent = textTmp;
            inputField.placeholder = phTmp;
            inputField.characterLimit = 32;

            // Buttons row : New / Save / Delete
            var btnsRow = NewChild("ButtonsRow", sidebar.transform);
            btnsRow.AddComponent<LayoutElement>().preferredHeight = 56f;
            var btnsHlg = btnsRow.AddComponent<HorizontalLayoutGroup>();
            btnsHlg.spacing = 6f;
            btnsHlg.childForceExpandWidth = true;
            btnsHlg.childForceExpandHeight = true;
            btnsHlg.childControlWidth = true;
            btnsHlg.childControlHeight = true;

            var newBtn = MakeButton(btnsRow.transform, "NewButton", "Nouveau", NewBtnColor, 0f, 0f);
            var saveBtn = MakeButton(btnsRow.transform, "SaveButton", "Save", SaveBtnColor, 0f, 0f);
            var deleteBtn = MakeButton(btnsRow.transform, "DeleteButton", "Suppr.", DeleteBtnColor, 0f, 0f);

            // === Status label (bottom) ===
            var statusGo = NewChild("StatusLabel", container.transform);
            var stRt = statusGo.GetComponent<RectTransform>();
            stRt.anchorMin = new Vector2(0f, 0f);
            stRt.anchorMax = new Vector2(1f, 0f);
            stRt.pivot = new Vector2(0.5f, 0f);
            stRt.sizeDelta = new Vector2(0f, 36f);
            stRt.anchoredPosition = new Vector2(0f, 6f);
            stRt.offsetMin = new Vector2(20f, stRt.offsetMin.y);
            stRt.offsetMax = new Vector2(-20f, stRt.offsetMax.y);
            var statusTmp = statusGo.AddComponent<TextMeshProUGUI>();
            statusTmp.text = "...";
            statusTmp.fontSize = 18f;
            statusTmp.color = new Color(0.85f, 0.85f, 0.9f);
            statusTmp.alignment = TextAlignmentOptions.Center;
            statusTmp.richText = true;

            // === Tooltip floating bas-centre (top-level dans Container pour rester au-dessus) ===
            var tooltipGo = NewChild("TooltipPanel", container.transform);
            var ttRt = tooltipGo.GetComponent<RectTransform>();
            ttRt.anchorMin = new Vector2(0.5f, 0f);
            ttRt.anchorMax = new Vector2(0.5f, 0f);
            ttRt.pivot = new Vector2(0.5f, 0f);
            ttRt.sizeDelta = new Vector2(700f, 240f);
            ttRt.anchoredPosition = new Vector2(0f, 80f);
            tooltipGo.AddComponent<Image>().color = TooltipBg;
            var ttTextGo = NewChild("Text", tooltipGo.transform);
            StretchToParent(ttTextGo);
            var ttTextRt = ttTextGo.GetComponent<RectTransform>();
            ttTextRt.offsetMin = new Vector2(18f, 16f);
            ttTextRt.offsetMax = new Vector2(-18f, -16f);
            var ttTmp = ttTextGo.AddComponent<TextMeshProUGUI>();
            ttTmp.fontSize = 18f;
            ttTmp.color = Color.white;
            ttTmp.alignment = TextAlignmentOptions.TopLeft;
            ttTmp.richText = true;
            ttTmp.enableWordWrapping = true;
            tooltipGo.SetActive(false);

            // === Wire SerializedFields ===
            var so = new SerializedObject(panel);
            so.FindProperty("_backendSettings").objectReferenceValue = settings;
            so.FindProperty("_spellCatalog").objectReferenceValue = catalog;
            so.FindProperty("_panelRoot").objectReferenceValue = panelRoot;
            so.FindProperty("_closeButton").objectReferenceValue = closeBtn.GetComponent<Button>();
            so.FindProperty("_classNameLabel").objectReferenceValue = classNameTmp;
            so.FindProperty("_signatureLabel").objectReferenceValue = sigTmp;
            so.FindProperty("_slotsRow").objectReferenceValue = slotsRt;
            so.FindProperty("_categoryTabsRow").objectReferenceValue = tabsRt;
            so.FindProperty("_spellsGrid").objectReferenceValue = sgRt;
            so.FindProperty("_decksList").objectReferenceValue = decksList.GetComponent<RectTransform>();
            so.FindProperty("_deckNameInput").objectReferenceValue = inputField;
            so.FindProperty("_newDeckButton").objectReferenceValue = newBtn.GetComponent<Button>();
            so.FindProperty("_saveDeckButton").objectReferenceValue = saveBtn.GetComponent<Button>();
            so.FindProperty("_deleteDeckButton").objectReferenceValue = deleteBtn.GetComponent<Button>();
            so.FindProperty("_statusLabel").objectReferenceValue = statusTmp;
            so.FindProperty("_tooltipPanel").objectReferenceValue = tooltipGo;
            so.FindProperty("_tooltipText").objectReferenceValue = ttTmp;
            so.ApplyModifiedPropertiesWithoutUndo();

            actions.Add("Layout deck builder genere + fields wired");

            panelRoot.SetActive(false);
        }

        // -----------------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------------
        private static GameObject NewChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void StretchToParent(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void AnchorTopStretch(GameObject go, float height, float topOffset)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = new Vector2(0f, -topOffset);
        }

        private static GameObject MakeButton(Transform parent, string name, string label, Color color, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            if (width > 0f && height > 0f) rt.sizeDelta = new Vector2(width, height);
            var img = go.GetComponent<Image>();
            img.color = color;
            go.GetComponent<Button>().targetGraphic = img;

            var labelGo = NewChild("Label", go.transform);
            StretchToParent(labelGo);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 18f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            return go;
        }
    }
}
