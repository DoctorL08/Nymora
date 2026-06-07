using Quantum;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Patch 5 juin — Traduit un StatusKind (+ sa magnitude) en libellé d'EFFET concis pour les
    /// panneaux d'infos joueurs (ResourcePanelView, coins haut-gauche/droite) et le tooltip de la
    /// timeline (TimelineSlotTooltipBuilder), au lieu d'afficher le nom technique de l'enum
    /// (« MovementMalus », « AnchorImmune »...). Demande Lorenzo : « plutôt que d'écrire les bonus
    /// ou les malus, juste ce que ça fait, genre -x PA / -x PM / malus tp ».
    ///
    /// Retourne "" pour les statuts internes/cachés (minuteurs, flags réservés) -> l'appelant les
    /// saute. Les statuts non mappés (ajouts futurs) retombent sur leur nom enum (visible mais
    /// technique) pour ne rien masquer par accident.
    /// </summary>
    public static class StatusEffectLabel
    {
        /// <summary>Libellé d'effet SANS la durée (l'appelant ajoute « (Nt) »). "" = à masquer.</summary>
        public static string Describe(StatusKind kind, int magnitude)
        {
            switch (kind)
            {
                // --- Maluses (ce que la cible SUBIT) ---
                case StatusKind.MovementMalus:               return $"-{magnitude} PM";
                case StatusKind.ActionMalus:                 return $"-{magnitude} PA";
                case StatusKind.AnchorImmune:                return "anti-déplacement";
                case StatusKind.AntiTeleport:                return "anti-téléport";
                case StatusKind.AntiHealShield:              return "soin/bouclier bloqué";
                case StatusKind.HealReductionPercent:        return $"-{magnitude}% soins reçus";
                case StatusKind.Provoked:                    return "provoqué";
                case StatusKind.Untargetable:                return "intouchable";
                case StatusKind.PlaieOuverte:                return $"plaie ouverte -{magnitude}/tour";
                case StatusKind.BleedDoT:                    return "saignement";
                case StatusKind.MarqueSacrificielle:         return $"venin +{magnitude}/tick";
                case StatusKind.MarqueDeLOmbre:              return $"marque de l'ombre +{magnitude}";
                case StatusKind.MarkedByCarnage:             return "marqué (Carnage)";
                case StatusKind.Contagious:                  return "contagion";

                // --- Bonus / défenses (ce que le porteur GAGNE) ---
                case StatusKind.ShieldActive:                return $"bouclier {magnitude}";
                case StatusKind.DamageReductionPercent:      return $"-{magnitude}% dégâts subis";
                case StatusKind.BuffNextOffensiveDmgPercent: return $"+{magnitude}% prochain sort";
                case StatusKind.NextStrikeBonus:             return $"+{magnitude} prochaine frappe";
                case StatusKind.RageInsatiableActive:        return "rage";
                case StatusKind.AffutActive:                 return $"affût (+2 portée, +{magnitude}%)";
                case StatusKind.SangBouillantActive:         return "sang bouillant";
                case StatusKind.EffondrementActive:          return "Effondrement (-1 PA, -30%)";
                case StatusKind.SymbioseMorbide:             return "vol de vie venin";
                case StatusKind.PestilenceAura:              return "nuée de spores";
                case StatusKind.DotImmune:                   return "immunisé DoT";
                case StatusKind.PasSpectralReady:            return "pas spectral";
                case StatusKind.PasAuDelaReady:              return "pas de l'au-delà";

                // --- Ripostes / auras ---
                case StatusKind.RipostMelee:                 return $"riposte {magnitude} (mêlée)";
                case StatusKind.RipostAll:                   return $"riposte {magnitude}";
                case StatusKind.RoncesAura:                  return $"aura de ronces ({magnitude})";
                case StatusKind.CarapaceVisqueuse:           return "carapace visqueuse";
                case StatusKind.LinceulDOmbres:              return $"linceul (riposte {magnitude})";

                // --- Internes / cachés / réservés : non affichés ---
                case StatusKind.None:                        return "";
                case StatusKind.VeninDecay:                  return ""; // minuteur d'expiration venin (caché)
                case StatusKind.DirectionLocked:             return ""; // réservé, plus utilisé

                // Ajout futur non mappé : on montre le nom enum (visible) plutôt que de le masquer.
                default:                                     return kind.ToString();
            }
        }
    }
}
