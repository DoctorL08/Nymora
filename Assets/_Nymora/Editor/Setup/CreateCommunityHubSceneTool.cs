using System.Collections.Generic;
using System.IO;
using Fusion;
using Nymora.Hub;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Genere la scene Assets/_Nymora/Scenes/10_CommunityHub.unity avec :
    ///   - Main Camera + HubCamera (target fallback HubAvatar.Local)
    ///   - GameObject "HubBootstrap" : NetworkRunner Fusion Shared + ref HubAvatar.prefab (4.2/4.3.c)
    ///   - GameObject "HubGrid" : 20x20 iso (4.3.a)
    ///   - GameObject "HubInputController" : clic A* + clic avatar remote (4.3.b/c + 4.8.a)
    ///   - GameObject "HubChatClient" : WebSocket client backend (4.6)
    ///   - Canvas + EventSystem + ChatPanel : UI chat in-game (4.6) + tabs Global/Prive (4.7)
    ///   - ChallengePopup : modale defi outgoing (4.8.a, cache au start)
    ///   - IncomingChallengePopup : modale defi incoming Accepter/Refuser (4.8.c, cache au start)
    ///
    /// Prerequis : Nymora > Setup > Create Hub Avatar Prefab DOIT etre lance avant.
    ///
    /// Menu : Nymora > Setup > Create Community Hub Scene
    /// </summary>
    public static class CreateCommunityHubSceneTool
    {
        private const string ScenesFolder = "Assets/_Nymora/Scenes";
        private const string ScenePath = ScenesFolder + "/10_CommunityHub.unity";
        private const string TileSpritePath = "Assets/_Nymora/Art/Sprites/TilePlaceholder.png";
        private const string AvatarPrefabPath = "Assets/_Nymora/Prefabs/Hub/HubAvatar.prefab";
        // 4.X — Assets fournis par le designer (mai 2026)
        private const string HubMapImagePath = "Assets/_Nymora/Art/Hub/map_hub_sans_fond.png";
        private const string HubBackgroundVideoPath = "Assets/_Nymora/Art/Hub/Fond_anim_hub.mp4";
        private const string HubGridBanListPath = "Assets/_Nymora/ScriptableObjects/Hub/HubGridBanList.asset";

        [MenuItem("Nymora/Setup/Create Community Hub Scene", priority = 33)]
        private static void CreateScene()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Community Hub Scene",
                    "Impossible de régénérer la scène pendant Play Mode.\n\n" +
                    "Stoppe Play (Ctrl+P) puis relance ce menu.",
                    "OK");
                return;
            }
            EnsureFolder(ScenesFolder);

            var tileSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TileSpritePath);
            if (tileSprite == null)
            {
                EditorUtility.DisplayDialog("Community Hub Scene",
                    $"Tile sprite introuvable : {TileSpritePath}",
                    "OK");
                return;
            }

            var avatarPrefabGo = AssetDatabase.LoadAssetAtPath<GameObject>(AvatarPrefabPath);
            if (avatarPrefabGo == null)
            {
                EditorUtility.DisplayDialog("Community Hub Scene",
                    $"Avatar prefab introuvable : {AvatarPrefabPath}\n\n" +
                    "Lance d'abord :\nNymora > Setup > Create Hub Avatar Prefab",
                    "OK");
                return;
            }
            var avatarNetworkObject = avatarPrefabGo.GetComponent<NetworkObject>();
            if (avatarNetworkObject == null)
            {
                EditorUtility.DisplayDialog("Community Hub Scene",
                    $"Le prefab {AvatarPrefabPath} n'a pas de NetworkObject. Regenere via Create Hub Avatar Prefab.",
                    "OK");
                return;
            }

            if (File.Exists(ScenePath))
            {
                if (!EditorUtility.DisplayDialog("Community Hub Scene",
                    "10_CommunityHub.unity existe deja. La regenerer (ecrasement) ?",
                    "Regenerer", "Annuler"))
                {
                    return;
                }
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Main Camera + HubCamera
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(HubCamera));
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            SceneManager.MoveGameObjectToScene(camGo, scene);

            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.15f, 0.18f, 1f);

            // HubBootstrap (Brique 4.2 + 4.3.c)
            var bootGo = new GameObject("HubBootstrap", typeof(HubBootstrap));
            SceneManager.MoveGameObjectToScene(bootGo, scene);
            var bootstrap = bootGo.GetComponent<HubBootstrap>();
            var bootSo = new SerializedObject(bootstrap);
            bootSo.FindProperty("_avatarPrefab").objectReferenceValue = avatarNetworkObject;
            bootSo.ApplyModifiedPropertiesWithoutUndo();

            // 4.X — BackgroundVideo : Quad mesh + RenderTexture + manual frame ping-pong (URP 2D compatible)
            var hubBgVideoClip = AssetDatabase.LoadAssetAtPath<VideoClip>(HubBackgroundVideoPath);
            if (hubBgVideoClip != null)
            {
                var videoGo = new GameObject("BackgroundVideo", typeof(MeshFilter), typeof(MeshRenderer), typeof(VideoPlayer), typeof(HubBackgroundVideo));
                SceneManager.MoveGameObjectToScene(videoGo, scene);
                videoGo.transform.position = Vector3.zero;
                videoGo.transform.localScale = new Vector3(40f, 25f, 1f);
                var mf = videoGo.GetComponent<MeshFilter>();
                mf.sharedMesh = BuildBackgroundQuadMesh();
                var mr = videoGo.GetComponent<MeshRenderer>();
                // Sprites/Default est le plus fiable en URP 2D : supporte sortingLayer/Order
                // et utilise _MainTex (compatible avec Material.mainTexture).
                var bestShader = Shader.Find("Sprites/Default")
                                 ?? Shader.Find("Universal Render Pipeline/Unlit")
                                 ?? Shader.Find("Unlit/Texture");
                mr.sharedMaterial = new Material(bestShader);
                mr.sortingLayerName = "Default";
                mr.sortingOrder = -200; // bien derrière la map (-100) et la grille (0+)
                var vp = videoGo.GetComponent<VideoPlayer>();
                vp.source = VideoSource.VideoClip;
                vp.clip = hubBgVideoClip;
                vp.audioOutputMode = VideoAudioOutputMode.None;
                vp.playOnAwake = false;
                vp.isLooping = false;
                var hubBgVid = videoGo.GetComponent<HubBackgroundVideo>();
                var bgVidSo = new SerializedObject(hubBgVid);
                bgVidSo.FindProperty("_videoPlayer").objectReferenceValue = vp;
                bgVidSo.FindProperty("_meshRenderer").objectReferenceValue = mr;
                bgVidSo.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning($"[Nymora.Setup] VideoClip fond hub introuvable : {HubBackgroundVideoPath} — la vidéo de fond ne sera pas lue.");
            }

            // HubGrid (Brique 4.3.a + 4.X bans)
            var gridGo = new GameObject("HubGrid", typeof(HubGridRenderer));
            SceneManager.MoveGameObjectToScene(gridGo, scene);
            var gridRenderer = gridGo.GetComponent<HubGridRenderer>();
            var banListAsset = AssetDatabase.LoadAssetAtPath<HubGridBanList>(HubGridBanListPath);
            var gridSo = new SerializedObject(gridRenderer);
            gridSo.FindProperty("_tileSprite").objectReferenceValue = tileSprite;
            if (banListAsset != null) gridSo.FindProperty("_banList").objectReferenceValue = banListAsset;
            gridSo.ApplyModifiedPropertiesWithoutUndo();

            // BackgroundImage (4.X) — créé APRES HubGrid pour pouvoir wire la ref directe
            var hubMapSprite = AssetDatabase.LoadAssetAtPath<Sprite>(HubMapImagePath);
            if (hubMapSprite != null)
            {
                var imgGo = new GameObject("BackgroundImage", typeof(SpriteRenderer), typeof(HubBackgroundImage));
                SceneManager.MoveGameObjectToScene(imgGo, scene);
                var sr = imgGo.GetComponent<SpriteRenderer>();
                sr.sprite = hubMapSprite;
                sr.sortingOrder = -100; // derrière la grille (0) et les avatars (100+)
                imgGo.transform.position = Vector3.zero;
                var bgImg = imgGo.GetComponent<HubBackgroundImage>();
                var bgImgSo = new SerializedObject(bgImg);
                bgImgSo.FindProperty("_grid").objectReferenceValue = gridRenderer;
                bgImgSo.FindProperty("_spriteRenderer").objectReferenceValue = sr;
                bgImgSo.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning($"[Nymora.Setup] Sprite map hub introuvable : {HubMapImagePath} — l'image de fond ne sera pas affichée.");
            }

            // HubInputController (Brique 4.3.b/c + 4.X ban list)
            var inputGo = new GameObject("HubInputController", typeof(HubInputController));
            SceneManager.MoveGameObjectToScene(inputGo, scene);
            var input = inputGo.GetComponent<HubInputController>();
            var inputSo = new SerializedObject(input);
            inputSo.FindProperty("_camera").objectReferenceValue = cam;
            inputSo.FindProperty("_grid").objectReferenceValue = gridRenderer;
            if (banListAsset != null) inputSo.FindProperty("_banList").objectReferenceValue = banListAsset;
            inputSo.ApplyModifiedPropertiesWithoutUndo();

            // 4.X — HubBanEditMode : touche B en Play -> mode clic-to-ban direct sur tiles
            var banEditGo = new GameObject("HubBanEditMode", typeof(HubBanEditMode));
            SceneManager.MoveGameObjectToScene(banEditGo, scene);
            var banEdit = banEditGo.GetComponent<HubBanEditMode>();
            var banEditSo = new SerializedObject(banEdit);
            banEditSo.FindProperty("_camera").objectReferenceValue = cam;
            banEditSo.FindProperty("_grid").objectReferenceValue = gridRenderer;
            banEditSo.FindProperty("_input").objectReferenceValue = input;
            if (banListAsset != null) banEditSo.FindProperty("_banList").objectReferenceValue = banListAsset;
            banEditSo.ApplyModifiedPropertiesWithoutUndo();

            // HubChatClient (Brique 4.6)
            var chatClientGo = new GameObject("HubChatClient", typeof(HubChatClient));
            SceneManager.MoveGameObjectToScene(chatClientGo, scene);
            var chatClient = chatClientGo.GetComponent<HubChatClient>();

            // HubMatchTransition (Brique 4.8.d.ii) — listen MATCH_READY -> transition scene combat
            var transitionGo = new GameObject("HubMatchTransition", typeof(HubMatchTransition));
            SceneManager.MoveGameObjectToScene(transitionGo, scene);

            // EventSystem + Canvas (4.6)
            var eventSystemGo = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(eventSystemGo, scene);

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasGo, scene);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // ChatPanel
            var panelGo = CreateUIChild(canvasGo, "ChatPanel", typeof(Image), typeof(HubChatUI));
            var panelImg = panelGo.GetComponent<Image>();
            panelImg.color = new Color(0.08f, 0.08f, 0.1f, 0.85f);
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(1f, 0f);
            panelRt.anchorMax = new Vector2(1f, 0f);
            panelRt.pivot = new Vector2(1f, 0f);
            panelRt.anchoredPosition = new Vector2(-20f, 20f);
            panelRt.sizeDelta = new Vector2(560f, 360f);

            // Tabs row (4.7) — Global / Prive en haut du panel
            var tabGlobalGo = CreateUIChild(panelGo, "TabGlobalButton", typeof(Image), typeof(Button));
            var tabGlobalImg = tabGlobalGo.GetComponent<Image>();
            tabGlobalImg.color = new Color(0.25f, 0.4f, 0.65f, 1f);
            var tabGlobalRt = tabGlobalGo.GetComponent<RectTransform>();
            tabGlobalRt.anchorMin = new Vector2(0f, 1f);
            tabGlobalRt.anchorMax = new Vector2(0f, 1f);
            tabGlobalRt.pivot = new Vector2(0f, 1f);
            tabGlobalRt.anchoredPosition = new Vector2(10f, -10f);
            tabGlobalRt.sizeDelta = new Vector2(120f, 40f);

            var tabGlobalLabelGo = CreateUIChild(tabGlobalGo, "Label", typeof(TextMeshProUGUI));
            var tabGlobalLabel = tabGlobalLabelGo.GetComponent<TextMeshProUGUI>();
            tabGlobalLabel.text = "Global";
            tabGlobalLabel.fontSize = 20;
            tabGlobalLabel.color = Color.white;
            tabGlobalLabel.alignment = TextAlignmentOptions.Center;
            StretchFull(tabGlobalLabelGo.GetComponent<RectTransform>());

            var tabPrivateGo = CreateUIChild(panelGo, "TabPrivateButton", typeof(Image), typeof(Button));
            var tabPrivateImg = tabPrivateGo.GetComponent<Image>();
            tabPrivateImg.color = new Color(0.2f, 0.2f, 0.24f, 1f);
            var tabPrivateRt = tabPrivateGo.GetComponent<RectTransform>();
            tabPrivateRt.anchorMin = new Vector2(0f, 1f);
            tabPrivateRt.anchorMax = new Vector2(0f, 1f);
            tabPrivateRt.pivot = new Vector2(0f, 1f);
            tabPrivateRt.anchoredPosition = new Vector2(140f, -10f);
            tabPrivateRt.sizeDelta = new Vector2(120f, 40f);

            var tabPrivateLabelGo = CreateUIChild(tabPrivateGo, "Label", typeof(TextMeshProUGUI));
            var tabPrivateLabel = tabPrivateLabelGo.GetComponent<TextMeshProUGUI>();
            tabPrivateLabel.text = "Privé";
            tabPrivateLabel.fontSize = 20;
            tabPrivateLabel.color = Color.white;
            tabPrivateLabel.alignment = TextAlignmentOptions.Center;
            StretchFull(tabPrivateLabelGo.GetComponent<RectTransform>());

            // Viewport + Mask + Content (history scroll)
            var scrollGo = CreateUIChild(panelGo, "HistoryScroll", typeof(ScrollRect));
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.offsetMin = new Vector2(10f, 70f);
            scrollRt.offsetMax = new Vector2(-10f, -60f);

            var viewportGo = CreateUIChild(scrollGo, "Viewport", typeof(Image), typeof(Mask));
            var viewportImg = viewportGo.GetComponent<Image>();
            viewportImg.color = new Color(0f, 0f, 0f, 0.2f);
            var viewportMask = viewportGo.GetComponent<Mask>();
            viewportMask.showMaskGraphic = true;
            StretchFull(viewportGo.GetComponent<RectTransform>());

            var contentGo = CreateUIChild(viewportGo, "Content", typeof(TextMeshProUGUI), typeof(ContentSizeFitter));
            var contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 0f);
            var historyText = contentGo.GetComponent<TextMeshProUGUI>();
            historyText.fontSize = 22;
            historyText.color = new Color(0.92f, 0.92f, 0.95f);
            historyText.alignment = TextAlignmentOptions.TopLeft;
            historyText.richText = true;
            historyText.enableWordWrapping = true;
            historyText.text = "";
            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var scrollRect = scrollGo.GetComponent<ScrollRect>();
            scrollRect.viewport = viewportGo.GetComponent<RectTransform>();
            scrollRect.content = contentRt;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            // Input field (bottom of panel)
            var inputFieldGo = CreateUIChild(panelGo, "InputField", typeof(Image), typeof(TMP_InputField));
            var inputFieldImg = inputFieldGo.GetComponent<Image>();
            inputFieldImg.color = new Color(0.2f, 0.2f, 0.24f, 1f);
            var inputFieldRt = inputFieldGo.GetComponent<RectTransform>();
            inputFieldRt.anchorMin = new Vector2(0f, 0f);
            inputFieldRt.anchorMax = new Vector2(1f, 0f);
            inputFieldRt.pivot = new Vector2(0f, 0f);
            inputFieldRt.anchoredPosition = new Vector2(10f, 10f);
            inputFieldRt.sizeDelta = new Vector2(-130f, 50f);

            var textArea = CreateUIChild(inputFieldGo, "Text Area", typeof(RectMask2D));
            var taRt = textArea.GetComponent<RectTransform>();
            taRt.anchorMin = Vector2.zero;
            taRt.anchorMax = Vector2.one;
            taRt.offsetMin = new Vector2(10f, 5f);
            taRt.offsetMax = new Vector2(-10f, -5f);

            var placeholderGo = CreateUIChild(textArea, "Placeholder", typeof(TextMeshProUGUI));
            var placeholderText = placeholderGo.GetComponent<TextMeshProUGUI>();
            placeholderText.text = "Tape un message... ou /w <user> <msg>";
            placeholderText.fontSize = 22;
            placeholderText.color = new Color(0.6f, 0.6f, 0.65f, 0.7f);
            placeholderText.alignment = TextAlignmentOptions.Left;
            StretchFull(placeholderGo.GetComponent<RectTransform>());

            var inputTextGo = CreateUIChild(textArea, "Text", typeof(TextMeshProUGUI));
            var inputTextTmp = inputTextGo.GetComponent<TextMeshProUGUI>();
            inputTextTmp.fontSize = 22;
            inputTextTmp.color = new Color(0.95f, 0.95f, 0.98f);
            inputTextTmp.alignment = TextAlignmentOptions.Left;
            StretchFull(inputTextGo.GetComponent<RectTransform>());

            var inputField = inputFieldGo.GetComponent<TMP_InputField>();
            inputField.textViewport = textArea.GetComponent<RectTransform>();
            inputField.textComponent = inputTextTmp;
            inputField.placeholder = placeholderText;
            inputField.lineType = TMP_InputField.LineType.SingleLine;

            // Send button (right of input)
            var sendBtnGo = CreateUIChild(panelGo, "SendButton", typeof(Image), typeof(Button));
            var sendImg = sendBtnGo.GetComponent<Image>();
            sendImg.color = new Color(0.25f, 0.4f, 0.65f, 1f);
            var sendBtnRt = sendBtnGo.GetComponent<RectTransform>();
            sendBtnRt.anchorMin = new Vector2(1f, 0f);
            sendBtnRt.anchorMax = new Vector2(1f, 0f);
            sendBtnRt.pivot = new Vector2(1f, 0f);
            sendBtnRt.anchoredPosition = new Vector2(-10f, 10f);
            sendBtnRt.sizeDelta = new Vector2(110f, 50f);

            var sendLabelGo = CreateUIChild(sendBtnGo, "Label", typeof(TextMeshProUGUI));
            var sendLabel = sendLabelGo.GetComponent<TextMeshProUGUI>();
            sendLabel.text = "Envoyer";
            sendLabel.fontSize = 22;
            sendLabel.color = Color.white;
            sendLabel.alignment = TextAlignmentOptions.Center;
            StretchFull(sendLabelGo.GetComponent<RectTransform>());

            // Wire HubChatUI refs
            var chatUI = panelGo.GetComponent<HubChatUI>();
            var chatUiSo = new SerializedObject(chatUI);
            chatUiSo.FindProperty("_inputField").objectReferenceValue = inputField;
            chatUiSo.FindProperty("_sendButton").objectReferenceValue = sendBtnGo.GetComponent<Button>();
            chatUiSo.FindProperty("_scrollRect").objectReferenceValue = scrollRect;
            chatUiSo.FindProperty("_historyText").objectReferenceValue = historyText;
            chatUiSo.FindProperty("_tabGlobalButton").objectReferenceValue = tabGlobalGo.GetComponent<Button>();
            chatUiSo.FindProperty("_tabPrivateButton").objectReferenceValue = tabPrivateGo.GetComponent<Button>();
            chatUiSo.ApplyModifiedPropertiesWithoutUndo();

            // ChallengePopup data-driven (4.8.a + 4.10.UX + refacto 4.10.refacto)
            // Layout : header (label + swatch) + container VerticalLayoutGroup pour boutons runtime
            var challengeRootGo = CreateUIChild(canvasGo, "ChallengePopupRoot", typeof(ChallengePopup));
            var challengeRootRt = challengeRootGo.GetComponent<RectTransform>();
            StretchFull(challengeRootRt);

            var challengePanelGo = CreateUIChild(challengeRootGo, "Panel", typeof(Image));
            var challengePanelImg = challengePanelGo.GetComponent<Image>();
            challengePanelImg.color = new Color(0.08f, 0.08f, 0.1f, 0.95f);
            var challengePanelRt = challengePanelGo.GetComponent<RectTransform>();
            challengePanelRt.anchorMin = new Vector2(0.5f, 0.5f);
            challengePanelRt.anchorMax = new Vector2(0.5f, 0.5f);
            challengePanelRt.pivot = new Vector2(0.5f, 0.5f);
            challengePanelRt.anchoredPosition = Vector2.zero;
            challengePanelRt.sizeDelta = new Vector2(480f, 480f);

            var labelGo = CreateUIChild(challengePanelGo, "Label", typeof(TextMeshProUGUI));
            var labelText = labelGo.GetComponent<TextMeshProUGUI>();
            labelText.text = "Actions";
            labelText.fontSize = 30;
            labelText.color = Color.white;
            labelText.alignment = TextAlignmentOptions.Center;
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0f, 1f);
            labelRt.anchorMax = new Vector2(1f, 1f);
            labelRt.pivot = new Vector2(0.5f, 1f);
            labelRt.anchoredPosition = new Vector2(0f, -20f);
            labelRt.sizeDelta = new Vector2(-40f, 50f);

            var swatchGo = CreateUIChild(challengePanelGo, "ColorSwatch", typeof(Image));
            var swatchImg = swatchGo.GetComponent<Image>();
            swatchImg.color = Color.white;
            var swatchRt = swatchGo.GetComponent<RectTransform>();
            swatchRt.anchorMin = new Vector2(0.5f, 1f);
            swatchRt.anchorMax = new Vector2(0.5f, 1f);
            swatchRt.pivot = new Vector2(0.5f, 1f);
            swatchRt.anchoredPosition = new Vector2(0f, -80f);
            swatchRt.sizeDelta = new Vector2(50f, 50f);

            // Container VerticalLayoutGroup pour les boutons runtime
            var containerGo = CreateUIChild(challengePanelGo, "ButtonContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
            var containerRt = containerGo.GetComponent<RectTransform>();
            containerRt.anchorMin = new Vector2(0f, 0f);
            containerRt.anchorMax = new Vector2(1f, 0f);
            containerRt.pivot = new Vector2(0.5f, 0f);
            containerRt.anchoredPosition = new Vector2(0f, 20f);
            containerRt.sizeDelta = new Vector2(-80f, 320f);
            var vlg = containerGo.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f;
            vlg.childAlignment = TextAnchor.LowerCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            var challengePopup = challengeRootGo.GetComponent<ChallengePopup>();
            var challengeSo = new SerializedObject(challengePopup);
            challengeSo.FindProperty("_panel").objectReferenceValue = challengePanelGo;
            challengeSo.FindProperty("_label").objectReferenceValue = labelText;
            challengeSo.FindProperty("_targetColorSwatch").objectReferenceValue = swatchImg;
            challengeSo.FindProperty("_buttonContainer").objectReferenceValue = containerRt;
            challengeSo.FindProperty("_chatUI").objectReferenceValue = chatUI;
            challengeSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire HubInputController._challengePopup (4.8.a)
            var inputSo2 = new SerializedObject(input);
            inputSo2.FindProperty("_challengePopup").objectReferenceValue = challengePopup;
            inputSo2.ApplyModifiedPropertiesWithoutUndo();

            // HubMatchResultDisplay (4.9 stub) — affiche result du dernier match au retour hub
            var resultDisplayGo = new GameObject("HubMatchResultDisplay", typeof(HubMatchResultDisplay));
            SceneManager.MoveGameObjectToScene(resultDisplayGo, scene);
            var resultDisplay = resultDisplayGo.GetComponent<HubMatchResultDisplay>();
            var resultSo = new SerializedObject(resultDisplay);
            resultSo.FindProperty("_chatUI").objectReferenceValue = chatUI;
            resultSo.ApplyModifiedPropertiesWithoutUndo();

            // IncomingChallengePopup (4.8.c) — auto-show à la réception INCOMING_CHALLENGE
            var incomingRootGo = CreateUIChild(canvasGo, "IncomingChallengePopupRoot", typeof(IncomingChallengePopup));
            StretchFull(incomingRootGo.GetComponent<RectTransform>());

            var incomingPanelGo = CreateUIChild(incomingRootGo, "Panel", typeof(Image));
            var incomingPanelImg = incomingPanelGo.GetComponent<Image>();
            incomingPanelImg.color = new Color(0.08f, 0.08f, 0.1f, 0.95f);
            var incomingPanelRt = incomingPanelGo.GetComponent<RectTransform>();
            incomingPanelRt.anchorMin = new Vector2(0.5f, 0.5f);
            incomingPanelRt.anchorMax = new Vector2(0.5f, 0.5f);
            incomingPanelRt.pivot = new Vector2(0.5f, 0.5f);
            incomingPanelRt.anchoredPosition = Vector2.zero;
            incomingPanelRt.sizeDelta = new Vector2(540f, 260f);

            var incomingLabelGo = CreateUIChild(incomingPanelGo, "Label", typeof(TextMeshProUGUI));
            var incomingLabelText = incomingLabelGo.GetComponent<TextMeshProUGUI>();
            incomingLabelText.text = "X\nvous défie !";
            incomingLabelText.fontSize = 28;
            incomingLabelText.color = Color.white;
            incomingLabelText.alignment = TextAlignmentOptions.Center;
            var incomingLabelRt = incomingLabelGo.GetComponent<RectTransform>();
            incomingLabelRt.anchorMin = new Vector2(0f, 1f);
            incomingLabelRt.anchorMax = new Vector2(1f, 1f);
            incomingLabelRt.pivot = new Vector2(0.5f, 1f);
            incomingLabelRt.anchoredPosition = new Vector2(0f, -20f);
            incomingLabelRt.sizeDelta = new Vector2(-40f, 130f);

            var acceptBtnGo = CreateUIChild(incomingPanelGo, "AcceptButton", typeof(Image), typeof(Button));
            var acceptImg = acceptBtnGo.GetComponent<Image>();
            acceptImg.color = new Color(0.25f, 0.55f, 0.35f, 1f);
            var acceptRt = acceptBtnGo.GetComponent<RectTransform>();
            acceptRt.anchorMin = new Vector2(0f, 0f);
            acceptRt.anchorMax = new Vector2(0f, 0f);
            acceptRt.pivot = new Vector2(0f, 0f);
            acceptRt.anchoredPosition = new Vector2(40f, 30f);
            acceptRt.sizeDelta = new Vector2(200f, 60f);

            var acceptLabelGo = CreateUIChild(acceptBtnGo, "Label", typeof(TextMeshProUGUI));
            var acceptLabel = acceptLabelGo.GetComponent<TextMeshProUGUI>();
            acceptLabel.text = "Accepter";
            acceptLabel.fontSize = 24;
            acceptLabel.color = Color.white;
            acceptLabel.alignment = TextAlignmentOptions.Center;
            StretchFull(acceptLabelGo.GetComponent<RectTransform>());

            var refuseBtnGo = CreateUIChild(incomingPanelGo, "RefuseButton", typeof(Image), typeof(Button));
            var refuseImg = refuseBtnGo.GetComponent<Image>();
            refuseImg.color = new Color(0.55f, 0.25f, 0.25f, 1f);
            var refuseRt = refuseBtnGo.GetComponent<RectTransform>();
            refuseRt.anchorMin = new Vector2(1f, 0f);
            refuseRt.anchorMax = new Vector2(1f, 0f);
            refuseRt.pivot = new Vector2(1f, 0f);
            refuseRt.anchoredPosition = new Vector2(-40f, 30f);
            refuseRt.sizeDelta = new Vector2(200f, 60f);

            var refuseLabelGo = CreateUIChild(refuseBtnGo, "Label", typeof(TextMeshProUGUI));
            var refuseLabel = refuseLabelGo.GetComponent<TextMeshProUGUI>();
            refuseLabel.text = "Refuser";
            refuseLabel.fontSize = 24;
            refuseLabel.color = Color.white;
            refuseLabel.alignment = TextAlignmentOptions.Center;
            StretchFull(refuseLabelGo.GetComponent<RectTransform>());

            var incomingPopup = incomingRootGo.GetComponent<IncomingChallengePopup>();
            var incomingSo = new SerializedObject(incomingPopup);
            incomingSo.FindProperty("_panel").objectReferenceValue = incomingPanelGo;
            incomingSo.FindProperty("_label").objectReferenceValue = incomingLabelText;
            incomingSo.FindProperty("_acceptButton").objectReferenceValue = acceptBtnGo.GetComponent<Button>();
            incomingSo.FindProperty("_refuseButton").objectReferenceValue = refuseBtnGo.GetComponent<Button>();
            incomingSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);

            Debug.Log($"[Nymora.Setup] Scene generee : {ScenePath} (+ ajoutee aux BuildSettings)");

            EditorUtility.DisplayDialog("Community Hub Scene",
                $"Scene generee : {ScenePath}\n\n" +
                "ETAPES SUIVANTES (4.6 + 4.7 + 4.8.a) :\n" +
                "1. Backend : npm run dev:token (copie le JWT)\n" +
                "2. Unity : selectionne HubChatClient dans la scene\n" +
                "3. Colle le JWT dans le champ _devToken de l'Inspector\n" +
                "4. Sauvegarde la scene (Ctrl+S)\n" +
                "5. Backend tourne : cd backend && npm run dev\n" +
                "6. Press Play -> chat panel bas-droite, 2 tabs Global / Prive\n" +
                "7. Test whisper : /w <user> <message>  (user = sub ou email)\n" +
                "8. Test filtre : tape 'putain salut' -> publie '****** salut'\n" +
                "9. 4.8.a/b : clic avatar remote -> popup Défier -> system line CHALLENGE_SENT/INCOMING_CHALLENGE\n" +
                "10. 4.8.c : popup auto Accepter/Refuser côté target -> system line CHALLENGE_RESPONSE\n" +
                "11. 4.8.d.ii : accept -> system line [MATCH] cyan -> 2s -> transition auto vers 33_CombatCasual\n" +
                "    (lance d'abord 'Nymora > Setup > Create Combat Casual Scene' si la scene n'existe pas)\n" +
                "12. 4.8.d.iii stub + 4.9 : dans 33_CombatCasual, clic Victoire/Défaite/Égalité -> retour hub\n" +
                "    -> system line de result colorée dans le tab Global au retour",
                "OK");
        }

        // ====== Helpers UI (inspires de CreateLoginSceneTool) ======

        private static Mesh BuildBackgroundQuadMesh()
        {
            var mesh = new Mesh { name = "BackgroundQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
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

        private static GameObject CreateMenuButton(GameObject parent, string name, string label, Color color, float yOffsetFromBottom)
        {
            var btnGo = CreateUIChild(parent, name, typeof(Image), typeof(Button));
            var img = btnGo.GetComponent<Image>();
            img.color = color;
            var rt = btnGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, yOffsetFromBottom);
            rt.sizeDelta = new Vector2(400f, 70f);

            var labelGo = CreateUIChild(btnGo, "Label", typeof(TextMeshProUGUI));
            var labelText = labelGo.GetComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = 26;
            labelText.color = Color.white;
            labelText.alignment = TextAlignmentOptions.Center;
            StretchFull(labelGo.GetComponent<RectTransform>());

            return btnGo;
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

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes)
            {
                if (s.path == scenePath)
                {
                    s.enabled = true;
                    EditorBuildSettings.scenes = scenes.ToArray();
                    return;
                }
            }
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
