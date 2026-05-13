using Nymora.Combat.View;
using Nymora.Combat.View.HUD;
using Quantum;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Genere le HUD combat complet (2.13.a) dans la scene active.
    ///
    /// Layout final (reference 1920x1080) :
    ///   - haut-gauche  : panneau P0 (HP/PA/PM + ressource + statuses)
    ///   - haut-droite  : panneau P1 idem
    ///   - haut-centre  : timer + numero de tour
    ///   - milieu-droite: bouton End Turn
    ///   - bas-gauche   : passif (icone classe + compteur)
    ///   - bas-centre   : 6 slots de sorts + slot signature
    ///   - bas-droite   : timeline "> P0 | P1"
    ///
    /// Menu : Nymora > Setup > Create Combat HUD
    /// Idempotent : detruit l'ancien Canvas avant de recreer.
    ///
    /// Auto-wire : reference le SpellIconRegistry asset standard et auto-cable le HUDController
    /// au CombatInputController de la scene (s'il existe).
    /// </summary>
    public static class CreateCombatHUDTool
    {
        private const string CanvasName = "CombatHUDCanvas";
        private const string HudRootName = "CombatHUD";
        private const string SpellIconRegistryPath = "Assets/_Nymora/ScriptableObjects/Spells/SpellIconRegistry.asset";

        [MenuItem("Nymora/Setup/Create Combat HUD")]
        public static void Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("Create Combat HUD", "Aucune scene ouverte.", "OK");
                return;
            }

            // 1. Supprime un eventuel ancien Canvas HUD pour repartir propre.
            var existing = GameObject.Find(CanvasName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            // 2. Canvas en ScreenSpaceOverlay.
            var canvasGo = new GameObject(CanvasName,
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // 3. Root HUD (porte le CombatHUDController).
            var hudGo = new GameObject(HudRootName, typeof(RectTransform));
            hudGo.transform.SetParent(canvasGo.transform, false);
            var hudRoot = hudGo.GetComponent<RectTransform>();
            hudRoot.anchorMin = Vector2.zero;
            hudRoot.anchorMax = Vector2.one;
            hudRoot.offsetMin = Vector2.zero;
            hudRoot.offsetMax = Vector2.zero;

            var controller = hudGo.AddComponent<CombatHUDController>();

            // 4. Widgets.
            ResourcePanelView p0Panel = BuildResourcePanel(hudRoot, "P0Panel",
                anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(0f, 1f),
                pivot: new Vector2(0f, 1f), anchoredPos: new Vector2(20f, -20f), size: new Vector2(380f, 160f));

            ResourcePanelView p1Panel = BuildResourcePanel(hudRoot, "P1Panel",
                anchorMin: new Vector2(1f, 1f), anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(1f, 1f), anchoredPos: new Vector2(-20f, -20f), size: new Vector2(380f, 160f));

            TimerView timer = BuildTimerView(hudRoot);
            PassivePanelView passive = BuildPassivePanel(hudRoot);
            TimelineView timeline = BuildTimelineView(hudRoot);
            Button endTurnButton = BuildEndTurnButton(hudRoot);

            // SpellBar : 6 slots + signature.
            SpellSlotView[] deckSlots;
            SpellSlotView signatureSlot;
            BuildSpellBar(hudRoot, out deckSlots, out signatureSlot);

            // 4.b — 2.13.c : tooltip panel (hidden par defaut, dans le meme canvas).
            SpellTooltipView tooltip = BuildTooltip(hudRoot);

            // 4.c — 2.16.c.ii : overlay Victory/Defeat (hidden par defaut).
            MatchEndOverlay matchEndOverlay = BuildMatchEndOverlay(hudRoot);

            // 5. Charge le SpellIconRegistry asset standard.
            var iconRegistry = AssetDatabase.LoadAssetAtPath<SpellIconRegistry>(SpellIconRegistryPath);
            if (iconRegistry == null)
            {
                Debug.LogWarning(
                    $"[CreateCombatHUDTool] SpellIconRegistry introuvable a {SpellIconRegistryPath}. " +
                    "Lance d'abord 'Nymora > Setup > Populate Spell Icon Registry'.");
            }

            // 6. Cable toutes les references via SerializedObject.
            var so = new SerializedObject(controller);
            SetObjectRef(so, "_iconRegistry", iconRegistry);
            SetObjectRef(so, "_p0Panel", p0Panel);
            SetObjectRef(so, "_p1Panel", p1Panel);
            SetObjectRef(so, "_timer", timer);
            SetObjectRef(so, "_passive", passive);
            SetObjectRef(so, "_timeline", timeline);
            SetObjectRef(so, "_endTurnButton", endTurnButton);
            SetObjectRef(so, "_signatureSlot", signatureSlot);
            SetObjectRef(so, "_tooltip", tooltip);
            SetObjectRef(so, "_matchEndOverlay", matchEndOverlay);

            // Array _spellSlots (6).
            var slotsProp = so.FindProperty("_spellSlots");
            if (slotsProp != null)
            {
                slotsProp.arraySize = deckSlots.Length;
                for (int i = 0; i < deckSlots.Length; i++)
                {
                    slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = deckSlots[i];
                }
            }

            // _testDeck : pre-rempli avec un deck Soulrender de demo (6 sorts equilibres).
            // Lorenzo peut tout reconfigurer dans l'Inspector apres coup.
            var deckProp = so.FindProperty("_testDeck");
            if (deckProp != null)
            {
                SpellId[] defaultDeck =
                {
                    SpellId.SoulrenderTrancheAme,
                    SpellId.SoulrenderOuvrePlaie,
                    SpellId.SoulrenderRugissement,
                    SpellId.SoulrenderPeauDeFer,
                    SpellId.SoulrenderSeveVive,
                    SpellId.SoulrenderMarqueDeCarnage,
                };
                deckProp.arraySize = defaultDeck.Length;
                for (int i = 0; i < defaultDeck.Length; i++)
                {
                    SetEnumValue(deckProp.GetArrayElementAtIndex(i), defaultDeck[i]);
                }
            }

            // _signatureSpell : valeur par defaut Ame Laceree.
            var sigProp = so.FindProperty("_signatureSpell");
            if (sigProp != null)
            {
                SetEnumValue(sigProp, SpellId.SoulrenderAmeLaceree);
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            // 7. Auto-wire le CombatInputController s'il est present dans la scene.
            var inputCtrl = Object.FindObjectOfType<CombatInputController>();
            if (inputCtrl != null)
            {
                var inputSo = new SerializedObject(inputCtrl);
                SetObjectRef(inputSo, "_hudController", controller);
                inputSo.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[CreateCombatHUDTool] Auto-cable HUDController sur CombatInputController.");
            }
            else
            {
                Debug.LogWarning("[CreateCombatHUDTool] CombatInputController introuvable dans la scene. " +
                                 "Cable manuellement HUDController dans son Inspector.");
            }

            // 7.b — 2.13.b : auto-wire les previews de range.
            //   - TargetingPreviewView : ajouter _hudController pour piloter le preview armed.
            //   - MovementRangePreview : ajouter comme sibling component si absent, wire GridRenderer + _hudController.
            var targetingPreview = Object.FindObjectOfType<TargetingPreviewView>();
            if (targetingPreview != null)
            {
                var tpSo = new SerializedObject(targetingPreview);
                // Recupere le GridRenderer deja reference (partage avec MovementRangePreview).
                GridRenderer sharedGridRenderer = tpSo.FindProperty("_gridRenderer")?.objectReferenceValue as GridRenderer;
                SetObjectRef(tpSo, "_hudController", controller);
                tpSo.ApplyModifiedPropertiesWithoutUndo();

                var movementPreview = targetingPreview.GetComponent<MovementRangePreview>();
                if (movementPreview == null)
                {
                    movementPreview = Undo.AddComponent<MovementRangePreview>(targetingPreview.gameObject);
                    Debug.Log("[CreateCombatHUDTool] MovementRangePreview ajoute comme sibling de TargetingPreviewView.");
                }
                var mpSo = new SerializedObject(movementPreview);
                SetObjectRef(mpSo, "_hudController", controller);
                if (sharedGridRenderer != null) SetObjectRef(mpSo, "_gridRenderer", sharedGridRenderer);
                mpSo.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[CreateCombatHUDTool] Auto-cable HUDController + GridRenderer sur previews (Targeting + Movement).");
            }
            else
            {
                Debug.LogWarning("[CreateCombatHUDTool] TargetingPreviewView introuvable dans la scene. " +
                                 "La 2.13.b necessite l'objet du 2.6 ; sinon cable manuellement.");
            }

            // 7.c — 2.13.c : floating text canvas + manager + HP watcher.
            BuildFloatingTextStack(scene);

            // 7.d — 2.13.d : auto-add CameraController sur Camera.main.
            AttachCameraController();

            // 8. EventSystem (necessaire pour UI Unity).
            var eventSystem = Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem == null)
            {
                var esGo = new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(esGo, "Create EventSystem");
            }

            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Combat HUD");
            EditorSceneManager.MarkSceneDirty(scene);

            Selection.activeGameObject = hudGo;
            Debug.Log($"[CreateCombatHUDTool] HUD complet genere dans la scene '{scene.name}'. Sauve (Ctrl+S) puis Play.");
            EditorUtility.DisplayDialog(
                "Create Combat HUD",
                $"HUD '{CanvasName}' cree dans la scene '{scene.name}'.\n\n" +
                "Sauve la scene (Ctrl+S) puis lance Play.",
                "OK");
        }

        // ----------------------------------------------------------------------
        // BUILDERS
        // ----------------------------------------------------------------------

        private static ResourcePanelView BuildResourcePanel(RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            var go = NewUIGameObject(name, parent);
            SetAnchors(go.GetComponent<RectTransform>(), anchorMin, anchorMax, pivot, anchoredPos, size);

            // Fond semi-transparent.
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);
            bg.raycastTarget = false;

            // 2.13.e — Avatar 72x72 a gauche du panel. Branche par ResourcePanelView.Refresh
            // sur le SpellIconRegistry (AvatarFor classe). enabled = false par defaut, sera
            // active automatiquement au 1er refresh si l'asset est present dans le registry.
            var avatarGo = NewUIGameObject(name + "_Avatar", go.transform);
            SetAnchors(avatarGo.GetComponent<RectTransform>(),
                anchorMin: new Vector2(0f, 0.5f), anchorMax: new Vector2(0f, 0.5f),
                pivot: new Vector2(0f, 0.5f), anchoredPos: new Vector2(10f, 0f), size: new Vector2(72f, 72f));
            var avatar = avatarGo.AddComponent<Image>();
            avatar.preserveAspect = true;
            avatar.raycastTarget = false;
            avatar.enabled = false;

            // Label principal HP/PA/PM/ressource. Margin gauche elargie pour laisser de la place au portrait.
            var labelText = CreateText(go.transform, name + "_Label",
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(0.5f, 0.5f), anchoredPos: Vector2.zero, size: Vector2.zero,
                content: "", fontSize: 22f, align: TextAlignmentOptions.TopLeft, color: Color.white);
            labelText.margin = new Vector4(94f, 8f, 10f, 8f);
            labelText.enableWordWrapping = false;

            // Status line (en bas, plus petit). Pour 2.13.a c'est suffisant ; les bulles
            // d'icones de statuses arrivent en 2.13.c.
            var statusText = CreateText(go.transform, name + "_Statuses",
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 0f),
                pivot: new Vector2(0f, 0f), anchoredPos: new Vector2(94f, 6f), size: new Vector2(-104f, 32f),
                content: "", fontSize: 14f, align: TextAlignmentOptions.BottomLeft,
                color: new Color(0.78f, 0.78f, 0.78f, 1f));
            statusText.enableWordWrapping = true;

            var panel = go.AddComponent<ResourcePanelView>();
            var so = new SerializedObject(panel);
            SetObjectRef(so, "_label", labelText);
            SetObjectRef(so, "_statusLine", statusText);
            SetObjectRef(so, "_avatar", avatar);
            so.ApplyModifiedPropertiesWithoutUndo();
            return panel;
        }

        private static TimerView BuildTimerView(RectTransform parent)
        {
            var go = NewUIGameObject("Timer", parent);
            SetAnchors(go.GetComponent<RectTransform>(),
                anchorMin: new Vector2(0.5f, 1f), anchorMax: new Vector2(0.5f, 1f),
                pivot: new Vector2(0.5f, 1f), anchoredPos: new Vector2(0f, -20f), size: new Vector2(320f, 130f));

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);
            bg.raycastTarget = false;

            var turnText = CreateText(go.transform, "TurnLabel",
                anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(0.5f, 1f), anchoredPos: new Vector2(0f, -8f), size: new Vector2(0f, 28f),
                content: "Tour 0", fontSize: 20f, align: TextAlignmentOptions.Center, color: Color.white);

            var timerText = CreateText(go.transform, "TimerLabel",
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(0.5f, 0.5f), anchoredPos: new Vector2(0f, -6f), size: Vector2.zero,
                content: "15.0s", fontSize: 56f, align: TextAlignmentOptions.Center,
                color: new Color(1.00f, 0.95f, 0.55f, 1f));
            timerText.fontStyle = FontStyles.Bold;

            var timer = go.AddComponent<TimerView>();
            var so = new SerializedObject(timer);
            SetObjectRef(so, "_label", timerText);
            SetObjectRef(so, "_turnLabel", turnText);
            so.ApplyModifiedPropertiesWithoutUndo();
            return timer;
        }

        private static PassivePanelView BuildPassivePanel(RectTransform parent)
        {
            var go = NewUIGameObject("PassivePanel", parent);
            SetAnchors(go.GetComponent<RectTransform>(),
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(0f, 0f),
                pivot: new Vector2(0f, 0f), anchoredPos: new Vector2(40f, 40f), size: new Vector2(140f, 160f));

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);
            bg.raycastTarget = false;

            // Icone 96x96 en haut.
            var iconGo = NewUIGameObject("Icon", go.transform);
            SetAnchors(iconGo.GetComponent<RectTransform>(),
                anchorMin: new Vector2(0.5f, 1f), anchorMax: new Vector2(0.5f, 1f),
                pivot: new Vector2(0.5f, 1f), anchoredPos: new Vector2(0f, -10f), size: new Vector2(96f, 96f));
            var icon = iconGo.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            // Counter en dessous (gros chiffre).
            var counter = CreateText(go.transform, "Counter",
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 0f),
                pivot: new Vector2(0.5f, 0f), anchoredPos: new Vector2(0f, 24f), size: new Vector2(0f, 30f),
                content: "0/5", fontSize: 26f, align: TextAlignmentOptions.Center, color: Color.white);
            counter.fontStyle = FontStyles.Bold;

            // Label tag (HG / PR / FD / PT / RM).
            var label = CreateText(go.transform, "Tag",
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 0f),
                pivot: new Vector2(0.5f, 0f), anchoredPos: new Vector2(0f, 4f), size: new Vector2(0f, 18f),
                content: "HG", fontSize: 14f, align: TextAlignmentOptions.Center,
                color: new Color(0.78f, 0.78f, 0.78f, 1f));

            var passive = go.AddComponent<PassivePanelView>();
            var so = new SerializedObject(passive);
            SetObjectRef(so, "_icon", icon);
            SetObjectRef(so, "_counter", counter);
            SetObjectRef(so, "_label", label);
            so.ApplyModifiedPropertiesWithoutUndo();
            return passive;
        }

        private static TimelineView BuildTimelineView(RectTransform parent)
        {
            var go = NewUIGameObject("Timeline", parent);
            SetAnchors(go.GetComponent<RectTransform>(),
                anchorMin: new Vector2(1f, 0f), anchorMax: new Vector2(1f, 0f),
                pivot: new Vector2(1f, 0f), anchoredPos: new Vector2(-40f, 40f), size: new Vector2(320f, 100f));

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);
            bg.raycastTarget = false;

            var label = CreateText(go.transform, "TimelineLabel",
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(0.5f, 0.5f), anchoredPos: Vector2.zero, size: Vector2.zero,
                content: "  P0  |    P1", fontSize: 28f, align: TextAlignmentOptions.Center, color: Color.white);
            label.fontStyle = FontStyles.Bold;

            var timeline = go.AddComponent<TimelineView>();
            var so = new SerializedObject(timeline);
            SetObjectRef(so, "_label", label);
            so.ApplyModifiedPropertiesWithoutUndo();
            return timeline;
        }

        private static Button BuildEndTurnButton(RectTransform parent)
        {
            var go = NewUIGameObject("EndTurnButton", parent);
            SetAnchors(go.GetComponent<RectTransform>(),
                anchorMin: new Vector2(1f, 0.5f), anchorMax: new Vector2(1f, 0.5f),
                pivot: new Vector2(1f, 0.5f), anchoredPos: new Vector2(-40f, 0f), size: new Vector2(220f, 90f));

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.65f, 0.15f, 0.10f, 0.95f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.normalColor      = new Color(1f, 1f, 1f, 1f);
            colors.highlightedColor = new Color(1.0f, 0.85f, 0.85f, 1f);
            colors.pressedColor     = new Color(0.85f, 0.65f, 0.65f, 1f);
            colors.disabledColor    = new Color(0.40f, 0.40f, 0.40f, 0.8f);
            btn.colors = colors;

            CreateText(go.transform, "Label",
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(0.5f, 0.5f), anchoredPos: Vector2.zero, size: Vector2.zero,
                content: "End Turn >", fontSize: 28f, align: TextAlignmentOptions.Center, color: Color.white)
                .fontStyle = FontStyles.Bold;

            return btn;
        }

        private static void BuildSpellBar(RectTransform parent, out SpellSlotView[] slots, out SpellSlotView signature)
        {
            const int slotCount = 6;
            const float slotSize = 96f;
            const float spacing = 10f;
            const float sigGap = 28f;
            float deckWidth = slotCount * slotSize + (slotCount - 1) * spacing;
            float totalWidth = deckWidth + sigGap + slotSize;

            var bar = NewUIGameObject("SpellBar", parent);
            SetAnchors(bar.GetComponent<RectTransform>(),
                anchorMin: new Vector2(0.5f, 0f), anchorMax: new Vector2(0.5f, 0f),
                pivot: new Vector2(0.5f, 0f), anchoredPos: new Vector2(0f, 40f), size: new Vector2(totalWidth, slotSize + 8f));

            slots = new SpellSlotView[slotCount];
            float deckStartX = -totalWidth * 0.5f + slotSize * 0.5f;
            for (int i = 0; i < slotCount; i++)
            {
                float x = deckStartX + i * (slotSize + spacing);
                slots[i] = BuildSpellSlot(bar.transform, $"Slot{i + 1}",
                    anchoredPos: new Vector2(x, 0f), size: new Vector2(slotSize, slotSize));
            }

            // Centre du slot signature = bord droit du deck (= deckStartX + deckWidth) + sigGap + slotSize/2.
            // En utilisant deckStartX (centre slot 1 = bord gauche deck + slotSize/2), on aboutit a :
            //   sigX = deckStartX + deckWidth + sigGap
            // qui donne deja directement le centre du slot signature.
            float sigX = deckStartX + deckWidth + sigGap;
            signature = BuildSpellSlot(bar.transform, "SlotSignature",
                anchoredPos: new Vector2(sigX, 0f), size: new Vector2(slotSize, slotSize));
        }

        private static SpellSlotView BuildSpellSlot(Transform parent, string name, Vector2 anchoredPos, Vector2 size)
        {
            var go = NewUIGameObject(name, parent);
            SetAnchors(go.GetComponent<RectTransform>(),
                anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
                pivot: new Vector2(0.5f, 0.5f), anchoredPos: anchoredPos, size: size);

            // Frame Image (fond, ciblage du Button + colorisation par SetState).
            var frame = go.AddComponent<Image>();
            frame.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);

            // Button sur le root.
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = frame;

            // Icone enfant (sprite du sort).
            var iconGo = NewUIGameObject("Icon", go.transform);
            var iconRT = iconGo.GetComponent<RectTransform>();
            iconRT.anchorMin = Vector2.zero;
            iconRT.anchorMax = Vector2.one;
            iconRT.offsetMin = new Vector2(6f, 6f);
            iconRT.offsetMax = new Vector2(-6f, -6f);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            // KeyLabel (raccourci clavier, top-left).
            var keyLabel = CreateText(go.transform, "KeyLabel",
                anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(0f, 1f),
                pivot: new Vector2(0f, 1f), anchoredPos: new Vector2(4f, -2f), size: new Vector2(24f, 24f),
                content: "", fontSize: 16f, align: TextAlignmentOptions.TopLeft, color: Color.white);
            keyLabel.fontStyle = FontStyles.Bold;
            keyLabel.raycastTarget = false;

            // 2.13.c — CooldownLabel (centre, visible si en cooldown).
            var cooldownLabel = CreateText(go.transform, "CooldownLabel",
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(0.5f, 0.5f), anchoredPos: Vector2.zero, size: Vector2.zero,
                content: "", fontSize: 36f, align: TextAlignmentOptions.Center,
                color: new Color(1.00f, 0.25f, 0.25f, 1f));
            cooldownLabel.fontStyle = FontStyles.Bold;
            cooldownLabel.raycastTarget = false;
            cooldownLabel.gameObject.SetActive(false);

            var slot = go.AddComponent<SpellSlotView>();
            var so = new SerializedObject(slot);
            SetObjectRef(so, "_iconImage", iconImg);
            SetObjectRef(so, "_frameImage", frame);
            SetObjectRef(so, "_keyLabel", keyLabel);
            SetObjectRef(so, "_cooldownLabel", cooldownLabel);
            SetObjectRef(so, "_button", btn);
            so.ApplyModifiedPropertiesWithoutUndo();
            return slot;
        }

        private static SpellTooltipView BuildTooltip(RectTransform parent)
        {
            // Panel hidden par defaut, sortingOrder dans la hierarchy = dernier enfant (au-dessus).
            var go = NewUIGameObject("SpellTooltip", parent);
            SetAnchors(go.GetComponent<RectTransform>(),
                anchorMin: new Vector2(0.5f, 0f), anchorMax: new Vector2(0.5f, 0f),
                pivot: new Vector2(0.5f, 0f), anchoredPos: Vector2.zero, size: new Vector2(340f, 160f));

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.92f);
            bg.raycastTarget = false;

            // VerticalLayoutGroup auto-size pour adapter la hauteur au contenu.
            var vlg = go.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 14, 10, 10);
            vlg.spacing = 6f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fitter = go.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

            var title = CreateText(go.transform, "Title",
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 0f),
                pivot: new Vector2(0f, 0f), anchoredPos: Vector2.zero, size: new Vector2(0f, 28f),
                content: "Nom du sort", fontSize: 20f, align: TextAlignmentOptions.TopLeft, color: Color.white);
            title.fontStyle = FontStyles.Bold;

            var cost = CreateText(go.transform, "Cost",
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 0f),
                pivot: new Vector2(0f, 0f), anchoredPos: Vector2.zero, size: new Vector2(0f, 22f),
                content: "X PA | Portee Y", fontSize: 14f, align: TextAlignmentOptions.TopLeft,
                color: new Color(1.00f, 0.85f, 0.40f, 1f));

            var description = CreateText(go.transform, "Description",
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 0f),
                pivot: new Vector2(0f, 0f), anchoredPos: Vector2.zero, size: new Vector2(0f, 60f),
                content: "Description Bible.", fontSize: 14f, align: TextAlignmentOptions.TopLeft,
                color: new Color(0.88f, 0.88f, 0.88f, 1f));
            description.enableWordWrapping = true;

            var tooltip = go.AddComponent<SpellTooltipView>();
            var so = new SerializedObject(tooltip);
            SetObjectRef(so, "_panel", go.GetComponent<RectTransform>());
            SetObjectRef(so, "_titleText", title);
            SetObjectRef(so, "_costText", cost);
            SetObjectRef(so, "_descriptionText", description);
            so.ApplyModifiedPropertiesWithoutUndo();

            // Hidden par defaut. La visibilite est pilotee par Show()/Hide().
            go.SetActive(false);
            return tooltip;
        }

        /// <summary>
        /// 2.16.c.ii : overlay Victory/Defeat. Hierarchie :
        ///   MatchEndOverlay (GO root, toujours actif, script attache)
        ///   └── Panel (GO enfant, inactif par defaut, contient les visuels)
        ///       ├── Background (Image fullscreen alpha 0.85 noir)
        ///       ├── Title TMP (centre, 96px, "VICTOIRE"/"DEFAITE"/"MATCH NUL")
        ///       ├── Subtitle TMP (24px, "Round X")
        ///       └── RestartButton ("Rejouer")
        /// </summary>
        private static MatchEndOverlay BuildMatchEndOverlay(RectTransform parent)
        {
            // Root GO toujours actif (porte le script + listeners button).
            var rootGo = NewUIGameObject("MatchEndOverlay", parent);
            SetAnchors(rootGo.GetComponent<RectTransform>(),
                anchorMin: Vector2.zero, anchorMax: Vector2.one,
                pivot: new Vector2(0.5f, 0.5f), anchoredPos: Vector2.zero, size: Vector2.zero);

            // Panel enfant : visible quand le match se termine.
            var panelGo = NewUIGameObject("Panel", rootGo.transform);
            SetAnchors(panelGo.GetComponent<RectTransform>(),
                anchorMin: Vector2.zero, anchorMax: Vector2.one,
                pivot: new Vector2(0.5f, 0.5f), anchoredPos: Vector2.zero, size: Vector2.zero);

            // Background dim sur tout l'ecran (bloque les clicks via raycastTarget=true).
            var bg = panelGo.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.85f);
            bg.raycastTarget = true;

            // Title central.
            var title = CreateText(panelGo.transform, "Title",
                anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
                pivot: new Vector2(0.5f, 0.5f), anchoredPos: new Vector2(0f, 80f), size: new Vector2(800f, 120f),
                content: "VICTOIRE", fontSize: 96f, align: TextAlignmentOptions.Center, color: Color.white);
            title.fontStyle = FontStyles.Bold;

            // Subtitle juste en dessous du titre.
            var subtitle = CreateText(panelGo.transform, "Subtitle",
                anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
                pivot: new Vector2(0.5f, 0.5f), anchoredPos: new Vector2(0f, -10f), size: new Vector2(800f, 40f),
                content: "Round 0", fontSize: 28f, align: TextAlignmentOptions.Center,
                color: new Color(0.85f, 0.85f, 0.85f, 1f));

            // 2.16.c.iv — 2 boutons cote a cote : "Rejouer Easy" (vert) et
            // "Rejouer Medium" (orange). Espacement 20px entre les deux.
            Button easyBtn = BuildDifficultyButton(panelGo.transform, "RestartEasyButton",
                "Rejouer Easy", new Vector2(-130f, -120f),
                new Color(0.20f, 0.55f, 0.25f, 0.95f));  // vert
            Button mediumBtn = BuildDifficultyButton(panelGo.transform, "RestartMediumButton",
                "Rejouer Medium", new Vector2(130f, -120f),
                new Color(0.85f, 0.50f, 0.10f, 0.95f)); // orange

            // Composant + cablage.
            var overlay = rootGo.AddComponent<MatchEndOverlay>();
            var so = new SerializedObject(overlay);
            SetObjectRef(so, "_panel", panelGo);
            SetObjectRef(so, "_titleText", title);
            SetObjectRef(so, "_subtitleText", subtitle);
            SetObjectRef(so, "_restartEasyButton", easyBtn);
            SetObjectRef(so, "_restartMediumButton", mediumBtn);
            so.ApplyModifiedPropertiesWithoutUndo();

            // Panel hidden par defaut. MatchEndOverlay.Refresh() le toggle quand
            // CombatState.CurrentPhase == MatchEnd.
            panelGo.SetActive(false);

            return overlay;
        }

        /// <summary>
        /// 2.16.c.iv — helper pour bouton "Rejouer (difficulty)" colore. Utilise par
        /// BuildMatchEndOverlay pour les 2 boutons Easy/Medium cote a cote.
        /// </summary>
        private static Button BuildDifficultyButton(Transform parent, string name, string label,
            Vector2 anchoredPos, Color bgColor)
        {
            var btnGo = NewUIGameObject(name, parent);
            SetAnchors(btnGo.GetComponent<RectTransform>(),
                anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
                pivot: new Vector2(0.5f, 0.5f), anchoredPos: anchoredPos, size: new Vector2(240f, 70f));

            var btnBg = btnGo.AddComponent<Image>();
            btnBg.color = bgColor;

            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnBg;
            var colors = btn.colors;
            colors.normalColor      = new Color(1f, 1f, 1f, 1f);
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor     = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.disabledColor    = new Color(0.40f, 0.40f, 0.40f, 0.8f);
            btn.colors = colors;

            CreateText(btnGo.transform, "Label",
                anchorMin: Vector2.zero, anchorMax: Vector2.one,
                pivot: new Vector2(0.5f, 0.5f), anchoredPos: Vector2.zero, size: Vector2.zero,
                content: label, fontSize: 28f, align: TextAlignmentOptions.Center, color: Color.white)
                .fontStyle = FontStyles.Bold;

            return btn;
        }

        /// <summary>
        /// 2.13.c : cree un Canvas dedie pour les floating texts (sortingOrder &lt; HUD)
        /// + FloatingTextManager + CombatantHPWatcher sur le meme GameObject.
        /// Recupere GridSettings depuis le TargetingPreviewView de la scene.
        /// </summary>
        private static void BuildFloatingTextStack(UnityEngine.SceneManagement.Scene scene)
        {
            const string CanvasName = "CombatFloatingTextCanvas";

            // Supprime l'ancien si present (idempotent).
            var existing = GameObject.Find(CanvasName);
            if (existing != null) Object.DestroyImmediate(existing);

            var canvasGo = new GameObject(CanvasName,
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90; // sous le HUD (100), au-dessus du reste

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // GraphicRaycaster : on le neutralise pour ne pas capturer les clics.
            var raycaster = canvasGo.GetComponent<GraphicRaycaster>();
            raycaster.enabled = false;

            var manager = canvasGo.AddComponent<FloatingTextManager>();
            var managerSo = new SerializedObject(manager);
            SetObjectRef(managerSo, "_canvas", canvas);
            SetObjectRef(managerSo, "_worldCamera", Camera.main);
            managerSo.ApplyModifiedPropertiesWithoutUndo();

            var watcher = canvasGo.AddComponent<CombatantHPWatcher>();
            var watcherSo = new SerializedObject(watcher);
            SetObjectRef(watcherSo, "_manager", manager);

            // Recupere GridSettings depuis le TargetingPreviewView (deja cable en scene).
            var targetingPreview = Object.FindObjectOfType<TargetingPreviewView>();
            if (targetingPreview != null)
            {
                var tpSo = new SerializedObject(targetingPreview);
                var gridSettings = tpSo.FindProperty("_gridSettings")?.objectReferenceValue;
                if (gridSettings != null) SetObjectRef(watcherSo, "_gridSettings", gridSettings);
            }
            watcherSo.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Floating Text Stack");
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[CreateCombatHUDTool] FloatingTextCanvas + Manager + HPWatcher crees.");
        }

        /// <summary>
        /// 2.13.d : ajoute CameraController sur Camera.main si pas deja present. Idempotent.
        /// </summary>
        private static void AttachCameraController()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                // Fallback : cherche n'importe quelle Camera dans la scene.
                camera = Object.FindObjectOfType<Camera>();
            }
            if (camera == null)
            {
                Debug.LogWarning("[CreateCombatHUDTool] Aucune Camera trouvee dans la scene. Skip CameraController.");
                return;
            }

            var existing = camera.GetComponent<CameraController>();
            if (existing != null)
            {
                Debug.Log("[CreateCombatHUDTool] CameraController deja present sur la camera, skip.");
                return;
            }

            var controller = Undo.AddComponent<CameraController>(camera.gameObject);
            // Cable la reference Camera vers celle qu'on a trouvee (par defaut le component
            // cherche dans Awake mais on prefere le set explicitement).
            var so = new SerializedObject(controller);
            SetObjectRef(so, "_camera", camera);
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[CreateCombatHUDTool] CameraController ajoute sur '{camera.name}'.");
        }

        // ----------------------------------------------------------------------
        // PRIMITIVES
        // ----------------------------------------------------------------------

        private static GameObject NewUIGameObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void SetAnchors(RectTransform rt,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size,
            string content, float fontSize, TextAlignmentOptions align, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            SetAnchors(go.GetComponent<RectTransform>(), anchorMin, anchorMax, pivot, anchoredPos, size);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = align;
            text.color = color;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void SetObjectRef(SerializedObject so, string propertyName, Object value)
        {
            var prop = so.FindProperty(propertyName);
            if (prop != null) prop.objectReferenceValue = value;
        }

        /// <summary>
        /// Set un SerializedProperty enum par NAME (resilient aux enums a valeurs non-sequentielles
        /// comme SpellId : Byte). enumValueIndex est l'INDEX de declaration, pas la valeur underlying.
        /// </summary>
        private static void SetEnumValue(SerializedProperty prop, System.Enum value)
        {
            if (prop == null) return;
            int idx = System.Array.IndexOf(prop.enumNames, value.ToString());
            if (idx >= 0) prop.enumValueIndex = idx;
        }
    }
}
