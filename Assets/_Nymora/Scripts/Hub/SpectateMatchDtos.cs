using System;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique S2 (mode spectateur) — DTOs du message WS MATCHES_LIST (réponse à LIST_MATCHES).
    /// Désérialisés par JsonUtility (les noms de champs DOIVENT matcher le JSON backend :
    /// cf matchRegistry.ts ActiveMatch / ActiveMatchPlayer).
    /// </summary>
    [Serializable]
    public class SpectateMatchPlayer
    {
        public string sub;
        public string displayName;
        public string classId; // vide tant que le relayer (S3) n'a pas annoncé la classe
    }

    [Serializable]
    public class SpectateMatchInfo
    {
        public string matchId;
        public string mode;        // "casual" | "ranked"
        public SpectateMatchPlayer[] players;
        public long startedAt;     // ms epoch (backend Date.now())
    }
}
