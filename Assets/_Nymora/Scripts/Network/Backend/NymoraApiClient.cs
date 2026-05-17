using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nymora.Core.Data;
using UnityEngine;
using UnityEngine.Networking;

namespace Nymora.Network.Backend
{
    /// <summary>Header HTTP qui transporte la version du client. Lu par versionGuard cote backend.</summary>
    internal static class HttpHeaders
    {
        public const string ClientVersion = "X-Nymora-Client-Version";
    }

    /// <summary>
    /// Resultat unifie d'un appel API : succes (data + status) ou echec (status + message).
    /// </summary>
    public readonly struct ApiResult<T>
    {
        public bool IsSuccess { get; }
        public T Data { get; }
        public int StatusCode { get; }
        public string ErrorMessage { get; }

        private ApiResult(bool ok, T data, int status, string err)
        {
            IsSuccess = ok;
            Data = data;
            StatusCode = status;
            ErrorMessage = err;
        }

        public static ApiResult<T> Success(T data, int status) => new ApiResult<T>(true, data, status, null);
        public static ApiResult<T> Failure(int status, string err) => new ApiResult<T>(false, default, status, err);
    }

    /// <summary>
    /// Client HTTP bas niveau pour le backend Nymora.
    /// Wrap UnityWebRequest, serialise/deserialise via JsonUtility natif Unity, async via UniTask.
    ///
    /// Convention : les methodes publiques retournent ApiResult&lt;T&gt; (jamais d'exception en sortie nominale).
    /// Une exception non-geree signale un bug ou un probleme inattendu (network down sans timeout, etc.).
    /// </summary>
    public class NymoraApiClient
    {
        private readonly NymoraBackendSettings _settings;
        private string _bearerToken;

        public NymoraApiClient(NymoraBackendSettings settings)
        {
            _settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
        }

        public bool HasBearerToken => !string.IsNullOrEmpty(_bearerToken);

        public void SetBearerToken(string token) => _bearerToken = token;

        public void ClearBearerToken() => _bearerToken = null;

        public UniTask<ApiResult<AuthResponse>> RegisterAsync(RegisterRequest body, CancellationToken ct = default)
            => PostJsonAsync<AuthResponse>("/auth/register", body, requireAuth: false, ct);

        public UniTask<ApiResult<AuthResponse>> LoginAsync(LoginRequest body, CancellationToken ct = default)
            => PostJsonAsync<AuthResponse>("/auth/login", body, requireAuth: false, ct);

        public UniTask<ApiResult<MeResponse>> GetMeAsync(CancellationToken ct = default)
            => GetJsonAsync<MeResponse>("/auth/me", requireAuth: true, ct);

        public UniTask<ApiResult<ProfileMeResponse>> GetProfileMeAsync(CancellationToken ct = default)
            => GetJsonAsync<ProfileMeResponse>("/profile/me", requireAuth: true, ct);

        // ====== Brique 4.10 — Amis ======

        public UniTask<ApiResult<FriendsListResponse>> GetFriendsAsync(CancellationToken ct = default)
            => GetJsonAsync<FriendsListResponse>("/friends", requireAuth: true, ct);

        public UniTask<ApiResult<FriendRequestsResponse>> GetFriendRequestsAsync(CancellationToken ct = default)
            => GetJsonAsync<FriendRequestsResponse>("/friends/requests", requireAuth: true, ct);

        public UniTask<ApiResult<FriendRequestCreatedResponse>> SendFriendRequestAsync(string targetDisplayName, CancellationToken ct = default)
            => PostJsonAsync<FriendRequestCreatedResponse>("/friends/request",
                new FriendRequestBody { targetDisplayName = targetDisplayName }, requireAuth: true, ct);

        public UniTask<ApiResult<FriendRespondResponse>> RespondFriendRequestAsync(string friendshipId, bool accepted, CancellationToken ct = default)
            => PostJsonAsync<FriendRespondResponse>("/friends/respond",
                new FriendRespondBody { friendshipId = friendshipId, accepted = accepted }, requireAuth: true, ct);

