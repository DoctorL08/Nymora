namespace Nymora.Core.Data
{
    /// <summary>
    /// Brique 5.7-D2 — Bridge static cross-scène pour le RÉSULTAT d'un match ranked 2v2, transmis
    /// du combat (MatchEndOverlay, au clic « Retour Hub ») vers le hub (HubMatchResultDisplay) qui
    /// POST /ranked2v2/report-result.
    ///
    /// Équivalent 2v2 de MatchBridge.LastMatchResult (1v1). Porte le verdict team-aware + le roster
    /// figé (4 sub + team) nécessaire au settle ELO team-avg côté backend + la classe/deck du local.
    /// Static -> survit au LoadScene. Reset() après consommation par le hub.
    /// </summary>
    public static class Match2v2ResultBridge
    {
        public static bool HasPendingResult { get; private set; }
        public static string MatchId { get; private set; }
        public static int MyTeam { get; private set; } = -1;
        public static bool LocalWon { get; private set; }
        /// <summary>Les 4 joueurs (sub + équipe), figés au lancement du match (Match2v2Bridge.Players).</summary>
        public static Match2v2Bridge.Player[] Roster { get; private set; }
        /// <summary>Classe jouée par le joueur local (pour l'XP par classe au settle).</summary>
        public static string ClassId { get; private set; }
        /// <summary>Deck joué (6 spellIds tech), optionnel.</summary>
        public static string[] Deck { get; private set; }

        public static void Set(string matchId, int myTeam, bool localWon,
                               Match2v2Bridge.Player[] roster, string classId, string[] deck)
        {
            MatchId = matchId;
            MyTeam = myTeam;
            LocalWon = localWon;
            Roster = roster;
            ClassId = classId;
            Deck = deck;
            HasPendingResult = true;
        }

        public static void Reset()
        {
            HasPendingResult = false;
            MatchId = null;
            MyTeam = -1;
            LocalWon = false;
            Roster = null;
            ClassId = null;
            Deck = null;
        }
    }
}
