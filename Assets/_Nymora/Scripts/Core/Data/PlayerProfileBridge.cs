namespace Nymora.Core.Data
{
    /// <summary>
    /// Bridge in-memory cross-scene pour transmettre pseudo + clan du joueur local et
    /// de l'adversaire (PvP) au combat. Set par le hub (HubArenaPanel / HubMatchTransition)
    /// avant LoadScene combat, lu par CombatantTooltipView (Combat.View).
    ///
    /// Sépare du MatchBridge volontairement : MatchBridge porte l'identite reseau
    /// (matchId, sub, email) requis par Photon Realtime room ; PlayerProfileBridge porte
    /// les infos UI affichage (pseudo human-friendly, clan). Pas besoin d'avoir tout dans
    /// un seul bridge geant.
    ///
    /// Static class : persiste entre LoadScene tant que le process Unity tourne.
    /// </summary>
    public static class PlayerProfileBridge
    {
        public static string LocalPseudo { get; private set; }
        public static string LocalClan { get; private set; }
        public static string OpponentPseudo { get; private set; }
        public static string OpponentClan { get; private set; }

        public static void SetLocal(string pseudo, string clan)
        {
            LocalPseudo = pseudo ?? "";
            LocalClan = clan ?? "";
        }

        public static void SetOpponent(string pseudo, string clan)
        {
            OpponentPseudo = pseudo ?? "";
            OpponentClan = clan ?? "";
        }

        public static void ClearOpponent()
        {
            OpponentPseudo = "";
            OpponentClan = "";
        }
    }
}
