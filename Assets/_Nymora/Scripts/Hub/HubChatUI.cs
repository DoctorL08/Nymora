using System.Collections.Generic;
using Nymora.Core.Data;
using Nymora.Hub.Menu;
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
        public enum ChatTab { Global, Private, Clan }

        [Header("Refs")]
        [SerializeField] private TMP_InputField _inputField;
        [SerializeField] private Button _sendButton;
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private TextMeshProUGUI _historyText;
        [SerializeField] private Button _tabGlobalButton;
        [SerializeField] private Button _tabPrivateButton;
        [SerializeField] private Button _tabClanButton; // créé/assigné par le tool de restyle (optionnel)

        [Header("Config")]
        [SerializeField] private string _channel = "global";

        [Tooltip("Interligne du fil de chat (lineSpacing TMP). Negatif = plus serre. " +
                 "Resserre l'affichage du journal de combat dense. 0 = defaut TMP ; en dessous " +
                 "de ~-12 les lignes commencent a se chevaucher selon la taille de police.")]
        [SerializeField] private float _historyLineSpacing = 8f;

        [Header("Tab style")]
        [SerializeField] private Color _tabActiveColor = new Color(0.25f, 0.4f, 0.65f, 1f);
        [SerializeField] private Color _tabInactiveColor = new Color(0.2f, 0.2f, 0.24f, 1f);
        // Couleur du LABEL selon l'état (sinon texte blanc sur onglet actif clair = illisible).
        [SerializeField] private Color _tabActiveTextColor = new Color(0.10f, 0.10f, 0.12f, 1f);
        [SerializeField] private Color _tabInactiveTextColor = new Color(0.78f, 0.80f, 0.85f, 1f);
        // Rayon des coins arrondis des onglets, appliqué au runtime via HubMenuUIFactory.RoundedSprite.
        // Aligné sur HubMenuTheme.CornerRadius (14). Le sprite est généré en mémoire (pas un asset),
        // donc il ne peut pas être baké dans le prefab ChatPanel -> il faut le réappliquer au runtime,
        // sinon les instances combat (qui n'ont jamais vu le restyle éditeur) gardent des coins carrés.
        [SerializeField] private float _tabCornerRadius = 14f;

        [Header("Style barre de saisie (DA menu hub)")]
        [Tooltip("Thème menu hub : style la barre de saisie + bouton Envoyer au runtime (identique hub/combat/replay). Si vide, fallback HubMenuShell.MenuTheme.")]
        [SerializeField] private HubMenuTheme _theme;

        /// <summary>Thème actif : champ sérialisé prioritaire, sinon celui posé par HubMenuShell. Peut être null.</summary>
        private HubMenuTheme ActiveTheme => _theme != null ? _theme : HubMenuShell.MenuTheme;

        private ChatTab _activeTab = ChatTab.Global;
        private string _joinedClanChannel; // "clan:<clanId>" si rejoint, sinon null

        // #9 — Messages non-lus par onglet (MP / Clan). Incrementes a la reception hors onglet
        // actif, remis a 0 au switch sur l'onglet. Affiches en pastille rouge sur le bouton d'onglet.
        private int _unreadPrivate;
        private int _unreadClan;
        private GameObject _privateBadge;
        private TextMeshProUGUI _privateBadgeText;
        private GameObject _clanBadge;
        private TextMeshProUGUI _clanBadgeText;
        private bool _badgesBuilt;

        private void OnEnable()
        {
            if (_sendButton != null) _sendButton.onClick.AddListener(OnSendClicked);
            if (_inputField != null) _inputField.onSubmit.AddListener(OnInputSubmit);
            if (_tabGlobalButton != null) _tabGlobalButton.onClick.AddListener(() => SwitchTab(ChatTab.Global));
            if (_tabPrivateButton != null) _tabPrivateButton.onClick.AddListener(() => SwitchTab(ChatTab.Private));
            if (_tabClanButton != null) _tabClanButton.onClick.AddListener(() => SwitchTab(ChatTab.Clan));
        }

        private void OnDisable()
        {
            if (_sendButton != null) _sendButton.onClick.RemoveListener(OnSendClicked);
            if (_inputField != null) _inputField.onSubmit.RemoveListener(OnInputSubmit);
            if (_tabGlobalButton != null) _tabGlobalButton.onClick.RemoveAllListeners();
            if (_tabPrivateButton != null) _tabPrivateButton.onClick.RemoveAllListeners();
            if (_tabClanButton != null) _tabClanButton.onClick.RemoveAllListeners();
        }

        private void Start()
        {
            // Feed persistant : on n'ajoute la ligne d'accueil qu'au tout premier affichage,
            // sinon elle se duplique à chaque retour dans le hub.
            if (ChatFeed.Global.Count == 0)
                AppendSystemLine(ChatTab.Global, "--- Chat connecting ---");
            // Journal de combat (31 mai) : au retour dans le hub (toute scene hors combat), purge
            // les lignes de combat de la session precedente (poussees dans l'onglet Global pendant
            // le match). On compare le nom de la scene de CE panel (combat = 30/33/40_Combat...).
            if (!gameObject.scene.name.Contains("Combat"))
                ChatFeed.PurgeCombat();
            // Abonnement au journal de combat (relais Core, alimente par CombatChatLogView).
            CombatLogRelay.OnLine += HandleCombatLine;
            EnsureTabSprites();
            UpdateTabStyles();
            EnsureInputBarStyle();
            if (_historyText != null) _historyText.lineSpacing = _historyLineSpacing;
            RefreshHistoryText();
            EnsureHistoryClickHandler();
            EnsureBadges();
            RefreshBadges();
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

            // Canal clan : rejoindre dès qu'on connaît le clan + se ré-abonner aux changements d'état.
            if (HubClanPanel.Instance != null) HubClanPanel.Instance.OnClanStateChanged += TryJoinClanChannel;
            TryJoinClanChannel();
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
            if (HubClanPanel.Instance != null) HubClanPanel.Instance.OnClanStateChanged -= TryJoinClanChannel;
            CombatLogRelay.OnLine -= HandleCombatLine;
        }

        // Journal de combat -> onglet Global (lignes taguees pour purge au retour hub).
        private void HandleCombatLine(string richLine)
        {
            ChatFeed.AppendCombat(richLine);
            if (_activeTab == ChatTab.Global) RefreshHistoryText();
        }

        // Rejoint le canal de chat du clan courant (clan:<clanId>) si on est dans un clan.
        // ⚠️ Nécessite que le backend relaie ce canal aux membres.
        private void TryJoinClanChannel()
        {
            string id = HubClanPanel.Instance != null ? HubClanPanel.Instance.MyClanId : null;
            string ch = string.IsNullOrEmpty(id) ? null : "clan:" + id;
            if (ch == _joinedClanChannel) return;
            _joinedClanChannel = ch;
            if (!string.IsNullOrEmpty(ch) && HubChatClient.Instance != null)
                HubChatClient.Instance.JoinChannel(ch);
        }

        private void HandleConnected()
        {
            AppendSystemLine(ChatTab.Global, "--- Connected ---");
            // FIX chat clan : à chaque (re)connexion, re-souscrire le canal clan courant.
            // JoinChannel no-op si le WS n'est pas connecté, et seul "global" est re-joint
            // automatiquement par HubChatClient -> sans ça, le canal clan rejoint trop tôt
            // (avant connexion) ou perdu sur une reconnexion ne revient jamais (le cache
            // _joinedClanChannel empêchait même un re-join). On reset le cache puis on relance.
            _joinedClanChannel = null;
            TryJoinClanChannel();
        }

        private void HandleDisconnected(string reason)
        {
            AppendSystemLine(ChatTab.Global, $"--- Disconnected: {reason} ---");
        }

        private void HandleMessage(string channel, string from, string text)
        {
            // Routage par canal : global -> onglet Global, canal clan rejoint -> onglet Clan.
            ChatTab tab;
            if (channel == _channel) tab = ChatTab.Global;
            else if (!string.IsNullOrEmpty(_joinedClanChannel) && channel == _joinedClanChannel) tab = ChatTab.Clan;
            else return; // canal non suivi
            // Pas de son ici : la notification est réservée aux MP REÇUS (cf HandleWhisper).
            // POLISH-7 polish (20 mai) : pseudo wrappe dans link cliquable pour ouvrir
            // le menu contextuel chat (MP / Ami / Inviter clan / Signaler).
            AppendLine(tab, $"{WrapPseudoLink(from)}: {text}");
            // #9 — message clan recu hors onglet Clan -> incremente le compteur non-lu (badge rouge).
            // (mes propres messages clan reviennent par le relai mais j'envoie depuis l'onglet Clan
            // actif, donc ils ne sont pas comptes.)
            if (tab == ChatTab.Clan && _activeTab != ChatTab.Clan) { _unreadClan++; RefreshBadges(); }
        }

        private void HandleWhisper(string from, string to, string text)
        {
            // Notification SON : uniquement sur MP REÇU (pas mes propres MP envoyés). Compare au
            // displayName officiel (HubChatClient) plutôt qu'à PlayerProfileBridge.LocalPseudo.
            string me = HubChatClient.Instance != null ? HubChatClient.Instance.MyDisplayName : null;
            bool received = !string.IsNullOrEmpty(from) && from != me;
            if (received)
                Nymora.Core.Audio.NymoraAudioManager.Instance?.PlaySfx(Nymora.Core.Audio.SoundId.MessageReceived);
            // POLISH-7 polish : 2 pseudos cliquables dans la ligne whisper (sender + receiver).
            AppendLine(ChatTab.Private, $"<color=#d8a4ff>[{WrapPseudoLink(from)} → {WrapPseudoLink(to)}]</color> {text}");
            // #9 — MP recu hors onglet Prive -> incremente le compteur non-lu (badge rouge).
            if (received && _activeTab != ChatTab.Private) { _unreadPrivate++; RefreshBadges(); }
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
            else if (_activeTab == ChatTab.Clan)
            {
                if (!string.IsNullOrEmpty(_joinedClanChannel))
                    HubChatClient.Instance.SendChatMessage(_joinedClanChannel, text);
                else
                {
                    AppendSystemLine(ChatTab.Clan, "<color=#ff7777>Rejoins un clan pour discuter ici.</color>");
                    return;
                }
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
            // #9 — ouvrir un onglet marque ses messages comme lus -> reset du compteur + badge.
            if (tab == ChatTab.Private) _unreadPrivate = 0;
            else if (tab == ChatTab.Clan) _unreadClan = 0;
            RefreshBadges();
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
            ApplyTabColor(_tabClanButton, _activeTab == ChatTab.Clan);
        }

        // Pose le sprite arrondi sur les onglets au runtime (le sprite généré en mémoire ne se bake
        // pas dans le prefab ChatPanel). Couvre l'Image propre ET la targetGraphic comme le restyle
        // éditeur, et passe la transition en None pour que ApplyTabColor pilote bien img.color.
        private void EnsureTabSprites()
        {
            RoundTab(_tabGlobalButton);
            RoundTab(_tabPrivateButton);
            RoundTab(_tabClanButton);
        }

        private void RoundTab(Button btn)
        {
            if (btn == null) return;
            btn.transition = Selectable.Transition.None;
            RoundImage(btn.GetComponent<Image>());
            RoundImage(btn.targetGraphic as Image);
        }

        private void RoundImage(Image img)
        {
            if (img == null) return;
            img.sprite = HubMenuUIFactory.RoundedSprite(_tabCornerRadius);
            img.type = Image.Type.Sliced;
        }

        private bool _inputBarStyled;

        /// <summary>Aligne la barre de saisie + le bouton Envoyer sur la DA du chat hub, AU RUNTIME,
        /// pour que ce soit identique dans toutes les scènes (hub/combat/replay) sans dépendre d'un
        /// restyle baké par scène. Mirroir de HubChatRestyleTool (champ de saisie ghost arrondi +
        /// bouton Envoyer accent arrondi + police Ari). No-op si aucun thème disponible.</summary>
        private void EnsureInputBarStyle()
        {
            if (_inputBarStyled) return;
            var t = ActiveTheme;
            if (t == null) return; // pas de thème (ex : boot combat direct en éditeur) -> on garde tel quel

            // --- Champ de saisie : fond ghost arrondi + police Ari + placeholder estompé ---
            if (_inputField != null)
            {
                var bg = _inputField.GetComponent<Image>();
                if (bg != null)
                {
                    bg.color = t.ButtonGhostBg;
                    bg.sprite = HubMenuUIFactory.RoundedSprite(t.CornerRadius);
                    bg.type = Image.Type.Sliced;
                }
                if (_inputField.textComponent != null)
                {
                    if (t.Font != null) _inputField.textComponent.font = t.Font;
                    _inputField.textComponent.color = t.TextPrimary;
                }
                if (_inputField.placeholder is TextMeshProUGUI ph)
                {
                    if (t.Font != null) ph.font = t.Font;
                    ph.color = t.TextMuted;
                    ph.fontStyle = FontStyles.Italic;
                }
            }

            // --- Bouton Envoyer : pilule accent claire arrondie + texte sombre Ari (ColorTint) ---
            if (_sendButton != null)
            {
                var img = _sendButton.GetComponent<Image>();
                if (img != null)
                {
                    img.color = Color.white; // base ; la teinte vient du ColorBlock
                    img.sprite = HubMenuUIFactory.RoundedSprite(t.CornerRadius);
                    img.type = Image.Type.Sliced;
                    _sendButton.targetGraphic = img;
                }
                _sendButton.transition = Selectable.Transition.ColorTint;
                var c = _sendButton.colors;
                c.normalColor = t.Accent;
                c.highlightedColor = Color.Lerp(t.Accent, Color.white, 0.15f);
                c.pressedColor = Color.Lerp(t.Accent, Color.black, 0.10f);
                c.selectedColor = t.Accent;
                c.fadeDuration = 0.1f;
                _sendButton.colors = c;

                var lbl = _sendButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (lbl != null)
                {
                    if (t.Font != null) lbl.font = t.Font;
                    lbl.color = t.TextOnLight;
                }
            }

            _inputBarStyled = true;
        }

        private void ApplyTabColor(Button btn, bool active)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = active ? _tabActiveColor : _tabInactiveColor;
            var lbl = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (lbl != null) lbl.color = active ? _tabActiveTextColor : _tabInactiveTextColor;
        }

        // #9 — Cree (une fois) la pastille de non-lus sur les boutons d'onglet MP et Clan.
        private void EnsureBadges()
        {
            if (_badgesBuilt) return;
            _badgesBuilt = true;
            if (_tabPrivateButton != null) CreateBadge(_tabPrivateButton.transform, out _privateBadge, out _privateBadgeText);
            if (_tabClanButton != null) CreateBadge(_tabClanButton.transform, out _clanBadge, out _clanBadgeText);
        }

        // Pastille rouge ancree en haut-droite du bouton d'onglet, masquee tant que 0 non-lu.
        private static void CreateBadge(Transform parent, out GameObject badge, out TextMeshProUGUI text)
        {
            badge = new GameObject("UnreadBadge", typeof(RectTransform), typeof(Image));
            badge.transform.SetParent(parent, worldPositionStays: false);
            var rt = (RectTransform)badge.transform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(24f, 24f);
            rt.anchoredPosition = new Vector2(-3f, -3f);
            var img = badge.GetComponent<Image>();
            img.color = new Color(0.85f, 0.16f, 0.16f, 1f); // rouge notif
            img.raycastTarget = false;

            var txtGo = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGo.transform.SetParent(badge.transform, worldPositionStays: false);
            var trt = (RectTransform)txtGo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            text = txtGo.GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 14f;
            text.color = Color.white;
            text.raycastTarget = false;
            text.enableWordWrapping = false;

            badge.SetActive(false);
        }

        // #9 — Reflete les compteurs de non-lus sur les pastilles (cap d'affichage a "+9").
        private void RefreshBadges()
        {
            UpdateBadge(_privateBadge, _privateBadgeText, _unreadPrivate);
            UpdateBadge(_clanBadge, _clanBadgeText, _unreadClan);
        }

        private static void UpdateBadge(GameObject badge, TextMeshProUGUI text, int count)
        {
            if (badge == null) return;
            if (count <= 0) { badge.SetActive(false); return; }
            badge.SetActive(true);
            if (text != null) text.text = count > 9 ? "+9" : ("+" + count);
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
            ChatFeed.Append(tab, richLine); // store persistant (survit aux changements de scène)
            if (tab == _activeTab) RefreshHistoryText();
        }

        private void RefreshHistoryText()
        {
            if (_historyText == null) return;
            _historyText.text = string.Join("\n", ChatFeed.For(_activeTab));
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
