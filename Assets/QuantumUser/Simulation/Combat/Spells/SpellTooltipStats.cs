namespace Quantum
{
    /// <summary>
    /// Mirror READ-ONLY des bonus de dégâts CÔTÉ CASTER (target-indépendants) pour l'affichage du
    /// tooltip de sort en combat (valeurs buffées en vert). Vit dans la sim (unsafe + accès StatusHelper)
    /// et est appelé par la View (Nymora.Combat.View.HUD.SpellTooltipText) qui n'a pas le droit unsafe.
    ///
    /// Périmètre : UNIQUEMENT les bonus lisibles depuis la fiche du caster, SANS cible (le tooltip
    /// s'affiche sans cible sélectionnée). Mirror exact de l'ordre de SpellSystem (cast réel) et de
    /// SpellPreview.FinalizeOffensive :
    ///   1. flats côté caster : Nightseer passif +30 (phase ≥ 2, évaluée POST-coût PR) ;
    ///   2. Pacte de Sang : +% (BuffNextOffensiveDmgPercent) ;
    ///   3. Peau de Fer : +30 si MÊLÉE (RangeMax 1) et ShieldActive ;
    ///   4. Frénésie : +% tant que RageInsatiableActive ;
    ///   5. Sang Bouillant : +flat (NextStrikeBonus).
    ///
    /// NON modélisés (dépendent de la cible ou de la grille) : dorsal Angle Mort, Marque de l'Ombre,
    /// "si Traqué", réduction défensive, shield ; Densité Inerte +20 (nécessite la Frame/adjacence).
    /// </summary>
    public static unsafe class SpellTooltipStats
    {
        /// <summary>
        /// Dégâts effectifs d'un sort offensif après les bonus côté caster. baseDamage = nombre de base
        /// (token {DMG:N}). Renvoie baseDamage tel quel si le sort n'est pas offensif ou base &lt;= 0.
        /// </summary>
        public static int CasterEffectiveDamage(Combatant caster, SpellDef def, int baseDamage)
        {
            if (baseDamage <= 0 || def.IsOffensive == 0) return baseDamage;

            Combatant local = caster;     // copie pile -> adressable en unsafe sans fixed
            Combatant* c = &local;
            int total = baseDamage;

            // 1. Flats côté caster. Nightseer : +30 en phase >= 2 (PR 3-4+), phase POST-coût PR
            //    (la sim consomme le PR avant d'appliquer le bonus -> ex Salve 3 PR retombe sous phase 2).
            if (caster.Class == NymoraClass.Nightseer)
            {
                int phaseRes = caster.Resource - def.HGCostMandatory;
                if (phaseRes < 0) phaseRes = 0;
                if (NightseerPassif.FlatDamageRangeBonusActive(phaseRes))
                    total += NightseerPassif.FlatDamageBonus;
            }

            // 2. Pacte de Sang : +% sur le prochain sort offensif.
            int pactePct = StatusHelper.GetMagnitude(c, StatusKind.BuffNextOffensiveDmgPercent, 0);
            if (pactePct > 0) total += total * pactePct / 100;

            // 3. Peau de Fer : +30 MÊLÉE (RangeMax 1) si ShieldActive.
            if (def.RangeMax == 1 && StatusHelper.GetMagnitude(c, StatusKind.ShieldActive, 0) > 0)
                total += SpellRegistry.PeauDeFerMeleeDmgBonus;

            // 4. Frénésie : +% tant que RageInsatiableActive.
            if (StatusHelper.Has(c, StatusKind.RageInsatiableActive))
                total += total * SpellRegistry.FrenesieDmgBonusPct / 100;

            // 5. Sang Bouillant : +flat (prochaine frappe).
            int nextStrike = StatusHelper.GetMagnitude(c, StatusKind.NextStrikeBonus, 0);
            if (nextStrike > 0) total += nextStrike;

            return total;
        }
    }
}
