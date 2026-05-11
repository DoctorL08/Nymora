namespace Quantum
{
    /// <summary>
    /// Definition statique d'un sort (cost + targeting + effets).
    /// En 2.7 le seul effet supporte est Damage (flat).
    /// Les autres (Heal, ApplyMark, Push, Pull, Spawn) viendront avec leurs sorts
    /// respectifs (2.9 Hemoglyphe, 2.10 sorts Soulrender complets, etc.).
    /// </summary>
    public struct SpellDef
    {
        public int PACost;
        public TargetingShape Shape;
        public TargetingFilter Filter;
        public int RangeMin;
        public int RangeMax;
        public int DamageAmount;
    }

    /// <summary>
    /// Catalogue statique des sorts par SpellId.
    /// Switch deterministe (pas de Dictionary heap-alloc).
    /// </summary>
    public static class SpellRegistry
    {
        public static bool TryGet(SpellId id, out SpellDef def)
        {
            switch (id)
            {
                // SOULRENDER — Tranche-Ame (Bible V7.1 : 3 PA, portee 1 melee, 220 dgts, +recul 2 cases si kill)
                // TODO 2.11 : effet "recul gratuit 2 cases si kill" (necessite systeme de mort + mouvement non-PM).
                case SpellId.SoulrenderTrancheAme:
                    def = new SpellDef
                    {
                        PACost = 3,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = 1,
                        DamageAmount = 220,
                    };
                    return true;

                // TestZap est conserve dans l'enum SpellId pour ne pas casser d'eventuelles commands
                // serialisees, mais retire du registry actif : il n'est plus castable.
                // Le sort de debug est remplace par Tranche-Ame qui est un vrai sort Bible V7.1.
                default:
                    def = default;
                    return false;
            }
        }
    }
}
