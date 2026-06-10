namespace Nymora.Core.Data
{
    /// <summary>
    /// Brique 5.7 (sous-brique A) — Bridge in-memory cross-scène pour un match RANKED 2v2 appairé.
    ///
    /// Équivalent de <see cref="MatchBridge"/> (1v1) mais pour 4 joueurs répartis en 2 équipes.
    /// Rempli par le hub quand il reçoit RANKED_MATCH_FOUND (mode "2v2") via HubChatClient, puis lu
    /// par le bootstrap réseau 2v2 (sous-brique B, scène 41) pour : connecter la room Photon
    /// (RoomName = MatchId, 4 joueurs) et AddPlayer avec le bon TeamId.
    ///
    /// Static : persiste entre LoadScene tant que le process tourne. Reset() au retour hub.
    /// Vit dans Nymora.Core.Data (comme MatchBridge / DeckBridge) pour être accessible côté Hub ET
    /// Combat sans violer la séparation d'asmdef.
    /// </summary>
    public static class Match2v2Bridge
    {
        /// <summary>Un des 4 joueurs du match, avec son équipe (0/1).</summary>
        public struct Player
        {
            public string Sub;
            public string Email;
            public string DisplayName;
            public int Team; // 0 ou 1
        }

        public static string PendingMatchId { get; private set; }
        /// <summary>Équipe du joueur LOCAL (0/1), telle qu'annoncée par le backend (myTeam).</summary>
        public static int LocalTeam { get; private set; } = -1;
        /// <summary>Les 4 joueurs (sub/email/pseudo/équipe). Ordre backend : équipe A puis équipe B.</summary>
        public static Player[] Players { get; private set; }

        /// <summary>Identité du joueur local (pour l'auth Photon + résolution self), comme MatchBridge.</summary>
        public static string LocalSub { get; private set; }
        public static string LocalEmail { get; private set; }

        public static bool HasPendingMatch => !string.IsNullOrEmpty(PendingMatchId);

        public static void SetPendingMatch(string matchId, int localTeam, Player[] players,
                                           string localSub = null, string localEmail = null)
        {
            PendingMatchId = matchId;
            LocalTeam = localTeam;
            Players = players;
            if (!string.IsNullOrEmpty(localSub)) LocalSub = localSub;
            if (!string.IsNullOrEmpty(localEmail)) LocalEmail = localEmail;
        }

        public static void Reset()
        {
            PendingMatchId = null;
            LocalTeam = -1;
            Players = null;
            LocalSub = null;
            LocalEmail = null;
        }
    }
}
