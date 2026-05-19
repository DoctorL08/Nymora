using System;
using System.Collections.Generic;
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
        // POLISH-7 polish (20 mai) : default 70 -> 40 pour menu plus compact (Lorenzo
        // peut tweaker Inspector si valeur deja serialisee differente).
        [SerializeField] private float _buttonHeight = 40f;
        [SerializeField] private int _buttonFontSize = 16;

        [Header("Refs externes")]
        [SerializeField] private HubChatUI _chatUI;

        public static ChallengePopup Instance { get; private set; }

        private HubAvatar _currentTarget;
        private readonly List<MenuAction> _actions = new List<MenuAction>();
        private readonly List<GameObject> _spawnedButtons = new List<GameObject>();

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

            // POLISH-7 polish — resserre le VerticalLayoutGroup du buttonContainer (spacing 4)
            // + ContentSizeFitter sur le panel pour que le bg noir s'ajuste a la hauteur des boutons.
            EnsureCompactLayout();
            // 4.11 polish — BuildActions() est appele dans Show() pour reflechir l'etat clan dynamique.
        }

        private void EnsureCompactLayout()
        {
            if (_buttonContainer != null)
            {
                var vlg = _buttonContainer.GetComponent<VerticalLayoutGroup>();
                if (vlg != null)
                {
                    vlg.spacing = 4f;
                    vlg.padding = new RectOffset(8, 8, 6, 6);
                    vlg.childForceExpandHeight = false;
                    vlg.childControlHeight = true;
                    vlg.childForceExpandWidth = true;
                    vlg.childControlWidth = true;
                }
            }
            if (_panel != null)
            {
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

        public void Show(HubAvatar target)
        {
            if (target == null || _panel == null) return;
            _currentTarget = target;
            // POLISH-7 polish (20 mai) — affiche le pseudo du target dans le titre au lieu du
            // generique "Actions". Fallback "Actions" si NetDisplayName pas encore sync (race
            // au Spawn). Le swatch blanc est cache au Awake (cf EnsureCompactLayout).
            if (_label != null)
            {
                string pseudo = target.NetDisplayName.ToString();
                _label.text = string.IsNullOrEmpty(pseudo) ? "Actions" : pseudo;
            }
            // 4.11 polish — rebuild la liste d'actions selon l'etat actuel (clan etc.)
            BuildActions();
            RebuildButtonsUI();
            _panel.SetActive(true);
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

        private static GameObject CreateRuntimeButton(Transform parent, string label, Color bgColor, float height, int fontSize)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = bgColor;

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
            // POLISH-7 polish (20 mai) : fontSize 26 -> 16 + bold pour meilleure lisibilite.
            labelText.fontSize = fontSize;
            labelText.color = Color.white;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.fontStyle = FontStyles.Bold;

            return go;
        }

        // ====== Handlers d'actions ======

        private void DefyTarget(HubAvatar target)
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
            HubChatClient.Instance.SendChallenge(targetSub);
            Hide();
        }

        private void WhisperTarget(HubAvatar target)
        {
            if (target == null) { Hide(); return; }
            var targetSub = target.Sub;
            if (string.IsNullOrEmpty(targetSub))
            {
                Debug.LogWarning("[ChallengePopup] Avatar remote sans NetSub — whisper annulé.");
                Hide();
                return;
            }
            if (_chatUI == null)
            {
                Debug.LogWarning("[ChallengePopup] _chatUI non assigné — whisper annulé.");
                Hide();
                return;
            }
            _chatUI.OpenWhisperToUser(targetSub);
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
