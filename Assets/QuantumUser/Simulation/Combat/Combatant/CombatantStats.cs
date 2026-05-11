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

        public static int GetMaxHP(NymoraClass _) => BaseMaxHP;

        public static int GetMaxPA(NymoraClass _) => BaseMaxPA;

        public static int GetMaxPM(NymoraClass nymoraClass)
        {
            // Bible V7.1 : seul le Colossar a 2 PM (les autres = 3).
            return nymoraClass == NymoraClass.Colossar ? ColossarMaxPM : DefaultMaxPM;
        }
    }
}