        /// <summary>DELETE /friends/:friendUserId. Retourne ApiResult avec status code 204 si OK.</summary>
        public async UniTask<ApiResult<EmptyResponse>> RemoveFriendAsync(string friendUserId, CancellationToken ct = default)
        {
            using var req = UnityWebRequest.Delete($"{_settings.BaseUrl}/friends/{friendUserId}");
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = _settings.TimeoutSeconds;
            ApplyAuth(req, requireAuth: true);
            ApplyClientVersion(req);

            try
            {
                await req.SendWebRequest().WithCancellation(ct);
            }
            catch (OperationCanceledException)
            {
                return ApiResult<EmptyResponse>.Failure(0, "request cancelled");
            }
            catch (UnityWebRequestException e)
            {
                int status = (int)e.ResponseCode;
                string err = e.Message;
                return ApiResult<EmptyResponse>.Failure(status, err);
            }

            int code = (int)req.responseCode;
            // 204 No Content -> succes sans body
            return ApiResult<EmptyResponse>.Success(new EmptyResponse(), code);
        }

        // ====== Brique 4.11 — Clans ======

        public UniTask<ApiResult<ClanDto>> CreateClanAsync(string name, string description = null, string bannerColor = null, CancellationToken ct = default)
            => PostJsonAsync<ClanDto>("/clans",
                new CreateClanBody { name = name, description = description, bannerColor = bannerColor },
                requireAuth: true, ct);

        public UniTask<ApiResult<ClanDto>> GetMyClanAsync(CancellationToken ct = default)
            => GetJsonAsync<ClanDto>("/clans/me", requireAuth: true, ct);

        public UniTask<ApiResult<ClanDto>> GetClanByIdAsync(string clanId, CancellationToken ct = default)
            => GetJsonAsync<ClanDto>($"/clans/{clanId}", requireAuth: true, ct);

        public UniTask<ApiResult<ClanInvitesListResponse>> GetClanInvitesAsync(CancellationToken ct = default)
            => GetJsonAsync<ClanInvitesListResponse>("/clans/invites/list", requireAuth: true, ct);

        public UniTask<ApiResult<ClanInviteCreatedResponse>> InviteToClanByDisplayNameAsync(string targetDisplayName, CancellationToken ct = default)
            => PostJsonAsync<ClanInviteCreatedResponse>("/clans/invite",
                new ClanInviteBody { targetDisplayName = targetDisplayName }, requireAuth: true, ct);

        public UniTask<ApiResult<ClanInviteCreatedResponse>> InviteToClanByUserIdAsync(string targetUserId, CancellationToken ct = default)
            => PostJsonAsync<ClanInviteCreatedResponse>("/clans/invite",
                new ClanInviteBody { targetUserId = targetUserId }, requireAuth: true, ct);

        public UniTask<ApiResult<ClanRespondResponse>> RespondClanInviteAsync(string inviteId, bool accepted, CancellationToken ct = default)
            => PostJsonAsync<ClanRespondResponse>($"/clans/invites/{inviteId}/respond",
                new ClanRespondBody { accepted = accepted }, requireAuth: true, ct);

        public UniTask<ApiResult<ClanGenericOkResponse>> PromoteClanMemberAsync(string targetUserId, string newRole, CancellationToken ct = default)
            => PostJsonAsync<ClanGenericOkResponse>("/clans/me/promote",
                new ClanPromoteBody { targetUserId = targetUserId, newRole = newRole }, requireAuth: true, ct);

        public UniTask<ApiResult<ClanGenericOkResponse>> KickClanMemberAsync(string targetUserId, CancellationToken ct = default)
            => PostJsonAsync<ClanGenericOkResponse>("/clans/me/kick",
                new ClanKickBody { targetUserId = targetUserId }, requireAuth: true, ct);

        public UniTask<ApiResult<ClanLeaveResponse>> LeaveClanAsync(CancellationToken ct = default)
            => PostJsonAsync<ClanLeaveResponse>("/clans/me/leave", new EmptyResponse(), requireAuth: true, ct);

        // ====== Brique 5.1 — Progression ======

        public UniTask<ApiResult<ProgressionMeResponse>> GetProgressionMeAsync(CancellationToken ct = default)
            => GetJsonAsync<ProgressionMeResponse>("/progression/me", requireAuth: true, ct);

