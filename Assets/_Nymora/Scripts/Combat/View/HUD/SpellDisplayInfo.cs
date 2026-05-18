using Nymora.Core.Data;
using Quantum;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Helpers statiques pour les infos d'affichage d'un sort cote View (display name,
    /// arbitrage arm-or-cast).
    ///
    /// Les couts/portees viennent de SpellRegistry (Quantum, pure static). L'icone vient
    /// de SpellIconRegistry (SO). Le nom affiche vient de <see cref="SpellBibleTexts"/>
    /// (Nymora.Core) — source unique partagee avec le Deck Builder.
    ///
    /// Pre-5.4 : switch hardcode Soulrender-only -> les 4 autres classes affichaient
    /// l'enum brut (ex "GhostraLameSpectrale") en titre tooltip.
    /// </summary>
    public static class SpellDisplayInfo
    {
        public static string GetDisplayName(SpellId id)
        {
            return SpellBibleTexts.TryGetByQuantumId((int)id, out var entry)
                ? entry.DisplayName
                : id.ToString();
        }

        /// <summary>
        /// Faut-il armer le sort (puis attendre clic sur grille) ou le caster directement ?
        /// Regle : Filter == Self -> cast immediat sur le caster. Sinon -> arm + click grille.
        ///
        /// Source de verite : SpellRegistry.TryGet(id).Filter (Quantum, static).
        /// </summary>
        public static bool NeedsArming(SpellId id)
        {
            if (!SpellRegistry.TryGet(id, out SpellDef def)) return false;
            return def.Filter != TargetingFilter.Self;
        }
    }
}
