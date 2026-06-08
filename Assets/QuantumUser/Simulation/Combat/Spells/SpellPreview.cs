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
    /// SpellRegistry, memes helpers passifs). N'est PAS dans la boucle de simulation -> aucune
    /// contrainte de determinisme (jamais appele par Update).
    ///
    /// Modifiers offensifs couverts (audit 5 juin — preview == sim par classe) :
    ///   - Pacte de Sang (+%), Frenesie (+%), Sang Bouillant (flat) [Soulrender]
    ///   - Peau de Fer (+30, MELEE only = RangeMax 1, si ShieldActive) [Soulrender]
    ///   - Bonus HG auto-depense : Ouvre-Plaie (+120), Detonation Sanglante (+40/HG) [Soulrender]
    ///   - Bonus dorsal Ghostra (+0/+50/+80) + Marque de l'Ombre (+20) [Ghostra]
    ///   - Passif phase >= 2 (+30 flat sur tous les sorts offensifs) [Nightseer]
    ///   - Densite Inerte adjacence (+20, sorts portee 1-2 si adjacent a un obstacle own) [Colossar]
    ///   - Reduction defensive cible (Densite Inerte + Ancrage + Garde Prot + Effondrement, cap 50%)
    ///   - Shield absorption (ShieldActive) / AntiHealShield (bloque heal)
    ///   - Sorts BYPASS shield/reduction : Detonation Virulente + Virus Fatal (tick venin),
    ///     Execution Spectrale (signature), Choc Sismique (reduc mais pas shield), Onde de Choc
    ///     (bonus +80 vs mur, heuristique de push)
    ///
    /// Approximations restantes (documentees, hors "bonus de degats") :
    ///   - Sorts AoE multi-cible : preview du dmg sur la cible survolee (cas central pour Salve)
    ///   - Effects post-damage (PlaieOuverte/marques venin appliquees) non chiffres separement
    ///   - Onde de Choc vs mur : la detection "pousse contre un mur" est une heuristique de trajet
    /// </summary>
    public unsafe static class SpellPreview
    {
        public static bool TryCompute(Frame f, EntityRef casterEntity, EntityRef targetEntity, SpellId spellId, out DamagePreviewResult preview)
        {
            preview = default;
            if (f == null) return false;
            if (!f.Unsafe.TryGetPointer<Combatant>(casterEntity, out Combatant* caster)) return false;
            if (!f.Unsafe.TryGetPointer<Combatant>(targetEntity, out Combatant* target)) return false;

            // Contexte du sort (portee = gate Peau de Fer melee + Densite Inerte adjacence Colossar).
            SpellRegistry.TryGet(spellId, out SpellDef sdef);
            int rangeMax = sdef.RangeMax;

            switch (spellId)
            {
                // ============== GHOSTRA ==============
                case SpellId.GhostraFrappeFantome:
                    return TryComputeFrappeFantome(f, caster, target, rangeMax, out preview);
                case SpellId.GhostraLameSpectrale:
                    return TryComputeLameSpectrale(f, caster, target, rangeMax, out preview);
                case SpellId.GhostraLameVoraceSpectrale:
                    return TryComputeLameVorace(f, caster, target, rangeMax, out preview);
                case SpellId.GhostraSaigneAme:
                    return TryComputeSaigneAme(f, caster, target, rangeMax, out preview);
                case SpellId.GhostraNueeSpectrale:
                {
                    // Degat scale sur les leurres : base + 70/leurre actif + 30/leurre adjacent (constantes
                    //   SpellRegistry, cf sim). PAS de bonus dorsal (le scaling leurres EST le bonus).
                    //   On ajoute le +20 Marque de l'Ombre comme la sim, puis reductions/shield.
                    int nActive = DecoyHelpers.CountActive(caster);
                    int nAdj = DecoyHelpers.CountOwnDecoysAdjacent(caster, target->GridX, target->GridY);
                    int nueeRaw = SpellRegistry.NueeSpectraleBaseDamage
                                + SpellRegistry.NueeSpectralePerLeurre * nActive
                                + SpellRegistry.NueeSpectralePerAdjacent * nAdj;
                    int nueeBonus = StatusHelper.GetMagnitude(target, StatusKind.MarqueDeLOmbre, 0);
                    preview = default;
                    FinalizeOffensive(f, caster, target, nueeRaw, nueeBonus, rangeMax, ref preview);
                    return true;
                }
                case SpellId.GhostraVoileSpectral:
                {
                    // #21 (5 juin) : 60 dmg par leurre ADJACENT a la cible (cap 180). Pas de dorsal (comme
                    //   Nuee, le scaling leurres EST le bonus). Passe par le pipeline standard (reductions/
                    //   shield) + Marque de l'Ombre. Mirror sim ligne ~677.
                    int voileAdj = DecoyHelpers.CountOwnDecoysAdjacent(caster, target->GridX, target->GridY);
                    int voileRaw = voileAdj * SpellRegistry.VoileSpectralDmgPerAdjacent;
                    if (voileRaw > SpellRegistry.VoileSpectralDmgMax) voileRaw = SpellRegistry.VoileSpectralDmgMax;
                    int voileBonus = StatusHelper.GetMagnitude(target, StatusKind.MarqueDeLOmbre, 0);
                    preview = default;
                    FinalizeOffensive(f, caster, target, voileRaw, voileBonus, rangeMax, ref preview);
                    return true;
                }
                // Permutation (ex-Volte-Face slot 90) : aucun degat -> pas de preview offensif. Falls through.
                case SpellId.GhostraEveilSpectral:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.EveilSpectraleDamage, rangeMax, out preview);
                case SpellId.GhostraExecutionSpectrale:
                    // Signature : 350 dmg DIRECT bypass shield + reduction (Bible-stricte). Pas de dorsal.
                    preview = default;
                    FinalizeBypass(f, target, SpellRegistry.ExecutionSpectraleDamage, applyReduction: false, ref preview);
                    return true;
                case SpellId.GhostraCommunionSpectrale:
                    return TryComputeHealSelf(f, caster, SpellRegistry.CommunionHeal, out preview);
                case SpellId.GhostraLinceulDOmbres:
                    return TryComputeShieldSelf(SpellRegistry.LinceulDOmbresShieldHP, out preview);
                case SpellId.GhostraDernierPas:
                    return TryComputeHealSelf(f, caster, SpellRegistry.DernierPasHealAmount, out preview);

                // ============== SOULRENDER ==============
                case SpellId.SoulrenderChargeBrutale:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.ChargeBrutaleDamage, rangeMax, out preview);
                case SpellId.SoulrenderOuvrePlaie:
                {
                    // 110 base + 120 si >= 1 HG depense. Conso HG AUTO depuis le 3 juin -> le bonus part
                    //   des qu'un HG optionnel est financable (mirror sim ligne 685).
                    int op = 110;
                    if (AutoHgSpend(caster, sdef) >= 1) op += 120;
                    return TryComputeOffensiveSimple(f, caster, target, op, rangeMax, out preview);
                }
                case SpellId.SoulrenderTrancheAme:
                    return TryComputeOffensiveSimple(f, caster, target, 220, rangeMax, out preview);
                case SpellId.SoulrenderDetonationSanglante:
                {
                    // 60 base + 40 par HG TOTAL (mandatory + optional). Conso AUTO depuis le 3 juin
                    //   -> totalHG = mandatory + auto (mirror sim ligne 691).
                    int totalHG = sdef.HGCostMandatory + AutoHgSpend(caster, sdef);
                    int dmg = SpellRegistry.DetonationBaseDamage + SpellRegistry.DetonationDamagePerHG * totalHG;
                    return TryComputeOffensiveSimple(f, caster, target, dmg, rangeMax, out preview);
                }
                case SpellId.SoulrenderAmeLaceree:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.AmeLaceeDamage, rangeMax, out preview);
                case SpellId.SoulrenderCuree:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.CureeDamage, rangeMax, out preview);
                case SpellId.SoulrenderPeauDeFer:
                    return TryComputeShieldSelf(SpellRegistry.PeauDeFerShieldHP, out preview);
                case SpellId.SoulrenderSeveVive:
                    return TryComputeHealSelf(f, caster, SpellRegistry.SeveViveHealBase, out preview);
                case SpellId.SoulrenderDernierSouffle:
                    return TryComputeHealSelf(f, caster, SpellRegistry.DernierSouffleHealAmount, out preview);
                case SpellId.SoulrenderCauterisation:
                    // SANG BOUILLANT (refonte 29 mai) : buff reactif sans nombre immediat -> pas de preview.
                    preview = default;
                    return false;

                // ============== NIGHTSEER ==============
                case SpellId.NightseerTirPrecis:
                {
                    int dmg = MarkHelpers.HasMark(target, MarkKind.Traque)
                        ? SpellRegistry.TirPrecisDmgIfTraque
                        : SpellRegistry.TirPrecisDmg;
                    return TryComputeOffensiveSimple(f, caster, target, dmg, rangeMax, out preview);
                }
                case SpellId.NightseerVoleeDEpines:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.VoleeDEpinesDmg, rangeMax, out preview);
                case SpellId.NightseerDetonationOnirique:
                {
                    // 170 base + le surplus +30 par piège détoné dans l'AoE (AddOwnTrapDetonationDamage).
                    //   L'ancien "+80 si couvre un piège" a été retiré (6 juin).
                    int dmg = SpellRegistry.DetonationOniriqueDmg;
                    if (!TryComputeOffensiveSimple(f, caster, target, dmg, rangeMax, out preview)) return false;
                    AddOwnTrapDetonationDamage(f, caster, target, ref preview);
                    return true;
                }
                case SpellId.NightseerFrappeDeLOmbre:
                {
                    // Patch 7 juin EXECUTEUR : 160 + 120 si la cible est TRAQUÉ (= 280, consommé).
                    int dmg = SpellRegistry.FrappeDeLOmbreDmg;
                    if (MarkHelpers.HasMark(target, MarkKind.Traque)) dmg += SpellRegistry.FrappeDeLOmbreDmgBonusTraque;
                    return TryComputeOffensiveSimple(f, caster, target, dmg, rangeMax, out preview);
                }
                // NightseerVoileDOmbre -> REPLI ÉPINEUX (patch 7 juin) : sort SELF (push + heal), aucun
                //   preview de dégâts offensif. Retiré du switch (tombe au défaut).
                case SpellId.NightseerSalveMortelle:
                {
                    // Preview = centre du carré 3x3 (cible directe -> elle est au centre). Bonus "cible
                    //   Traqué" retiré (6 juin) ; seul bonus restant = +40 par piège du caster sous le
                    //   carré 3x3 (non consommé). La phase Nightseer (+30 flat P2+) est évaluée sur la
                    //   ressource APRÈS la dépense des 3 PR (la sim consomme avant d'appliquer le bonus) :
                    //   Salve à 3 PR retombe sous la phase 2 -> jamais de +30 (fix preview inexact).
                    int dmg = SpellRegistry.SalveMortelleDmgCenter;
                    int phaseAfterCost = caster->Resource - SpellRegistry.SalveMortelleHGCost;
                    if (phaseAfterCost < 0) phaseAfterCost = 0;
                    if (!TryComputeOffensiveSimple(f, caster, target, dmg, rangeMax, out preview, phaseAfterCost)) return false;
                    AddSalveTrapBonus(f, caster, target, ref preview);
                    return true;
                }
                case SpellId.NightseerSouffleGlacial: // PIEGE BONDISSANT (refonte 29 mai)
                    preview = default;
                    return false;
                case SpellId.NightseerFiletDeRonces:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.FiletDeRoncesDmg, rangeMax, out preview);
                case SpellId.NightseerChampDeMines:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.ChampDeMinesDmg, rangeMax, out preview);
                case SpellId.NightseerCamouflageRonces:
                    return TryComputeShieldSelf(SpellRegistry.CamouflageRoncesShieldHP, out preview);
                case SpellId.NightseerSeveSauvage:
                    return TryComputeHealSelf(f, caster, SpellRegistry.SeveSauvageHealBase, out preview);
                case SpellId.NightseerEvanescence:
                    return TryComputeHealSelf(f, caster, SpellRegistry.EvanescenceHeal, out preview);
                case SpellId.NightseerTraquenard:
                {
                    int dmg = SpellRegistry.TraquenardDmgBase;
                    if (MarkHelpers.HasMark(target, MarkKind.Traque)
                        || MarkHelpers.HasMark(target, MarkKind.Empreinte))
                    {
                        dmg += SpellRegistry.TraquenardDmgBonusIfMarked;
                    }
                    return TryComputeOffensiveSimple(f, caster, target, dmg, rangeMax, out preview);
                }

                // ============== COLOSSAR ==============
                case SpellId.ColossarFrappeLourde:
                {
                    int dmg = ColossarPassif.IsTargetPinnedFromCaster(f, caster, target)
                        ? SpellRegistry.FrappeLourdeDmgIfPinned
                        : SpellRegistry.FrappeLourdeDmgBase;
                    return TryComputeOffensiveSimple(f, caster, target, dmg, rangeMax, out preview);
                }
                case SpellId.ColossarMarteauPunisseur:
                {
                    int dmg = target->PA < SpellRegistry.MarteauPunisseurDepletedPAThreshold
                        ? SpellRegistry.MarteauPunisseurDmgIfDepleted
                        : SpellRegistry.MarteauPunisseurDmg;
                    return TryComputeOffensiveSimple(f, caster, target, dmg, rangeMax, out preview);
                }
                case SpellId.ColossarRepresailles:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.RepresaillesDmgImmediate, rangeMax, out preview);
                case SpellId.ColossarOndeDeChoc:
                {
                    // Base AoE via pipeline. + bonus +80 DIRECT (bypass) si la cible est poussee contre un
                    //   mur/bord (heuristique de trajet, mirror sim "stoppedAgainst" ligne ~3348).
                    int obonus = ComputeOffensiveBonusGeneric(f, caster, target, rangeMax);
                    preview = default;
                    FinalizeOffensive(f, caster, target, SpellRegistry.OndeDeChocDmg, obonus, rangeMax, ref preview);
                    if (WouldStopAgainstWall(f, caster, target, SpellRegistry.OndeDeChocPushDistance))
                    {
                        int wall = SpellRegistry.OndeDeChocBonusVsWall;
                        preview.FinalDamage += wall;
                        int room = target->HP - preview.HpLost;
                        if (room < 0) room = 0;
                        preview.HpLost += wall > room ? room : wall;
                    }
                    return true;
                }
                case SpellId.ColossarChocSismique:
                    // Bypass shield (simplification sim ligne 3451) mais la reduction (Densite Inerte +
                    //   Ancrage) s'applique. Pas de buff offensif (Colossar n'en a pas).
                    preview = default;
                    FinalizeBypass(f, target, SpellRegistry.ChocSismiqueDmgBase, applyReduction: true, ref preview);
                    return true;
                case SpellId.ColossarBrisure:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.BrisureDamage, rangeMax, out preview);
                case SpellId.ColossarEffondrement:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.EffondrementDamage, rangeMax, out preview);
                case SpellId.ColossarStoicisme:
                    return TryComputeShieldSelf(SpellRegistry.StoicismeShieldHP, out preview);
                case SpellId.ColossarRessacVital:
                    return TryComputeHealSelf(f, caster, SpellRegistry.RessacVitalHealBase, out preview);
                case SpellId.ColossarSoinLourd:
                    // EBOULEMENT (refonte 29 mai) : AoE autour d'un Pilier (pas de cible directe) -> pas de preview.
                    preview = default;
                    return false;

                // ============== NECRAM ==============
                case SpellId.NecramCrachatAcide:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.CrachatAcideDmg, rangeMax, out preview);
                case SpellId.NecramMorsurePutride:
                {
                    int marks = target->VeninStacks;
                    int bonus = marks * SpellRegistry.MorsurePutrideDmgPerMark;
                    if (bonus > SpellRegistry.MorsurePutrideDmgBonusCap) bonus = SpellRegistry.MorsurePutrideDmgBonusCap;
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.MorsurePutrideDmgBase + bonus, rangeMax, out preview);
                }
                case SpellId.NecramDetonationVirulente:
                {
                    // Tick venin complet = marques * clock Floraison + Marque Sac. BYPASS shield + reduction
                    //   (comme un tick venin standard, sim ligne 2312).
                    // FIX MIROIR v141 : densite par-equipe de la cible (match la sim Detonation Virulente).
                    int density = VeninHelpers.GetDensityOnTeam(f, target->PlayerIndex);
                    int dmg = target->VeninStacks * VeninHelpers.GetTickDmgPerMark(density)
                            + StatusHelper.GetMagnitude(target, StatusKind.MarqueSacrificielle, 0);
                    preview = default;
                    FinalizeBypass(f, target, dmg, applyReduction: false, ref preview);
                    return true;
                }
                case SpellId.NecramFauxDecharnee:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.FauxDecharneeDmg, rangeMax, out preview);
                case SpellId.NecramBrumeToxique:
                    // Refonte 29 mai : zone de marques + tick majore, plus de degats directs -> pas de preview.
                    preview = default;
                    return false;
                case SpellId.NecramDrainVital:
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.DrainVitalDamage, rangeMax, out preview);
                case SpellId.NecramPasSpectral: // ECHANGE SPECTRAL (refonte 29 mai) : swap + 80 dmg
                    return TryComputeOffensiveSimple(f, caster, target, SpellRegistry.EchangeSpectralDamage, rangeMax, out preview);
                case SpellId.NecramPulseSanguinVert:
                {
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
        /// Calc generique d'un sort offensif : bonus offensifs (dorsal/Marque/Nightseer/Colossar) +
        /// pipeline final (buffs % + reduction + shield). Majorite des sorts offensifs.
        /// </summary>
        private static bool TryComputeOffensiveSimple(Frame f, Combatant* caster, Combatant* target, int rawDamage, int rangeMax, out DamagePreviewResult p, int phaseResourceOverride = -1)
        {
            p = default;
            int offensiveBonus = ComputeOffensiveBonusGeneric(f, caster, target, rangeMax, phaseResourceOverride);
            FinalizeOffensive(f, caster, target, rawDamage, offensiveBonus, rangeMax, ref p);
            return true;
        }

        /// <summary>
        /// Frappe Fantome : meme pipeline mais le bonus dorsal Ghostra simule le teleport
        /// (back-cell free check) au lieu de la position actuelle du caster.
        /// </summary>
        private static bool TryComputeFrappeFantome(Frame f, Combatant* caster, Combatant* target, int rangeMax, out DamagePreviewResult p)
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
            FinalizeOffensive(f, caster, target, SpellRegistry.FrappeFantomeDmgBase, offensiveBonus, rangeMax, ref p);
            return true;
        }

        private static bool TryComputeLameSpectrale(Frame f, Combatant* caster, Combatant* target, int rangeMax, out DamagePreviewResult p)
        {
            int raw = SpellRegistry.LameSpectraleDmgBase;
            if (StatusHelper.Has(target, StatusKind.PlaieOuverte)) raw += SpellRegistry.LameSpectralePlaieBonus;
            int offensiveBonus = ComputeOffensiveBonusGeneric(f, caster, target, rangeMax);
            p = default;
            FinalizeOffensive(f, caster, target, raw, offensiveBonus, rangeMax, ref p);
            return true;
        }

        private static bool TryComputeLameVorace(Frame f, Combatant* caster, Combatant* target, int rangeMax, out DamagePreviewResult p)
        {
            int raw = SpellRegistry.LameVoraceDmgBase;
            if (StatusHelper.Has(target, StatusKind.PlaieOuverte)) raw += SpellRegistry.LameVoracePlaieBonus;
            int offensiveBonus = ComputeOffensiveBonusGeneric(f, caster, target, rangeMax);
            p = default;
            FinalizeOffensive(f, caster, target, raw, offensiveBonus, rangeMax, ref p);
            return true;
        }

        private static bool TryComputeSaigneAme(Frame f, Combatant* caster, Combatant* target, int rangeMax, out DamagePreviewResult p)
        {
            int raw = SpellRegistry.SaigneAmeDmgBase;
            if (StatusHelper.Has(target, StatusKind.PlaieOuverte)) raw += SpellRegistry.SaigneAmePlaieBonus;
            int offensiveBonus = ComputeOffensiveBonusGeneric(f, caster, target, rangeMax);
            p = default;
            FinalizeOffensive(f, caster, target, raw, offensiveBonus, rangeMax, ref p);
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
        /// HG optionnel auto-depense (3 juin) : max finançable apres le cout mandatory, plafonne par
        /// HGCostMaxOptional. Mirror exact de SpellSystem (cmd.HGSpend=0 cote preview -> auto au max).
        /// </summary>
        private static int AutoHgSpend(Combatant* caster, in SpellDef sdef)
        {
            int optionalBudget = caster->Resource - sdef.HGCostMandatory;
            if (optionalBudget < 0) optionalBudget = 0;
            int hg = sdef.HGCostMaxOptional;
            if (hg > optionalBudget) hg = optionalBudget;
            if (hg < 0) hg = 0;
            return hg;
        }

        /// <summary>
        /// Bonus offensifs cote CASTER (flat, AVANT le pipeline %/reduction), par classe :
        ///   - Ghostra : dorsal (Angle Mort) + Marque de l'Ombre (+20)
        ///   - Nightseer : passif phase >= 2 (+30 flat) — mirror SpellSystem ligne ~1072
        ///   - Colossar : Densite Inerte adjacence (+20 sorts portee 1-2 si adjacent obstacle own) — ~1085
        /// Les bonus sont mutuellement exclusifs par classe du caster (jamais 2 a la fois).
        /// </summary>
        /// <param name="phaseResourceOverride">Prescience à utiliser pour évaluer la phase Nightseer.
        ///   -1 = ressource actuelle. Les sorts qui DÉPENSENT du PR (ex : Salve Mortelle, 3 PR) passent
        ///   (Resource - coût) : la sim consomme le PR AVANT d'appliquer le bonus de phase (SpellSystem
        ///   ligne ~1066), donc le preview doit évaluer la phase POST-dépense pour ne pas sur-estimer.</param>
        private static int ComputeOffensiveBonusGeneric(Frame f, Combatant* caster, Combatant* target, int rangeMax, int phaseResourceOverride = -1)
        {
            int phaseResource = phaseResourceOverride >= 0 ? phaseResourceOverride : caster->Resource;
            int bonus = 0;
            // Ghostra.
            bonus += GhostraPassif.GetDorsalBonusIfApplicable(caster, target);
            bonus += StatusHelper.GetMagnitude(target, StatusKind.MarqueDeLOmbre, 0);
            // Nightseer : +30 flat en phase >= 2 (PR 3-4+), évalué sur la ressource POST-dépense.
            if (caster->Class == NymoraClass.Nightseer
                && NightseerPassif.FlatDamageBonusActive(phaseResource))
            {
                bonus += NightseerPassif.FlatDamageBonus;
            }
            // Colossar : +20 si sort portee 1-2 ET adjacent a un de ses obstacles.
            if (caster->Class == NymoraClass.Colossar
                && rangeMax >= 1
                && rangeMax <= SpellRegistry.DensiteInerteAdjacenceMaxRange
                && ColossarPassif.IsAdjacentToOwnObstacle(f, caster, caster->PlayerIndex))
            {
                bonus += SpellRegistry.DensiteInerteAdjacenceBonus;
            }
            return bonus;
        }

        /// <summary>
        /// Pipeline final : buffs % du caster (Pacte de Sang, Peau de Fer melee, Frenesie, Sang
        /// Bouillant) + reduction defensive cible + shield. Remplit DamagePreviewResult.
        /// isMelee = rangeMax == 1 (gate Peau de Fer, melee only Bible).
        /// </summary>
        private static void FinalizeOffensive(Frame f, Combatant* caster, Combatant* target, int rawDamage, int offensiveBonus, int rangeMax, ref DamagePreviewResult p)
        {
            // Sort sans degat de base (ex : Fleche Tracante sur cible non-Traque) = AUCUN bonus offensif :
            // la sim gate les bonus (Nightseer phase +30, etc.) sur dmgThisTarget > 0. Preview = 0.
            if (rawDamage <= 0)
            {
                p.Valid = true;
                p.RawDamage = 0;
                return;
            }
            int totalBeforeMultipliers = rawDamage + offensiveBonus;

            // Pacte de Sang : multiplier +% (Bible 50%).
            int pacteBuffPct = StatusHelper.GetMagnitude(caster, StatusKind.BuffNextOffensiveDmgPercent, 0);
            if (pacteBuffPct > 0)
            {
                totalBeforeMultipliers += totalBeforeMultipliers * pacteBuffPct / 100;
            }
            // Peau de Fer : +30 MELEE (RangeMax 1) si caster a ShieldActive (mirror SpellSystem ligne 714).
            if (rangeMax == 1 && StatusHelper.GetMagnitude(caster, StatusKind.ShieldActive, 0) > 0)
            {
                totalBeforeMultipliers += SpellRegistry.PeauDeFerMeleeDmgBonus;
            }
            // Frenesie : +% dgts offensifs tant que RageInsatiableActive (mirror SpellSystem ~ligne 723).
            if (StatusHelper.Has(caster, StatusKind.RageInsatiableActive))
            {
                totalBeforeMultipliers += totalBeforeMultipliers * SpellRegistry.FrenesieDmgBonusPct / 100;
            }
            // Affut (patch 7 juin, Nightseer) : +% dgts offensifs tant que AffutActive (mirror SpellSystem).
            if (StatusHelper.Has(caster, StatusKind.AffutActive))
            {
                totalBeforeMultipliers += totalBeforeMultipliers * SpellRegistry.AffutDmgBonusPct / 100;
            }
            // Sang Bouillant : bonus FLAT "prochaine frappe +X" (NextStrikeBonus, mirror SpellSystem ~ligne 729).
            int nextStrikeBonus = StatusHelper.GetMagnitude(caster, StatusKind.NextStrikeBonus, 0);
            if (nextStrikeBonus > 0)
            {
                totalBeforeMultipliers += nextStrikeBonus;
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

        /// <summary>
        /// Finalize pour les sorts qui BYPASSENT le shield (ticks venin / signature / Choc Sismique).
        /// applyReduction = applique la reduction defensive (Choc Sismique : Densite Inerte/Ancrage) ;
        /// false = bypass total (tick venin, Execution Spectrale). Jamais de shield absorb.
        /// </summary>
        private static void FinalizeBypass(Frame f, Combatant* target, int rawDamage, bool applyReduction, ref DamagePreviewResult p)
        {
            int reductionPct = 0;
            int dmg = rawDamage;
            if (applyReduction)
            {
                reductionPct = ColossarPassif.GetCombinedDamageReductionPercent(f, target);
                dmg = dmg * (100 - reductionPct) / 100;
            }
            if (dmg < 0) dmg = 0;
            int hpLost = dmg > target->HP ? target->HP : dmg;

            p.Valid = true;
            p.RawDamage = rawDamage;
            p.OffensiveBonus = 0;
            p.DefensiveReductionPercent = reductionPct;
            p.FinalDamage = dmg;
            p.AbsorbedByShield = 0; // bypass shield
            p.HpLost = hpLost;
        }

        /// <summary>
        /// Équilibrage 6 juin — précision preview pour Détonation Onirique qui "détone tes embûches"
        /// (cf FogHelpers.DetonateOwnTrapsInArea). Chaque piège-DÉGÂTS (Filet / Mine) du CASTER présent
        /// SOUS L'AoE croix de 5 (centrée sur la cible) ajoute un SURPLUS PLAT de +30
        /// (ZoneTrapDetonationSurplusDmg) DIRECT (bypass shield + réduction) à la cible — même si elle
        /// n'est pas pile sur la case-piège, vu qu'un ennemi n'occupe jamais une case-piège. Piège
        /// Bondissant = 0 (catapulte). On ne touche que les ennemis (les pièges ne déclenchent pas sur
        /// le poseur). Salve Mortelle ne détone plus les pièges -> n'appelle plus cette méthode.
        /// </summary>
        private static void AddOwnTrapDetonationDamage(Frame f, Combatant* caster, Combatant* target,
            ref DamagePreviewResult p)
        {
            if (!p.Valid) return;
            if (target->PlayerIndex == caster->PlayerIndex) return;

            int totalTrapDmg = 0;
            // AoE croix de 5 centrée sur la cible (Détonation Onirique).
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx != 0 && dy != 0) continue; // croix : exclut les diagonales

                    int cx = target->GridX + dx, cy = target->GridY + dy;
                    if (FogHelpers.GetTrapOwner(f, cx, cy) != caster->PlayerIndex) continue;

                    switch (FogHelpers.GetTrapKind(f, cx, cy))
                    {
                        case TrapKind.FiletRonces:
                        case TrapKind.Mine:
                            totalTrapDmg += SpellRegistry.ZoneTrapDetonationSurplusDmg; // +30 plat/piège
                            break;
                        default: continue; // Bondissant / None : pas de surplus
                    }
                }
            }
            if (totalTrapDmg <= 0) return;

            // Dégâts DIRECTS (ni shield ni réduction) -> ajout brut au total affiché et aux HP perdus.
            p.FinalDamage += totalTrapDmg;
            p.HpLost += totalTrapDmg;
            if (p.HpLost > target->HP) p.HpLost = target->HP;
        }

        /// <summary>
        /// Salve Mortelle (6 juin) — preview du bonus pièges SANS consommation : +50
        /// (SalveMortelleTrapBonusDmg) par piège du CASTER présent sous le carré 3x3 centré sur la cible.
        /// Dégâts DIRECTS (bypass shield/réduction). Les pièges ne sont pas consommés (mirror
        /// FogHelpers.ApplyZoneTrapBonusNoConsume).
        /// </summary>
        private static void AddSalveTrapBonus(Frame f, Combatant* caster, Combatant* target, ref DamagePreviewResult p)
        {
            if (!p.Valid) return;
            if (target->PlayerIndex == caster->PlayerIndex) return;

            int trapCount = 0;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int cx = target->GridX + dx, cy = target->GridY + dy;
                    if (FogHelpers.GetTrapOwner(f, cx, cy) != caster->PlayerIndex) continue;
                    if (FogHelpers.GetTrapKind(f, cx, cy) == TrapKind.None) continue;
                    trapCount++;
                }
            }
            if (trapCount <= 0) return;

            int bonus = trapCount * SpellRegistry.SalveMortelleTrapBonusDmg;
            p.FinalDamage += bonus;
            p.HpLost += bonus;
            if (p.HpLost > target->HP) p.HpLost = target->HP;
        }

        /// <summary>
        /// Heuristique Onde de Choc : la cible est poussee pushDistance cases loin du caster (cardinal).
        /// "Stoppee contre un mur" = une case du trajet est hors-grille / non-walkable / occupee.
        /// Approximation du "stoppedAgainst" de PushAndTriggerEx (suffisant pour le cas central).
        /// </summary>
        private static bool WouldStopAgainstWall(Frame f, Combatant* caster, Combatant* target, int pushDistance)
        {
            int pdx = target->GridX > caster->GridX ? 1 : (target->GridX < caster->GridX ? -1 : 0);
            int pdy = target->GridY > caster->GridY ? 1 : (target->GridY < caster->GridY ? -1 : 0);
            if (pdx == 0 && pdy == 0) return false;
            int cx = target->GridX, cy = target->GridY;
            for (int step = 0; step < pushDistance; step++)
            {
                int nx = cx + pdx, ny = cy + pdy;
                if (!GridHelpers.InBounds(nx, ny) || !GridHelpers.IsWalkable(f, nx, ny)
                    || GridHelpers.GetOccupant(f, nx, ny) != EntityRef.None)
                {
                    return true;
                }
                cx = nx; cy = ny;
            }
            return false;
        }
    }
}
