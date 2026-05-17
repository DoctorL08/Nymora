namespace Nymora.Core.Data
{
    /// <summary>
    /// Brique 4.8.d.ii — Bridge in-memory cross-scène pour transmettre les infos
    /// d'un match accepté entre la scène hub et la scène combat.
    /// Brique 4.8.d.iii (stub) + 4.9 — Ajout LastMatchResult pour le retour hub.
    /// Brique 4.14.c — Deplace de Nymora.Hub vers Nymora.Core.Data pour que CombatBootstrapCasual
    ///   (asmdef Nymora.Combat) puisse acceder sans violer la separation Hub/Combat
    ///   (pattern miroir DeckBridge ligne Nymora.Core.Data).
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
        // 4.14.c — Identite du LOCAL player (set par le hub au moment du wire OnMatchReady).
        // Sert au CombatBootstrapCasual pour authentifier le client Photon Realtime room
        // (PlayerName = LocalEmail). Evite que Combat refasse une dependance directe sur Hub.
        public static string LocalSub { get; private set; }
        public static string LocalEmail { get; private set; }
        public static bool HasPendingMatch => !string.IsNullOrEmpty(PendingMatchId);

        // 4.8.d.iii stub / 4.9 — résultat du dernier match, lu une fois côté hub puis reset.
        public static MatchResult LastMatchResult { get; private set; } = MatchResult.None;
        public static string LastOpponentEmail { get; private set; }
        public static string LastMatchId { get; private set; }
        public static bool HasPendingResult => LastMatchResult != MatchResult.None;

        public static void SetPendingMatch(string matchId, string opponentSub, string opponentEmail,
                                            string localSub = null, string localEmail = null)
        {
            PendingMatchId = matchId;
            OpponentSub = opponentSub;
            OpponentEmail = opponentEmail;
            // 4.14.c — localSub/localEmail optionnels pour ne pas casser les anciens callsites
            // (brique 4.8.d.ii). Set quand on entre dans un vrai match PvP via 4.14.e.
            if (!string.IsNullOrEmpty(localSub)) LocalSub = localSub;
            if (!string.IsNullOrEmpty(localEmail)) LocalEmail = localEmail;
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
            LocalSub = null;
            LocalEmail = null;
            LastMatchResult = MatchResult.None;
            LastMatchId = null;
            LastOpponentEmail = null;
        }
    }
}
