using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using Nymora.Core.Data;
using Nymora.Core.SceneFlow;
using UnityEngine;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 4.6 — Client WebSocket Unity pour le chat backend Express + ws.
    /// Brique 4.7 — SendWhisper + event OnWhisperReceived.
    /// Brique 4.8.b — Parse WELCOME (binding sub backend), SendChallenge, events challenge in/out.
    ///
    /// Connection auto au Start avec JWT dev (a generer via `npm run dev:token` cote backend
    /// et coller dans _devToken SerializedField).
    ///
    /// Architecture thread-safe :
    /// - ClientWebSocket.ReceiveAsync sur un background task (ReceiveLoopAsync)
    /// - Push les events dans une ConcurrentQueue
    /// - Update() pop la queue cote main thread et dispatch les C# events
    /// </summary>
    public sealed class HubChatClient : MonoBehaviour
    {
        [Header("Connection")]
        [SerializeField] private string _backendUrl = "ws://localhost:3000";
        [SerializeField, TextArea(2, 4)] private string _devToken;
        [SerializeField] private bool _autoConnect = true;
        [SerializeField] private string _autoJoinChannel = "global";

        [Header("Debug")]
        [SerializeField] private bool _logVerbose = true;

        public static HubChatClient Instance { get; private set; }

        // 4.8.b — Identité officielle du client, set par le message WELCOME du backend.
        public string MyUserId { get; private set; }
        public string MyEmail { get; private set; }
        // POLISH-7 (20 mai) — pseudo officiel (Profile.displayName) push par le backend
        // au WELCOME. Source de verite pour TOUT affichage UI (chat, tooltip, popups).
        // L'email reste expose en MyEmail uniquement pour debug/logs internes — plus jamais affiche.
        public string MyDisplayName { get; private set; }

        // PlayerPrefs key du JWT pose par AuthService apres login/register (00_Login).
        // Doit rester synchro avec AuthService.PlayerPrefsTokenKey.
        private const string AuthTokenPlayerPrefsKey = "nymora.auth.jwt";

        // B6.5 — Token effectif du hub (WS + REST). Cascade :
        //   1. JWT du compte loggé (PlayerPrefs, pose par le flow 00_Login)
        //   2. fallback _devToken Inspector (workflow dev local sans passer par login)
        // Tout le hub passe par ce point unique (chaque panel lit DevToken).
        public string DevToken => ResolveToken();

        private string ResolveToken()
        {
            string authToken = PlayerPrefs.GetString(AuthTokenPlayerPrefsKey, string.Empty);
            return !string.IsNullOrWhiteSpace(authToken) ? authToken : _devToken;
        }

        // POLISH-7 (20 mai) : tous les events portent maintenant un displayName (pseudo) la
        // ou ils portaient un email. L'email reste echange via MyEmail pour debug interne.
        public event Action<string, string, string> OnWelcome;                 // sub, email, displayName
        public event Action<string, string, string> OnMessageReceived; // channel, fromDisplayName, text
        public event Action<string, string, string> OnWhisperReceived; // fromDisplayName, toDisplayName, text
        public event Action<string, string, string> OnIncomingChallenge; // challengeId, fromUserId, fromDisplayName
        public event Action<string, string, string> OnChallengeSent;     // challengeId, toUserId, toDisplayName
        public event Action<string, bool, string, string> OnChallengeResponse; // challengeId, accepted, fromUserId (responder), fromDisplayName
        public event Action<string, string, string> OnMatchReady;              // matchId, opponentSub, opponentDisplayName (4.8.d.i)
        // 6.2 — Matchmaking ranked 1v1
        public event Action<string, string, string> OnRankedMatchFound;        // matchId, opponentSub, opponentDisplayName
        public event Action<int> OnRankedQueueJoined;                          // mmr (ack entree en file)
        public event Action OnRankedQueueLeft;                                 // ack sortie de file
        // 6.3 — MMR mis a jour apres un match ranked settle.
        public event Action<MmrUpdateData> OnMmrUpdated;
        // Joueurs en ligne — compteur de users distincts, push live par le backend
        // (ONLINE_COUNT) au connect/disconnect de n'importe qui. Cache la derniere valeur
        // recue (OnlineCount) pour les vues qui apparaissent apres le push (ex : retour hub).
        public event Action<int> OnOnlineCountChanged;
        public int OnlineCount { get; private set; }

        // S2 spectateur — liste des matchs 1v1 en cours (reponse a LIST_MATCHES). Cache la
        // derniere liste recue (LatestMatches) pour les vues qui s'ouvrent avant le 1er push.
        public event Action<SpectateMatchInfo[]> OnMatchesList;
        public SpectateMatchInfo[] LatestMatches { get; private set; } = new SpectateMatchInfo[0];

        public struct MmrUpdateData
        {
            public int Mmr;
            public int Delta;
            public int RankedGames;
            public int RankedWins;
            public int RankedLosses;
        }
        public event Action<string> OnReportSent;                              // targetDisplayName (4.13)
        public event Action<string, long> OnModerationNotice;                  // kind (reported|muted), muteUntil ms (4.13)
        // 4.10 — Friend events
        public event Action<string, string, string> OnIncomingFriendRequest;     // friendshipId, fromUserId, fromDisplayName
        public event Action<string, string, string> OnFriendRequestSent;         // friendshipId, toUserId, toDisplayName
        public event Action<string, bool, string, string> OnFriendRequestResponse; // friendshipId, accepted, fromUserId, fromDisplayName
        public event Action<string, string> OnFriendRemoved;                     // friendUserId (l'autre), friendDisplayName
        // 4.10.g — Online status events
        public event Action<string[]> OnFriendsOnlineList;                       // friendUserIds[] (au connect)
        public event Action<string> OnFriendOnline;                              // friendUserId (un ami vient online)
        public event Action<string> OnFriendOffline;                             // friendUserId (un ami passe offline)
        // 4.11 — Clan events
        public event Action<string, string, string, string, string, string> OnIncomingClanInvite;
            // inviteId, clanId, clanName, clanBannerColor, fromUserId, fromDisplayName
        public event Action<string, bool, string, string, string> OnClanInviteResponse;
            // inviteId, accepted, clanId, fromUserId, fromDisplayName
        public event Action<string, string, string, string> OnClanMemberJoined;
            // clanId, userId, displayName, role
        public event Action<string, string, string, string> OnClanMemberRoleChanged;
            // clanId, userId, displayName, newRole
        public event Action<string, string, string, string> OnClanMemberLeft;
            // clanId, userId, displayName, reason ('left'|'kicked')
        public event Action<string> OnClanDisbanded;                             // clanId
        // 5.1 — Progression events
        public event Action<XpAwardedData> OnXpAwarded;
        public event Action<string, int> OnClassLevelUp;                         // classId, newLevel

        public struct XpAwardedData
        {
            public string ClassId;
            public int OldLevel;
            public int NewLevel;
            public int OldXp;
            public int NewXp;
            public int Gained;
            public int XpToNext;
            public string Source;
        }

        // 5.2 — Achievement events
        public event Action<string, int, int, int, bool> OnAchievementProgress;  // id, oldProgress, newProgress, target, justUnlocked
        public event Action<string, string, int> OnAchievementUnlocked;          // id, title, points
        // 5.3 — Deck events. Signal generique : le panel doit refetch ses decks.
        // Payload ignore pour simplicite (createdAt/updatedAt/spellIds parsing nested JsonUtility = chiant).
        public event Action OnDeckChanged;
        // 5.4 — Wallet events
        public event Action<WalletUpdateData> OnWalletUpdate;
        public event Action OnConnected;
        public event Action<string> OnDisconnected;

        public struct WalletUpdateData
        {
            public int Nymos;
            public int Shards;
            public string DeltaCurrency;  // "Nymos" | "Shards" | "" si pas de delta
            public int DeltaAmount;       // signe
            public string Reason;
        }

        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private readonly ConcurrentQueue<IncomingEvent> _queue = new ConcurrentQueue<IncomingEvent>();

        public bool IsConnected => _ws != null && _ws.State == WebSocketState.Open;

        private enum EventKind { Connected, Disconnected, Welcome, Message, Whisper, IncomingChallenge, ChallengeSent, ChallengeResponse, MatchReady, RankedMatchFound, RankedQueueJoined, RankedQueueLeft, MmrUpdated, ReportSent, ModerationNotice, IncomingFriendRequest, FriendRequestSent, FriendRequestResponse, FriendRemoved, FriendsOnlineList, FriendOnline, FriendOffline, IncomingClanInvite, ClanInviteResponse, ClanMemberJoined, ClanMemberRoleChanged, ClanMemberLeft, ClanDisbanded, ClanBannerUpdated, XpAwarded, ClassLevelUp, AchievementProgress, AchievementUnlocked, DeckChanged, WalletUpdate, OnlineCount, MatchesList, ForceDisconnect }

        private struct IncomingEvent
        {
            public EventKind Kind;
            public string Channel;
            public string From;
            public string To;
            public string Text;
            public string Reason;
            public string Sub;
            public string Email;
            public string FromEmail;
            public string ToEmail;
            // POLISH-7 (20 mai) — pseudo Profile.displayName push par le backend dans
            // chaque payload sortant. Remplace l'email pour l'affichage UI.
            // FromDisplayName / ToDisplayName sont REUTILISES (deja presents pour friends 4.10) :
            // les events sont mutuellement exclusifs au runtime, pas de collision possible.
            public string DisplayName;       // payload WELCOME
            public string ChallengeId;
            public bool Accepted;
            public string ModerationKind;
            public long MuteUntil;
            // 4.10
            public string FriendshipId;
            public string FromDisplayName;
            public string ToDisplayName;
            public string FriendDisplayName;
            // 4.10.g — online status
            public string[] FriendUserIds;
            // 4.11 — clan (Reason est partage avec Disconnected, c'est OK car events disjoints)
            public string InviteId;
            public string ClanId;
            public string ClanName;
            public string ClanBannerColor;
            public string Role;
            public string NewRole;
            // 5.1 — progression
            public string ClassId;
            public int OldLevel;
            public int NewLevel;
            public int OldXp;
            public int NewXp;
            public int Gained;
            public int XpToNext;
            // 5.2 — achievements
            public string AchievementId;
            public int OldProgress;
            public int NewProgress;
            public int Target;
            public bool Unlocked;
            public string Title;
            public int Points;
            // 5.4 — wallet
            public int Nymos;
            public int Shards;
            public string DeltaCurrency;
            public int DeltaAmount;
            // 6.2 / 6.3 — matchmaking + MMR ranked
            public int Mmr;
            public int MmrDelta;
            public int RankedGames;
            public int RankedWins;
            public int RankedLosses;
            // Joueurs en ligne
            public int OnlineCountValue;
            // S2 spectateur — liste des matchs en cours (MATCHES_LIST)
            public SpectateMatchInfo[] Matches;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Doublon (cas : Lorenzo reload la scene hub apres etre passe en combat).
                // On detruit le GameObject entier — pas juste le component — pour eviter
                // les MonoBehaviour fantomes attaches sans Instance.
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // POLISH-5d (17 mai) : le client WS persiste hub -> combat -> retour hub. La
            // connexion WebSocket reste vivante, l'historique chat est conserve, et la
            // scene combat instancie son propre HubChatUI brancho sur ce singleton.
            DontDestroyOnLoad(gameObject);

            // S3 spectateur — relaie au backend le flux capté par SpectateRelay (Nymora.Combat).
            // Bus statique Core (Combat ne reference pas Hub). Persiste hub<->combat avec ce singleton.
            SpectateRelayBus.OnStart += HandleRelayStart;
            SpectateRelayBus.OnChunk += HandleRelayChunk;
            SpectateRelayBus.OnEnd += HandleRelayEnd;
        }

        private void HandleRelayStart(string matchId, string headerJson) => SendSpectateStart(matchId, headerJson);
        private void HandleRelayChunk(string matchId, int seq, string dataB64) => SendSpectateChunk(matchId, seq, dataB64);
        private void HandleRelayEnd(string matchId) => SendSpectateEnd(matchId);

        private async void Start()
        {
            if (_autoConnect) await ConnectAsync();
        }

        public async Task ConnectAsync()
        {
            if (IsConnected)
            {
                Debug.Log("[ChatClient] Already connected");
                return;
            }
            string token = ResolveToken();
            if (string.IsNullOrWhiteSpace(token))
            {
                Debug.LogError("[ChatClient] Aucun token. Connecte-toi via 00_Login, ou colle un JWT dev dans _devToken (workflow local).");
                return;
            }

            _ws = new ClientWebSocket();
            _cts = new CancellationTokenSource();
            var uri = new Uri($"{_backendUrl}/?token={token}");

            try
            {
                Debug.Log($"[ChatClient] Connecting to {_backendUrl} ...");
                await _ws.ConnectAsync(uri, _cts.Token);
                Debug.Log("[ChatClient] Connected");
                _queue.Enqueue(new IncomingEvent { Kind = EventKind.Connected });
                _ = ReceiveLoopAsync(_cts.Token);

                if (!string.IsNullOrEmpty(_autoJoinChannel))
                {
                    await SendJsonAsync($"{{\"type\":\"JOIN_CHANNEL\",\"channel\":\"{_autoJoinChannel}\"}}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChatClient] ConnectAsync failed: {ex.Message}");
                _queue.Enqueue(new IncomingEvent { Kind = EventKind.Disconnected, Reason = ex.Message });
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            var buffer = new byte[8192];
            var sb = new StringBuilder();
            try
            {
                while (_ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    sb.Clear();
                    WebSocketReceiveResult res;
                    do
                    {
                        res = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                        if (res.MessageType == WebSocketMessageType.Close)
                        {
                            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by server", ct);
                            _queue.Enqueue(new IncomingEvent { Kind = EventKind.Disconnected, Reason = "Server closed" });
                            return;
                        }
                        sb.Append(Encoding.UTF8.GetString(buffer, 0, res.Count));
                    } while (!res.EndOfMessage);

                    var json = sb.ToString();
                    if (_logVerbose) Debug.Log($"[ChatClient] RX: {json}");
                    ParseAndEnqueue(json);
                }
            }
            catch (OperationCanceledException) { /* normal close on shutdown */ }
            catch (Exception ex)
            {
                Debug.LogError($"[ChatClient] ReceiveLoop error: {ex.Message}");
                _queue.Enqueue(new IncomingEvent { Kind = EventKind.Disconnected, Reason = ex.Message });
            }
        }

        private bool _forcedLogoutTriggered;

        /// <summary>
        /// Déconnexion forcée par le serveur (kick / ban / maintenance) : mémorise le
        /// message, ferme proprement le réseau et renvoie à l'écran de login où une
        /// pop-up s'affiche. Tourne sur le thread principal (appelé depuis Update).
        /// </summary>
        private void HandleForceDisconnect(string reason, string message)
        {
            if (_forcedLogoutTriggered) return; // une seule fois
            _forcedLogoutTriggered = true;

            string title =
                reason == "banned" ? "Compte banni" :
                reason == "maintenance" ? "Serveurs fermés" :
                reason == "update" ? "Mise à jour disponible" :
                "Déconnecté";
            if (string.IsNullOrEmpty(message))
                message = "Tu as été déconnecté du serveur.";
            ForcedLogoutNotice.Set(title, message);

            // Efface la session pour ne pas se reconnecter en boucle (re-kick immédiat).
            PlayerPrefs.DeleteKey(AuthTokenPlayerPrefsKey);
            PlayerPrefs.Save();

            // Ferme le runner Fusion (hub) AVANT la transition, puis se détruit (mirror
            // du flow logout). En combat (pas de runner Fusion), l'unload de scène suffit.
            var runner = FindFirstObjectByType<NetworkRunner>();
            SceneTransition.LoadAsync("00_Login", async () =>
            {
                if (runner != null)
                {
                    try { await runner.Shutdown(); }
                    catch (Exception ex) { Debug.LogWarning($"[ChatClient] Shutdown runner échoué : {ex.Message}"); }
                }
                if (Instance != null) Destroy(Instance.gameObject);
            });
        }

        private void ParseAndEnqueue(string json)
        {
            try
            {
                var msg = JsonUtility.FromJson<ServerMessage>(json);
                if (msg == null || string.IsNullOrEmpty(msg.type)) return;
                switch (msg.type)
                {
                    case "FORCE_DISCONNECT":
                        // Kick / ban / maintenance : reason = type, message = texte lisible.
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.ForceDisconnect,
                            Reason = msg.payload?.reason ?? "",
                            Text = msg.payload?.message ?? "",
                        });
                        break;
                    case "WELCOME":
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.Welcome,
                            Sub = msg.payload?.sub ?? "",
                            Email = msg.payload?.email ?? "",
                            // POLISH-7 : displayName officiel du backend ; fallback email.split('@')[0] si vieux backend.
                            DisplayName = !string.IsNullOrEmpty(msg.payload?.displayName)
                                ? msg.payload.displayName
                                : SplitEmailLocal(msg.payload?.email),
                        });
                        break;
                    case "CHANNEL_MESSAGE":
                        // POLISH-7 : From porte le displayName (pseudo) si push par backend ; fallback email.
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.Message,
                            Channel = msg.channel,
                            From = !string.IsNullOrEmpty(msg.payload?.fromDisplayName)
                                ? msg.payload.fromDisplayName
                                : (msg.payload?.from ?? ""),
                            Text = msg.payload?.text ?? "",
                        });
                        break;
                    case "WHISPER_RECEIVED":
                        // POLISH-7 : From/To portent les displayName si pushed ; fallback email.
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.Whisper,
                            From = !string.IsNullOrEmpty(msg.payload?.fromDisplayName)
                                ? msg.payload.fromDisplayName
                                : (msg.payload?.from ?? ""),
                            To = !string.IsNullOrEmpty(msg.payload?.toDisplayName)
                                ? msg.payload.toDisplayName
                                : (msg.payload?.to ?? ""),
                            Text = msg.payload?.text ?? "",
                        });
                        break;
                    case "INCOMING_CHALLENGE":
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.IncomingChallenge,
                            ChallengeId = msg.payload?.challengeId ?? "",
                            From = msg.payload?.from ?? "",
                            FromEmail = msg.payload?.fromEmail ?? "",
                            // POLISH-7 : fallback split email si vieux backend.
                            FromDisplayName = !string.IsNullOrEmpty(msg.payload?.fromDisplayName)
                                ? msg.payload.fromDisplayName
                                : SplitEmailLocal(msg.payload?.fromEmail),
                        });
                        break;
                    case "CHALLENGE_SENT":
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.ChallengeSent,
                            ChallengeId = msg.payload?.challengeId ?? "",
                            To = msg.payload?.to ?? "",
                            ToEmail = msg.payload?.toEmail ?? "",
                            ToDisplayName = !string.IsNullOrEmpty(msg.payload?.toDisplayName)
                                ? msg.payload.toDisplayName
                                : SplitEmailLocal(msg.payload?.toEmail),
                        });
                        break;
                    case "CHALLENGE_RESPONSE":
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.ChallengeResponse,
                            ChallengeId = msg.payload?.challengeId ?? "",
                            Accepted = msg.payload != null && msg.payload.accepted,
                            From = msg.payload?.from ?? "",
                            FromEmail = msg.payload?.fromEmail ?? "",
                            FromDisplayName = !string.IsNullOrEmpty(msg.payload?.fromDisplayName)
                                ? msg.payload.fromDisplayName
                                : SplitEmailLocal(msg.payload?.fromEmail),
                        });
                        break;
                    case "MATCH_READY":
                    {
                        var opps = msg.payload?.opponents;
                        if (opps == null || opps.Length < 2) break;
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.MatchReady,
                            ChallengeId = msg.payload?.matchId ?? "",
                            From = opps[0]?.sub ?? "",
                            FromEmail = opps[0]?.email ?? "",
                            FromDisplayName = !string.IsNullOrEmpty(opps[0]?.displayName)
                                ? opps[0].displayName
                                : SplitEmailLocal(opps[0]?.email),
                            To = opps[1]?.sub ?? "",
                            ToEmail = opps[1]?.email ?? "",
                            ToDisplayName = !string.IsNullOrEmpty(opps[1]?.displayName)
                                ? opps[1].displayName
                                : SplitEmailLocal(opps[1]?.email),
                        });
                        break;
                    }
                    case "RANKED_MATCH_FOUND":
                    {
                        var ropps = msg.payload?.opponents;
                        if (ropps == null || ropps.Length < 2) break;
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.RankedMatchFound,
                            ChallengeId = msg.payload?.matchId ?? "",
                            From = ropps[0]?.sub ?? "",
                            FromEmail = ropps[0]?.email ?? "",
                            FromDisplayName = !string.IsNullOrEmpty(ropps[0]?.displayName)
                                ? ropps[0].displayName
                                : SplitEmailLocal(ropps[0]?.email),
                            To = ropps[1]?.sub ?? "",
                            ToEmail = ropps[1]?.email ?? "",
                            ToDisplayName = !string.IsNullOrEmpty(ropps[1]?.displayName)
                                ? ropps[1].displayName
                                : SplitEmailLocal(ropps[1]?.email),
                        });
                        break;
                    }
                    case "RANKED_QUEUE_JOINED":
                        _queue.Enqueue(new IncomingEvent { Kind = EventKind.RankedQueueJoined, Mmr = msg.payload?.mmr ?? 0 });
                        break;
                    case "RANKED_QUEUE_LEFT":
                        _queue.Enqueue(new IncomingEvent { Kind = EventKind.RankedQueueLeft });
                        break;
                    case "MMR_UPDATED":
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.MmrUpdated,
                            Mmr = msg.payload?.mmr ?? 0,
                            MmrDelta = msg.payload?.mmrDelta ?? 0,
                            RankedGames = msg.payload?.rankedGames ?? 0,
                            RankedWins = msg.payload?.rankedWins ?? 0,
                            RankedLosses = msg.payload?.rankedLosses ?? 0,
                        });
                        break;
                    case "ONLINE_COUNT":
                        _queue.Enqueue(new IncomingEvent { Kind = EventKind.OnlineCount, OnlineCountValue = msg.payload?.count ?? 0 });
                        break;
                    case "MATCHES_LIST":
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.MatchesList,
                            Matches = msg.payload?.matches ?? new SpectateMatchInfo[0],
                        });
                        break;
                    case "REPORT_SENT":
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.ReportSent,
                            ToEmail = msg.payload?.toEmail ?? "",
                            ToDisplayName = !string.IsNullOrEmpty(msg.payload?.toDisplayName)
                                ? msg.payload.toDisplayName
                                : SplitEmailLocal(msg.payload?.toEmail),
                        });
                        break;
                    case "MODERATION_NOTICE":
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.ModerationNotice,
                            ModerationKind = msg.payload?.kind ?? "",
                            MuteUntil = msg.payload?.muteUntil ?? 0L,
                        });
                        break;
                    case "INCOMING_FRIEND_REQUEST":
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.IncomingFriendRequest,
                            FriendshipId = msg.payload?.friendshipId ?? "",
                            From = msg.payload?.fromUserId ?? "",
                            FromDisplayName = msg.payload?.fromDisplayName ?? "",
                        });
                        break;
                    case "FRIEND_REQUEST_SENT":
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.FriendRequestSent,
                            FriendshipId = msg.payload?.friendshipId ?? "",
                            To = msg.payload?.toUserId ?? "",
                            ToDisplayName = msg.payload?.toDisplayName ?? "",
                        });
                        break;
                    case "FRIEND_REQUEST_RESPONSE":
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.FriendRequestResponse,
                            FriendshipId = msg.payload?.friendshipId ?? "",
                            Accepted = msg.payload != null && msg.payload.accepted,
                            From = msg.payload?.fromUserId ?? "",
                            FromDisplayName = msg.payload?.fromDisplayName ?? "",
                        });
                        break;
                    case "FRIEND_REMOVED":
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.FriendRemoved,
                            From = msg.payload?.friendUserId ?? "",
                            FriendDisplayName = msg.payload?.friendDisplayName ?? "",
                        });
                        break;
                    case "FRIENDS_ONLINE_LIST":
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.FriendsOnlineList,
                            FriendUserIds = msg.payload?.friendUserIds ?? new string[0],
                        });
                        break;
                    case "FRIEND_ONLINE":
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.FriendOnline,
                            From = msg.payload?.friendUserId ?? "",
                        });
                        break;
                    case "FRIEND_OFFLINE":
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.FriendOffline,
                            From = msg.payload?.friendUserId ?? "",
                        });
                        break;
                    case "INCOMING_CLAN_INVITE":
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.IncomingClanInvite,
                            InviteId = msg.payload?.inviteId ?? "",
                            ClanId = msg.payload?.clanId ?? "",
                            ClanName = msg.payload?.clanName ?? "",
                            ClanBannerColor = msg.payload?.clanBannerColor ?? "",
                            From = msg.payload?.fromUserId ?? "",
                            FromDisplayName = msg.payload?.fromDisplayName ?? "",
                        });
                        break;
                    case "CLAN_INVITE_RESPONSE":
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.ClanInviteResponse,
                            InviteId = msg.payload?.inviteId ?? "",
                            Accepted = msg.payload != null && msg.payload.accepted,
                            ClanId = msg.payload?.clanId ?? "",
                            From = msg.payload?.fromUserId ?? "",
                            FromDisplayName = msg.payload?.fromDisplayName ?? "",
                        });
                        break;
                    case "CLAN_MEMBER_JOINED":
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.ClanMemberJoined,
                            ClanId = msg.payload?.clanId ?? "",
                            From = msg.payload?.userId ?? "",
                            FromDisplayName = msg.payload?.displayName ?? "",
                            Role = msg.payload?.role ?? "",
                        });
                        break;
                    case "CLAN_MEMBER_ROLE_CHANGED":
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.ClanMemberRoleChanged,
                            ClanId = msg.payload?.clanId ?? "",
                            From = msg.payload?.userId ?? "",
                            FromDisplayName = msg.payload?.displayName ?? "",
                            NewRole = msg.payload?.newRole ?? "",
                        });
                        break;
                    case "CLAN_MEMBER_LEFT":
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.ClanMemberLeft,
                            ClanId = msg.payload?.clanId ?? "",
                            From = msg.payload?.userId ?? "",
                            FromDisplayName = msg.payload?.displayName ?? "",
                            Reason = msg.payload?.reason ?? "",
                        });
                        break;
                    case "CLAN_DISBANDED":
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.ClanDisbanded,
                            ClanId = msg.payload?.clanId ?? "",
                        });
                        break;
                    case "CLAN_BANNER_UPDATED":
                        // 5.12 — Propagation LIVE du bandeau de clan : invalide le cache local
                        // (traité sur le main thread via la file) -> re-fetch au prochain survol.
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.ClanBannerUpdated,
                            ClanId = msg.payload?.clanId ?? "",
                            ClanName = msg.payload?.clanName ?? "",
                        });
                        break;
                    case "XP_AWARDED":
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.XpAwarded,
                            ClassId = msg.payload?.classId ?? "",
                            OldLevel = msg.payload?.oldLevel ?? 0,
                            NewLevel = msg.payload?.newLevel ?? 0,
                            OldXp = msg.payload?.oldXp ?? 0,
                            NewXp = msg.payload?.newXp ?? 0,
                            Gained = msg.payload?.gained ?? 0,
                            XpToNext = msg.payload?.xpToNext ?? 0,
                            Reason = msg.payload?.source ?? "", // reuse Reason field for source
                        });
                        break;
                    case "CLASS_LEVEL_UP":
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.ClassLevelUp,
                            ClassId = msg.payload?.classId ?? "",
                            NewLevel = msg.payload?.newLevel ?? 0,
                        });
                        break;
                    case "ACHIEVEMENT_PROGRESS":
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.AchievementProgress,
                            AchievementId = msg.payload?.achievementId ?? "",
                            OldProgress = msg.payload?.oldProgress ?? 0,
                            NewProgress = msg.payload?.newProgress ?? 0,
                            Target = msg.payload?.target ?? 0,
                            Unlocked = msg.payload != null && msg.payload.unlocked,
                        });
                        break;
                    case "ACHIEVEMENT_UNLOCKED":
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.AchievementUnlocked,
                            AchievementId = msg.payload?.achievementId ?? "",
                            Title = msg.payload?.title ?? "",
                            Points = msg.payload?.points ?? 0,
                        });
                        break;
                    case "DECK_CREATED":
                    case "DECK_UPDATED":
                    case "DECK_DELETED":
                        // 5.3 — signal generique : le panel re-fetch la liste complete
                        // (plus simple que parser le nested DeckDto via JsonUtility).
                        _queue.Enqueue(new IncomingEvent { Kind = EventKind.DeckChanged });
                        break;
                    case "WALLET_UPDATE":
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.WalletUpdate,
                            Nymos = msg.payload?.nymos ?? 0,
                            Shards = msg.payload?.shards ?? 0,
                            DeltaCurrency = msg.payload?.delta?.currency ?? "",
                            DeltaAmount = msg.payload?.delta?.amount ?? 0,
                            Reason = msg.payload?.delta?.reason ?? "",
                        });
                        break;
                    case "ERROR":
                        Debug.LogWarning($"[ChatClient] Server ERROR: {msg.payload?.code}/{msg.payload?.message}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ChatClient] Parse failed: {ex.Message}, json={json}");
            }
        }

        [Serializable]
        private class ServerMessage
        {
            public string type;
            public string channel;
            public Payload payload;
        }

        [Serializable]
        private class Payload
        {
            public string from;
            public string to;
            public string text;
            public string code;
            public string message;
            public string sub;
            public string email;
            public string challengeId;
            public string fromEmail;
            public string toEmail;
            public bool accepted;
            public string matchId;
            public Opponent[] opponents;
            public string kind;     // MODERATION_NOTICE
            public long muteUntil;  // MODERATION_NOTICE
            // 4.10 friends
            public string friendshipId;
            public string fromUserId;
            public string fromDisplayName;
            public string toUserId;
            public string toDisplayName;
            public string friendUserId;
            public string friendDisplayName;
            // 4.10.g — online list
            public string[] friendUserIds;
            // 4.11 — clan
            public string inviteId;
            public string clanId;
            public string clanName;
            public string clanBannerColor;
            public string userId;
            public string displayName;
            public string role;
            public string newRole;
            public string reason;
            // 5.1 — progression
            public string classId;
            public int oldLevel;
            public int newLevel;
            public int oldXp;
            public int newXp;
            public int gained;
            public int xpToNext;
            public string source;
            // 5.2 — achievements
            public string achievementId;
            public int oldProgress;
            public int newProgress;
            public int target;
            public bool unlocked;
            public string title;
            public int points;
            // 5.4 — wallet
            public int nymos;
            public int shards;
            public WalletDeltaPayload delta;
            // 6.2 / 6.3 — matchmaking + MMR ranked
            public int mmr;
            public int mmrDelta;
            public int rankedGames;
            public int rankedWins;
            public int rankedLosses;
            // Joueurs en ligne
            public int count;
            // S2 spectateur — liste des matchs en cours (MATCHES_LIST)
            public SpectateMatchInfo[] matches;
        }

        [Serializable]
        private class WalletDeltaPayload
        {
            public string currency;
            public int amount;
            public string reason;
        }

        [Serializable]
        private class Opponent
        {
            public string sub;
            public string email;
            // POLISH-7 (20 mai) — push par le backend dans MATCH_READY.opponents[]
            public string displayName;
        }

        private void Update()
        {
            while (_queue.TryDequeue(out var ev))
            {
                switch (ev.Kind)
                {
                    case EventKind.Connected:
                        OnConnected?.Invoke();
                        break;
                    case EventKind.Disconnected:
                        OnDisconnected?.Invoke(ev.Reason);
                        break;
                    case EventKind.ForceDisconnect:
                        HandleForceDisconnect(ev.Reason, ev.Text);
                        break;
                    case EventKind.Welcome:
                        MyUserId = ev.Sub;
                        MyEmail = ev.Email;
                        MyDisplayName = ev.DisplayName;
                        Debug.Log($"[ChatClient] WELCOME sub={MyUserId} email={MyEmail} displayName={MyDisplayName}");
                        // POLISH-7 : signature etendue (sub, email, displayName).
                        OnWelcome?.Invoke(MyUserId, MyEmail, MyDisplayName);
                        break;
                    case EventKind.Message:
                        // POLISH-7 : ev.From porte le displayName (cf parse CHANNEL_MESSAGE).
                        OnMessageReceived?.Invoke(ev.Channel, ev.From, ev.Text);
                        break;
                    case EventKind.Whisper:
                        // POLISH-7 : ev.From / ev.To portent les displayName (cf parse WHISPER_RECEIVED).
                        OnWhisperReceived?.Invoke(ev.From, ev.To, ev.Text);
                        break;
                    case EventKind.IncomingChallenge:
                        // POLISH-7 : 3e param = fromDisplayName au lieu de fromEmail.
                        OnIncomingChallenge?.Invoke(ev.ChallengeId, ev.From, ev.FromDisplayName);
                        break;
                    case EventKind.ChallengeSent:
                        OnChallengeSent?.Invoke(ev.ChallengeId, ev.To, ev.ToDisplayName);
                        break;
                    case EventKind.ChallengeResponse:
                        OnChallengeResponse?.Invoke(ev.ChallengeId, ev.Accepted, ev.From, ev.FromDisplayName);
                        break;
                    case EventKind.MatchReady:
                    {
                        // Resoudre l'opponent : celui dont le sub != MyUserId
                        bool localIsFirst = (ev.From == MyUserId);
                        string oppSub = localIsFirst ? ev.To : ev.From;
                        string oppDisplayName = localIsFirst ? ev.ToDisplayName : ev.FromDisplayName;
                        string oppEmailLog = localIsFirst ? ev.ToEmail : ev.FromEmail;
                        Debug.Log($"[ChatClient] MATCH_READY matchId={ev.ChallengeId} opponent={oppDisplayName} (email={oppEmailLog} sub={oppSub})");
                        // POLISH-7 : 3e param = opponentDisplayName au lieu d'opponentEmail.
                        OnMatchReady?.Invoke(ev.ChallengeId, oppSub, oppDisplayName);
                        break;
                    }
                    case EventKind.RankedMatchFound:
                    {
                        // Resoudre l'opponent : celui dont le sub != MyUserId (mirror MatchReady).
                        bool localIsFirst = (ev.From == MyUserId);
                        string oppSub = localIsFirst ? ev.To : ev.From;
                        string oppDisplayName = localIsFirst ? ev.ToDisplayName : ev.FromDisplayName;
                        Debug.Log($"[ChatClient] RANKED_MATCH_FOUND matchId={ev.ChallengeId} opponent={oppDisplayName} sub={oppSub}");
                        OnRankedMatchFound?.Invoke(ev.ChallengeId, oppSub, oppDisplayName);
                        break;
                    }
                    case EventKind.RankedQueueJoined:
                        OnRankedQueueJoined?.Invoke(ev.Mmr);
                        break;
                    case EventKind.RankedQueueLeft:
                        OnRankedQueueLeft?.Invoke();
                        break;
                    case EventKind.MmrUpdated:
                        OnMmrUpdated?.Invoke(new MmrUpdateData
                        {
                            Mmr = ev.Mmr,
                            Delta = ev.MmrDelta,
                            RankedGames = ev.RankedGames,
                            RankedWins = ev.RankedWins,
                            RankedLosses = ev.RankedLosses,
                        });
                        break;
                    case EventKind.OnlineCount:
                        OnlineCount = ev.OnlineCountValue;
                        OnOnlineCountChanged?.Invoke(OnlineCount);
                        break;
                    case EventKind.MatchesList:
                        LatestMatches = ev.Matches ?? new SpectateMatchInfo[0];
                        OnMatchesList?.Invoke(LatestMatches);
                        break;
                    case EventKind.ReportSent:
                        // POLISH-7 : param = toDisplayName au lieu de toEmail.
                        OnReportSent?.Invoke(ev.ToDisplayName);
                        break;
                    case EventKind.ModerationNotice:
                        OnModerationNotice?.Invoke(ev.ModerationKind, ev.MuteUntil);
                        break;
                    case EventKind.IncomingFriendRequest:
                        OnIncomingFriendRequest?.Invoke(ev.FriendshipId, ev.From, ev.FromDisplayName);
                        break;
                    case EventKind.FriendRequestSent:
                        OnFriendRequestSent?.Invoke(ev.FriendshipId, ev.To, ev.ToDisplayName);
                        break;
                    case EventKind.FriendRequestResponse:
                        OnFriendRequestResponse?.Invoke(ev.FriendshipId, ev.Accepted, ev.From, ev.FromDisplayName);
                        break;
                    case EventKind.FriendRemoved:
                        OnFriendRemoved?.Invoke(ev.From, ev.FriendDisplayName);
                        break;
                    case EventKind.FriendsOnlineList:
                        OnFriendsOnlineList?.Invoke(ev.FriendUserIds ?? new string[0]);
                        break;
                    case EventKind.FriendOnline:
                        OnFriendOnline?.Invoke(ev.From);
                        break;
                    case EventKind.FriendOffline:
                        OnFriendOffline?.Invoke(ev.From);
                        break;
                    case EventKind.IncomingClanInvite:
                        OnIncomingClanInvite?.Invoke(ev.InviteId, ev.ClanId, ev.ClanName, ev.ClanBannerColor, ev.From, ev.FromDisplayName);
                        break;
                    case EventKind.ClanInviteResponse:
                        OnClanInviteResponse?.Invoke(ev.InviteId, ev.Accepted, ev.ClanId, ev.From, ev.FromDisplayName);
                        break;
                    case EventKind.ClanMemberJoined:
                        OnClanMemberJoined?.Invoke(ev.ClanId, ev.From, ev.FromDisplayName, ev.Role);
                        break;
                    case EventKind.ClanMemberRoleChanged:
                        OnClanMemberRoleChanged?.Invoke(ev.ClanId, ev.From, ev.FromDisplayName, ev.NewRole);
                        break;
                    case EventKind.ClanMemberLeft:
                        OnClanMemberLeft?.Invoke(ev.ClanId, ev.From, ev.FromDisplayName, ev.Reason);
                        break;
                    case EventKind.ClanDisbanded:
                        OnClanDisbanded?.Invoke(ev.ClanId);
                        break;
                    case EventKind.ClanBannerUpdated:
                        // 5.12 — Bandeau de clan modifié par le chef : vide le cache du tooltip pour
                        // ce clan -> le prochain survol d'un de ses membres refetch la config à jour
                        // (pas de relog). Main thread : safe pour le Dictionary statique.
                        HubAvatarHoverTooltip.InvalidateClanBanner(ev.ClanName);
                        break;
                    case EventKind.XpAwarded:
                        OnXpAwarded?.Invoke(new XpAwardedData
                        {
                            ClassId = ev.ClassId,
                            OldLevel = ev.OldLevel,
                            NewLevel = ev.NewLevel,
                            OldXp = ev.OldXp,
                            NewXp = ev.NewXp,
                            Gained = ev.Gained,
                            XpToNext = ev.XpToNext,
                            Source = ev.Reason,
                        });
                        break;
                    case EventKind.ClassLevelUp:
                        OnClassLevelUp?.Invoke(ev.ClassId, ev.NewLevel);
                        break;
                    case EventKind.AchievementProgress:
                        OnAchievementProgress?.Invoke(ev.AchievementId, ev.OldProgress, ev.NewProgress, ev.Target, ev.Unlocked);
                        break;
                    case EventKind.AchievementUnlocked:
                        OnAchievementUnlocked?.Invoke(ev.AchievementId, ev.Title, ev.Points);
                        break;
                    case EventKind.DeckChanged:
                        OnDeckChanged?.Invoke();
                        break;
                    case EventKind.WalletUpdate:
                        OnWalletUpdate?.Invoke(new WalletUpdateData
                        {
                            Nymos = ev.Nymos,
                            Shards = ev.Shards,
                            DeltaCurrency = ev.DeltaCurrency,
                            DeltaAmount = ev.DeltaAmount,
                            Reason = ev.Reason,
                        });
                        break;
                }
            }
        }

        public async void SendChatMessage(string channel, string text)
        {
            if (!IsConnected || string.IsNullOrWhiteSpace(text)) return;
            string escaped = EscapeJsonString(text);
            string json = $"{{\"type\":\"SEND_MESSAGE\",\"channel\":\"{channel}\",\"payload\":{{\"text\":\"{escaped}\"}}}}";
            await SendJsonAsync(json);
        }

        /// <summary>Rejoint un canal de chat (ex : "global", "clan:&lt;clanId&gt;"). Nécessite que le
        /// backend relaie ce canal aux membres qui l'ont rejoint.</summary>
        public async void JoinChannel(string channel)
        {
            if (!IsConnected || string.IsNullOrWhiteSpace(channel)) return;
            string escaped = EscapeJsonString(channel);
            await SendJsonAsync($"{{\"type\":\"JOIN_CHANNEL\",\"channel\":\"{escaped}\"}}");
        }

        public async void SendWhisper(string targetUser, string text)
        {
            if (!IsConnected || string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(targetUser)) return;
            string escapedTarget = EscapeJsonString(targetUser);
            string escapedText = EscapeJsonString(text);
            string json = $"{{\"type\":\"SEND_WHISPER\",\"payload\":{{\"targetUser\":\"{escapedTarget}\",\"text\":\"{escapedText}\"}}}}";
            await SendJsonAsync(json);
        }

        public async void SendChallenge(string targetUser)
        {
            if (!IsConnected || string.IsNullOrWhiteSpace(targetUser)) return;
            string escapedTarget = EscapeJsonString(targetUser);
            string json = $"{{\"type\":\"SEND_CHALLENGE\",\"payload\":{{\"targetUser\":\"{escapedTarget}\"}}}}";
            await SendJsonAsync(json);
        }

        public async void SendChallengeResponse(string challengeId, bool accepted)
        {
            if (!IsConnected || string.IsNullOrWhiteSpace(challengeId)) return;
            string escapedId = EscapeJsonString(challengeId);
            string acceptedStr = accepted ? "true" : "false";
            string json = $"{{\"type\":\"RESPOND_CHALLENGE\",\"payload\":{{\"challengeId\":\"{escapedId}\",\"accepted\":{acceptedStr}}}}}";
            await SendJsonAsync(json);
        }

        // ====== Brique 6.2 — Matchmaking ranked 1v1 ======

        /// <summary>Rejoint la file de matchmaking ranked 1v1. Le MMR est lu cote serveur (Profile.mmr).</summary>
        public async void SendEnqueueRanked()
        {
            if (!IsConnected) return;
            await SendJsonAsync("{\"type\":\"ENQUEUE_RANKED\"}");
        }

        /// <summary>Quitte la file de matchmaking ranked 1v1.</summary>
        public async void SendDequeueRanked()
        {
            if (!IsConnected) return;
            await SendJsonAsync("{\"type\":\"DEQUEUE_RANKED\"}");
        }

        // ====== Brique S2 — Spectateur : liste des matchs en cours ======

        /// <summary>Demande la liste des matchs 1v1 en cours (casual + ranked). Reponse via OnMatchesList.</summary>
        public async void SendListMatches()
        {
            if (!IsConnected) return;
            await SendJsonAsync("{\"type\":\"LIST_MATCHES\"}");
        }

        // ====== Brique S3 — Spectateur : relay du flux (cote RELAYER, depuis SpectateRelayBus) ======

        /// <summary>Ouvre le flux spectateur + envoie le header (config Quantum). headerJson = JSON
        /// deja serialise (injecte brut comme objet payload.header).</summary>
        public async void SendSpectateStart(string matchId, string headerJson)
        {
            if (!IsConnected || string.IsNullOrEmpty(matchId) || string.IsNullOrEmpty(headerJson)) return;
            string json = $"{{\"type\":\"SPECTATE_START\",\"payload\":{{\"matchId\":\"{matchId}\",\"header\":{headerJson}}}}}";
            await SendJsonAsync(json);
        }

        /// <summary>Pousse un bloc d'octets (base64) du RecordInputStream.</summary>
        public async void SendSpectateChunk(string matchId, int seq, string dataB64)
        {
            if (!IsConnected || string.IsNullOrEmpty(matchId) || string.IsNullOrEmpty(dataB64)) return;
            string json = $"{{\"type\":\"SPECTATE_CHUNK\",\"payload\":{{\"matchId\":\"{matchId}\",\"seq\":{seq},\"data\":\"{dataB64}\"}}}}";
            await SendJsonAsync(json);
        }

        /// <summary>Clot le flux spectateur (match termine / relayer detruit).</summary>
        public async void SendSpectateEnd(string matchId)
        {
            if (!IsConnected || string.IsNullOrEmpty(matchId)) return;
            string json = $"{{\"type\":\"SPECTATE_END\",\"payload\":{{\"matchId\":\"{matchId}\"}}}}";
            await SendJsonAsync(json);
        }

        public async void SendReport(string targetUser)
        {
            if (!IsConnected || string.IsNullOrWhiteSpace(targetUser)) return;
            string escapedTarget = EscapeJsonString(targetUser);
            string json = $"{{\"type\":\"REPORT_USER\",\"payload\":{{\"targetUser\":\"{escapedTarget}\"}}}}";
            await SendJsonAsync(json);
        }

        // ====== Brique 4.10 — Amis ======

        public async void SendFriendRequest(string targetDisplayName)
        {
            if (!IsConnected || string.IsNullOrWhiteSpace(targetDisplayName)) return;
            string escaped = EscapeJsonString(targetDisplayName);
            string json = $"{{\"type\":\"SEND_FRIEND_REQUEST\",\"payload\":{{\"targetUser\":\"{escaped}\"}}}}";
            await SendJsonAsync(json);
        }

        /// <summary>4.10 — Variante par UUID, utilisée par ChallengePopup (clic avatar remote, on a NetSub).</summary>
        public async void SendFriendRequestByUserId(string targetUserId)
        {
            if (!IsConnected || string.IsNullOrWhiteSpace(targetUserId)) return;
            string escaped = EscapeJsonString(targetUserId);
            string json = $"{{\"type\":\"SEND_FRIEND_REQUEST\",\"payload\":{{\"targetUserId\":\"{escaped}\"}}}}";
            await SendJsonAsync(json);
        }

        public async void RespondFriendRequest(string friendshipId, bool accepted)
        {
            if (!IsConnected || string.IsNullOrWhiteSpace(friendshipId)) return;
            string escaped = EscapeJsonString(friendshipId);
            string acceptedStr = accepted ? "true" : "false";
            string json = $"{{\"type\":\"RESPOND_FRIEND_REQUEST\",\"payload\":{{\"friendshipId\":\"{escaped}\",\"accepted\":{acceptedStr}}}}}";
            await SendJsonAsync(json);
        }

        public async void RemoveFriend(string friendUserId)
        {
            if (!IsConnected || string.IsNullOrWhiteSpace(friendUserId)) return;
            string escaped = EscapeJsonString(friendUserId);
            string json = $"{{\"type\":\"REMOVE_FRIEND\",\"payload\":{{\"friendUserId\":\"{escaped}\"}}}}";
            await SendJsonAsync(json);
        }

        private static string EscapeJsonString(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
        }

        // POLISH-7 (20 mai) — fallback uniquement si le backend (vieux build) n'envoie pas
        // de displayName : on prend l'email avant '@'. Cote backend post-POLISH-7.a, ce
        // fallback ne devrait jamais s'activer.
        private static string SplitEmailLocal(string email)
        {
            if (string.IsNullOrEmpty(email)) return "";
            int atIdx = email.IndexOf('@');
            return atIdx > 0 ? email.Substring(0, atIdx) : email;
        }

        private async Task SendJsonAsync(string json)
        {
            if (_ws == null || _ws.State != WebSocketState.Open) return;
            var bytes = Encoding.UTF8.GetBytes(json);
            try
            {
                await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
                if (_logVerbose) Debug.Log($"[ChatClient] TX: {json}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChatClient] SendAsync failed: {ex.Message}");
            }
        }

        private async void OnDestroy()
        {
            // S3 spectateur — désabonnement du bus (handlers liés à CETTE instance ; pas d'impact
            // sur l'instance survivante en cas de doublon, les délégués diffèrent par `this`).
            SpectateRelayBus.OnStart -= HandleRelayStart;
            SpectateRelayBus.OnChunk -= HandleRelayChunk;
            SpectateRelayBus.OnEnd -= HandleRelayEnd;

            if (Instance == this) Instance = null;
            try
            {
                _cts?.Cancel();
                if (_ws != null && _ws.State == WebSocketState.Open)
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client destroyed", CancellationToken.None);
                }
            }
            catch
            {
                // ignore exceptions during teardown
            }
            _ws?.Dispose();
            _cts?.Dispose();
        }
    }
}
