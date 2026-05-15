using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

        // 4.12 — Expose le JWT dev pour reutilisation par HubProfilePanel (REST /profile/me).
        // En dev local, Lorenzo colle un seul devToken ici et tout le hub (WS + REST) l'utilise.
        // Quand le flow login 00_Login sera systematise, basculer sur AuthService + PlayerPrefs.
        public string DevToken => _devToken;

        public event Action<string, string> OnWelcome;                 // sub, email
        public event Action<string, string, string> OnMessageReceived; // channel, from, text
        public event Action<string, string, string> OnWhisperReceived; // from, to, text
        public event Action<string, string, string> OnIncomingChallenge; // challengeId, fromUserId, fromEmail
        public event Action<string, string, string> OnChallengeSent;     // challengeId, toUserId, toEmail
        public event Action<string, bool, string, string> OnChallengeResponse; // challengeId, accepted, fromUserId (responder), fromEmail
        public event Action<string, string, string> OnMatchReady;              // matchId, opponentSub, opponentEmail (4.8.d.i)
        public event Action<string> OnReportSent;                              // targetEmail (4.13)
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
        public event Action OnConnected;
        public event Action<string> OnDisconnected;

        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private readonly ConcurrentQueue<IncomingEvent> _queue = new ConcurrentQueue<IncomingEvent>();

        public bool IsConnected => _ws != null && _ws.State == WebSocketState.Open;

        private enum EventKind { Connected, Disconnected, Welcome, Message, Whisper, IncomingChallenge, ChallengeSent, ChallengeResponse, MatchReady, ReportSent, ModerationNotice, IncomingFriendRequest, FriendRequestSent, FriendRequestResponse, FriendRemoved, FriendsOnlineList, FriendOnline, FriendOffline, IncomingClanInvite, ClanInviteResponse, ClanMemberJoined, ClanMemberRoleChanged, ClanMemberLeft, ClanDisbanded, XpAwarded, ClassLevelUp, AchievementProgress, AchievementUnlocked }

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
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

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
            if (string.IsNullOrWhiteSpace(_devToken))
            {
                Debug.LogError("[ChatClient] _devToken vide. Lance 'npm run dev:token' cote backend et colle le JWT dans le SerializedField.");
                return;
            }

            _ws = new ClientWebSocket();
            _cts = new CancellationTokenSource();
            var uri = new Uri($"{_backendUrl}/?token={_devToken}");

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

        private void ParseAndEnqueue(string json)
        {
            try
            {
                var msg = JsonUtility.FromJson<ServerMessage>(json);
                if (msg == null || string.IsNullOrEmpty(msg.type)) return;
                switch (msg.type)
                {
                    case "WELCOME":
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.Welcome,
                            Sub = msg.payload?.sub ?? "",
                            Email = msg.payload?.email ?? "",
                        });
                        break;
                    case "CHANNEL_MESSAGE":
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.Message,
                            Channel = msg.channel,
                            From = msg.payload?.from ?? "",
                            Text = msg.payload?.text ?? "",
                        });
                        break;
                    case "WHISPER_RECEIVED":
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.Whisper,
                            From = msg.payload?.from ?? "",
                            To = msg.payload?.to ?? "",
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
                        });
                        break;
                    case "CHALLENGE_SENT":
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.ChallengeSent,
                            ChallengeId = msg.payload?.challengeId ?? "",
                            To = msg.payload?.to ?? "",
                            ToEmail = msg.payload?.toEmail ?? "",
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
                            To = opps[1]?.sub ?? "",
                            ToEmail = opps[1]?.email ?? "",
                        });
                        break;
                    }
                    case "REPORT_SENT":
                        _queue.Enqueue(new IncomingEvent
                        {
                            Kind = EventKind.ReportSent,
                            ToEmail = msg.payload?.toEmail ?? "",
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
        }

        [Serializable]
        private class Opponent
        {
            public string sub;
            public string email;
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
                    case EventKind.Welcome:
                        MyUserId = ev.Sub;
                        MyEmail = ev.Email;
                        Debug.Log($"[ChatClient] WELCOME sub={MyUserId} email={MyEmail}");
                        OnWelcome?.Invoke(MyUserId, MyEmail);
                        break;
                    case EventKind.Message:
                        OnMessageReceived?.Invoke(ev.Channel, ev.From, ev.Text);
                        break;
                    case EventKind.Whisper:
                        OnWhisperReceived?.Invoke(ev.From, ev.To, ev.Text);
                        break;
                    case EventKind.IncomingChallenge:
                        OnIncomingChallenge?.Invoke(ev.ChallengeId, ev.From, ev.FromEmail);
                        break;
                    case EventKind.ChallengeSent:
                        OnChallengeSent?.Invoke(ev.ChallengeId, ev.To, ev.ToEmail);
                        break;
                    case EventKind.ChallengeResponse:
                        OnChallengeResponse?.Invoke(ev.ChallengeId, ev.Accepted, ev.From, ev.FromEmail);
                        break;
                    case EventKind.MatchReady:
                    {
                        // Resoudre l'opponent : celui dont le sub != MyUserId
                        string oppSub = ev.From == MyUserId ? ev.To : ev.From;
                        string oppEmail = ev.From == MyUserId ? ev.ToEmail : ev.FromEmail;
                        Debug.Log($"[ChatClient] MATCH_READY matchId={ev.ChallengeId} opponent={oppEmail}");
                        OnMatchReady?.Invoke(ev.ChallengeId, oppSub, oppEmail);
                        break;
                    }
                    case EventKind.ReportSent:
                        OnReportSent?.Invoke(ev.ToEmail);
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
