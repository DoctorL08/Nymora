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
                case SpellId.TestZap:
                    def = new SpellDef
                    {
                        PACost = 3,
                        Shape = TargetingShape.SingleTile,
                        Filter = TargetingFilter.Enemy,
                        RangeMin = 1,
                        RangeMax = 5,
                        DamageAmount = 100,
                    };
                    return true;

                default:
                    def = default;
                    return false;
            }
        }
    }
}
