using System;

namespace Nymora.Core.Data
{
    /// <summary>
    /// Brique S3 (mode spectateur) — Pont découplé entre la capture (Nymora.Combat, qui voit
    /// Quantum) et le transport WS (Nymora.Hub / HubChatClient). Comme Combat ne référence pas
    /// Hub (séparation asmdef), le relayer publie des données PLATES (strings/base64) sur ce bus
    /// statique de Core, et HubChatClient s'y abonne pour relayer au backend.
    ///
    /// Payloads volontairement opaques côté Hub : le header (config Quantum) est un JSON déjà
    /// sérialisé par le relayer, injecté tel quel dans le message WS SPECTATE_START.
    /// </summary>
    public static class SpectateRelayBus
    {
        /// <summary>(matchId, headerJson) — ouverture du flux + config Quantum sérialisée.</summary>
        public static event Action<string, string> OnStart;

        /// <summary>(matchId, seq, dataBase64) — un bloc d'octets du RecordInputStream.</summary>
        public static event Action<string, int, string> OnChunk;

        /// <summary>(matchId) — fin du flux (match terminé / relayer détruit).</summary>
        public static event Action<string> OnEnd;

        public static void RaiseStart(string matchId, string headerJson) => OnStart?.Invoke(matchId, headerJson);
        public static void RaiseChunk(string matchId, int seq, string dataBase64) => OnChunk?.Invoke(matchId, seq, dataBase64);
        public static void RaiseEnd(string matchId) => OnEnd?.Invoke(matchId);
    }
}
