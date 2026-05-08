using Nymora.Core.Enums;

namespace Nymora.Core.Data
{
    /// <summary>
    /// Paquet de degats applique a une cible.
    /// Immuable. Les reductions/amplifications sont calculees par le systeme de combat,
    /// pas par cette structure.
    /// </summary>
    public readonly struct Damage
    {
        public readonly int Amount;
        public readonly DamageType Type;
        public readonly bool IsCritical;
        public readonly bool IgnoresReduction;

        public Damage(int amount, DamageType type, bool isCritical = false, bool ignoresReduction = false)
        {
            Amount = amount;
            Type = type;
            IsCritical = isCritical;
            IgnoresReduction = ignoresReduction;
        }

        public static Damage None => new Damage(0, DamageType.None);

        public override string ToString()
        {
            string crit = IsCritical ? " CRIT" : string.Empty;
            string ignore = IgnoresReduction ? " (ignores reductions)" : string.Empty;
            return $"{Amount} {Type}{crit}{ignore}";
        }
    }
}
