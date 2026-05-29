namespace Quantum
{
    /// <summary>
    /// Point central d'application des SOINS et de pose des BOUCLIERS, qui respecte les debuffs
    /// anti-sustain (refonte 29 mai 2026) :
    ///   - <see cref="StatusKind.AntiHealShield"/> : bloque TOUT soin recu (heal = 0).
    ///   - <see cref="StatusKind.HealReductionPercent"/> : reduit de Magnitude% les soins ET les
    ///     boucliers recus (Ouvre-Plaie 1 HG = 50 -> ÷2).
    ///
    /// Migration progressive : les sorts touches par la refonte routent leurs heals via
    /// <see cref="ApplyHeal"/> et leurs boucliers via <see cref="EffectiveShieldGain"/>. Les sites
    /// pas encore migres (autres classes) restent sur l'ancien check AntiHealShield inline et
    /// seront repris au fil des briques de classe.
    /// </summary>
    public static unsafe class HealHelper
    {
        /// <summary>
        /// Soigne `c` de `amount` HP en respectant AntiHealShield (bloque) et HealReductionPercent
        /// (reduit %). Clamp a MaxHP. Retourne le heal EFFECTIF applique (0 si bloque / negatif).
        /// </summary>
        public static int ApplyHeal(Combatant* c, int amount)
        {
            if (amount <= 0) return 0;
            if (StatusHelper.Has(c, StatusKind.AntiHealShield)) return 0;

            int reductionPct = StatusHelper.GetMagnitude(c, StatusKind.HealReductionPercent, 0);
            if (reductionPct > 0)
            {
                amount -= amount * reductionPct / 100;
            }
            if (amount <= 0) return 0;

            int before = c->HP;
            c->HP += amount;
            if (c->HP > c->MaxHP) c->HP = c->MaxHP;
            return c->HP - before;
        }

        /// <summary>
        /// Magnitude de bouclier EFFECTIVE a la pose : reduite de HealReductionPercent% si actif
        /// (Ouvre-Plaie ÷2). A appeler au moment ou un sort pose un ShieldActive sur `c`.
        /// </summary>
        public static int EffectiveShieldGain(Combatant* c, int magnitude)
        {
            if (magnitude <= 0) return 0;
            int reductionPct = StatusHelper.GetMagnitude(c, StatusKind.HealReductionPercent, 0);
            if (reductionPct > 0)
            {
                magnitude -= magnitude * reductionPct / 100;
            }
            return magnitude < 0 ? 0 : magnitude;
        }
    }
}
