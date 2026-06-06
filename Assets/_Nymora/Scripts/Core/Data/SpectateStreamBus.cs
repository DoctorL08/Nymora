using System;

namespace Nymora.Core.Data
{
    /// <summary>
    /// Brique S4 (mode spectateur) — Pont découplé entre la scène combat (Nymora.Combat,
    /// LiveSpectateController) et le transport WS (Nymora.Hub, HubChatClient). Combat ne référence
    /// pas Hub (asmdef) → on passe par ce bus statique de Core. Symétrique du [[SpectateRelayBus]].
    ///
    /// Sens Combat → Hub : le spectateur demande à rejoindre / quitter un flux.
    /// Sens Hub → Combat : le backend pousse le header (INIT) puis les octets (CHUNK) et la fin (END).
    /// </summary>
    public static class SpectateStreamBus
    {
        // ===== Combat → Hub (requêtes) =====
        public static event Action<string> OnJoinRequested;
        public static event Action<string> OnLeaveRequested;
        public static void RequestJoin(string matchId) => OnJoinRequested?.Invoke(matchId);
        public static void RequestLeave(string matchId) => OnLeaveRequested?.Invoke(matchId);

        // ===== Hub → Combat (flux) =====
        public static event Action<string, string> OnInit;       // matchId, headerJson
        public static event Action<string, int, string> OnChunk; // matchId, seq, dataBase64
        public static event Action<string> OnEnd;                // matchId
        public static void RaiseInit(string matchId, string headerJson) => OnInit?.Invoke(matchId, headerJson);
        public static void RaiseChunk(string matchId, int seq, string dataBase64) => OnChunk?.Invoke(matchId, seq, dataBase64);
        public static void RaiseEnd(string matchId) => OnEnd?.Invoke(matchId);
    }
}
