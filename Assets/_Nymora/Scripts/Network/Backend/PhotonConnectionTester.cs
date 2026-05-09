using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Photon.Realtime;
using Quantum;
using UnityEngine;

namespace Nymora.Network.Backend
{
    /// <summary>
    /// Teste la Custom Auth Photon : tente une connexion au Master Server avec un JWT,
    /// puis se deconnecte aussitot. Si OnConnectedToMaster est appele, c'est que le webhook
    /// backend a valide le JWT (ResultCode=1). Sinon on capture l'erreur via OnCustomAuthenticationFailed
    /// ou OnDisconnected (DisconnectCause).
    ///
    /// Usage typique :
    ///   var tester = gameObject.AddComponent&lt;PhotonConnectionTester&gt;();
    ///   var result = await tester.TestConnectAsync(jwt, ct);
    ///   if (result.IsSuccess) { ... }
    ///
    /// Une seule connexion a la fois ; appelle Disconnect() ou attends le retour avant un nouveau test.
    /// </summary>
    public class PhotonConnectionTester : MonoBehaviour, IConnectionCallbacks
    {
        public struct PhotonConnectResult
        {
            public bool IsSuccess;
            public string Region;
            public string UserId;
            public string FailureMessage;
        }

        private RealtimeClient _client;
        private TaskCompletionSource<PhotonConnectResult> _tcs;
        private string _customAuthFailureMsg;

        private void Update()
        {
            _client?.Service();
        }

        public async UniTask<PhotonConnectResult> TestConnectAsync(string jwt, CancellationToken ct = default)
        {
            if (_client != null)
            {
                throw new InvalidOperationException("Une connexion test est deja en cours");
            }

            if (!PhotonServerSettings.TryGetGlobal(out var photonSettings) || photonSettings == null)
            {
                return new PhotonConnectResult
                {
                    FailureMessage = "PhotonServerSettings.Global non configure (lance Quantum Hub setup)",
                };
            }

            _customAuthFailureMsg = null;
            _tcs = new TaskCompletionSource<PhotonConnectResult>();

            _client = new RealtimeClient();
            _client.AddCallbackTarget(this);
            _client.AuthValues = PhotonAuthBridge.BuildAuthValues(jwt);

            bool connectStarted;
            try
            {
                connectStarted = _client.ConnectUsingSettings(photonSettings.AppSettings);
            }
            catch (Exception e)
            {
                Cleanup();
                return new PhotonConnectResult { FailureMessage = $"Exception au Connect : {e.Message}" };
            }

            if (!connectStarted)
            {
                Cleanup();
                return new PhotonConnectResult { FailureMessage = "ConnectUsingSettings a renvoye false (parametres invalides)" };
            }

            PhotonConnectResult result;
            using (ct.Register(() =>
            {
                _tcs?.TrySetResult(new PhotonConnectResult { FailureMessage = "Connexion annulee (CancellationToken)" });
                _client?.Disconnect();
            }))
            {
                result = await _tcs.Task.AsUniTask();
            }

            // Disconnect proprement : on laisse Update() pomper Service() jusqu'a ce que
            // le client atteigne l'etat terminal Disconnected (sinon warning Photon
            // "DispatchIncomingCommands() wasn't called for > 5000s").
            if (_client != null && _client.State != ClientState.Disconnected)
            {
                _client.Disconnect();
                var deadline = Time.realtimeSinceStartup + 2f;
                while (_client != null && _client.State != ClientState.Disconnected
                       && Time.realtimeSinceStartup < deadline)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }
            }

            Cleanup();
            return result;
        }

        private void Cleanup()
        {
            if (_client != null)
            {
                _client.RemoveCallbackTarget(this);
                _client = null;
            }
            _tcs = null;
            _customAuthFailureMsg = null;
        }

        private void OnDestroy()
        {
            _client?.Disconnect();
            Cleanup();
        }

        // ---- IConnectionCallbacks ----

        public void OnConnected() { }

        public void OnConnectedToMaster()
        {
            _tcs?.TrySetResult(new PhotonConnectResult
            {
                IsSuccess = true,
                Region = _client?.CurrentRegion,
                UserId = _client?.UserId,
            });
        }

        public void OnDisconnected(DisconnectCause cause)
        {
            if (_tcs == null || _tcs.Task.IsCompleted) return;

            string msg = string.IsNullOrEmpty(_customAuthFailureMsg)
                ? $"Disconnected: {cause}"
                : $"Custom auth refused: {_customAuthFailureMsg} (DisconnectCause={cause})";

            _tcs.TrySetResult(new PhotonConnectResult { FailureMessage = msg });
        }

        public void OnRegionListReceived(RegionHandler regionHandler) { }

        public void OnCustomAuthenticationResponse(Dictionary<string, object> data) { }

        public void OnCustomAuthenticationFailed(string debugMessage)
        {
            // Photon va aussi appeler OnDisconnected derriere ; on garde le message
            // pour l'inclure dans le PhotonConnectResult final.
            _customAuthFailureMsg = debugMessage;
        }
    }
}
