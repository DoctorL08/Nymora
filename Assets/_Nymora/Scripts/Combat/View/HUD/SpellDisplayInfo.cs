using Quantum;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Helpers statiques pour les infos d'affichage d'un sort cote View (display name,
    /// raccourci clavier, faut-il armer avant cast).
    ///
    /// Les couts/portees viennent de SpellRegistry (Quantum, pure static). L'icone vient
    /// de SpellIconRegistry (SO). Ici on ajoute juste le texte humain + arbitrage arm.
    ///
    /// Etend ce switch quand on ajoute Nightseer (2.14) puis les autres classes.
    /// </summary>
    public static class SpellDisplayInfo
    {
        public static string GetDisplayName(SpellId id)
        {
            switch (id)
            {
                case SpellId.SoulrenderTrancheAme:          return "Tranche-Ame";
                case SpellId.SoulrenderOuvrePlaie:          return "Ouvre-Plaie";
                case SpellId.SoulrenderPacteDeSang:         return "Pacte de Sang";
                case SpellId.SoulrenderRugissement:         return "Rugissement";
                case SpellId.SoulrenderRageInsatiable:      return "Rage Insatiable";
                case SpellId.SoulrenderRiposteCarmin:       return "Riposte Carmin";
                case SpellId.SoulrenderMarqueDeCarnage:     return "Marque de Carnage";
                case SpellId.SoulrenderEmpoignade:          return "Empoignade";
                case SpellId.SoulrenderPeauDeFer:           return "Peau de Fer";
                case SpellId.SoulrenderSeveVive:            return "Seve Vive";
                case SpellId.SoulrenderDernierSouffle:      return "Dernier Souffle";
                case SpellId.SoulrenderChargeBrutale:       return "Charge Brutale";
                case SpellId.SoulrenderDetonationSanglante: return "Detonation Sanglante";
                case SpellId.SoulrenderCuree:               return "Curee";
                case SpellId.SoulrenderCauterisation:       return "Cauterisation";
                case SpellId.SoulrenderAmeLaceree:          return "Ame Laceree";
                default:                                    return id.ToString();
            }
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
