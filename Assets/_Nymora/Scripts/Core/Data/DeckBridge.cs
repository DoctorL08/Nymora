namespace Nymora.Core.Data
{
    /// <summary>
    /// Brique 5.3.g — Bridge static cross-scene pour transmettre le deck equipe
    /// du hub (Deck Builder) vers la scene combat (CombatHUDController).
    ///
    /// Place dans Nymora.Core (asmdef accessible Hub + Combat). Pattern miroir de
    /// MatchBridge mais place ici car les 2 cotes en ont besoin (Hub set, Combat read).
    ///
    /// Usage :
    ///   Hub : DeckBridge.SetPendingDeck(classId, spellIds) puis SceneManager.LoadScene(...)
    ///   Combat : if (DeckBridge.HasPending) { CombatHUDController lit PendingSpellIds }
    ///
    /// Les champs sont static donc survivent au LoadScene. Clear() apres consommation.
    /// </summary>
    public static class DeckBridge
    {
        /// <summary>
        /// Classe selectionnee : "Soulrender" | "Nightseer" | "Colossar" | "Necram" | "Ghostra".
        /// Null si aucun deck pending.
        /// </summary>
        public static string PendingClassId { get; private set; }

        /// <summary>
        /// 6 SpellIdTech (snake_case, ex "soulrender_tranche_ame") du deck equipe.
        /// Null si aucun deck pending.
        /// </summary>
        public static string[] PendingSpellIds { get; private set; }

        /// <summary>Optionnel : nom du deck pour affichage (debug / system line combat).</summary>
        public static string PendingDeckName { get; private set; }

        public static bool HasPending =>
            !string.IsNullOrEmpty(PendingClassId)
            && PendingSpellIds != null
            && PendingSpellIds.Length == 6;

        public static void SetPendingDeck(string classId, string[] spellIds, string deckName = null)
        {
            PendingClassId = classId;
            PendingSpellIds = spellIds;
            PendingDeckName = deckName;
        }

        public static void Clear()
        {
            PendingClassId = null;
            PendingSpellIds = null;
            PendingDeckName = null;
        }
    }
}
