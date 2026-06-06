using Nymora.Core.Data;
using Quantum;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Descriptions Bible V7.1 affichees dans la tooltip combat au survol d'une icone.
    ///
    /// Pre-5.4 : switch hardcode Soulrender-only (16 sorts) -> les 4 autres classes
    /// (Nightseer, Colossar, Necram, Ghostra) affichaient "(Description Bible non
    /// disponible)" en combat IA + combat casual.
    ///
    /// 5.4 (18 mai 2026) : source unique <see cref="SpellBibleTexts"/> dans Nymora.Core
    /// (80 entries, partage avec le Deck Builder via PopulateSpellCatalog).
    /// </summary>
    public static class SpellDescriptions
    {
        /// <summary>
        /// Description "plain" (valeurs de base) : les tokens dynamiques {DMG:N}/{HEAL:N} sont résolus
        /// à leur valeur brute. Le tooltip combat passe désormais par <see cref="SpellTooltipText"/>
        /// (valeurs buffées en vert) ; cette méthode reste un fallback sans contexte de caster.
        /// </summary>
        public static string Get(SpellId id)
        {
            if (id == SpellId.None) return string.Empty;
            return SpellBibleTexts.TryGetByQuantumId((int)id, out var entry)
                ? SpellBibleTexts.ResolvePlain(entry.Description)
                : "(Description Bible non disponible)";
        }
    }
}
