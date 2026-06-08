using Quantum;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Patch UI combat 8 juin (3b) — Traduit un StatusKind en CODE COURT (≤6 car. ASCII, BMP-safe
    /// cf font TMP sans emoji) + une POLARITÉ pour colorer la chip affichée au-dessus du portrait
    /// timeline (malus rouge / bonus ambre / défense bleu). La description complète au survol reste
    /// fournie par <see cref="StatusEffectLabel"/>.
    ///
    /// Codes volontairement abrégés (placeholder lisible) : des icônes dessinées par le designer
    /// pourront les remplacer plus tard sans toucher la logique d'affichage.
    /// </summary>
    public static class StatusIconInfo
    {
        public enum Polarity { Malus, Buff, Defense, Neutral }

        /// <summary>
        /// Retourne false pour les statuts internes/cachés (minuteurs, flags réservés) -> l'appelant
        /// ne crée pas de chip. Les statuts non mappés (ajouts futurs) renvoient leur nom enum tronqué
        /// en Neutral plutôt que d'être masqués par accident.
        /// </summary>
        public static bool TryGet(StatusKind kind, int magnitude, out string code, out Polarity polarity)
        {
            switch (kind)
            {
                // --- Maluses (ce que la cible SUBIT) ---
                case StatusKind.MovementMalus:        code = $"-{magnitude}PM"; polarity = Polarity.Malus;   return true;
                case StatusKind.ActionMalus:          code = $"-{magnitude}PA"; polarity = Polarity.Malus;   return true;
                case StatusKind.AnchorImmune:         code = "NoMV";            polarity = Polarity.Malus;   return true;
                case StatusKind.AntiTeleport:         code = "NoTP";            polarity = Polarity.Malus;   return true;
                case StatusKind.AntiHealShield:       code = "NoSO";            polarity = Polarity.Malus;   return true;
                case StatusKind.HealReductionPercent: code = $"-{magnitude}%S"; polarity = Polarity.Malus;   return true;
                case StatusKind.Provoked:             code = "PROV";            polarity = Polarity.Malus;   return true;
                case StatusKind.PlaieOuverte:         code = "PLAI";            polarity = Polarity.Malus;   return true;
                case StatusKind.BleedDoT:             code = "SAIG";            polarity = Polarity.Malus;   return true;
                case StatusKind.MarqueSacrificielle:  code = "VEN+";            polarity = Polarity.Malus;   return true;
                case StatusKind.MarqueDeLOmbre:       code = "OMB+";            polarity = Polarity.Malus;   return true;
                case StatusKind.MarkedByCarnage:      code = "CARN";            polarity = Polarity.Malus;   return true;
                case StatusKind.Contagious:           code = "CONT";            polarity = Polarity.Malus;   return true;

                // --- Bonus (ce que le porteur GAGNE) ---
                case StatusKind.BuffNextOffensiveDmgPercent: code = $"+{magnitude}%"; polarity = Polarity.Buff; return true;
                case StatusKind.NextStrikeBonus:             code = $"+{magnitude}";  polarity = Polarity.Buff; return true;
                case StatusKind.RageInsatiableActive:        code = "RAGE";           polarity = Polarity.Buff; return true;
                case StatusKind.AffutActive:                 code = "AFUT";           polarity = Polarity.Buff; return true;
                case StatusKind.SangBouillantActive:         code = "SANG";           polarity = Polarity.Buff; return true;
                case StatusKind.EffondrementActive:          code = "EFFO";           polarity = Polarity.Buff; return true;
                case StatusKind.SymbioseMorbide:             code = "SYMB";           polarity = Polarity.Buff; return true;
                case StatusKind.PestilenceAura:              code = "SPOR";           polarity = Polarity.Buff; return true;
                case StatusKind.PasSpectralReady:            code = "PAS-S";          polarity = Polarity.Buff; return true;
                case StatusKind.PasAuDelaReady:              code = "PAS-A";          polarity = Polarity.Buff; return true;

                // --- Défenses / ripostes / auras ---
                case StatusKind.ShieldActive:           code = $"B{magnitude}";   polarity = Polarity.Defense; return true;
                case StatusKind.DamageReductionPercent: code = $"-{magnitude}%D"; polarity = Polarity.Defense; return true;
                case StatusKind.Untargetable:           code = "UNTG";            polarity = Polarity.Defense; return true;
                case StatusKind.DotImmune:              code = "iDoT";            polarity = Polarity.Defense; return true;
                case StatusKind.RipostMelee:            code = "RIPm";            polarity = Polarity.Defense; return true;
                case StatusKind.RipostAll:              code = "RIP";             polarity = Polarity.Defense; return true;
                case StatusKind.RoncesAura:             code = "RONC";            polarity = Polarity.Defense; return true;
                case StatusKind.CarapaceVisqueuse:      code = "CARA";            polarity = Polarity.Defense; return true;
                case StatusKind.LinceulDOmbres:         code = "LINC";            polarity = Polarity.Defense; return true;

                // --- Internes / cachés / réservés : pas de chip ---
                case StatusKind.None:
                case StatusKind.VeninDecay:
                case StatusKind.DirectionLocked:
                    code = ""; polarity = Polarity.Neutral; return false;

                // Ajout futur non mappé : chip neutre avec nom enum tronqué (visible, pas masqué).
                default:
                    string n = kind.ToString();
                    code = n.Length > 5 ? n.Substring(0, 5) : n;
                    polarity = Polarity.Neutral;
                    return true;
            }
        }
    }
}
