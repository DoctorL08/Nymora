using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Nymora.Core.Data;
using Nymora.Network.Backend;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nymora.Hub
{
    /// <summary>
    /// Analytics D4 (client) — Remonte les ERREURS du client Unity au backend (POST /telemetry/error),
    /// affichées dans le dashboard admin (api.nymora.fr/admin → onglet erreurs).
    ///
    /// - Capte Application.logMessageReceived (Error / Exception / Assert ; PAS les warnings, trop bruyants).
    /// - DÉDUPLIQUE par message (compteur d'occurrences) et plafonne à 50 entrées (= cap backend/requête).
    /// - BATCH : flush toutes les 30 s, UNIQUEMENT si un joueur est connecté (token dispo). Sinon, les
    ///   erreurs restent en buffer et partent à la connexion.
    /// - Auto-instancié au boot (RuntimeInitializeOnLoadMethod), persistant (DontDestroyOnLoad) → capte
    ///   dès le login. Token = HubChatClient.DevToken ; settings résolus via Resources (pas d'Inspector).
    ///
    /// VIEW/NETWORK ONLY, aucun impact gameplay.
    /// </summary>
    public sealed class ClientErrorReporter : MonoBehaviour
    {
        private const int MaxUnique = 50;          // = cap backend (50 entrées / requête)
        private const float FlushIntervalSec = 30f;
        private const int MaxMessageLen = 500;
        private const int MaxStackLen = 4000;

        private static ClientErrorReporter _instance;

        private readonly Dictionary<string, ClientErrorItem> _pending = new Dictionary<string, ClientErrorItem>();
        private float _flushTimer;
        private bool _flushing;
        private NymoraApiClient _api;
        private bool _apiResolved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("ClientErrorReporter");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ClientErrorReporter>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            Application.logMessageReceived += OnLog;
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            if (_instance == this) _instance = null;
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            string level;
            switch (type)
            {
                case LogType.Exception: level = "fatal"; break;
                case LogType.Error:
                case LogType.Assert: level = "error"; break;
                default: return; // Warning / Log ignorés (trop bruyants)
            }
            if (string.IsNullOrEmpty(condition)) return;
            // Anti-boucle : ne jamais re-capter nos propres logs (ex : un échec de POST loggué).
            if (condition.StartsWith("[ClientErrorReporter]")) return;

            string key = level + "|" + condition;
            if (_pending.TryGetValue(key, out var e))
            {
                e.count++;
                _pending[key] = e;
            }
            else if (_pending.Count < MaxUnique)
            {
                _pending[key] = new ClientErrorItem
                {
                    message = Trunc(condition, MaxMessageLen),
                    stack = Trunc(stackTrace, MaxStackLen),
                    scene = SafeSceneName(),
                    level = level,
                    count = 1,
                };
            }
        }

        private void Update()
        {
            _flushTimer += Time.unscaledDeltaTime;
            if (_flushTimer < FlushIntervalSec) return;
            _flushTimer = 0f;
            TryFlush();
        }

        private void TryFlush()
        {
            if (_flushing || _pending.Count == 0) return;
            string token = HubChatClient.Instance != null ? HubChatClient.Instance.DevToken : null;
            if (string.IsNullOrEmpty(token)) return; // pas connecté -> on garde en buffer
            if (!EnsureApi()) return;

            var batch = new ClientErrorItem[_pending.Count];
            _pending.Values.CopyTo(batch, 0);
            _pending.Clear();

            _api.SetBearerToken(token);
            FlushAsync(batch).Forget();
        }

        private async UniTask FlushAsync(ClientErrorItem[] batch)
        {
            _flushing = true;
            try
            {
                var res = await _api.ReportClientErrorsAsync(GameVersion.Current, batch);
                if (!res.IsSuccess)
                {
                    // Volontairement PAS de re-buffer (éviter une boucle d'erreurs réseau qui se re-loggue).
                    Debug.LogWarning($"[ClientErrorReporter] POST /telemetry/error échoué : {res.StatusCode} {res.ErrorMessage} ({batch.Length} erreurs perdues).");
                }
            }
            finally { _flushing = false; }
        }

        private bool EnsureApi()
        {
            if (_api != null) return true;
            if (_apiResolved) return false;
            _apiResolved = true;
            var all = Resources.FindObjectsOfTypeAll<NymoraBackendSettings>();
            if (all == null || all.Length == 0)
            {
                _apiResolved = false; // retentera (les settings se chargent quand une scène les référence)
                return false;
            }
            _api = new NymoraApiClient(all[0]);
            return true;
        }

        private static string SafeSceneName()
        {
            var s = SceneManager.GetActiveScene();
            return s.IsValid() ? s.name : "?";
        }

        private static string Trunc(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s.Substring(0, max);
        }
    }
}