        public UniTask<ApiResult<AwardXpResponse>> AwardXpAsync(string classId, int amount, string source = null, CancellationToken ct = default)
            => PostJsonAsync<AwardXpResponse>("/progression/award-xp",
                new AwardXpBody { classId = classId, amount = amount, source = source },
                requireAuth: true, ct);

        // ====== Brique 5.2 — Succès ======

        public UniTask<ApiResult<AchievementCatalogResponse>> GetAchievementsCatalogAsync(CancellationToken ct = default)
            => GetJsonAsync<AchievementCatalogResponse>("/achievements/catalog", requireAuth: true, ct);

        public UniTask<ApiResult<AchievementsMeResponse>> GetAchievementsMeAsync(CancellationToken ct = default)
            => GetJsonAsync<AchievementsMeResponse>("/achievements/me", requireAuth: true, ct);

        // ====== Brique 5.3 — Deck Builder ======

        /// <summary>GET /decks ou /decks?classId=Soulrender. Si classId null, retourne tous decks user.</summary>
        public UniTask<ApiResult<DecksListResponse>> GetDecksAsync(string classId = null, CancellationToken ct = default)
        {
            string path = string.IsNullOrEmpty(classId) ? "/decks" : $"/decks?classId={UnityWebRequest.EscapeURL(classId)}";
            return GetJsonAsync<DecksListResponse>(path, requireAuth: true, ct);
        }

        public UniTask<ApiResult<DeckCreatedResponse>> CreateDeckAsync(string classId, string name, string[] spellIds, CancellationToken ct = default)
            => PostJsonAsync<DeckCreatedResponse>("/decks",
                new CreateDeckBody { classId = classId, name = name, spellIds = spellIds },
                requireAuth: true, ct);

        public UniTask<ApiResult<DeckUpdatedResponse>> UpdateDeckAsync(string deckId, string name, string[] spellIds, CancellationToken ct = default)
            => PutJsonAsync<DeckUpdatedResponse>($"/decks/{deckId}",
                new UpdateDeckBody { name = name, spellIds = spellIds },
                requireAuth: true, ct);

        /// <summary>DELETE /decks/:id. Retourne ApiResult avec status code 204 si OK.</summary>
        public async UniTask<ApiResult<EmptyResponse>> DeleteDeckAsync(string deckId, CancellationToken ct = default)
        {
            using var req = UnityWebRequest.Delete($"{_settings.BaseUrl}/decks/{deckId}");
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = _settings.TimeoutSeconds;
            ApplyAuth(req, requireAuth: true);
            ApplyClientVersion(req);

            try
            {
                await req.SendWebRequest().WithCancellation(ct);
            }
            catch (OperationCanceledException)
            {
                return ApiResult<EmptyResponse>.Failure(0, "request cancelled");
            }
            catch (UnityWebRequestException e)
            {
                return ApiResult<EmptyResponse>.Failure((int)e.ResponseCode, e.Message);
            }

            int code = (int)req.responseCode;
            return ApiResult<EmptyResponse>.Success(new EmptyResponse(), code);
        }

        /// <summary>DELETE /clans/me — chef seul. Retourne ApiResult avec body { status:'DISBANDED', clanId }.</summary>
        public async UniTask<ApiResult<ClanDisbandResponse>> DisbandClanAsync(CancellationToken ct = default)
        {
            using var req = UnityWebRequest.Delete($"{_settings.BaseUrl}/clans/me");
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = _settings.TimeoutSeconds;
            ApplyAuth(req, requireAuth: true);
            ApplyClientVersion(req);

            try
            {
                await req.SendWebRequest().WithCancellation(ct);
            }
            catch (OperationCanceledException)
            {
                return ApiResult<ClanDisbandResponse>.Failure(0, "request cancelled");
            }
            catch (UnityWebRequestException e)
            {
                return ApiResult<ClanDisbandResponse>.Failure((int)e.ResponseCode, e.Message);
            }

            int code = (int)req.responseCode;
            string text = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
            try
            {
                var data = JsonUtility.FromJson<ClanDisbandResponse>(text);
                return ApiResult<ClanDisbandResponse>.Success(data, code);
            }
            catch
            {
                return ApiResult<ClanDisbandResponse>.Success(new ClanDisbandResponse { status = "DISBANDED" }, code);
            }
        }

