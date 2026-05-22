using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nymora.Network.Backend
{
    /// <summary>
    /// Facade haut-niveau pour l'authentification Nymora.
    /// - Persiste le JWT en PlayerPrefs (temporaire pour la Phase 1 ; on migrera vers
    ///   un stockage securise plateforme-specifique en Phase 7+ avant alpha).
    /// - Restaure le token au demarrage (constructeur) si present.
    /// - Injecte automatiquement le token dans les appels du NymoraApiClient.
    ///
    /// Cycle de vie typique :
    ///   var api = new NymoraApiClient(settings);
    ///   var auth = new AuthService(api);
    ///   if (!auth.IsLoggedIn) await auth.LoginAsync(pseudo, password);
    ///   var me = await auth.GetMeAsync();
    /// </summary>
    public class AuthService
    {
        private const string PlayerPrefsTokenKey = "nymora.auth.jwt";

        private readonly NymoraApiClient _client;

        public AuthService(NymoraApiClient client)
        {
            _client = client;
            string saved = PlayerPrefs.GetString(PlayerPrefsTokenKey, string.Empty);
            if (!string.IsNullOrEmpty(saved))
            {
                _client.SetBearerToken(saved);
            }
        }

        public bool IsLoggedIn => _client.HasBearerToken;

        public string Token => PlayerPrefs.GetString(PlayerPrefsTokenKey, string.Empty);

        public async UniTask<ApiResult<AuthResponse>> RegisterAsync(
            string email, string password, string displayName, CancellationToken ct = default)
        {
            var res = await _client.RegisterAsync(
                new RegisterRequest { email = email, password = password, displayName = displayName }, ct);
            if (res.IsSuccess)
            {
                PersistToken(res.Data.token);
            }
            return res;
        }

        public async UniTask<ApiResult<AuthResponse>> LoginAsync(
            string displayName, string password, CancellationToken ct = default)
        {
            var res = await _client.LoginAsync(
                new LoginRequest { displayName = displayName, password = password }, ct);
            if (res.IsSuccess)
            {
                PersistToken(res.Data.token);
            }
            return res;
        }

        public UniTask<ApiResult<MeResponse>> GetMeAsync(CancellationToken ct = default) => _client.GetMeAsync(ct);

        public void Logout()
        {
            _client.ClearBearerToken();
            PlayerPrefs.DeleteKey(PlayerPrefsTokenKey);
            PlayerPrefs.Save();
        }

        private void PersistToken(string token)
        {
            _client.SetBearerToken(token);
            PlayerPrefs.SetString(PlayerPrefsTokenKey, token);
            PlayerPrefs.Save();
        }
    }
}
