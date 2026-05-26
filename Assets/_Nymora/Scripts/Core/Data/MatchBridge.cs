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
        // POLISH-7 (20 mai) — pseudo affichage opponent (Profile.displayName push par
        // le backend dans MATCH_READY.opponents[].displayName). Lu par CombatBootstrapCasual /
        // MatchEndOverlay / HubMatchResultDisplay pour afficher le pseudo a la place de l'email.
        public static string OpponentDisplayName { get; private set; }
        // 4.14.c — Identite du LOCAL player (set par le hub au moment du wire OnMatchReady).
        // Sert au CombatBootstrapCasual pour authentifier le client Photon Realtime room
        // (PlayerName = LocalEmail). Evite que Combat refasse une dependance directe sur Hub.
        public static string LocalSub { get; private set; }
        public static string LocalEmail { get; private set; }
        // POLISH-7 (20 mai) — pseudo local pour affichage combat (tooltip Combatant).
        public static string LocalDisplayName { get; private set; }
        public static bool HasPendingMatch => !string.IsNullOrEmpty(PendingMatchId);

        // Phase 6.1 — flag VIEW-ONLY (aucun impact sur la simulation Quantum).
        // true si le match en cours est CLASSE (ranked). Set par le flow matchmaking
        // (brique 6.2) au moment du SetPendingMatch ; reste false pour casual/IA.
        // Lu en brique 6.3 pour decider si le resultat impacte le MMR.
        public static bool IsRanked { get; private set; }

        /// <summary>Marque le match pending comme classe (ranked) ou non. Phase 6.1.</summary>
        public static void SetRanked(bool ranked) => IsRanked = ranked;

        // 4.8.d.iii stub / 4.9 — résultat du dernier match, lu une fois côté hub puis reset.
        public static MatchResult LastMatchResult { get; private set; } = MatchResult.None;
        public static string LastOpponentEmail { get; private set; }
        // POLISH-7 (20 mai) — pseudo opponent pour retour hub (system line "VICTOIRE vs X").
        public static string LastOpponentDisplayName { get; private set; }
        public static string LastMatchId { get; private set; }
        // 6.3 — sub (UUID) de l'adversaire + flag ranked, conserves jusqu'au retour hub
        // pour reporter le resultat classe au backend (POST /ranked/report-result).
        public static string LastOpponentSub { get; private set; }
        public static bool LastIsRanked { get; private set; }
        public static bool HasPendingResult => LastMatchResult != MatchResult.None;

        // ===== S-STATS.b — stats de combat du joueur local (View-observed) =====
        // Alimentees en continu par CombatStatsCollector (asmdef Nymora.Combat) pendant le
        // match, lues par HubMatchResultDisplay au moment du report ranked puis reset.
        // NON-autoritatives (observation View, double-accord alpha) cf backend S-STATS.a.
        public static int StatDamageDealt { get; private set; }
        public static int StatDamageTaken { get; private set; }
        public static int StatSpellsCast { get; private set; }
        public static int StatTurns { get; private set; }
        public static bool HasCombatStats { get; private set; }

        /// <summary>Pousse les stats courantes du joueur local (appele chaque frame par le collecteur).</summary>
        public static void SetCombatStats(int damageDealt, int damageTaken, int spellsCast, int turns)
        {
            StatDamageDealt = damageDealt;
            StatDamageTaken = damageTaken;
            StatSpellsCast = spellsCast;
            StatTurns = turns;
            HasCombatStats = true;
        }

        /// <summary>Remet les stats a zero (debut de match cote collecteur, et apres report cote hub).</summary>
        public static void ResetCombatStats()
        {
            StatDamageDealt = 0;
            StatDamageTaken = 0;
            StatSpellsCast = 0;
            StatTurns = 0;
            HasCombatStats = false;
        }

        public static void SetPendingMatch(string matchId, string opponentSub, string opponentEmail,
                                            string localSub = null, string localEmail = null,
                                            string opponentDisplayName = null, string localDisplayName = null)
        {
            PendingMatchId = matchId;
            OpponentSub = opponentSub;
            OpponentEmail = opponentEmail;
            // POLISH-7 : nouveaux params optionnels (pour ne pas casser d'anciens callsites de tests).
            if (!string.IsNullOrEmpty(opponentDisplayName)) OpponentDisplayName = opponentDisplayName;
            if (!string.IsNullOrEmpty(localDisplayName)) LocalDisplayName = localDisplayName;
            // 4.14.c — localSub/localEmail optionnels pour ne pas casser les anciens callsites
            // (brique 4.8.d.ii). Set quand on entre dans un vrai match PvP via 4.14.e.
            if (!string.IsNullOrEmpty(localSub)) LocalSub = localSub;
            if (!string.IsNullOrEmpty(localEmail)) LocalEmail = localEmail;
        }

        public static void SetMatchResult(MatchResult result, string matchId, string opponentEmail,
                                          string opponentDisplayName = null)
        {
            LastMatchResult = result;
            LastMatchId = matchId;
            LastOpponentEmail = opponentEmail;
            // POLISH-7 : fallback sur OpponentDisplayName si nouveau param non fourni
            // (callsites pre-POLISH-7 qui n'ont pas encore migre).
            LastOpponentDisplayName = !string.IsNullOrEmpty(opponentDisplayName)
                ? opponentDisplayName
                : OpponentDisplayName;
            // 6.3 — capture le sub adverse + le flag ranked AVANT de clear le pending.
            LastOpponentSub = OpponentSub;
            LastIsRanked = IsRanked;
            // On clear la pending match — le match a été consommé.
            PendingMatchId = null;
            OpponentSub = null;
            OpponentEmail = null;
            OpponentDisplayName = null;
        }

        public static MatchResult ConsumeLastResult(out string matchId, out string opponentEmail,
                                                    out string opponentDisplayName,
                                                    out string opponentSub, out bool isRanked)
        {
            var r = LastMatchResult;
            matchId = LastMatchId;
            opponentEmail = LastOpponentEmail;
            opponentDisplayName = LastOpponentDisplayName;
            opponentSub = LastOpponentSub;
            isRanked = LastIsRanked;
            LastMatchResult = MatchResult.None;
            LastMatchId = null;
            LastOpponentEmail = null;
            LastOpponentDisplayName = null;
            LastOpponentSub = null;
            LastIsRanked = false;
            return r;
        }

        public static void Reset()
        {
            PendingMatchId = null;
            OpponentSub = null;
            OpponentEmail = null;
            OpponentDisplayName = null;
            LocalSub = null;
            LocalEmail = null;
            LocalDisplayName = null;
            IsRanked = false;
            LastMatchResult = MatchResult.None;
            LastMatchId = null;
            LastOpponentEmail = null;
            LastOpponentDisplayName = null;
            LastOpponentSub = null;
            LastIsRanked = false;
            ResetCombatStats();
        }
    }
}
