namespace Quantum
{
    /// <summary>
    /// Resultat preview pour un sort sur une cible. Read-only struct.
    /// </summary>
    public struct DamagePreviewResult
    {
        public bool Valid;
        public int RawDamage;
        public int OffensiveBonus;
        public int DefensiveReductionPercent;
        public int FinalDamage;
        public int AbsorbedByShield;
        public int HpLost;
        public int Healed;
        public int ShieldGained;
    }

    /// <summary>
    /// POLISH-6a-f (19 mai 2026) — Helper PUR read-only qui calcule la preview damage/heal/shield
    /// d'un sort sur une cible AVANT cast. Permet a la View d'afficher au survol "ce que ferait
    /// le sort si je clique ici". Source : mirror du pipeline SpellSystem.cs (memes constantes
    /// SpellRegistry, memes helpers passifs).
    ///
    /// Scope :
    ///   - POLISH-6a : Ghostra Frappe Fantome (ref implementation)
    ///   - POLISH-6b-f : Soulrender + Nightseer + Colossar + Necram + Ghostra (80 sorts)
    ///
    /// Modifiers couverts (pipeline offensif generique) :
    ///   - Pacte de Sang (BuffNextOffensiveDmgPercent +50%)
    ///   - Peau de Fer (+30 melee si caster a ShieldActive)
    ///   - Bonus dorsal Ghostra (+0/+50/+80 selon leurres)
    ///   - Marque de l'Ombre Ghostra (+20)
    ///   - Reduction defensive cible (Densite Inerte + Ancrage + Garde Protectrice cap 50%)
    ///   - Shield absorption (ShieldActive)
    ///   - AntiHealShield (bloque heal)
    ///
    /// Approximations (Bible-fidele a 90% — debug visuel + ajustement par sort plus tard) :
    ///   - Sorts AoE multi-cible : preview du dmg base par cible touchee (cas central)
    ///   - Sorts mobilite avec hit (Charge Brutale, Pas de l'Au-Dela) : dmg final post-modifiers
    ///   - Effects post-damage (PlaieOuverte, marques venin, status apply) NON modelises dans dmg
    ///     mais affiches eventuellement separement plus tard
    /// </summary>
    public unsafe static class SpellPreview
    {
        public static bool TryCompute(Frame f, EntityRef casterEntity, EntityRef targetEntity, SpellId spellId, out DamagePreviewResult preview)
        {
            preview = default;
            if (f == null) return false;
            if (!f.Unsafe.TryGetPointer<Combatant>(casterEntity, out Combatant* caster)) return false;
            if (!f.Unsafe.TryGetPointer<Combatant>(targetEntity, out Combatant* target)) return false;

            switch (spellId)
            {
                // ============== GHOSTRA ==============
                case SpellId.GhostraFrappeFantome:
                    return TryComputeFrappeFantome(f, caster, target, out preview);
                case SpellId.GhostraLameSpectrale:
                    return TryComputeLameSpectrale(f, caster, target, out preview);
                case SpellId.GhostraLameVoraceSpectrale:
                    return TryComputeLameVorace(f, caster, target, out preview);
                case SpellId.GhostraSaigneAme:
                    return TryComputeSaigneAme(f, caster, target, out preview);
                case SpellId.GhostraDanseDesLames:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.DanseDesLamesDmg, out preview);
                case SpellId.GhostraVolteFace:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.VolteFaceDmg, out preview);
                case SpellId.GhostraDagueLancee:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.DagueLanceeDmgBase, out preview);
                case SpellId.GhostraExecutionSpectrale:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.ExecutionSpectraleDamage, out preview);
                case SpellId.GhostraPasDeLAuDela:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.PasAuDelaDorsalDamage, out preview);
                case SpellId.GhostraLinceulDOmbres:
                    return TryComputeShieldSelf(SpellRegistry.LinceulDOmbresShieldHP, out preview);
                case SpellId.GhostraDernierPas:
                    return TryComputeHealSelf(f, caster, SpellRegistry.DernierPasHealAmount, out preview);

                // ============== SOULRENDER ==============
                case SpellId.SoulrenderChargeBrutale:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.ChargeBrutaleDamage, out preview);
                case SpellId.SoulrenderOuvrePlaie:
                    // Bible 110 base, +120 si 1 HG depense (preview pessimiste : sans HG).
                    return TryComputeOffensiveSimple(f, caster, target, 110, out preview);
                case SpellId.SoulrenderTrancheAme:
                    return TryComputeOffensiveSimple(f, caster, target, 220, out preview);
                case SpellId.SoulrenderDetonationSanglante:
                    // Bible : 60 base + 40 par HG depense optionnel. Preview pessimiste : sans HG.
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.DetonationBaseDamage, out preview);
                case SpellId.SoulrenderAmeLaceree:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.AmeLaceeDamage, out preview);
                case SpellId.SoulrenderCuree:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.CureeDamage, out preview);
                case SpellId.SoulrenderPeauDeFer:
                    return TryComputeShieldSelf(SpellRegistry.PeauDeFerShieldHP, out preview);
                case SpellId.SoulrenderSeveVive:
                    return TryComputeHealSelf(f, caster, SpellRegistry.SeveViveHealBase, out preview);
                case SpellId.SoulrenderDernierSouffle:
                    return TryComputeHealSelf(f, caster, SpellRegistry.DernierSouffleHealAmount, out preview);
                case SpellId.SoulrenderCauterisation:
                    // SANG BOUILLANT (refonte 29 mai) : buff reactif sans nombre immediat -> pas de
                    //   preview chiffre (comme Frenesie / Riposte Carmin).
                    preview = default;
                    return false;

                // ============== NIGHTSEER ==============
                case SpellId.NightseerTirPrecis:
                {
                    int dmg = MarkHelpers.HasMark(target, MarkKind.Traque)
                        ? SpellRegistry.TirPrecisDmgIfTraque
                        : SpellRegistry.TirPrecisDmg;
                    return TryComputeOffensiveSimple(f, caster, target, dmg, out preview);
                }
                case SpellId.NightseerVoleeDEpines:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.VoleeDEpinesDmg, out preview);
                case SpellId.NightseerDetonationOnirique:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.DetonationOniriqueDmg, out preview);
                case SpellId.NightseerFrappeDeLOmbre:
                {
                    // Refonte 29 mai : 160 (+50 si le Nightseer a dépensé >= 3 PM au dernier tour).
                    int dmg = SpellRegistry.FrappeDeLOmbreDmg;
                    if (caster->PMSpentLastTurn >= 3) dmg += SpellRegistry.FrappeDeLOmbreDmgBonusPM;
                    return TryComputeOffensiveSimple(f, caster, target, dmg, out preview);
                }
                case SpellId.NightseerVoileDOmbre: // FLÈCHE TRAÇANTE (refonte 29 mai)
                {
                    // 60/PM dépensé au dernier tour (max 180) si la cible est Traqué, sinon 0.
                    int dmg = 0;
                    if (MarkHelpers.HasMark(target, MarkKind.Traque))
                    {
                        dmg = caster->PMSpentLastTurn * SpellRegistry.FlecheTracanteDmgPerPM;
                        if (dmg > SpellRegistry.FlecheTracanteMaxDmg) dmg = SpellRegistry.FlecheTracanteMaxDmg;
                    }
                    return TryComputeOffensiveSimple(f, caster, target, dmg, out preview);
                }
                case SpellId.NightseerSalveMortelle:
                {
                    // Bible : 220 centre / 130 cotes. Preview = centre (Lorenzo verra cas par cas).
                    int dmg = SpellRegistry.SalveMortelleDmgCenter;
                    if (MarkHelpers.HasMark(target, MarkKind.Traque)) dmg += SpellRegistry.SalveMortelleDmgIfTraque;
                    return TryComputeOffensiveSimple(f, caster, target, dmg, out preview);
                }
                case SpellId.NightseerSouffleGlacial: // PIÈGE BONDISSANT (refonte 29 mai)
                    // Pose de piège-catapulte (pas de dégât direct) -> pas de preview chiffré.
                    preview = default;
                    return false;
                case SpellId.NightseerFiletDeRonces:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.FiletDeRoncesDmg, out preview);
                case SpellId.NightseerChampDeMines:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.ChampDeMinesDmg, out preview);
                case SpellId.NightseerCamouflageRonces:
                    return TryComputeShieldSelf(SpellRegistry.CamouflageRoncesShieldHP, out preview);
                case SpellId.NightseerSeveSauvage:
                    return TryComputeHealSelf(f, caster, SpellRegistry.SeveSauvageHealBase, out preview);
                case SpellId.NightseerEvanescence:
                    return TryComputeHealSelf(f, caster, SpellRegistry.EvanescenceHeal, out preview);
                case SpellId.NightseerTraquenard:
                {
                    int dmg = SpellRegistry.TraquenardDmgBase;
                    // Si target porte Traque/Voile/Empreinte : +80
                    if (MarkHelpers.HasMark(target, MarkKind.Traque)
                        || MarkHelpers.HasMark(target, MarkKind.Empreinte))
                    {
                        dmg += SpellRegistry.TraquenardDmgBonusIfMarked;
                    }
                    return TryComputeOffensiveSimple(f, caster, target, dmg, out preview);
                }

                // ============== COLOSSAR ==============
                case SpellId.ColossarFrappeLourde:
                {
                    int dmg = ColossarPassif.IsTargetPinnedFromCaster(f, caster, target)
                        ? SpellRegistry.FrappeLourdeDmgIfPinned
                        : SpellRegistry.FrappeLourdeDmgBase;
                    return TryComputeOffensiveSimple(f, caster, target, dmg, out preview);
                }
                case SpellId.ColossarMarteauPunisseur:
                {
                    int dmg = target->PA < SpellRegistry.MarteauPunisseurDepletedPAThreshold
                        ? SpellRegistry.MarteauPunisseurDmgIfDepleted
                        : SpellRegistry.MarteauPunisseurDmg;
                    return TryComputeOffensiveSimple(f, caster, target, dmg, out preview);
                }
                case SpellId.ColossarRepresailles:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.RepresaillesDmgImmediate, out preview);
                case SpellId.ColossarOndeDeChoc:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.OndeDeChocDmg, out preview);
                case SpellId.ColossarChocSismique:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.ChocSismiqueDmgBase, out preview);
                case SpellId.ColossarBrisure:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.BrisureDamage, out preview);
                case SpellId.ColossarEffondrement:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.EffondrementDamage, out preview);
                case SpellId.ColossarStoicisme:
                    return TryComputeShieldSelf(SpellRegistry.StoicismeShieldHP, out preview);
                case SpellId.ColossarRessacVital:
                    return TryComputeHealSelf(f, caster, SpellRegistry.RessacVitalHealBase, out preview);
                case SpellId.ColossarSoinLourd:
                    // EBOULEMENT (refonte 29 mai) : AoE 150 autour d'un Pilier ciblé (pas de cible
                    //   combattant directe) -> pas de preview chiffré sur la case.
                    preview = default;
                    return false;

                // ============== NECRAM ==============
                case SpellId.NecramCrachatAcide:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.CrachatAcideDmg, out preview);
                case SpellId.NecramMorsurePutride:
                {
                    int marks = target->VeninStacks;
                    int bonus = marks * SpellRegistry.MorsurePutrideDmgPerMark;
                    if (bonus > SpellRegistry.MorsurePutrideDmgBonusCap) bonus = SpellRegistry.MorsurePutrideDmgBonusCap;
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.MorsurePutrideDmgBase + bonus, out preview);
                }
                case SpellId.NecramDetonationVirulente:
                {
                    // Refonte 29 mai : tick venin complet = marques * clock Floraison + Marque Sac (bypass).
                    int density = VeninHelpers.GetGlobalDensity(f);
                    int dmg = target->VeninStacks * VeninHelpers.GetTickDmgPerMark(density)
                            + StatusHelper.GetMagnitude(target, StatusKind.MarqueSacrificielle, 0);
                    return TryComputeOffensiveSimple(f, caster, target, dmg, out preview);
                }
                case SpellId.NecramFauxDecharnee:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.FauxDecharneeDmg, out preview);
                case SpellId.NecramBrumeToxique:
                    // Refonte 29 mai : zone de marques + tick majoré, plus de dégâts directs -> pas de preview chiffré.
                    preview = default;
                    return false;
                case SpellId.NecramDrainVital:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.DrainVitalDamage, out preview);
                case SpellId.NecramPasSpectral: // ÉCHANGE SPECTRAL (refonte 29 mai) : swap + 80 dmg
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.EchangeSpectralDamage, out preview);
                case SpellId.NecramPulseSanguinVert:
                {
                    // Heal = base + 15 par marque venin sur ennemis dans rayon, cap 90
                    int marks = target->VeninStacks;
                    int bonus = marks * SpellRegistry.PulseSanguinVertHealPerMark;
                    if (bonus > SpellRegistry.PulseSanguinVertHealCap) bonus = SpellRegistry.PulseSanguinVertHealCap;
                    return TryComputeHealSelf(f, caster, SpellRegistry.PulseSanguinVertHealBase + bonus, out preview);
                }
                case SpellId.NecramCoconPutride:
                    return TryComputeHealSelf(f, caster, SpellRegistry.CoconPutrideHealAmount, out preview);
                case SpellId.NecramCarapaceVisqueuse:
                    return TryComputeShieldSelf(SpellRegistry.CarapaceVisqueuseShieldHP, out preview);

                default:
                    // Sort pas encore implemente / sort utilitaire (mobilite, debuff sans dmg).
                    return false;
            }
        }

        // =====================================================================
        // PIPELINES PARTAGES — mirror du pipeline SpellSystem.cs
        // =====================================================================

        /// <summary>
        /// Calc generique d'un sort offensif : applique le pipeline complet (Pacte de Sang +
        /// Peau de Fer + dorsal Ghostra + Marque de l'Ombre + reductions defensives + shield).
        /// Utilise pour la majorite des sorts offensifs (Lame Spec, Charge Brutale, Tir Precis, etc.).
        /// </summary>
        private static bool TryComputeOffensiveSimple(Frame f, Combatant* caster, Combatant* target, int rawDamage, out DamagePreviewResult p)
        {
            p = default;
            int offensiveBonus = ComputeOffensiveBonusGeneric(f, caster, target);
            FinalizeOffensive(f, caster, target, rawDamage, offensiveBonus, ref p);
            return true;
        }

        /// <summary>
        /// Frappe Fantome : meme pipeline mais le bonus dorsal Ghostra simule le teleport
        /// (back-cell free check) au lieu de la position actuelle du caster.
        /// </summary>
        private static bool TryComputeFrappeFantome(Frame f, Combatant* caster, Combatant* target, out DamagePreviewResult p)
        {
            p = default;
            int offensiveBonus = 0;
            // Dorsal post-teleport simule : back libre = dorsal garanti.
            if (caster->Class == NymoraClass.Ghostra)
            {
                IsoFacing back = FacingHelpers.Opposite(target->Facing);
                FacingHelpers.IsoFacingToGridDelta(back, out int bdx, out int bdy);
                int bx = target->GridX + bdx, by = target->GridY + bdy;
                if (GridHelpers.InBounds(bx, by) && GridHelpers.IsWalkable(f, bx, by) && GridHelpers.GetOccupant(f, bx, by) == EntityRef.None)
                {
                    offensiveBonus += GhostraPassif.GetDorsalBonusForGhostra(caster);
                }
            }
            offensiveBonus += StatusHelper.GetMagnitude(target, StatusKind.MarqueDeLOmbre, 0);
            FinalizeOffensive(f, caster, target, SpellRegistry.FrappeFantomeDmgBase, offensiveBonus, ref p);
            return true;
        }

        private static bool TryComputeLameSpectrale(Frame f, Combatant* caster, Combatant* target, out DamagePreviewResult p)
        {
            int raw = SpellRegistry.LameSpectraleDmgBase;
            if (StatusHelper.Has(target, StatusKind.PlaieOuverte)) raw += SpellRegistry.LameSpectralePlaieBonus;
            int offensiveBonus = ComputeOffensiveBonusGeneric(f, caster, target);
            p = default;
            FinalizeOffensive(f, caster, target, raw, offensiveBonus, ref p);
            return true;
        }

        private static bool TryComputeLameVorace(Frame f, Combatant* caster, Combatant* target, out DamagePreviewResult p)
        {
            int raw = SpellRegistry.LameVoraceDmgBase;
            if (StatusHelper.Has(target, StatusKind.PlaieOuverte)) raw += SpellRegistry.LameVoracePlaieBonus;
            int offensiveBonus = ComputeOffensiveBonusGeneric(f, caster, target);
            p = default;
            FinalizeOffensive(f, caster, target, raw, offensiveBonus, ref p);
            return true;
        }

        private static bool TryComputeSaigneAme(Frame f, Combatant* caster, Combatant* target, out DamagePreviewResult p)
        {
            int raw = SpellRegistry.SaigneAmeDmgBase;
            if (StatusHelper.Has(target, StatusKind.PlaieOuverte)) raw += SpellRegistry.SaigneAmePlaieBonus;
            int offensiveBonus = ComputeOffensiveBonusGeneric(f, caster, target);
            p = default;
            FinalizeOffensive(f, caster, target, raw, offensiveBonus, ref p);
            return true;
        }

        private static bool TryComputeShieldSelf(int shieldHp, out DamagePreviewResult p)
        {
            p = new DamagePreviewResult { Valid = true, ShieldGained = shieldHp };
            return true;
        }

        private static bool TryComputeHealSelf(Frame f, Combatant* caster, int healAmount, out DamagePreviewResult p)
        {
            p = default;
            // AntiHealShield bloque heal.
            if (StatusHelper.Has(caster, StatusKind.AntiHealShield))
            {
                p = new DamagePreviewResult { Valid = true, Healed = 0 };
                return true;
            }
            // Cap au max HP missing.
            int missing = caster->MaxHP - caster->HP;
            if (missing < 0) missing = 0;
            int actualHeal = healAmount > missing ? missing : healAmount;
            p = new DamagePreviewResult { Valid = true, Healed = actualHeal };
            return true;
        }

        /// <summary>
        /// Modifiers offensifs cote CASTER applicables a la plupart des sorts dmg :
        ///   - Bonus dorsal Ghostra (Angle Mort) si caster Ghostra et hit dorsal depuis position actuelle
        ///   - Bonus Marque de l'Ombre (+20) si target marque
        /// </summary>
        private static int ComputeOffensiveBonusGeneric(Frame f, Combatant* caster, Combatant* target)
        {
            int bonus = 0;
            bonus += GhostraPassif.GetDorsalBonusIfApplicable(caster, target);
            bonus += StatusHelper.GetMagnitude(target, StatusKind.MarqueDeLOmbre, 0);
            return bonus;
        }

        /// <summary>
        /// Applique le pipeline final : multiplier Pacte de Sang + Peau de Fer + reduction defensive
        /// + shield absorb. Remplit la struct DamagePreviewResult.
        /// </summary>
        private static void FinalizeOffensive(Frame f, Combatant* caster, Combatant* target, int rawDamage, int offensiveBonus, ref DamagePreviewResult p)
        {
            int totalBeforeMultipliers = rawDamage + offensiveBonus;

            // Pacte de Sang : multiplier +% (Bible 50%).
            int pacteBuffPct = StatusHelper.GetMagnitude(caster, StatusKind.BuffNextOffensiveDmgPercent, 0);
            if (pacteBuffPct > 0)
            {
                totalBeforeMultipliers += totalBeforeMultipliers * pacteBuffPct / 100;
            }
            // Peau de Fer : +30 melee si caster a ShieldActive (cf SpellSystem ligne 484).
            // Approximation : on assume tous les sorts dmg sont eligibles (ne distinguons pas
            // melee vs distance dans la preview — Lorenzo verra cas par cas).
            if (StatusHelper.GetMagnitude(caster, StatusKind.ShieldActive, 0) > 0)
            {
                totalBeforeMultipliers += SpellRegistry.PeauDeFerMeleeDmgBonus;
            }

            // Reduction defensive cible (Densite Inerte + Ancrage + Garde Prot + Effondrement, cap 50%).
            int reductionPct = ColossarPassif.GetCombinedDamageReductionPercent(f, target);
            int finalDamage = totalBeforeMultipliers * (100 - reductionPct) / 100;
            if (finalDamage < 0) finalDamage = 0;

            // Shield absorption.
            int shieldMag = StatusHelper.GetMagnitude(target, StatusKind.ShieldActive, 0);
            int absorbed = finalDamage > shieldMag ? shieldMag : finalDamage;
            if (absorbed < 0) absorbed = 0;
            int hpLost = finalDamage - absorbed;
            if (hpLost < 0) hpLost = 0;
            if (hpLost > target->HP) hpLost = target->HP;

            p.Valid = true;
            p.RawDamage = rawDamage;
            p.OffensiveBonus = offensiveBonus;
            p.DefensiveReductionPercent = reductionPct;
            p.FinalDamage = finalDamage;
            p.AbsorbedByShield = absorbed;
            p.HpLost = hpLost;
        }
    }
}
