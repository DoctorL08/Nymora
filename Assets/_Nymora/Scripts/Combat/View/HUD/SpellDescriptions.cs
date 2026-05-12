using Quantum;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Descriptions Bible V7.1 condensees par SpellId (Soulrender 2.13.c, Nightseer 2.14+).
    /// Affichees dans la tooltip au survol d'une icone.
    ///
    /// Format : 1-2 phrases courtes, effet principal + variante HG si pertinente.
    /// Les couts/portees viennent de SpellRegistry et sont affiches separement.
    /// </summary>
    public static class SpellDescriptions
    {
        public static string Get(SpellId id)
        {
            switch (id)
            {
                // SOULRENDER — offensifs
                case SpellId.SoulrenderTrancheAme:
                    return "Inflige 220 dgts melee. Recul 2 cases si kill.";
                case SpellId.SoulrenderOuvrePlaie:
                    return "Inflige 110 dgts. +1 HG : 230 dgts + anti-heal 2 tours.";
                case SpellId.SoulrenderChargeBrutale:
                    return "Charge en ligne droite, 180 dgts a la 1ere cible. Pose Vapeur Carmin (1 tour).";
                case SpellId.SoulrenderDetonationSanglante:
                    return "AoE croix 3. 60 dgts +40 par HG depense (min 2 HG, max 5). Sang Coagule au centre.";
                case SpellId.SoulrenderCuree:
                    return "2 HG. 150 dgts. Kill : heal 50% HP manquants + 4 PA prochain tour. Miss : -60 HP self.";

                // SOULRENDER — tactiques
                case SpellId.SoulrenderPacteDeSang:
                    return "1/match. -80 HP self, +3 HG, +50% dgts sur le prochain sort offensif.";
                case SpellId.SoulrenderMarqueDeCarnage:
                    return "Marque la cible 3 tours. Tes casts Soulrender sur cible marquee : +1 HG bonus.";
                case SpellId.SoulrenderEmpoignade:
                    return "Tire la cible adjacente. Anti-teleport 1 tour.";
                case SpellId.SoulrenderRugissement:
                    return "AoE rayon 3 autour de toi. Ennemis subissent -1 PM + anti-tp (-2 PM si HP<50%).";
                case SpellId.SoulrenderRageInsatiable:
                    return "2 tours : tes sorts coutent +1 PA mais tu regen 1 PA apres chaque cast offensif.";

                // SOULRENDER — survie
                case SpellId.SoulrenderRiposteCarmin:
                    return "1 tour : prochaine attaque melee subie -> 100 dgts retour + -1 PM attaquant.";
                case SpellId.SoulrenderCauterisation:
                    return "Retire tous DoT. Heal 60 par DoT retire (min 60, max 180).";
                case SpellId.SoulrenderPeauDeFer:
                    return "Shield 200 HP / 2 tours. +30 dgts sur tes attaques melee pendant la duree.";
                case SpellId.SoulrenderSeveVive:
                    return "Heal 100. +1 HG : +60 heal. Si tu as un DoT actif : +50 heal.";
                case SpellId.SoulrenderDernierSouffle:
                    return "1/match. HP<30% requis. Heal 200 + 3 HG.";

                // SIGNATURE
                case SpellId.SoulrenderAmeLaceree:
                    return "5 HG. Inflige 320 dgts melee, heal toi de 50% des dgts qui passent. Cooldown 4 tours. Kill : Sang Coagule croix 5.";

                default:
                    return "(Description Bible non disponible)";
            }
        }
    }
}
