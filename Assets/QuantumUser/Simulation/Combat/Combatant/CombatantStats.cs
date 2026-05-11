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
        public const int ColossarMaxPM = 2;

        // Caps de ressource de classe (Bible V7.1).
        public const int SoulrenderMaxHemoglyph = 5;
        public const int NightseerMaxPrescience = 4;
        public const int ColossarMaxFondation = 3;
        public const int NecramMaxPutrefaction = 6;
        public const int GhostraMaxRemanence = 3;

        public static int GetMaxHP(NymoraClass _) => BaseMaxHP;

        public static int GetMaxPA(NymoraClass _) => BaseMaxPA;

        public static int GetMaxPM(NymoraClass nymoraClass)
        {
            // Bible V7.1 : seul le Colossar a 2 PM (les autres = 3).
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
