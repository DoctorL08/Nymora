using System.Text.RegularExpressions;
using Nymora.Core.Data;
using Quantum;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Résolution des tokens de valeurs dynamiques ({DMG:N} / {HEAL:N}) dans les descriptions de
    /// sorts, CÔTÉ COMBAT : remplace le nombre de base par la valeur EFFECTIVE en fonction des bonus
    /// ACTIFS du caster local (passif phasé, buffs self…), et la colore en VERT si elle diffère de la
    /// base. Hors combat / sans caster -> résolution "plain" (valeur brute) via SpellBibleTexts.
    ///
    /// Périmètre (le tooltip s'affiche SANS cible sélectionnée) : uniquement les bonus CÔTÉ CASTER,
    /// lisibles depuis sa fiche Combatant. Les bonus dépendant de la cible (dorsal Angle Mort, Marque
    /// de l'Ombre sur l'ennemi, « +X si Traqué ») ne sont PAS modélisés ici.
    ///
    /// Chantier livré classe par classe (brique par brique). Brique 1 (6 juin) : Nightseer
    /// (passif phasé +30 flat dégâts en phase ≥ 2 / +1 portée). Les autres classes (Soulrender Pacte
    /// de Sang %, Sang Bouillant, Colossar Densité Inerte…) s'ajoutent dans <see cref="BuffedDamage"/>.
    /// </summary>
    public static class SpellTooltipText
    {
        public const string GreenHex = "#7CFC8A";

        private static readonly Regex TokenRegex = new Regex(@"\{(DMG|HEAL):(\d+)\}", RegexOptions.Compiled);

        /// <summary>
        /// Description du sort avec tokens résolus selon les bonus actifs du caster (vert si buffé).
        /// hasCaster=false ou sort introuvable -> résolution "plain" (valeurs brutes).
        /// </summary>
        public static string ResolveDescription(SpellId spell, in Combatant caster, bool hasCaster)
        {
            string raw = SpellBibleTexts.TryGetByQuantumId((int)spell, out var entry)
                ? entry.Description
                : "(Description Bible non disponible)";

            if (string.IsNullOrEmpty(raw) || raw.IndexOf('{') < 0) return raw;
            if (!hasCaster || !SpellRegistry.TryGet(spell, out SpellDef def))
                return SpellBibleTexts.ResolvePlain(raw);

            // 'in' ne peut pas être capturé par la lambda -> copie locale (struct, lecture seule).
            Combatant c = caster;
            SpellDef d = def;
            return TokenRegex.Replace(raw, m =>
            {
                int baseVal = int.Parse(m.Groups[2].Value);
                bool isDmg = m.Groups[1].Value == "DMG";
                int buffed = isDmg ? BuffedDamage(c, d, baseVal) : BuffedHeal(c, d, baseVal);
                return buffed == baseVal
                    ? baseVal.ToString()
                    : $"<color={GreenHex}>{buffed}</color>";
            });
        }

        /// <summary>
        /// Dégâts effectifs d'un sort offensif après les bonus CÔTÉ CASTER (Nightseer phase +30,
        /// Pacte de Sang +%, Peau de Fer +30 mêlée, Frénésie +%, Sang Bouillant +flat). Délègue au
        /// mirror sim <see cref="Quantum.SpellTooltipStats"/> (accès StatusHelper unsafe centralisé).
        /// </summary>
        public static int BuffedDamage(in Combatant caster, in SpellDef def, int baseDamage)
            => SpellTooltipStats.CasterEffectiveDamage(caster, def, baseDamage);

        /// <summary>Soin effectif après bonus côté caster. Aucun bonus de soin côté caster modélisé pour l'instant.</summary>
        public static int BuffedHeal(in Combatant caster, in SpellDef def, int baseHeal) => baseHeal;

        /// <summary>
        /// Portée effective affichée (ligne de coût) après le bonus de phase Nightseer (+1 en phase ≥ 2).
        /// Mirror sim : la portée est évaluée AVANT la dépense de PR (SpellSystem ligne ~334) -> ressource
        /// ACTUELLE (pas post-coût, contrairement aux dégâts). Renvoie aussi via out si elle est buffée.
        /// </summary>
        public static int EffectiveRange(in Combatant caster, in SpellDef def, out bool buffed)
        {
            int eff = NightseerPassif.RangeWithPhaseBonus(caster.Class, caster.Resource, def.RangeMax);
            buffed = eff != def.RangeMax;
            return eff;
        }
    }
}
