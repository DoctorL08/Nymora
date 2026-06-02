using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Nymora.Core.Data;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace Nymora.Network.Backend
{
    /// <summary>
    /// Brique Analytics D4 — remontee des erreurs CLIENT vers le backend (table error_logs,
    /// onglet "Erreurs" du dashboard /admin, source='client').
    ///
    /// Hooke <see cref="Application.logMessageReceived"/> : capture TOUTES les erreurs runtime
    /// (Debug.LogError, exceptions non-catchees, et donc aussi NymoraLog.Error/Critical qui
    /// passent par Debug.LogError). Dedoublonne, batch toutes les 5s, envoie le JWT du joueur.
    ///
    /// Auto-demarre via [RuntimeInitializeOnLoadMethod] (capture des le boot, avant toute scene).
    /// Aucune manip Unity : <see cref="Configure"/> est appele depuis l'ecran de login (par code)
    /// pour fournir la base URL backend.
    ///
    /// Robustesse (un reporter d'erreurs ne doit JAMAIS causer/aggraver d'erreurs) :
    ///   - garde anti-recursion (_inHandler), ignore ses propres logs (SelfTag),
    ///   - caps stricts (file, batch, envois/session, longueurs),
    ///   - n'envoie que si configure ET joueur loggue (sinon bufferise),
    ///   - n'emet jamais d'erreur en cas d'echec d'envoi (best-effort silencieux).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClientErrorReporter : MonoBehaviour
    {
        private const string TokenKey = "nymora.auth.jwt";
        private const string SelfTag = "[Nymora.Telemetry]";
        private const float FlushIntervalSec = 5f;
        private const int MaxQueue = 30;          // entrees distinctes en attente d'envoi
        private const int MaxPerFlush = 15;       // entrees par requete HTTP
        private const int MaxSessionSends = 200;  // plafond dur d'entrees envoyees / session
        private const int MaxMessageLen = 1000;
        private const int MaxStackLen = 3000;

        private static ClientErrorReporter _instance;
        private static string _baseUrl;

        private readonly Dictionary<string, PendingError> _pending = new Dictionary<string, PendingError>();
        private readonly List<PendingError> _order = new List<PendingError>();
        private bool _inHandler;
        private int _sentThisSession;

        private sealed class PendingError
        {
            public string Signature;
            public string Message;
            public string Stack;
            public string Scene;
            public string Level;
            public int Count;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[ClientErrorReporter]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ClientErrorReporter>();
        }

        /// <summary>
        /// Fournit la base URL backend (ex: https://api.nymora.fr). A appeler une fois (login).
        /// Sans ca, les erreurs sont capturees et bufferisees mais pas envoyees.
        /// </summary>
        public static void Configure(string baseUrl)
        {
            if (!string.IsNullOrEmpty(baseUrl)) _baseUrl = baseUrl.TrimEnd('/');
        }

        private void OnEnable() => Application.logMessageReceived += HandleLog;
        private void OnDisable() => Application.logMessageReceived -= HandleLog;
        private void Start() => InvokeRepeating(nameof(Flush), FlushIntervalSec, FlushIntervalSec);

        private void HandleLog(string condition, string stackTrace, LogType type)
        {
            if (_inHandler) return; // anti-recursion
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (!string.IsNullOrEmpty(condition) && condition.Contains(SelfTag)) return; // nos propres logs

            _inHandler = true;
            try
            {
                string message = Truncate(string.IsNullOrEmpty(condition) ? "(no message)" : condition, MaxMessageLen);
                string level =
                    type == LogType.Exception ? "fatal" :
                    type == LogType.Assert ? "warn" :
                    (message.Contains("[CRITICAL]") ? "fatal" : "error"); // NymoraLog.Critical
                string scene = SceneManager.GetActiveScene().name;
                string sig = level + "|" + message + "|" + FirstLine(stackTrace);

                if (_pending.TryGetValue(sig, out var existing))
                {
                    existing.Count++;
                }
                else if (_order.Count < MaxQueue)
                {
                    var pe = new PendingError
                    {
                        Signature = sig,
                        Message = message,
                        Stack = Truncate(stackTrace ?? string.Empty, MaxStackLen),
                        Scene = scene,
                        Level = level,
                        Count = 1,
                    };
                    _pending[sig] = pe;
                    _order.Add(pe);
                }
                // else : file pleine -> on droppe (best-effort)
            }
            catch
            {
                // un handler de log ne doit jamais throw
            }
            finally
            {
                _inHandler = false;
            }
        }

        private void Flush()
        {
            if (_order.Count == 0 || string.IsNullOrEmpty(_baseUrl)) return;
            if (_sentThisSession >= MaxSessionSends)
            {
                _order.Clear();
                _pending.Clear();
                return;
            }
            string token = PlayerPrefs.GetString(TokenKey, string.Empty);
            if (string.IsNullOrEmpty(token)) return; // pas loggue -> on garde en buffer

            int take = Mathf.Min(MaxPerFlush, _order.Count);
            var batch = _order.GetRange(0, take);
            _order.RemoveRange(0, take);
            foreach (var pe in batch) _pending.Remove(pe.Signature);

            _sentThisSession += batch.Count;
            StartCoroutine(Send(BuildJson(batch), token));
        }

        private IEnumerator Send(string json, string token)
        {
            byte[] payload = Encoding.UTF8.GetBytes(json);
            using var req = new UnityWebRequest(_baseUrl + "/telemetry/error", UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(payload),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 10,
            };
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + token);
            req.SetRequestHeader("X-Nymora-Client-Version", GameVersion.Current);
            yield return req.SendWebRequest();
            // Echec d'envoi : silencieux (anti-boucle). Best-effort, on ne re-essaie pas.
        }

        // ---- JSON (JsonUtility + wrappers serialisables) ----

        [Serializable]
        private sealed class Dto
        {
            public string message;
            public string stack;
            public string scene;
            public string level;
            public int count;
        }

        [Serializable]
        private sealed class Batch
        {
            public string appVersion;
            public Dto[] errors;
        }

        private static string BuildJson(List<PendingError> batch)
        {
            var dtos = new Dto[batch.Count];
            for (int i = 0; i < batch.Count; i++)
            {
                var pe = batch[i];
                dtos[i] = new Dto { message = pe.Message, stack = pe.Stack, scene = pe.Scene, level = pe.Level, count = pe.Count };
            }
            return JsonUtility.ToJson(new Batch { appVersion = GameVersion.Current, errors = dtos });
        }

        private static string Truncate(string s, int max)
            => string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max);

        private static string FirstLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            int nl = s.IndexOf('\n');
            return nl < 0 ? s : s.Substring(0, nl);
        }
    }
}
