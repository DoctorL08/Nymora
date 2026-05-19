using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// POLISH-7 polish (20 mai) — menu contextuel affiche au click sur un pseudo dans le chat.
    /// Pattern miroir ChallengePopup mais identifie le target par DISPLAY_NAME (pas par HubAvatar)
    /// puisque le chat n'a pas de reference avatar — juste le pseudo string.
    ///
    /// Auto-construit en code (pas de manip Unity / SerializeField) :
    /// - Au 1er appel a Show(), instance le panel + boutons via le pattern CreateRuntimeButton
    /// - Positionne au curseur (Input.mousePosition) au moment du Show
    /// - Singleton via Instance.Show(displayName) depuis HubChatUI.OnPointerClick
    ///
    /// 4 actions + Annuler :
    ///   - Message prive (whisper /w pseudo dans le tab Prive)
    ///   - Ajouter en ami (SendFriendRequest par displayName)
    ///   - Inviter dans clan (CONDITIONNEL : visible si Lorenzo a un clan + droit invite)
    ///   - Signaler (SendReport par displayName)
    ///   - Annuler (close)
    /// </summary>
    public sealed class ChatUserContextMenu : MonoBehaviour
    {
        private const float ButtonHeight = 38f;
        private const int ButtonFontSize = 15;
        private const float MenuWidth = 220f;
        private const float Spacing = 4f;
        private static readonly RectOffset Padding = new RectOffset(8, 8, 6, 6);

        private static readonly Color BgPanelColor = new Color(0.08f, 0.09f, 0.12f, 0.95f);
        private static readonly Color BgMessageColor = new Color(0.25f, 0.40f, 0.65f, 1f);
        private static readonly Color BgFriendColor = new Color(0.45f, 0.30f, 0.60f, 1f);
        private static readonly Color BgClanColor = new Color(0.25f, 0.35f, 0.55f, 1f);
        private static readonly Color BgReportColor = new Color(0.65f, 0.45f, 0.15f, 1f);
        private static readonly Color BgCancelColor = new Color(0.40f, 0.25f, 0.25f, 1f);

        public static ChatUserContextMenu Instance { get; private set; }

        private RectTransform _panel;
        private TextMeshProUGUI _titleLabel;
        private RectTransform _buttonContainer;
        private string _currentTarget;
        private readonly List<GameObject> _spawnedButtons = new List<GameObject>();
        private Canvas _hostCanvas;
        private HubChatUI _chatUIRef;

        private sealed class MenuAction
        {
            public string Label;
            public Color BgColor;
            public Action<string> Execute;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Ouvre le menu sur le pseudo donne, position au curseur (Input.mousePosition).
        /// chatUI sert pour le shortcut "Message prive" (OpenWhisperToUser).
        /// </summary>
        public void Show(string targetDisplayName, HubChatUI chatUI)
        {
            if (string.IsNullOrEmpty(targetDisplayName)) return;
            // Ne pas afficher le menu sur SOI-MEME.
            string myPseudo = HubChatClient.Instance?.MyDisplayName;
            if (!string.IsNullOrEmpty(myPseudo) && myPseudo == targetDisplayName) return;

            _currentTarget = targetDisplayName;
            _chatUIRef = chatUI;
            EnsurePanelBuilt();
            _titleLabel.text = targetDisplayName;
            RebuildButtons();
            PositionAtMouse();
            _panel.gameObject.SetActive(true);
        }

        public void Hide()
        {
            _currentTarget = null;
            if (_panel != null) _panel.gameObject.SetActive(false);
        }

        private void Update()
        {
            // Click hors du panel = close. Empeche d'avoir le menu coince.
            if (_panel == null || !_panel.gameObject.activeSelf) return;
            if (Input.GetMouseButtonDown(0))
            {
                Vector2 mouse = Input.mousePosition;
                if (!RectTransformUtility.RectangleContainsScreenPoint(_panel, mouse, null))
                {
                    Hide();
                }
            }
            // ESC = close
            if (Input.GetKeyDown(KeyCode.Escape)) Hide();
        }

        private void EnsurePanelBuilt()
        {
            if (_panel != null) return;
            // Trouve un Canvas dans la scene (le hub en a un). Au runtime on parente notre
            // panel directement au canvas pour benefit de l'overlay UI.
            _hostCanvas = FindAnyObjectByType<Canvas>();
            if (_hostCanvas == null)
            {
                Debug.LogError("[ChatUserContextMenu] Aucun Canvas dans la scene — menu desactive.");
                return;
            }

            var panelGo = new GameObject("ChatUserContextMenu_Panel",
                typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panelGo.transform.SetParent(_hostCanvas.transform, false);
            _panel = (RectTransform)panelGo.transform;
            _panel.anchorMin = new Vector2(0f, 1f);
            _panel.anchorMax = new Vector2(0f, 1f);
            _panel.pivot = new Vector2(0f, 1f);
            _panel.sizeDelta = new Vector2(MenuWidth, 0f);

            panelGo.GetComponent<Image>().color = BgPanelColor;
            // Sorting order : au-dessus de tout (sera sur z-order via SetAsLastSibling au Show)
            panelGo.transform.SetAsLastSibling();

            var vlg = panelGo.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = Spacing;
            vlg.padding = Padding;
            vlg.childForceExpandHeight = false;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlWidth = true;

            var fitter = panelGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Title (pseudo du target)
            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            titleGo.transform.SetParent(panelGo.transform, false);
            _titleLabel = titleGo.GetComponent<TextMeshProUGUI>();
            _titleLabel.fontSize = 16;
            _titleLabel.color = new Color(1f, 0.96f, 0.85f, 1f);
            _titleLabel.alignment = TextAlignmentOptions.Center;
            _titleLabel.fontStyle = FontStyles.Bold;
            titleGo.GetComponent<LayoutElement>().preferredHeight = 28f;

            // Conteneur des boutons (re-utilise _panel directement, evite un GO en plus)
            _buttonContainer = _panel;

            _panel.gameObject.SetActive(false);
        }

        private void RebuildButtons()
        {
            // Cleanup anciens boutons (garde le title qui est le 1er child)
            foreach (var go in _spawnedButtons) if (go != null) Destroy(go);
            _spawnedButtons.Clear();

            var actions = BuildActions();
            foreach (var a in actions)
            {
                var captured = a;
                var btn = CreateRuntimeButton(_buttonContainer, captured.Label, captured.BgColor);
                btn.GetComponent<Button>().onClick.AddListener(() =>
                {
                    captured.Execute?.Invoke(_currentTarget);
                    Hide();
                });
                _spawnedButtons.Add(btn);
            }
        }

        private List<MenuAction> BuildActions()
        {
            var list = new List<MenuAction>
            {
                new MenuAction
                {
                    Label = "Message privé",
                    BgColor = BgMessageColor,
                    Execute = target =>
                    {
                        if (_chatUIRef != null) _chatUIRef.OpenWhisperToUser(target);
                    },
                },
                new MenuAction
                {
                    Label = "Ajouter en ami",
                    BgColor = BgFriendColor,
                    Execute = target =>
                    {
                        if (HubChatClient.Instance != null)
                            HubChatClient.Instance.SendFriendRequest(target);
                    },
                },
            };

            // Bouton Inviter clan : conditionnel (Leader/Officer + clan actif).
            if (HubClanPanel.Instance != null && HubClanPanel.Instance.CanInviteToClan)
            {
                list.Add(new MenuAction
                {
                    Label = "Inviter dans clan",
                    BgColor = BgClanColor,
                    Execute = target => HubClanPanel.Instance.InviteByDisplayNameFromContextMenu(target),
                });
            }

            list.Add(new MenuAction
            {
                Label = "Signaler",
                BgColor = BgReportColor,
                Execute = target =>
                {
                    if (HubChatClient.Instance != null)
                        HubChatClient.Instance.SendReport(target);
                },
            });
            list.Add(new MenuAction
            {
                Label = "Annuler",
                BgColor = BgCancelColor,
                Execute = _ => { /* Hide gere par le wrapper */ },
            });
            return list;
        }

        private static GameObject CreateRuntimeButton(Transform parent, string label, Color bgColor)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = bgColor;
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = ButtonHeight;
            le.flexibleWidth = 1f;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(go.transform, false);
            var rt = (RectTransform)labelGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var tmp = labelGo.GetComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = ButtonFontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            return go;
        }

        private void PositionAtMouse()
        {
            if (_panel == null || _hostCanvas == null) return;
            // Convertit mousePos screen -> position relative au canvas RectTransform
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)_hostCanvas.transform,
                Input.mousePosition,
                _hostCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _hostCanvas.worldCamera,
                out localPoint);
            _panel.anchoredPosition = localPoint;
            _panel.SetAsLastSibling();
        }

        // Auto-create au lancement du hub : evite d'avoir a manip une scene + drag-and-drop.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoadedStatic;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoadedStatic;
            TryCreateForActiveScene();
        }

        private static void OnSceneLoadedStatic(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            TryCreateForActiveScene();
        }

        private static void TryCreateForActiveScene()
        {
            if (Instance != null) return;
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (!sceneName.Contains("Hub")) return;
            var go = new GameObject("ChatUserContextMenu");
            go.AddComponent<ChatUserContextMenu>();
        }
    }
}
