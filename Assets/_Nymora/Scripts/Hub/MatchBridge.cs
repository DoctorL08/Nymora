namespace Nymora.Hub
{
    /// <summary>
    /// Brique 4.8.d.ii — Bridge in-memory cross-scène pour transmettre les infos
    /// d'un match accepté entre la scène hub et la scène combat.
    /// Brique 4.8.d.iii (stub) + 4.9 — Ajout LastMatchResult pour le retour hub.
    ///
    /// Static class : persiste entre LoadScene tant que le process Unity tourne.
    /// Reset() à appeler quand le match est terminé et affiché côté hub.
    /// </summary>
    public enum MatchResult { None, Victory, Defeat, Draw }

    public static class MatchBridge
    {
        public static string PendingMatchId { get; private set; }
        public static string OpponentSub { get; private set; }
        public static string OpponentEmail { get; private set; }
        public static bool HasPendingMatch => !string.IsNullOrEmpty(PendingMatchId);

        // 4.8.d.iii stub / 4.9 — résultat du dernier match, lu une fois côté hub puis reset.
        public static MatchResult LastMatchResult { get; private set; } = MatchResult.None;
        public static string LastOpponentEmail { get; private set; }
        public static string LastMatchId { get; private set; }
        public static bool HasPendingResult => LastMatchResult != MatchResult.None;

        public static void SetPendingMatch(string matchId, string opponentSub, string opponentEmail)
        {
            PendingMatchId = matchId;
            OpponentSub = opponentSub;
            OpponentEmail = opponentEmail;
        }

        public static void SetMatchResult(MatchResult result, string matchId, string opponentEmail)
        {
            LastMatchResult = result;
            LastMatchId = matchId;
            LastOpponentEmail = opponentEmail;
            // On clear la pending match — le match a été consommé.
            PendingMatchId = null;
            OpponentSub = null;
            OpponentEmail = null;
        }

        public static MatchResult ConsumeLastResult(out string matchId, out string opponentEmail)
        {
            var r = LastMatchResult;
            matchId = LastMatchId;
            opponentEmail = LastOpponentEmail;
            LastMatchResult = MatchResult.None;
            LastMatchId = null;
            LastOpponentEmail = null;
            return r;
        }

        public static void Reset()
        {
            PendingMatchId = null;
            OpponentSub = null;
            OpponentEmail = null;
            LastMatchResult = MatchResult.None;
            LastMatchId = null;
            LastOpponentEmail = null;
        }
    }
}
