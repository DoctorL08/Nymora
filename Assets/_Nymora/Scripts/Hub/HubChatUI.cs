using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 4.6 — UI chat in-game minimaliste : panel bas-droite avec history scroll + input + Send.
    /// Brique 4.7 — 2 tabs (Global / Prive), parser slash /w &lt;user&gt; &lt;msg&gt; pour whispers.
    /// History limit a _maxLines par tab pour eviter explosion memoire si session longue.
    /// </summary>
    public sealed class HubChatUI : MonoBehaviour
    {
        public enum ChatTab { Global, Private }

        [Header("Refs")]
        [SerializeField] private TMP_InputField _inputField;
        [SerializeField] private Button _sendButton;
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private TextMeshProUGUI _historyText;
        [SerializeField] private Button _tabGlobalButton;
        [SerializeField] private Button _tabPrivateButton;

        [Header("Config")]
        [SerializeField] private string _channel = "global";
        [SerializeField, Range(20, 500)] private int _maxLines = 100;

        [Header("Tab style")]
        [SerializeField] private Color _tabActiveColor = new Color(0.25f, 0.4f, 0.65f, 1f);
        [SerializeField] private Color _tabInactiveColor = new Color(0.2f, 0.2f, 0.24f, 1f);

        private readonly List<string> _globalHistory = new List<string>();
        private readonly List<string> _privateHistory = new List<string>();
        private ChatTab _activeTab = ChatTab.Global;

        private void OnEnable()
        {
            if (_sendButton != null) _sendButton.onClick.AddListener(OnSendClicked);
            if (_inputField != null) _inputField.onSubmit.AddListener(OnInputSubmit);
            if (_tabGlobalButton != null) _tabGlobalButton.onClick.AddListener(() => SwitchTab(ChatTab.Global));
            if (_tabPrivateButton != null) _tabPrivateButton.onClick.AddListener(() => SwitchTab(ChatTab.Private));
        }

        private void OnDisable()
        {
            if (_sendButton != null) _sendButton.onClick.RemoveListener(OnSendClicked);
            if (_inputField != null) _inputField.onSubmit.RemoveListener(OnInputSubmit);
            if (_tabGlobalButton != null) _tabGlobalButton.onClick.RemoveAllListeners();
            if (_tabPrivateButton != null) _tabPrivateButton.onClick.RemoveAllListeners();
        }

        private void Start()
        {
            AppendSystemLine(ChatTab.Global, "--- Chat connecting ---");
            UpdateTabStyles();
            RefreshHistoryText();
            EnsureHistoryClickHandler();
            if (HubChatClient.Instance != null)
            {
                HubChatClient.Instance.OnConnected += HandleConnected;
                HubChatClient.Instance.OnDisconnected += HandleDisconnected;
                HubChatClient.Instance.OnMessageReceived += HandleMessage;
                HubChatClient.Instance.OnWhisperReceived += HandleWhisper;
                HubChatClient.Instance.OnIncomingChallenge += HandleIncomingChallenge;
                HubChatClient.Instance.OnChallengeSent += HandleChallengeSent;
                HubChatClient.Instance.OnChallengeResponse += HandleChallengeResponse;
                HubChatClient.Instance.OnMatchReady += HandleMatchReady;
                HubChatClient.Instance.OnReportSent += HandleReportSent;
                HubChatClient.Instance.OnModerationNotice += HandleModerationNotice;
            }
        }

        private void OnDestroy()
        {
            if (HubChatClient.Instance != null)
            {
                HubChatClient.Instance.OnConnected -= HandleConnected;
                HubChatClient.Instance.OnDisconnected -= HandleDisconnected;
                HubChatClient.Instance.OnMessageReceived -= HandleMessage;
                HubChatClient.Instance.OnWhisperReceived -= HandleWhisper;
                HubChatClient.Instance.OnIncomingChallenge -= HandleIncomingChallenge;
                HubChatClient.Instance.OnChallengeSent -= HandleChallengeSent;
                HubChatClient.Instance.OnChallengeResponse -= HandleChallengeResponse;
                HubChatClient.Instance.OnMatchReady -= HandleMatchReady;
                HubChatClient.Instance.OnReportSent -= HandleReportSent;
                HubChatClient.Instance.OnModerationNotice -= HandleModerationNotice;
            }
        }

        private void HandleConnected()
        {
            AppendSystemLine(ChatTab.Global, "--- Connected ---");
        }

        private void HandleDisconnected(string reason)
        {
            AppendSystemLine(ChatTab.Global, $"--- Disconnected: {reason} ---");
        }

        private void HandleMessage(string channel, string from, string text)
        {
            if (channel != _channel) return;
            // POLISH-7 polish (20 mai) : pseudo wrappe dans link cliquable pour ouvrir
            // le menu contextuel chat (MP / Ami / Inviter clan / Signaler).
            AppendLine(ChatTab.Global, $"{WrapPseudoLink(from)}: {text}");
        }

        private void HandleWhisper(string from, string to, string text)
        {
            // POLISH-7 polish : 2 pseudos cliquables dans la ligne whisper (sender + receiver).
            AppendLine(ChatTab.Private, $"<color=#d8a4ff>[{WrapPseudoLink(from)} → {WrapPseudoLink(to)}]</color> {text}");
        }

        // POLISH-7 (20 mai) : les events portent maintenant le displayName (pseudo officiel
        // Profile.displayName) au 3e param, plus l'email. Affichage UI = pseudo partout.
        // POLISH-7 polish : retire les "(id=xxxxxxxx)" des lignes chat (vestige debug 4.8.b/c
        // que Lorenzo confondait avec un id joueur). Les ids restent en console via Debug.Log.

        private void HandleIncomingChallenge(string challengeId, string fromUserId, string fromDisplayName)
        {
            AppendLine(ChatTab.Global, $"<color=#ffaa00>[DEFI] {fromDisplayName} vous défie !</color>");
        }

        private void HandleChallengeSent(string challengeId, string toUserId, string toDisplayName)
        {
            AppendLine(ChatTab.Global, $"<color=#88ff88>[DEFI] Défi envoyé à {toDisplayName}</color>");
        }

        private void HandleChallengeResponse(string challengeId, bool accepted, string fromUserId, string fromDisplayName)
        {
            if (accepted)
            {
                AppendLine(ChatTab.Global, $"<color=#88ff88>[DEFI] {fromDisplayName} a accepté votre défi !</color>");
            }
            else
            {
                AppendLine(ChatTab.Global, $"<color=#ff8888>[DEFI] {fromDisplayName} a refusé votre défi</color>");
            }
        }

        private void HandleMatchReady(string matchId, string opponentSub, string opponentDisplayName)
        {
            AppendLine(ChatTab.Global, $"<color=#88e0ff>[MATCH] Prêt vs {opponentDisplayName}</color>");
        }

        private void HandleReportSent(string toDisplayName)
        {
            AppendLine(ChatTab.Global, $"<color=#ffa040>[MODÉRATION] Signalement envoyé contre {toDisplayName}</color>");
        }

        private void HandleModerationNotice(string kind, long muteUntilMs)
        {
            if (kind == "reported")
            {
                AppendLine(ChatTab.Global, "<color=#ffa040>[MODÉRATION] Tu as été signalé par un autre joueur</color>");
            }
            else if (kind == "muted")
            {
                var dur = FormatMuteDuration(muteUntilMs);
                AppendLine(ChatTab.Global, $"<color=#ff5050>[MODÉRATION] Tu es mute pendant {dur} — tu ne peux plus envoyer de messages</color>");
            }
        }

        private static string FormatMuteDuration(long muteUntilMs)
        {
            var nowMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var remainingSec = Mathf.Max(0, (int)((muteUntilMs - nowMs) / 1000));
            if (remainingSec < 60) return $"{remainingSec}s";
            if (remainingSec < 3600) return $"{remainingSec / 60}min{remainingSec % 60:D2}s";
            return $"{remainingSec / 3600}h{(remainingSec % 3600) / 60:D2}min";
        }

        private void OnSendClicked()
        {
            SendCurrent();
        }

        private void OnInputSubmit(string text)
        {
            SendCurrent();
        }

        private void SendCurrent()
        {
            if (_inputField == null) return;
            var text = _inputField.text?.Trim();
            if (string.IsNullOrEmpty(text)) return;
            if (HubChatClient.Instance == null) return;

            if (text.StartsWith("/w ", System.StringComparison.Ordinal))
            {
                TrySendWhisperCommand(text);
            }
            else
            {
                HubChatClient.Instance.SendChatMessage(_channel, text);
            }

            _inputField.text = "";
            _inputField.ActivateInputField();
        }

        private void TrySendWhisperCommand(string fullText)
        {
            // Format attendu : "/w <target> <message...>"
            var afterCmd = fullText.Substring(3).TrimStart();
            int spaceIdx = afterCmd.IndexOf(' ');
            if (spaceIdx <= 0)
            {
                AppendSystemLine(_activeTab, "<color=#ff7777>Usage: /w &lt;user&gt; &lt;message&gt;</color>");
                return;
            }
            var target = afterCmd.Substring(0, spaceIdx).Trim();
            var message = afterCmd.Substring(spaceIdx + 1).Trim();
            if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(message))
            {
                AppendSystemLine(_activeTab, "<color=#ff7777>Usage: /w &lt;user&gt; &lt;message&gt;</color>");
                return;
            }
            HubChatClient.Instance.SendWhisper(target, message);
        }

        private void SwitchTab(ChatTab tab)
        {
            if (_activeTab == tab) return;
            _activeTab = tab;
            UpdateTabStyles();
            RefreshHistoryText();
        }

        // 4.10.UX — exposé pour ChallengePopup (raccourci "Message privé" du menu contextuel avatar).
        // Switch sur le tab Privé + pré-remplit l'input avec "/w <target> " + active le focus clavier.
        public void OpenWhisperToUser(string targetSubOrEmail)
        {
            if (string.IsNullOrWhiteSpace(targetSubOrEmail) || _inputField == null) return;
            SwitchTab(ChatTab.Private);
            _inputField.text = $"/w {targetSubOrEmail} ";
            _inputField.ActivateInputField();
            _inputField.caretPosition = _inputField.text.Length;
        }

        private void UpdateTabStyles()
        {
            ApplyTabColor(_tabGlobalButton, _activeTab == ChatTab.Global);
            ApplyTabColor(_tabPrivateButton, _activeTab == ChatTab.Private);
        }

        private void ApplyTabColor(Button btn, bool active)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = active ? _tabActiveColor : _tabInactiveColor;
        }

        private void AppendSystemLine(ChatTab tab, string line)
        {
            AppendLine(tab, $"<color=#888888>{line}</color>");
        }

        // 4.9 — exposé pour HubMatchResultDisplay (résultat du dernier match au retour hub).
        public void AppendSystemLineExternal(string richLine)
        {
            AppendLine(ChatTab.Global, richLine);
        }

        private void AppendLine(ChatTab tab, string richLine)
        {
            var list = tab == ChatTab.Global ? _globalHistory : _privateHistory;
            list.Add(richLine);
            if (list.Count > _maxLines) list.RemoveRange(0, list.Count - _maxLines);
            if (tab == _activeTab) RefreshHistoryText();
        }

        private void RefreshHistoryText()
        {
            if (_historyText == null) return;
            var list = _activeTab == ChatTab.Global ? _globalHistory : _privateHistory;
            _historyText.text = string.Join("\n", list);
            Canvas.ForceUpdateCanvases();
            if (_scrollRect != null) _scrollRect.verticalNormalizedPosition = 0f;
        }

        // ====== POLISH-7 polish (20 mai) — Pseudos cliquables dans le chat ======

        // Prefix utilise dans le link tag TMP : <link="user:dev-2">dev-2</link>
        private const string LinkPrefix = "user:";

        /// <summary>Wrap un pseudo dans un &lt;link&gt; TMP + couleur bleue claire pour le distinguer.</summary>
        private static string WrapPseudoLink(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return displayName;
            return $"<link=\"{LinkPrefix}{displayName}\"><color=#9bcdf5>{displayName}</color></link>";
        }

        /// <summary>
        /// Auto-attache un PointerClickHandler sur le _historyText au Start. Active aussi
        /// raycastTarget pour que les clicks soient detectes par l'EventSystem.
        /// </summary>
        private void EnsureHistoryClickHandler()
        {
            if (_historyText == null) return;
            _historyText.raycastTarget = true;
            var handler = _historyText.GetComponent<ChatHistoryClickHandler>();
            if (handler == null) handler = _historyText.gameObject.AddComponent<ChatHistoryClickHandler>();
            handler.Bind(this, _historyText);
        }

        /// <summary>Callback depuis ChatHistoryClickHandler quand un link est clique.</summary>
        internal void OnPseudoLinkClicked(string linkId)
        {
            if (string.IsNullOrEmpty(linkId) || !linkId.StartsWith(LinkPrefix)) return;
            string displayName = linkId.Substring(LinkPrefix.Length);
            if (string.IsNullOrEmpty(displayName)) return;
            if (ChatUserContextMenu.Instance == null)
            {
                Debug.LogWarning("[HubChatUI] ChatUserContextMenu.Instance null — auto-create devrait s'etre declenchee au load de la scene hub.");
                return;
            }
            ChatUserContextMenu.Instance.Show(displayName, this);
        }
    }

    /// <summary>
    /// Sub-component attache au TMP _historyText. Detecte les clicks sur les pseudos
    /// (wrappes en &lt;link&gt;) et delegue a HubChatUI.OnPseudoLinkClicked.
    /// </summary>
    internal sealed class ChatHistoryClickHandler : MonoBehaviour, IPointerClickHandler
    {
        private HubChatUI _owner;
        private TextMeshProUGUI _text;

        public void Bind(HubChatUI owner, TextMeshProUGUI text)
        {
            _owner = owner;
            _text = text;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_owner == null || _text == null) return;
            Camera cam = _text.canvas != null && _text.canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _text.canvas.worldCamera
                : null;
            int linkIdx = TMP_TextUtilities.FindIntersectingLink(_text, eventData.position, cam);
            if (linkIdx < 0) return;
            var linkInfo = _text.textInfo.linkInfo[linkIdx];
            _owner.OnPseudoLinkClicked(linkInfo.GetLinkID());
        }
    }
}
