using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Nymora.Hub.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 4.8.a — Menu contextuel affiché au clic sur un avatar remote.
    /// Brique 4.10.UX refacto — pattern data-driven : List&lt;MenuAction&gt; + boutons instanciés runtime
    /// via VerticalLayoutGroup. Ajouter une action future (Ami 4.10, Profil 4.12, Signaler 4.13) =
    /// 1 ligne dans BuildActions() au lieu de 5 manips (SerializedField + Editor tool + handler).
    ///
    /// Note : le nom de classe `ChallengePopup` est conservé pour éviter de toucher
    /// HubInputController + meta files Unity. Rename pur en `AvatarContextMenu` = chantier séparé.
    /// </summary>
    public sealed class ChallengePopup : MonoBehaviour
    {
        [Header("Refs UI")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private Image _targetColorSwatch;
        [SerializeField] private RectTransform _buttonContainer;
        // Menu contextuel façon Dofus (28 mai) : lignes de texte compactes, hauteur de ligne
        // réduite. Lorenzo peut tweaker dans l'Inspector.
        [SerializeField] private float _buttonHeight = 26f;
        [SerializeField] private int _buttonFontSize = 16;
        [Tooltip("Largeur fixe de la boîte du menu contextuel (px). La hauteur s'ajuste au contenu.")]
        [SerializeField] private float _menuWidth = 215f;

        [Header("Refs externes")]
        [SerializeField] private HubChatUI _chatUI;

        [Header("Style (DA menu hub)")]
        [Tooltip("Thème menu hub. Si laissé vide, fallback sur HubMenuShell.MenuTheme (dès qu'un menu a été ouvert), sinon valeurs par défaut.")]
        [SerializeField] private HubMenuTheme _theme;

        // Palette du menu contextuel (Dofus-like) : boîte quasi noire + texte blanc + survol clair.
        private static readonly Color MenuBg = new Color(0.05f, 0.05f, 0.06f, 0.98f);
        private static readonly Color RowHover = new Color(1f, 1f, 1f, 0.14f);
        private static readonly Color RowPressed = new Color(1f, 1f, 1f, 0.22f);
        private static readonly Color RowTransparent = new Color(1f, 1f, 1f, 0f);
        private static readonly Color TextWhite = new Color(0.93f, 0.94f, 0.96f, 1f);

        public static ChallengePopup Instance { get; private set; }

        /// <summary>Thème actif : champ sérialisé prioritaire, sinon celui posé par HubMenuShell. Peut être null.</summary>
        private HubMenuTheme ActiveTheme => _theme != null ? _theme : HubMenuShell.MenuTheme;

        private HubAvatar _currentTarget;
        private readonly List<MenuAction> _actions = new List<MenuAction>();
        private readonly List<GameObject> _spawnedButtons = new List<GameObject>();

        // Frame d'ouverture : ignore le clic qui OUVRE le menu (sinon le close-au-clic-dehors
        // ci-dessous le refermerait immédiatement, le clic sur l'avatar étant hors du panneau).
        private int _openedFrame = -1;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        private sealed class MenuAction
        {
            public string Label;
            public Color BgColor;
            public Action<HubAvatar> Execute;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            if (_panel != null) _panel.SetActive(false);

            // POLISH-7 polish (20 mai) — swatch blanc 4.8.a (placeholder couleur HSV per-player)
            // devenu inutile depuis les sprites de classe. On le desactive proprement sans toucher
            // la scene (idempotent : si Lorenzo l'a deja supprime du scene, _targetColorSwatch=null).
            if (_targetColorSwatch != null) _targetColorSwatch.gameObject.SetActive(false);

            // Menu contextuel : la boîte s'empile (titre + actions) et s'ajuste au contenu,
            // largeur fixe, ancrée pour être positionnée au clic.
            EnsureContextMenuLayout();
            // 4.11 polish — BuildActions() est appele dans Show() pour reflechir l'etat clan dynamique.
        }

        private void EnsureContextMenuLayout()
        {
            // --- Conteneur d'actions : lignes serrées, pleine largeur ---
            if (_buttonContainer != null)
            {
                var vlg = _buttonContainer.GetComponent<VerticalLayoutGroup>();
                if (vlg != null)
                {
                    vlg.spacing = 0f;
                    vlg.padding = new RectOffset(0, 0, 2, 4);
                    vlg.childForceExpandHeight = false;
                    vlg.childControlHeight = true;
                    vlg.childForceExpandWidth = true;
                    vlg.childControlWidth = true;
                }
            }

            // --- Panneau : empile titre + conteneur, largeur fixe, hauteur auto ---
            if (_panel != null)
            {
                var panelRT = (RectTransform)_panel.transform;
                // Ancrage ponctuel + pivot haut-gauche : le menu s'ouvre vers le bas-droite du clic.
                panelRT.anchorMin = panelRT.anchorMax = new Vector2(0.5f, 0.5f);
                panelRT.pivot = new Vector2(0f, 1f);
                panelRT.sizeDelta = new Vector2(_menuWidth, panelRT.sizeDelta.y);

                var panelVlg = _panel.GetComponent<VerticalLayoutGroup>();
                if (panelVlg == null) panelVlg = _panel.AddComponent<VerticalLayoutGroup>();
                panelVlg.spacing = 0f;
                panelVlg.padding = new RectOffset(0, 0, 0, 0);
                panelVlg.childForceExpandHeight = false;
                panelVlg.childControlHeight = true;
                panelVlg.childForceExpandWidth = true;
                panelVlg.childControlWidth = true;

                var fitter = _panel.GetComponent<ContentSizeFitter>();
                if (fitter == null) fitter = _panel.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ====== Liste des actions ======
        // Ajouter une action future = nouvelle ligne ici + un handler privé en bas du fichier.
        private void BuildActions()
        {
            _actions.Clear();
            _actions.Add(new MenuAction
            {
                Label = "Défier",
                BgColor = new Color(0.25f, 0.55f, 0.35f, 1f),
                Execute = DefyTarget,
            });
            _actions.Add(new MenuAction
            {
                Label = "Message privé",
                BgColor = new Color(0.25f, 0.4f, 0.65f, 1f),
                Execute = WhisperTarget,
            });
            // 4.10 Amis — action Ajouter en ami (violet)
            _actions.Add(new MenuAction
            {
                Label = "Ajouter en ami",
                BgColor = new Color(0.45f, 0.30f, 0.60f, 1f),
                Execute = AddFriendTarget,
            });
            // 4.11 Clan — action Inviter dans clan (bleu marine).
            // Affichee uniquement si le user a un clan ET le droit d'inviter (Leader/Officer).
            if (HubClanPanel.Instance != null && HubClanPanel.Instance.CanInviteToClan)
            {
                _actions.Add(new MenuAction
                {
                    Label = "Inviter dans clan",
                    BgColor = new Color(0.25f, 0.35f, 0.55f, 1f),
                    Execute = ClanInviteTarget,
                });
            }
            // 4.13 Modération — action Signaler (orange)
            _actions.Add(new MenuAction
            {
                Label = "Signaler",
                BgColor = new Color(0.65f, 0.45f, 0.15f, 1f),
                Execute = ReportTarget,
            });
            _actions.Add(new MenuAction
            {
                Label = "Annuler",
                BgColor = new Color(0.4f, 0.25f, 0.25f, 1f),
                Execute = _ => Hide(),
            });
        }

        private bool _chromeStyled;

        /// <summary>Applique le style du menu contextuel (boîte quasi noire + titre blanc à gauche),
        /// une seule fois. Utilise la police Ari du thème si dispo, sinon la police par défaut.</summary>
        private void StyleStaticChrome()
        {
            if (_chromeStyled) return;
            var t = ActiveTheme; // peut être null : on garde des fallbacks

            if (_panel != null)
            {
                var panelImg = _panel.GetComponent<Image>();
                if (panelImg != null)
                {
                    panelImg.color = MenuBg;
                    // Coins légèrement arrondis (petit rayon = look compact, pas la grosse carte menu).
                    panelImg.sprite = HubMenuUIFactory.RoundedSprite(6f);
                    panelImg.type = Image.Type.Sliced;
                    panelImg.raycastTarget = true; // bloque les clics derrière le menu
                }
            }

            if (_label != null)
            {
                var bold = t != null && t.FontBold != null ? t.FontBold : HubMenuShell.MenuFont;
                if (bold != null) _label.font = bold;
                _label.color = t != null ? t.TextPrimary : TextWhite;
                _label.fontSize = t != null ? t.FontSizeBody : _buttonFontSize + 3;
                _label.fontStyle = FontStyles.Bold;
                _label.alignment = TextAlignmentOptions.MidlineLeft;
                _label.enableWordWrapping = false;
                _label.overflowMode = TextOverflowModes.Ellipsis;
                _label.margin = new Vector4(12f, 4f, 12f, 4f); // padding interne (titre collé au bord sinon)
                var le = _label.GetComponent<LayoutElement>();
                if (le == null) le = _label.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = _buttonHeight + 6f; // titre un poil plus haut que les lignes
            }

            _chromeStyled = true;
        }

        public void Show(HubAvatar target)
        {
            if (target == null || _panel == null) return;
            StyleStaticChrome();
            _currentTarget = target;
            // POLISH-7 polish (20 mai) — affiche le pseudo du target dans le titre au lieu du
            // generique "Actions". Fallback "Actions" si NetDisplayName pas encore sync (race
            // au Spawn). Le swatch blanc est cache au Awake (cf EnsureContextMenuLayout).
            if (_label != null)
            {
                string pseudo = target.NetDisplayName.ToString();
                _label.text = string.IsNullOrEmpty(pseudo) ? "Actions" : pseudo;
            }
            // 4.11 polish — rebuild la liste d'actions selon l'etat actuel (clan etc.)
            BuildActions();
            RebuildButtonsUI();
            _panel.SetActive(true);
            _openedFrame = Time.frameCount;
            PositionAtCursor();
        }

        // Ferme le menu contextuel quand on clique EN DEHORS de la boîte (comportement attendu Dofus-like).
        //   - Garde _openedFrame : on saute la frame d'ouverture (le clic sur l'avatar est hors panneau).
        //   - Clic DANS le panneau (titre ou bouton) -> ne ferme pas (les boutons gèrent leur action).
        //   - Re-clic sur un autre avatar -> HubInputController rappelle Show() -> _openedFrame réactualisé.
        private void Update()
        {
            if (!IsOpen || Time.frameCount <= _openedFrame) return;
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                if (!IsPointerOverPanel(Input.mousePosition)) Hide();
            }
        }

        private bool IsPointerOverPanel(Vector2 screenPos)
        {
            if (_panel == null) return false;
            var rt = (RectTransform)_panel.transform;
            var canvas = _panel.GetComponentInParent<Canvas>();
            Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? canvas.worldCamera
                : null;
            return RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, cam);
        }

        /// <summary>Place la boîte du menu à la position de la souris (le clic vient d'avoir lieu),
        /// puis la borne pour qu'elle reste à l'écran. Pivot haut-gauche = ouverture bas-droite.</summary>
        private void PositionAtCursor()
        {
            var panelRT = (RectTransform)_panel.transform;
            if (panelRT.parent is not RectTransform parentRT) return;

            var canvas = _panel.GetComponentInParent<Canvas>();
            Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? canvas.worldCamera
                : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRT, Input.mousePosition, cam, out var local))
                return;

            // Taille réelle après layout (titre + lignes) pour le clamp.
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRT);
            Vector2 size = panelRT.rect.size;
            float hw = parentRT.rect.width * 0.5f;
            float hh = parentRT.rect.height * 0.5f;

            // local = coord relative au centre du parent (pivot 0.5). Le menu occupe
            // x:[local.x, local.x+size.x], y:[local.y-size.y, local.y] (pivot haut-gauche).
            float x = local.x;
            float y = local.y;
            if (x + size.x > hw) x = hw - size.x;
            if (x < -hw) x = -hw;
            if (y - size.y < -hh) y = -hh + size.y;
            if (y > hh) y = hh;

            panelRT.anchoredPosition = new Vector2(x, y);
        }

        public void Hide()
        {
            _currentTarget = null;
            if (_panel != null) _panel.SetActive(false);
        }

        private void RebuildButtonsUI()
        {
            // Cleanup anciens boutons
            foreach (var go in _spawnedButtons)
            {
                if (go != null) Destroy(go);
            }
            _spawnedButtons.Clear();

            if (_buttonContainer == null) return;

            foreach (var action in _actions)
            {
                var btnGo = CreateRuntimeButton(_buttonContainer, action.Label, action.BgColor, _buttonHeight, _buttonFontSize);
                var captured = action; // capture pour closure
                btnGo.GetComponent<Button>().onClick.AddListener(() => captured.Execute(_currentTarget));
                _spawnedButtons.Add(btnGo);
            }
        }

        private GameObject CreateRuntimeButton(Transform parent, string label, Color bgColor, float height, int fontSize)
        {
            var t = ActiveTheme;
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            // Ligne de menu façon Dofus : fond transparent au repos, barre claire au survol.
            // Le ColorBlock pilote la couleur du graphique cible (transparent -> survol -> pressé).
            var img = go.GetComponent<Image>();
            img.color = RowTransparent;
            img.sprite = null; // barre plate (pas de coins arrondis sur les lignes)
            img.raycastTarget = true;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            var c = btn.colors;
            c.normalColor = RowTransparent;
            c.highlightedColor = RowHover;
            c.pressedColor = RowPressed;
            c.selectedColor = RowTransparent;
            c.disabledColor = RowTransparent;
            c.colorMultiplier = 1f;
            c.fadeDuration = 0.08f;
            btn.colors = c;

            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = height;
            le.flexibleWidth = 1f;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var labelText = labelGo.GetComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontSize = fontSize;
            labelText.color = t != null ? t.TextPrimary : TextWhite;
            labelText.alignment = TextAlignmentOptions.MidlineLeft; // aligné à gauche (Dofus)
            labelText.margin = new Vector4(14f, 0f, 10f, 0f);        // retrait gauche
            labelText.enableWordWrapping = false;
            labelText.overflowMode = TextOverflowModes.Ellipsis;
            var font = t != null && t.Font != null ? t.Font : HubMenuShell.MenuFont;
            if (font != null) labelText.font = font;
            labelText.raycastTarget = false;

            return go;
        }

        // ====== Handlers d'actions ======

        private async void DefyTarget(HubAvatar target)
        {
            if (target == null) { Hide(); return; }
            var targetSub = target.Sub;
            if (string.IsNullOrEmpty(targetSub))
            {
                Debug.LogWarning("[ChallengePopup] Avatar remote sans NetSub — défi annulé.");
                Hide();
                return;
            }
            if (HubChatClient.Instance == null)
            {
                Debug.LogWarning("[ChallengePopup] HubChatClient.Instance null — défi annulé.");
                Hide();
                return;
            }
            // Ferme le menu contextuel tout de suite (la vérif de deck est asynchrone).
            Hide();

            // Garde deck : pas de défi sans deck équipé pour la classe sélectionnée — sinon
            // l'adversaire accepte et poireaute pendant qu'on annule côté local (cf le TODO
            // "cancel" de HubMatchTransition). On bloque donc AVANT l'envoi du défi.
            var dbp = HubDeckBuilderPanel.Instance;
            if (dbp == null || !await dbp.HasEquippedDeckForSelectedClassAsync())
            {
                HubNoticePopup.ShowNoDeck();
                return;
            }

            HubChatClient.Instance?.SendChallenge(targetSub);
        }

        private void WhisperTarget(HubAvatar target)
        {
            if (target == null) { Hide(); return; }
            // D2 (22 mai, test designer) — le whisper /w resout sa cible par PSEUDO (displayName),
            // PAS par Sub/UUID : la commande "/w <user>" est faite pour des pseudos tapes par des
            // humains, et ChatUserContextMenu (clic pseudo dans le chat) whisper deja par
            // displayName. On passait target.Sub -> "/w <UUID>" = cible incomprehensible/erronee.
            string targetName = target.NetDisplayName.ToString();
            if (string.IsNullOrEmpty(targetName))
            {
                Debug.LogWarning("[ChallengePopup] Avatar remote sans NetDisplayName — whisper annulé.");
                Hide();
                return;
            }
            if (_chatUI == null)
            {
                Debug.LogWarning("[ChallengePopup] _chatUI non assigné — whisper annulé.");
                Hide();
                return;
            }
            _chatUI.OpenWhisperToUser(targetName);
            Hide();
        }

        private void ReportTarget(HubAvatar target)
        {
            if (target == null) { Hide(); return; }
            var targetSub = target.Sub;
            if (string.IsNullOrEmpty(targetSub))
            {
                Debug.LogWarning("[ChallengePopup] Avatar remote sans NetSub — signalement annulé.");
                Hide();
                return;
            }
            if (HubChatClient.Instance == null)
            {
                Debug.LogWarning("[ChallengePopup] HubChatClient.Instance null — signalement annulé.");
                Hide();
                return;
            }
            HubChatClient.Instance.SendReport(targetSub);
            Hide();
        }

        private void AddFriendTarget(HubAvatar target)
        {
            if (target == null) { Hide(); return; }
            var targetSub = target.Sub;
            if (string.IsNullOrEmpty(targetSub))
            {
                Debug.LogWarning("[ChallengePopup] Avatar remote sans NetSub — ami annulé.");
                Hide();
                return;
            }
            if (HubChatClient.Instance == null)
            {
                Debug.LogWarning("[ChallengePopup] HubChatClient.Instance null — ami annulé.");
                Hide();
                return;
            }
            HubChatClient.Instance.SendFriendRequestByUserId(targetSub);
            Hide();
        }

        // 4.11 — Invitation clan : passe par REST via HubClanPanel.Instance pour faciliter
        // la gestion du feedback (succès / déjà en clan / pas chef). Si pas de panel = pas de clan.
        private void ClanInviteTarget(HubAvatar target)
        {
            if (target == null) { Hide(); return; }
            var targetSub = target.Sub;
            if (string.IsNullOrEmpty(targetSub))
            {
                Debug.LogWarning("[ChallengePopup] Avatar remote sans NetSub — invitation clan annulée.");
                Hide();
                return;
            }
            if (HubClanPanel.Instance == null)
            {
                Debug.LogWarning("[ChallengePopup] HubClanPanel.Instance null — invitation clan annulée.");
                Hide();
                return;
            }
            HubClanPanel.Instance.InviteByUserIdFromContextMenu(targetSub);
            Hide();
        }
    }
}
