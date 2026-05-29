namespace Quantum
{
    /// <summary>
    /// Stats Bible V7.1 par classe — verrouillees, ne PAS modifier sans incrementer
    /// CombatRulesVersion (cote Nymora.Core.GameVersion) car ces valeurs definissent
    /// l'equilibrage du combat.
    /// </summary>
    public static class CombatantStats
    {
        public const int BaseMaxHP = 1500;
        public const int BaseMaxPA = 8;
        public const int DefaultMaxPM = 3;
        // Refonte 29 mai : Colossar passe a 3 PM (plus aucune classe a 2 PM).
        public const int ColossarMaxPM = 3;

        // Caps de ressource de classe (Bible V7.1).
        public const int SoulrenderMaxHemoglyph = 5;
        // Refonte 29 mai : Prescience cap 4 -> 5 (passif phasé, palier 3 à 5 PR).
        public const int NightseerMaxPrescience = 5;
        // Refonte 29 mai : Fondation cap 3 -> 5.
        public const int ColossarMaxFondation = 5;
        public const int NecramMaxPutrefaction = 6;
        public const int GhostraMaxRemanence = 3;

        public static int GetMaxHP(NymoraClass _) => BaseMaxHP;

        public static int GetMaxPA(NymoraClass _) => BaseMaxPA;

        public static int GetMaxPM(NymoraClass nymoraClass)
        {
            // Refonte 29 mai : toutes les classes a 3 PM (Colossar n'est plus a 2).
            return nymoraClass == NymoraClass.Colossar ? ColossarMaxPM : DefaultMaxPM;
        }

        /// <summary>
        /// Cap de la ressource de classe pour cette classe. Retourne 0 pour les
        /// classes non encore implementees (Nightseer/Colossar/Necram/Ghostra
        /// en attendant Phase 2.13 et Phase 3).
        /// </summary>
        public static int GetMaxResource(NymoraClass nymoraClass)
        {
            switch (nymoraClass)
            {
                case NymoraClass.Soulrender: return SoulrenderMaxHemoglyph;
                case NymoraClass.Nightseer:  return NightseerMaxPrescience;
                case NymoraClass.Colossar:   return ColossarMaxFondation;
                case NymoraClass.Necram:     return NecramMaxPutrefaction;
                case NymoraClass.Ghostra:    return GhostraMaxRemanence;
                default:                     return 0;
            }
        }
    }
}