        /// <summary>
        /// Interroge le serveur sur les versions supportees. N'envoie PAS le header de version
        /// pour eviter le chicken-and-egg (sinon un client trop vieux ne pourrait meme pas
        /// apprendre quelle version est requise).
        /// </summary>
        public UniTask<ApiResult<VersionResponse>> GetVersionAsync(CancellationToken ct = default)
            => GetJsonAsync<VersionResponse>("/version", requireAuth: false, ct, sendVersionHeader: false);

        private async UniTask<ApiResult<TResponse>> PostJsonAsync<TResponse>(
            string path, object body, bool requireAuth, CancellationToken ct, bool sendVersionHeader = true)
        {
            string json = JsonUtility.ToJson(body);
            byte[] payload = Encoding.UTF8.GetBytes(json);

            using var req = new UnityWebRequest($"{_settings.BaseUrl}{path}", UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(payload),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = _settings.TimeoutSeconds,
            };
            req.SetRequestHeader("Content-Type", "application/json");
            ApplyAuth(req, requireAuth);
            if (sendVersionHeader) ApplyClientVersion(req);

            return await SendAsync<TResponse>(req, ct);
        }

        private async UniTask<ApiResult<TResponse>> GetJsonAsync<TResponse>(
            string path, bool requireAuth, CancellationToken ct, bool sendVersionHeader = true)
        {
            using var req = UnityWebRequest.Get($"{_settings.BaseUrl}{path}");
            req.timeout = _settings.TimeoutSeconds;
            ApplyAuth(req, requireAuth);
            if (sendVersionHeader) ApplyClientVersion(req);

            return await SendAsync<TResponse>(req, ct);
        }

        private async UniTask<ApiResult<TResponse>> PutJsonAsync<TResponse>(
            string path, object body, bool requireAuth, CancellationToken ct, bool sendVersionHeader = true)
        {
            string json = JsonUtility.ToJson(body);
            byte[] payload = Encoding.UTF8.GetBytes(json);

            using var req = new UnityWebRequest($"{_settings.BaseUrl}{path}", UnityWebRequest.kHttpVerbPUT)
            {
                uploadHandler = new UploadHandlerRaw(payload),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = _settings.TimeoutSeconds,
            };
            req.SetRequestHeader("Content-Type", "application/json");
            ApplyAuth(req, requireAuth);
            if (sendVersionHeader) ApplyClientVersion(req);

            return await SendAsync<TResponse>(req, ct);
        }

        private void ApplyAuth(UnityWebRequest req, bool requireAuth)
        {
            if (requireAuth && !string.IsNullOrEmpty(_bearerToken))
            {
                req.SetRequestHeader("Authorization", $"Bearer {_bearerToken}");
            }
        }

        private static void ApplyClientVersion(UnityWebRequest req)
        {
            req.SetRequestHeader(HttpHeaders.ClientVersion, GameVersion.Current);
        }

        private static async UniTask<ApiResult<TResponse>> SendAsync<TResponse>(
            UnityWebRequest req, CancellationToken ct)
        {
            try
            {
                await req.SendWebRequest().WithCancellation(ct);
            }
            catch (OperationCanceledException)
            {
                return ApiResult<TResponse>.Failure(0, "request cancelled");
            }
            catch (UnityWebRequestException e)
            {
                int status = (int)e.ResponseCode;
                string err = ExtractErrorMessage(e.Text) ?? e.Message;
                return ApiResult<TResponse>.Failure(status, err);
            }

            int code = (int)req.responseCode;
            string text = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;

            try
            {
                var data = JsonUtility.FromJson<TResponse>(text);
                return ApiResult<TResponse>.Success(data, code);
            }
            catch (Exception e)
            {
                return ApiResult<TResponse>.Failure(code, $"deserialize error: {e.Message}");
            }
        }

        private static string ExtractErrorMessage(string body)
        {
            if (string.IsNullOrEmpty(body)) return null;
            try
            {
                var err = JsonUtility.FromJson<ApiErrorBody>(body);
                return string.IsNullOrEmpty(err?.error) ? null : err.error;
            }
            catch
            {
                return null;
            }
        }
    }
}
