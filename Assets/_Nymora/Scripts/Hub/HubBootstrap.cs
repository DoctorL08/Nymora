using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 4.2 + 4.3.c — Bootstrap du Community Hub.
    /// - Spawn un NetworkRunner Shared Mode au Start
    /// - Implemente INetworkRunnerCallbacks
    /// - OnPlayerJoined : spawn l'avatar Fusion pour le joueur LOCAL uniquement
    ///   (en Shared Mode, chaque client spawn son propre avatar)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HubBootstrap : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Header("Session")]
        [SerializeField] private string _sessionName = "nymora-hub-dev";
        [SerializeField] private int _maxPlayers = 100;
        [SerializeField] private bool _autoStartOnPlay = true;

        [Header("Avatar Spawn")]
        [SerializeField] private NetworkObject _avatarPrefab;
        [SerializeField] private int _spawnGridX = 10;
        [SerializeField] private int _spawnGridY = 10;

        private NetworkRunner _runner;

        private async void Start()
        {
            // C1 — routeur des bulles de chat (hub only : vit/meurt avec cette scène).
            if (GetComponent<HubChatBubbleRouter>() == null) gameObject.AddComponent<HubChatBubbleRouter>();

            // GFX — atmosphère/profondeur (particules flottantes) du hub (hub only).
            if (GetComponent<HubAtmosphere>() == null) gameObject.AddComponent<HubAtmosphere>();

            if (_autoStartOnPlay)
            {
                await StartHub();
            }
        }

        public async Task StartHub()
        {
            if (_runner != null && _runner.IsRunning)
            {
                Debug.Log("[Hub] Runner deja actif, skip StartHub");
                return;
            }

            _runner = gameObject.GetComponent<NetworkRunner>();
            if (_runner == null)
            {
                _runner = gameObject.AddComponent<NetworkRunner>();
            }

            _runner.ProvideInput = true;
            _runner.AddCallbacks(this);

            Debug.Log($"[Hub] StartGame Shared session='{_sessionName}' maxPlayers={_maxPlayers}");

            var args = new StartGameArgs
            {
                GameMode = GameMode.Shared,
                SessionName = _sessionName,
                PlayerCount = _maxPlayers,
            };

            var result = await _runner.StartGame(args);

            if (result.Ok)
            {
                var region = _runner.SessionInfo != null ? _runner.SessionInfo.Region : "?";
                Debug.Log($"[Hub] Connected. LocalPlayer={_runner.LocalPlayer} Region={region}");
            }
            else
            {
                Debug.LogError($"[Hub] StartGame failed : {result.ShutdownReason} | {result.ErrorMessage}");
            }
        }

        // ====== INetworkRunnerCallbacks ======

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            // Shared Mode : chaque client spawn son propre avatar (et UNIQUEMENT le sien)
            if (player != runner.LocalPlayer) return;

            if (_avatarPrefab == null)
            {
                Debug.LogError("[Hub] _avatarPrefab non assigne — impossible de spawn l'avatar local.");
                return;
            }

            // Spawn world position (peu importe : HubAvatar.Spawned() recalcule via grid)
            var spawnPos = Vector3.zero;
            var spawned = runner.Spawn(_avatarPrefab, spawnPos, Quaternion.identity, player);
            Debug.Log($"[Hub] OnPlayerJoined player={player} → avatar spawne (NetworkId={spawned.Id})");
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[Hub] OnPlayerLeft player={player}");
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Debug.Log($"[Hub] Runner shutdown : {shutdownReason}");
        }

        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    }
}
