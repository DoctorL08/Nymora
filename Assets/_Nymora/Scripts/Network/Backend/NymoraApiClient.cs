using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Nymora.Network.Backend
{
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

        private async UniTask<ApiResult<TResponse>> PostJsonAsync<TResponse>(
            string path, object body, bool requireAuth, CancellationToken ct)
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

            return await SendAsync<TResponse>(req, ct);
        }

        private async UniTask<ApiResult<TResponse>> GetJsonAsync<TResponse>(
            string path, bool requireAuth, CancellationToken ct)
        {
            using var req = UnityWebRequest.Get($"{_settings.BaseUrl}{path}");
            req.timeout = _settings.TimeoutSeconds;
            ApplyAuth(req, requireAuth);

            return await SendAsync<TResponse>(req, ct);
        }

        private void ApplyAuth(UnityWebRequest req, bool requireAuth)
        {
            if (requireAuth && !string.IsNullOrEmpty(_bearerToken))
            {
                req.SetRequestHeader("Authorization", $"Bearer {_bearerToken}");
            }
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
