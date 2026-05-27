namespace Nymora.Core.Data
{
    /// <summary>
    /// Brique 5.10 (A2) — Pont hub → combat pour les cosmétiques équipés.
    ///
    /// Le combat (asmdef Nymora.Combat) ne référence PAS le réseau/backend (séparation des
    /// concerns). Le hub, qui résout déjà le skin/familier équipé via NymoraApiClient, écrit ici
    /// les cosmeticId du joueur LOCAL avant de lancer un combat. Le CombatantRenderer les lit puis
    /// résout les assets via les catalogues Core (CosmeticSkinCatalog / PetCatalog).
    ///
    /// Statique volontairement : survit au changement de scène (hub → combat). Réinitialisé par
    /// le hub à chaque résolution d'inventaire (équiper/déséquiper re-pousse la valeur courante).
    ///
    /// A3 ajoutera les cosmétiques de l'ADVERSAIRE (transmis par réseau / backend), indexés par
    /// PlayerIndex Quantum.
    /// </summary>
    public static class CombatCosmeticsContext
    {
        /// <summary>cosmeticId du skin équipé par le joueur local ("" = aucun → visuel de classe).</summary>
        public static string LocalSkinId = "";

        /// <summary>cosmeticId du familier équipé par le joueur local ("" = aucun). Utilisé en B5.</summary>
        public static string LocalPetId = "";
    }
}
