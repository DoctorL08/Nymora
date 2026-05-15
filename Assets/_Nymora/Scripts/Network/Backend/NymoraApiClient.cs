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
