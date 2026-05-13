namespace Quantum
{
    using Photon.Deterministic;

    /// <summary>
    /// Traite les CastSpellCommands envoyees par les joueurs.
    ///
    /// Pipeline (2.10.a — refactor) :
    ///   1. Validations base (phase, joueur actif, sort existe, caster existe)
    ///   2. PA effectif (RageInsatiableActive : +1) — rejet si insuffisant
    ///   3. Check once-per-match (bitfield sur Combatant)
    ///   4. Clamp HGSpend a HGCostMaxOptional + valide HGCostMandatory + HGSpend <= caster.Resource
    ///   5. Range + Filter
    ///   6. Consommation : PA, HG, bit once-per-match
    ///   7. Resolution AoE (Rugissement = override manuel rayon 3, sinon TargetingResolver)
    ///   8. Damage loop : applique dgts (avec buff +50% Pacte, HG +120 Ouvre-Plaie), gain HG
    ///      caster/cible, trigger Riposte Carmin si cible a RipostMelee et range==1.
    ///   9. Post-cast : applique les statuses specifiques au SpellId, consomme buff +50%
    ///      si offensif, regen 1 PA si RageInsatiableActive et offensif (max 1 par tour).
    /// </summary>
    public unsafe class SpellSystem : SystemMainThread
    {
        public override void Update(Frame f)
        {
            if (!f.TryGetSingleton<CombatState>(out var state)) return;
            if (state.CurrentPhase != CombatPhase.TurnActive) return;

            for (int playerIndex = 0; playerIndex < TurnConstants.PlayerCount; playerIndex++)
            {
                if (f.GetPlayerCommand(playerIndex) is CastSpellCommand cmd)
                {
                    TryCastSpell(f, playerIndex, cmd, state.ActivePlayerIndex);
                }
            }
        }

        /// <summary>
        /// Pipeline complet de cast d'un sort. Visible publiquement pour que l'IA
        /// (AISystem en 2.16.a.iii) puisse l'appeler avec une CastSpellCommand
        /// construite a la volee — meme path de validation et d'execution que les
        /// commands clients, donc semantique strictement identique.
        /// </summary>
        public static void TryCastSpell(Frame f, int playerIndex, CastSpellCommand cmd, int activePlayerIndex)
        {
            if (playerIndex != activePlayerIndex)
            {
                Log.Warn($"[Spell] rejet : ce n'est pas le tour de P{playerIndex}");
                return;
            }

            if (!SpellRegistry.TryGet(cmd.Spell, out var spellDef))
            {
                Log.Warn($"[Spell] rejet : sort inconnu {cmd.Spell}");
                return;
            }

            EntityRef casterEntity = EntityRef.None;
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef entity, out Combatant* c))
            {
                if (c->PlayerIndex == playerIndex)
                {
                    casterEntity = entity;
                    break;
                }
            }

            if (casterEntity == EntityRef.None)
            {
                Log.Warn($"[Spell] rejet : pas de Combatant pour P{playerIndex}");
                return;
            }

            var caster = f.Unsafe.GetPointer<Combatant>(casterEntity);

            // 2.11 : compute target HP ratio for passif Appel du Sang (Soulrender -1 PA si <70% HP).
            int targetHPRatio = EffectiveStats.ResolveTargetHPRatio(f, cmd.TargetX, cmd.TargetY, playerIndex);

            int effectivePACost = EffectiveStats.GetPACost(spellDef, caster, targetHPRatio);
            if (caster->PA < effectivePACost)
            {
                Log.Warn($"[Spell] rejet : PA {caster->PA} < cost {effectivePACost} (base {spellDef.PACost})");
                return;
            }

            // Once-per-match check.
            if (spellDef.OncePerMatchBit != SpellRegistry.OncePerMatchBitNone)
            {
                int mask = 1 << spellDef.OncePerMatchBit;
                if ((caster->OncePerMatchUsedFlags & mask) != 0)
                {
                    Log.Warn($"[Spell] rejet : {cmd.Spell} deja utilise (1 fois par match) par P{playerIndex}");
                    return;
                }
            }

            // Dernier Souffle (2.10.b) : conditionnel HP < 30% MaxHP (Bible V7.1).
            // Check avant consommation PA pour eviter de "perdre" le cast.
            if (cmd.Spell == SpellId.SoulrenderDernierSouffle)
            {
                if (caster->HP * 100 >= caster->MaxHP * SpellRegistry.DernierSouffleHPThresholdPct)
                {
                    Log.Warn($"[Spell] rejet : Dernier Souffle requiert HP < {SpellRegistry.DernierSouffleHPThresholdPct}% (actuel {caster->HP}/{caster->MaxHP})");
                    return;
                }
            }

            // 2.11 : Ame Laceree cooldown 4 tours apres usage. Detonation 5 HG set aussi
            // LastAmeLaceeUsedOnTurn (interdit Ame Laceree). Bible : re-castable si HG remonte
            // a 5 ET cooldown expire.
            int currentTurnForCooldown = f.TryGetSingleton<CombatState>(out var stateCD) ? stateCD.TurnNumber : 0;
            if (cmd.Spell == SpellId.SoulrenderAmeLaceree)
            {
                int turnsSinceUse = currentTurnForCooldown - caster->LastAmeLaceeUsedOnTurn;
                if (turnsSinceUse < SpellRegistry.AmeLaceeCooldownTurns)
                {
                    Log.Warn($"[Spell] rejet : Ame Laceree en cooldown ({turnsSinceUse}/{SpellRegistry.AmeLaceeCooldownTurns} tours depuis dernier usage tour {caster->LastAmeLaceeUsedOnTurn})");
                    return;
                }
            }

            // HG validation (mandatory + optional clamped).
            int hgSpend = cmd.HGSpend;
            if (hgSpend > spellDef.HGCostMaxOptional) hgSpend = spellDef.HGCostMaxOptional;
            if (hgSpend < 0) hgSpend = 0;
            int totalHgCost = spellDef.HGCostMandatory + hgSpend;
            if (caster->Resource < totalHgCost)
            {
                Log.Warn($"[Spell] rejet : HG {caster->Resource} < cost {totalHgCost} (mand {spellDef.HGCostMandatory} + opt {hgSpend})");
                return;
            }

            // Range Manhattan caster -> target.
            int dx = cmd.TargetX - caster->GridX;
            int dy = cmd.TargetY - caster->GridY;
            int absDx = dx < 0 ? -dx : dx;
            int absDy = dy < 0 ? -dy : dy;
            int dist = absDx + absDy;
            if (dist < spellDef.RangeMin || dist > spellDef.RangeMax)
            {
                Log.Warn($"[Spell] rejet : distance {dist} hors range [{spellDef.RangeMin},{spellDef.RangeMax}]");
                return;
            }

            // Filter sur la case ciblee (sauf Rugissement : Filter=Self valide deja, on resoud
            // les vraies cibles dans le damage loop via le check enemy/distance).
            if (!TargetingResolver.MatchesFilter(f, cmd.TargetX, cmd.TargetY, spellDef.Filter, casterEntity, playerIndex))
            {
                Log.Warn($"[Spell] rejet : ({cmd.TargetX},{cmd.TargetY}) ne match pas filter {spellDef.Filter}");
                return;
            }

            // 2.15.c — Voile d'Ombre : si cible directe (filter Enemy/AnyUnit) est un combatant
            // Untargetable, reject. Les AoE qui passent par sa case continuent de toucher
            // (Bible : invisibilite directe, pas immortalite). Self/Ally/EmptyTile non concernes.
            if (spellDef.Filter == TargetingFilter.Enemy ||
                spellDef.Filter == TargetingFilter.AnyUnit)
            {
                EntityRef targetOcc = GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY);
                if (targetOcc != EntityRef.None
                    && f.Unsafe.TryGetPointer<Combatant>(targetOcc, out Combatant* targetC)
                    && StatusHelper.Has(targetC, StatusKind.Untargetable))
                {
                    Log.Warn($"[Spell] rejet : cible P{targetC->PlayerIndex} Untargetable (Voile d'Ombre)");
                    return;
                }
            }

            // 2.15.c — Evanescence : conditionnel HP < 30% MaxHP (Bible). Check avant consommation.
            if (cmd.Spell == SpellId.NightseerEvanescence)
            {
                if (caster->HP * 100 >= caster->MaxHP * SpellRegistry.EvanescenceHpThresholdPct)
                {
                    Log.Warn($"[Spell] rejet : Evanescence requiert HP < {SpellRegistry.EvanescenceHpThresholdPct}% (actuel {caster->HP}/{caster->MaxHP})");
                    return;
                }
            }

            // 2.16 — Traquenard : cooldown 4 tours + pre-validation case adjacente libre.
            if (cmd.Spell == SpellId.NightseerTraquenard)
            {
                int turnsSinceUse = currentTurnForCooldown - caster->LastTraquenardUsedOnTurn;
                if (turnsSinceUse < SpellRegistry.TraquenardCooldownTurns)
                {
                    Log.Warn($"[Spell] rejet : Traquenard en cooldown ({turnsSinceUse}/{SpellRegistry.TraquenardCooldownTurns} tours depuis dernier usage tour {caster->LastTraquenardUsedOnTurn})");
                    return;
                }
                if (!TryFindTraquenardLandingCell(f, caster, cmd.TargetX, cmd.TargetY, out _, out _))
                {
                    Log.Warn($"[Spell] rejet : Traquenard pas de case adjacente libre autour de ({cmd.TargetX},{cmd.TargetY})");
                    return;
                }
            }

            // ===== Consommation des ressources =====
            caster->PA -= effectivePACost;

            if (totalHgCost > 0)
            {
                caster->Resource -= totalHgCost;
                Log.Info($"[Spell] HG consume {totalHgCost} (mand {spellDef.HGCostMandatory} + opt {hgSpend}) -> {caster->Resource}");
            }

            if (spellDef.OncePerMatchBit != SpellRegistry.OncePerMatchBitNone)
            {
                caster->OncePerMatchUsedFlags |= 1 << spellDef.OncePerMatchBit;
            }

            int currentTurn = f.TryGetSingleton<CombatState>(out var state) ? state.TurnNumber : 0;

            // ===== Calcul damage effectif (buffs + HG variants) =====
            int effectiveDmg = spellDef.DamageAmount;
            // Ouvre-Plaie : 1 HG depense -> +120 dgts (Bible V7.1)
            if (cmd.Spell == SpellId.SoulrenderOuvrePlaie && hgSpend >= 1)
            {
                effectiveDmg += 120;
            }
            // Detonation Sanglante (2.10.c) : 60 + 40 par HG total consomme (mandatory + optional).
            // Avec HGSpend=0 -> 60+80=140. Avec HGSpend=3 (max) -> 60+200=260.
            if (cmd.Spell == SpellId.SoulrenderDetonationSanglante)
            {
                int totalHGForDetonation = spellDef.HGCostMandatory + hgSpend;
                effectiveDmg = SpellRegistry.DetonationBaseDamage
                             + SpellRegistry.DetonationDamagePerHG * totalHGForDetonation;
            }
            // 2.16 — Traquenard +80 dgts si cible Traque/Empreinte ou case Voilee owner caster (Bible).
            // La consommation effective de la marque/voile + gain +2 PR sont dans ApplySpellSpecificEffects.
            if (cmd.Spell == SpellId.NightseerTraquenard
                && TraquenardHasMarkOrOwnVeil(f, cmd.TargetX, cmd.TargetY, caster->PlayerIndex))
            {
                effectiveDmg += SpellRegistry.TraquenardDmgBonusIfMarked;
            }
            // Pacte de Sang +50% : applique a tout sort OFFENSIF (DamageAmount > 0).
            // Consume apres le damage loop (1 seul cast offensif).
            int pacteBuffPct = StatusHelper.GetMagnitude(caster, StatusKind.BuffNextOffensiveDmgPercent, 0);
            if (effectiveDmg > 0 && pacteBuffPct > 0)
            {
                effectiveDmg += effectiveDmg * pacteBuffPct / 100;
            }
            // Peau de Fer (2.10.b) : pendant la duree du shield, sorts melee du caster
            // gagnent +30 dgts (Bible V7.1). spellDef.RangeMax == 1 = sort melee.
            // Le bonus s'applique uniquement si le shield a encore des HP (Magnitude > 0).
            if (effectiveDmg > 0
                && spellDef.RangeMax == 1
                && StatusHelper.GetMagnitude(caster, StatusKind.ShieldActive, 0) > 0)
            {
                effectiveDmg += SpellRegistry.PeauDeFerMeleeDmgBonus;
            }

            // ===== Resolution AoE =====
            // Cas special Rugissement : rayon 3 Manhattan autour du caster (TargetingResolver
            // n'a pas CircleLarge implemente en 2.6, on inline ici).
            int* effectBuffer = stackalloc int[GridConstants.Count];
            int effectCount;
            if (cmd.Spell == SpellId.SoulrenderRugissement)
            {
                ResolveCircleManhattan(caster->GridX, caster->GridY, radius: 3, effectBuffer, out effectCount);
            }
            else
            {
                TargetingResolver.ResolveEffectCells(
                    f,
                    caster->GridX, caster->GridY,
                    cmd.TargetX, cmd.TargetY,
                    spellDef.Shape,
                    effectBuffer,
                    out effectCount);
            }

            // ===== Damage loop + Shield absorption + Riposte trigger + gain HG =====
            bool casterHitSomething = false;       // au moins 1 cible a perdu des HP (apres shield)
            bool castHitMarkedTarget = false;      // au moins 1 cible MarkedByCarnage a perdu des HP
            bool wasKill = false;                  // 2.10.c : au moins 1 cible est tombee a HP=0 sur ce cast
            int killedTargetX = -1;                // position de la cible tuee (pour recul Tranche-Ame)
            int killedTargetY = -1;
            bool isMelee = spellDef.RangeMax == 1; // pour trigger Riposte (Bible : attaque MELEE)
            int lastHitHPLoss = 0;                 // 2.11 : HP loss de la derniere cible touchee (Ame Laceree heal 50%)
            bool castTriggeredLeCri = false;       // 2.11 : passif <20% HP -> Sang Coagule croix 5
            // 2.15.a — tracking pour effets Nightseer post-damage / per-cast.
            int nightseerPrescienceGain = 0;       // Tir Precis sur Traque : +1 PR cumulable per hit
            int volEpinesLastHitX = -1;            // Volee d'Epines : derniere case touchee pour pose Filet
            int volEpinesLastHitY = -1;

            // 2.15.a — Boucle damage : entree conditionnee a IsOffensive (pas DamageAmount > 0)
            // pour permettre aux sorts a damage 100% custom-per-cell d'y entrer aussi (ex Salve
            // Mortelle 220 centre/130 cotes, DamageAmount = 0 dans SpellDef).
            if (spellDef.IsOffensive != 0)
            {
                for (int i = 0; i < effectCount; i++)
                {
                    int idx = effectBuffer[i];
                    int cx = idx % GridConstants.Width;
                    int cy = idx / GridConstants.Width;
                    EntityRef target = GridHelpers.GetOccupant(f, cx, cy);
                    if (target == EntityRef.None) continue;
                    if (!f.Unsafe.TryGetPointer<Combatant>(target, out Combatant* targetC)) continue;
                    if (target == casterEntity) continue; // pas d'auto-damage offensif

                    // 2.15.a — Nightseer per-cell damage variants (override effectiveDmg).
                    // Pacte/Peau de Fer (Soulrender) restent appliques en amont sur effectiveDmg
                    // donc en pratique sans effet pour les sorts Nightseer (caster Nightseer
                    // n'a pas ces statuses normalement).
                    int dmgThisTarget = effectiveDmg;
                    if (cmd.Spell == SpellId.NightseerTirPrecis)
                    {
                        if (MarkHelpers.HasMark(targetC, MarkKind.Traque))
                        {
                            dmgThisTarget = SpellRegistry.TirPrecisDmgIfTraque;
                            nightseerPrescienceGain += 1;
                        }
                    }
                    else if (cmd.Spell == SpellId.NightseerFrappeDeLOmbre)
                    {
                        // Bonus si target s'est deplacee : PM courant < MaxPM/2.
                        if (targetC->MaxPM > 0 && targetC->PM * 2 < targetC->MaxPM)
                        {
                            dmgThisTarget = SpellRegistry.FrappeDeLOmbreDmgIfMoved;
                            // Empreinte 2 tours posee a la fin de la boucle (apres damage applique).
                            // On le fait ici (juste apres calcul dmg) car simple et evite tracker side-effect.
                            MarkHelpers.ApplyMark(targetC, MarkKind.Empreinte,
                                SpellRegistry.FrappeDeLOmbreEmpreinteTurns,
                                caster->PlayerIndex, currentTurn);
                            Log.Info($"[Spell] Frappe de l'Ombre : Empreinte {SpellRegistry.FrappeDeLOmbreEmpreinteTurns} tours sur P{targetC->PlayerIndex} (PM {targetC->PM}/{targetC->MaxPM})");
                        }
                    }
                    else if (cmd.Spell == SpellId.NightseerSalveMortelle)
                    {
                        bool isCenter = (cx == cmd.TargetX && cy == cmd.TargetY);
                        dmgThisTarget = isCenter
                            ? SpellRegistry.SalveMortelleDmgCenter
                            : SpellRegistry.SalveMortelleDmgSide;
                        if (MarkHelpers.HasMark(targetC, MarkKind.Traque))
                        {
                            dmgThisTarget += SpellRegistry.SalveMortelleDmgIfTraque;
                            nightseerPrescienceGain += 1; // 2.15.b — declenchement Traque
                        }
                        // Voile dans la cell : +50 dgts ET dechire le voile (cleared apres apply dmg).
                        int veilOwnerHere = FogHelpers.GetVeilOwner(f, cx, cy);
                        if (veilOwnerHere >= 0)
                        {
                            dmgThisTarget += SpellRegistry.SalveMortelleDmgIfVoile;
                            FogHelpers.ClearVeil(f, cx, cy);
                            nightseerPrescienceGain += 1; // 2.15.b — declenchement Voile
                            Log.Info($"[Spell] Salve Mortelle dechire Voile sur ({cx},{cy})");
                        }
                    }
                    else if (cmd.Spell == SpellId.NightseerDetonationOnirique)
                    {
                        int veilOwnerHere = FogHelpers.GetVeilOwner(f, cx, cy);
                        if (veilOwnerHere >= 0)
                        {
                            dmgThisTarget += SpellRegistry.DetonationOniriqueDmgVoile;
                            FogHelpers.ClearVeil(f, cx, cy);
                            nightseerPrescienceGain += 1; // 2.15.b — declenchement Voile
                            Log.Info($"[Spell] Detonation Onirique dechire Voile sur ({cx},{cy})");
                        }
                    }
                    else if (cmd.Spell == SpellId.ColossarFrappeLourde)
                    {
                        // 3.3.a.i — Bible Frappe Lourde : 180 base, +100 si cible epinglee
                        // (case opposee au caster contient obstacle/bord).
                        if (ColossarPassif.IsTargetPinnedFromCaster(f, caster, targetC))
                        {
                            dmgThisTarget = SpellRegistry.FrappeLourdeDmgIfPinned;
                            Log.Info($"[Spell] Frappe Lourde EPINGLEE : {SpellRegistry.FrappeLourdeDmgIfPinned} dgts sur P{targetC->PlayerIndex} ({cx},{cy})");
                        }
                    }

                    // 3.3.a.i — Passif Densite Inerte bonus adjacence : si caster Colossar
                    // adjacent a un de ses obstacles ET sort range max <= 2 -> +20 dmg.
                    // S'applique a TOUS les sorts Colossar (Frappe Lourde, Represailles,
                    // Onde de Choc 3.3.a.ii, Marteau Punisseur 3.3.a.ii). Pas Choc Sismique
                    // (range 4) ni sorts survie. Bible : "Quand le Colossar est adjacent a
                    // un de ses Piliers/Murs : ses sorts a portee 1-2 gagnent +20 degats".
                    if (caster->Class == NymoraClass.Colossar
                        && spellDef.RangeMax <= SpellRegistry.DensiteInerteAdjacenceMaxRange
                        && spellDef.RangeMax >= 1 // exclut self-target (range 0)
                        && ColossarPassif.IsAdjacentToOwnObstacle(f, caster, caster->PlayerIndex))
                    {
                        dmgThisTarget += SpellRegistry.DensiteInerteAdjacenceBonus;
                        Log.Info($"[Densite Inerte] +{SpellRegistry.DensiteInerteAdjacenceBonus} dmg adjacence sur P{caster->PlayerIndex} (sort {cmd.Spell}) -> {dmgThisTarget}");
                    }

                    // 2.15.a — Volee d'Epines : on retient la derniere case touchee (pour pose
                    // Filet apres la boucle). En Line, l'iteration suit l'ordre de proximite
                    // depuis le caster, donc la derniere iter = la plus loin.
                    if (cmd.Spell == SpellId.NightseerVoleeDEpines)
                    {
                        volEpinesLastHitX = cx;
                        volEpinesLastHitY = cy;
                    }

                    // 3.2 — Densite Inerte (Bible V7.1 Colossar passif) : -8% dmg subis par
                    // obstacle owner=cible actif, cap -24% (3 obstacles). Applique sur dmgThisTarget
                    // BEFORE shield/HP calc, donc le shield absorbe le montant deja reduit (Bible :
                    // "subit -X% degats" = c'est l'incoming qui est reduit). Helper no-op si target
                    // != Colossar, donc safe pour tous les casters/cibles.
                    if (targetC->Class == NymoraClass.Colossar)
                    {
                        int dmgBefore = dmgThisTarget;
                        dmgThisTarget = ColossarPassif.ApplyDamageReduction(f, targetC, dmgThisTarget);
                        if (dmgThisTarget != dmgBefore)
                        {
                            int pct = ColossarPassif.GetDamageReductionPercent(f, targetC);
                            Log.Info($"[Densite Inerte] -{pct}% dmg sur P{targetC->PlayerIndex} : {dmgBefore} -> {dmgThisTarget}");
                        }
                    }

                    // Shield absorption (2.10.b) : ShieldActive absorbe avant HP.
                    // 2.11 Passif RAGE OUVERTE : si target <40% HP pre-damage ET caster Soulrender ET
                    // sort melee -> 50% des dgts bypass shield direct au HP. L'autre 50% va shield -> HP overflow.
                    int targetHPRatioPreDmg = targetC->MaxHP > 0 ? (targetC->HP * 100 / targetC->MaxHP) : 100;
                    int rageOuverteBypass = 0;
                    int shieldBefore = StatusHelper.GetMagnitude(targetC, StatusKind.ShieldActive, 0);
                    if (caster->Class == NymoraClass.Soulrender
                        && isMelee
                        && shieldBefore > 0
                        && targetHPRatioPreDmg < SpellRegistry.AppelDuSangPalierRageOuverte)
                    {
                        rageOuverteBypass = dmgThisTarget * SpellRegistry.AppelDuSangShieldBypassPct / 100;
                        Log.Info($"[Spell] Rage Ouverte (<{SpellRegistry.AppelDuSangPalierRageOuverte}% HP) : {rageOuverteBypass} dgts bypass shield sur P{targetC->PlayerIndex}");
                    }

                    // 2.15.b — Passif L'Œil qui n'est pas (Bible V7.1) : sorts Nightseer sur cible
                    // Traque ignorent 30% du shield (= 30% des dgts bypass shield direct au HP).
                    int oeilTraqueBypass = 0;
                    if (caster->Class == NymoraClass.Nightseer
                        && shieldBefore > 0
                        && MarkHelpers.HasMark(targetC, MarkKind.Traque))
                    {
                        oeilTraqueBypass = dmgThisTarget * SpellRegistry.OeilQuiNestPasShieldPiercePct / 100;
                        Log.Info($"[Spell] L'Œil qui n'est pas : {oeilTraqueBypass} dgts pierce shield sur P{targetC->PlayerIndex} (Traque, {SpellRegistry.OeilQuiNestPasShieldPiercePct}%)");
                    }

                    int totalShieldBypass = rageOuverteBypass + oeilTraqueBypass;
                    int dmgToShield = dmgThisTarget - totalShieldBypass; // partie shield-able
                    if (shieldBefore > 0 && dmgToShield > 0)
                    {
                        int absorbed = dmgToShield > shieldBefore ? shieldBefore : dmgToShield;
                        int shieldAfter = shieldBefore - absorbed;
                        if (shieldAfter == 0)
                        {
                            StatusHelper.Consume(targetC, StatusKind.ShieldActive);
                            Log.Info($"[Spell] Shield brise sur P{targetC->PlayerIndex} ({cx},{cy}) (absorbe {absorbed})");
                        }
                        else
                        {
                            StatusHelper.SetMagnitude(targetC, StatusKind.ShieldActive, shieldAfter);
                            Log.Info($"[Spell] Shield absorbe {absorbed} sur P{targetC->PlayerIndex} ({cx},{cy}) (shield {shieldBefore} -> {shieldAfter})");
                        }
                        dmgToShield -= absorbed;
                    }

                    int totalHPLoss = dmgToShield + totalShieldBypass; // ce qui passe au HP
                    if (totalHPLoss > 0)
                    {
                        int before = targetC->HP;
                        targetC->HP -= totalHPLoss;
                        if (targetC->HP < 0) targetC->HP = 0;
                        casterHitSomething = true;
                        lastHitHPLoss = totalHPLoss; // 2.11 : sert au heal Ame Laceree (50% des dgts qui passent)
                        Log.Info($"[Spell] Damage {dmgThisTarget} (HP loss {totalHPLoss}, dont bypass {totalShieldBypass}) sur P{targetC->PlayerIndex} ({cx},{cy}) HP {before} -> {targetC->HP}");

                        // 2.15.a — tracker dgts subis par target ce round (Bible Prescience).
                        targetC->DamageTakenThisRound += totalHPLoss;

                        // 2.10.c : Kill detection. Tracker si au moins 1 cible est tombee a HP=0.
                        // killedTargetX/Y sert au recul Tranche-Ame (direction opposee a la cible tuee).
                        if (targetC->HP == 0 && before > 0)
                        {
                            wasKill = true;
                            killedTargetX = cx;
                            killedTargetY = cy;
                            Log.Info($"[Spell] KILL : P{targetC->PlayerIndex} tombe a HP=0 sur ({cx},{cy})");
                        }

                        // 2.11 Passif LE CRI (<20% HP post-hit) : Sang Coagule croix 5 autour caster.
                        // Trigger une seule fois par cast (le bool ne sert qu'a marquer, l'application se fait apres la boucle).
                        if (caster->Class == NymoraClass.Soulrender
                            && targetC->MaxHP > 0
                            && targetC->HP * 100 < targetC->MaxHP * SpellRegistry.AppelDuSangPalierLeCri)
                        {
                            castTriggeredLeCri = true;
                        }

                        // Marque de Carnage tracker (bonus HG cote caster, applique 1x apres la boucle).
                        if (StatusHelper.Has(targetC, StatusKind.MarkedByCarnage))
                        {
                            castHitMarkedTarget = true;
                        }

                        // Trigger Riposte Carmin si cible porte RipostMelee et sort = melee.
                        if (isMelee && StatusHelper.Has(targetC, StatusKind.RipostMelee))
                        {
                            int reflectDmg = StatusHelper.GetMagnitude(targetC, StatusKind.RipostMelee, 100);
                            int casterBefore = caster->HP;
                            caster->HP -= reflectDmg;
                            if (caster->HP < 0) caster->HP = 0;
                            Log.Info($"[Spell] Riposte Carmin : P{caster->PlayerIndex} prend {reflectDmg} dgts (HP {casterBefore} -> {caster->HP})");

                            // L'attaquant prend MovementMalus 1 (1 tour) sur son prochain mouvement.
                            StatusHelper.Apply(caster, StatusKind.MovementMalus, magnitude: 1, turnsLeft: 1, currentTurn);
                        }

                        // Gain HG cote CIBLE (Bible V7.1) : Soulrender qui subit, max 1 par tour adverse.
                        // Conditionne a dgts effectifs au HP (shield total absorption = pas de gain).
                        if (targetC->Class == NymoraClass.Soulrender)
                        {
                            if (targetC->LastResourceGainOnHitTurn != currentTurn)
                            {
                                int maxResource = CombatantStats.GetMaxResource(targetC->Class);
                                int beforeRes = targetC->Resource;
                                targetC->Resource = (beforeRes + 1 > maxResource) ? maxResource : beforeRes + 1;
                                targetC->LastResourceGainOnHitTurn = currentTurn;
                                if (targetC->Resource != beforeRes)
                                {
                                    Log.Info($"[Spell] HG +1 sur P{targetC->PlayerIndex} (subi dgts, tour {currentTurn}) : {beforeRes} -> {targetC->Resource}");
                                }
                            }
                        }
                    }
                }
            }

            // Gain HG cote CASTER (Bible V7.1) : Soulrender qui inflige, max 1 par sort.
            if (casterHitSomething && caster->Class == NymoraClass.Soulrender)
            {
                int maxResource = CombatantStats.GetMaxResource(caster->Class);
                int beforeRes = caster->Resource;
                caster->Resource = (beforeRes + 1 > maxResource) ? maxResource : beforeRes + 1;
                if (caster->Resource != beforeRes)
                {
                    Log.Info($"[Spell] HG +1 sur P{caster->PlayerIndex} (inflige dgts) : {beforeRes} -> {caster->Resource}");
                }

                // Marque de Carnage (2.10.b) : +1 HG bonus si on a touche au moins 1 cible marquee.
                // Max 1 bonus par cast peu importe le nb de cibles marquees touchees.
                if (castHitMarkedTarget)
                {
                    int beforeBonus = caster->Resource;
                    caster->Resource = (beforeBonus + 1 > maxResource) ? maxResource : beforeBonus + 1;
                    if (caster->Resource != beforeBonus)
                    {
                        Log.Info($"[Spell] HG +1 bonus (Marque de Carnage) sur P{caster->PlayerIndex} : {beforeBonus} -> {caster->Resource}");
                    }
                }
            }

            // 2.15.a — Gain Prescience cote CASTER Nightseer (Bible : +1 PR par hit Traque sur
            // Tir Precis). Capacite a 4 PR. Pas de cap sur le gain per-cast (cumulable si
            // plusieurs cibles Traque dans une AoE — mais Tir Precis SingleTile, donc max 1).
            if (nightseerPrescienceGain > 0 && caster->Class == NymoraClass.Nightseer)
            {
                int maxResource = CombatantStats.GetMaxResource(caster->Class);
                int beforeRes = caster->Resource;
                caster->Resource = beforeRes + nightseerPrescienceGain;
                if (caster->Resource > maxResource) caster->Resource = maxResource;
                if (caster->Resource != beforeRes)
                {
                    Log.Info($"[Spell] PR +{nightseerPrescienceGain} sur P{caster->PlayerIndex} (Tir Precis sur Traque) : {beforeRes} -> {caster->Resource}");
                }
            }

            // 2.15.a — Volee d'Epines : pose un Filet de Ronces sur la DERNIERE case touchee.
            // Si aucune cible n'a ete touchee (ligne tiree dans le vide), pas de Filet pose.
            if (cmd.Spell == SpellId.NightseerVoleeDEpines && volEpinesLastHitX >= 0)
            {
                FogHelpers.PlaceTrap(f, volEpinesLastHitX, volEpinesLastHitY, TrapKind.FiletRonces, caster->PlayerIndex, currentTurn);
                Log.Info($"[Spell] Volee d'Epines : Filet de Ronces pose sur ({volEpinesLastHitX},{volEpinesLastHitY}) par P{caster->PlayerIndex}");
            }

            // ===== Consume Pacte buff si utilise =====
            if (effectiveDmg > 0 && pacteBuffPct > 0)
            {
                StatusHelper.Consume(caster, StatusKind.BuffNextOffensiveDmgPercent);
                Log.Info($"[Spell] BuffNextOffensiveDmgPercent consume sur P{caster->PlayerIndex} (+{pacteBuffPct}%)");
            }

            // 2.11 Passif LE CRI : si target <20% HP post-hit, pose Sang Coagule sur croix 5
            // (caster + 4 cardinales). Une fois par cast peu importe le nb de cibles.
            if (castTriggeredLeCri)
            {
                int cx0 = caster->GridX;
                int cy0 = caster->GridY;
                GridHelpers.SetTerrain(f, cx0,     cy0,     TerrainKind.SangCoagule, SpellRegistry.SangCoaguleTurns, currentTurn);
                GridHelpers.SetTerrain(f, cx0 + 1, cy0,     TerrainKind.SangCoagule, SpellRegistry.SangCoaguleTurns, currentTurn);
                GridHelpers.SetTerrain(f, cx0 - 1, cy0,     TerrainKind.SangCoagule, SpellRegistry.SangCoaguleTurns, currentTurn);
                GridHelpers.SetTerrain(f, cx0,     cy0 + 1, TerrainKind.SangCoagule, SpellRegistry.SangCoaguleTurns, currentTurn);
                GridHelpers.SetTerrain(f, cx0,     cy0 - 1, TerrainKind.SangCoagule, SpellRegistry.SangCoaguleTurns, currentTurn);
                Log.Info($"[Spell] LE CRI ! Cible <{SpellRegistry.AppelDuSangPalierLeCri}% HP, Sang Coagule pose en croix 5 autour P{caster->PlayerIndex} ({cx0},{cy0})");
            }

            // ===== Effets specifiques par sort (apres damage) =====
            ApplySpellSpecificEffects(f, cmd, spellDef, caster, casterEntity,
                casterHitSomething, hgSpend, currentTurn,
                effectBuffer, effectCount,
                wasKill, killedTargetX, killedTargetY, lastHitHPLoss);

            // ===== Rage Insatiable : regen 1 PA si offensif (max 1 par tour) =====
            if (spellDef.IsOffensive != 0 && StatusHelper.Has(caster, StatusKind.RageInsatiableActive))
            {
                int lastTurnGained = StatusHelper.GetMagnitude(caster, StatusKind.RageInsatiableActive, -1);
                if (lastTurnGained != currentTurn)
                {
                    int paBefore = caster->PA;
                    caster->PA += 1;
                    if (caster->PA > caster->MaxPA) caster->PA = caster->MaxPA;
                    StatusHelper.SetMagnitude(caster, StatusKind.RageInsatiableActive, currentTurn);
                    Log.Info($"[Spell] Rage Insatiable : P{caster->PlayerIndex} regen 1 PA ({paBefore} -> {caster->PA})");
                }
            }

            // 2.12.bis : tracker du dernier cast pour driver les anims cote View (cast/attack).
            // La View pole la diff `LastCastOnTurn` et map `LastCastSpellId` vers SpellCategory pour
            // choisir l'anim (Survival/Tactical/Offensive/Signature) + range pour Attack vs Cast.
            // 2.13.e : on capture aussi la TargetX/Y pour permettre aux VFX de spawn a la case
            // visee plutot qu'a la case du caster (cf CombatVFXView).
            caster->LastCastOnTurn = currentTurn;
            caster->LastCastSpellId = cmd.Spell;
            caster->LastCastTargetX = cmd.TargetX;
            caster->LastCastTargetY = cmd.TargetY;
            // 2.13.e bugfix : compteur monotone pour permettre a la View de detecter
            // chaque cast individuellement, meme multiples dans le meme tour.
            caster->LastCastSequence += 1;

            Log.Info($"[Spell] P{playerIndex} cast {cmd.Spell} target=({cmd.TargetX},{cmd.TargetY}) PA restant={caster->PA}");
        }

        /// <summary>
        /// Applique les effets non-damage specifiques au sort : statuses, self-effects.
        /// Le damage / gain HG / Riposte trigger sont deja faits dans la boucle principale.
        ///
        /// 2.10.c : prend wasKill + killedTargetX/Y en parametre pour les effets conditionnels
        /// au kill (recul Tranche-Ame, Curee kill chain).
        /// 2.11 : lastHitHPLoss sert au heal Ame Laceree (50% des dgts qui passent).
        /// </summary>
        private static void ApplySpellSpecificEffects(
            Frame f,
            CastSpellCommand cmd,
            SpellDef spellDef,
            Combatant* caster,
            EntityRef casterEntity,
            bool casterHitSomething,
            int hgSpend,
            int currentTurn,
            int* effectBuffer,
            int effectCount,
            bool wasKill,
            int killedTargetX,
            int killedTargetY,
            int lastHitHPLoss)
        {
            switch (cmd.Spell)
            {
                case SpellId.SoulrenderTrancheAme:
                {
                    // 2.10.c : recul 2 cases gratuites si la cible est tuee par Tranche-Ame.
                    // Direction opposee a la cible (sur l'axe melee, donc dx ou dy != 0 mais pas les 2).
                    if (wasKill)
                    {
                        int dxFromTarget = caster->GridX - killedTargetX;
                        int dyFromTarget = caster->GridY - killedTargetY;
                        // Tente 2 cases d'abord, fallback 1 case si bloque.
                        int reculDist = SpellRegistry.TrancheAmeKillRecul;
                        bool moved = false;
                        for (int dist = reculDist; dist >= 1 && !moved; dist--)
                        {
                            int newX = caster->GridX + dxFromTarget * dist;
                            int newY = caster->GridY + dyFromTarget * dist;
                            moved = MovementHelpers.MoveNonPM(f, casterEntity, caster, newX, newY);
                            if (moved)
                            {
                                Log.Info($"[Spell] Tranche-Ame recul {dist} case(s) sur P{caster->PlayerIndex} -> ({newX},{newY})");
                            }
                        }
                        if (!moved)
                        {
                            Log.Info($"[Spell] Tranche-Ame recul impossible (cases bloquees) sur P{caster->PlayerIndex}");
                        }
                    }
                    break;
                }

                case SpellId.SoulrenderOuvrePlaie:
                    // Si 1 HG depense ET cible touchee : applique AntiHealShield 2 tours sur la cible.
                    if (hgSpend >= 1 && casterHitSomething)
                    {
                        EntityRef target = GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY);
                        if (target != EntityRef.None
                            && f.Unsafe.TryGetPointer<Combatant>(target, out Combatant* targetC))
                        {
                            StatusHelper.Apply(targetC, StatusKind.AntiHealShield, magnitude: 0, turnsLeft: 2, currentTurn);
                            Log.Info($"[Spell] Ouvre-Plaie : AntiHealShield 2 tours sur P{targetC->PlayerIndex}");
                        }
                    }
                    break;

                case SpellId.SoulrenderPacteDeSang:
                    // -80 HP self, +3 HG self (clampe au cap), buff +50% next offensif 1 tour.
                    int hpBefore = caster->HP;
                    caster->HP -= 80;
                    if (caster->HP < 0) caster->HP = 0;
                    Log.Info($"[Spell] Pacte de Sang : self-damage 80 (HP {hpBefore} -> {caster->HP})");

                    int maxRes = CombatantStats.GetMaxResource(caster->Class);
                    int resBefore = caster->Resource;
                    caster->Resource += 3;
                    if (caster->Resource > maxRes) caster->Resource = maxRes;
                    Log.Info($"[Spell] Pacte de Sang : +3 HG (clamped, {resBefore} -> {caster->Resource})");

                    StatusHelper.Apply(caster, StatusKind.BuffNextOffensiveDmgPercent, magnitude: 50, turnsLeft: 1, currentTurn);
                    Log.Info($"[Spell] Pacte de Sang : BuffNextOffensiveDmgPercent +50% (1 tour) sur P{caster->PlayerIndex}");
                    break;

                case SpellId.SoulrenderRugissement:
                    // AoE rayon 3 deja resolue dans effectBuffer. Pour chaque ennemi dedans :
                    // MovementMalus + AntiTeleport (1 tour). Magnitude PM malus = 2 si cible <50% HP, sinon 1.
                    for (int i = 0; i < effectCount; i++)
                    {
                        int idx = effectBuffer[i];
                        int cx = idx % GridConstants.Width;
                        int cy = idx / GridConstants.Width;
                        EntityRef target = GridHelpers.GetOccupant(f, cx, cy);
                        if (target == EntityRef.None || target == casterEntity) continue;
                        if (!f.Unsafe.TryGetPointer<Combatant>(target, out Combatant* targetC)) continue;
                        if (targetC->PlayerIndex == caster->PlayerIndex) continue; // skip allies (1v1 = N/A, mais futur-proof)

                        int pmMalus = (targetC->HP * 2 < targetC->MaxHP) ? 2 : 1;
                        StatusHelper.Apply(targetC, StatusKind.MovementMalus, magnitude: pmMalus, turnsLeft: 1, currentTurn);
                        StatusHelper.Apply(targetC, StatusKind.AntiTeleport, magnitude: 0, turnsLeft: 1, currentTurn);
                        Log.Info($"[Spell] Rugissement : -{pmMalus} PM + AntiTeleport sur P{targetC->PlayerIndex} ({cx},{cy})");
                    }
                    break;

                case SpellId.SoulrenderRageInsatiable:
                    // RageInsatiableActive 2 tours. Magnitude = LastTurnPAGained tracker (init -1).
                    StatusHelper.Apply(caster, StatusKind.RageInsatiableActive, magnitude: -1, turnsLeft: 2, currentTurn);
                    Log.Info($"[Spell] Rage Insatiable : actif 2 tours sur P{caster->PlayerIndex}");
                    break;

                case SpellId.SoulrenderRiposteCarmin:
                    // RipostMelee 1 tour, magnitude = 100 dgts reflect.
                    StatusHelper.Apply(caster, StatusKind.RipostMelee, magnitude: 100, turnsLeft: 1, currentTurn);
                    Log.Info($"[Spell] Riposte Carmin : RipostMelee 100 dgts (1 tour) sur P{caster->PlayerIndex}");
                    break;

                // -------------------------------------------------------------
                // 2.10.b
                // -------------------------------------------------------------

                case SpellId.SoulrenderMarqueDeCarnage:
                {
                    EntityRef target = GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY);
                    if (target != EntityRef.None
                        && f.Unsafe.TryGetPointer<Combatant>(target, out Combatant* targetC))
                    {
                        StatusHelper.Apply(targetC, StatusKind.MarkedByCarnage, magnitude: 0,
                            turnsLeft: SpellRegistry.MarqueDeCarnageTurns, currentTurn);
                        Log.Info($"[Spell] Marque de Carnage : P{targetC->PlayerIndex} marque pour {SpellRegistry.MarqueDeCarnageTurns} tours");
                    }
                    break;
                }

                case SpellId.SoulrenderEmpoignade:
                {
                    EntityRef target = GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY);
                    if (target != EntityRef.None
                        && f.Unsafe.TryGetPointer<Combatant>(target, out Combatant* targetC))
                    {
                        int beforeX = targetC->GridX;
                        int beforeY = targetC->GridY;
                        bool pulled = PullTargetAdjacent(f, caster, target, targetC);
                        if (pulled)
                        {
                            Log.Info($"[Spell] Empoignade : P{targetC->PlayerIndex} tire ({beforeX},{beforeY}) -> ({targetC->GridX},{targetC->GridY})");
                        }
                        else
                        {
                            Log.Info($"[Spell] Empoignade : P{targetC->PlayerIndex} deja adjacent ou pas de case libre (no-op move)");
                        }
                        // AntiTeleport applique meme si pas de pull (cible ne peut pas tp au prochain tour).
                        StatusHelper.Apply(targetC, StatusKind.AntiTeleport, magnitude: 0, turnsLeft: 1, currentTurn);
                    }
                    break;
                }

                case SpellId.SoulrenderPeauDeFer:
                    // ShieldActive 2 tours, magnitude = 200 HP de shield.
                    // Le bonus +30 dgts melee est calcule au runtime dans effective damage (lit Magnitude).
                    StatusHelper.Apply(caster, StatusKind.ShieldActive,
                        magnitude: SpellRegistry.PeauDeFerShieldHP,
                        turnsLeft: SpellRegistry.PeauDeFerShieldTurns,
                        currentTurn);
                    Log.Info($"[Spell] Peau de Fer : ShieldActive {SpellRegistry.PeauDeFerShieldHP} HP / {SpellRegistry.PeauDeFerShieldTurns} tours sur P{caster->PlayerIndex}");
                    break;

                case SpellId.SoulrenderSeveVive:
                {
                    int healAmount = SpellRegistry.SeveViveHealBase;
                    int hgBonus = (hgSpend >= 1) ? SpellRegistry.SeveViveHealBonusHG : 0;
                    healAmount += hgBonus;
                    bool isBleeding = StatusHelper.Has(caster, StatusKind.BleedDoT);
                    int bleedBonus = isBleeding ? SpellRegistry.SeveViveHealBonusBleed : 0;
                    healAmount += bleedBonus;

                    if (StatusHelper.Has(caster, StatusKind.AntiHealShield))
                    {
                        Log.Info($"[Spell] Seve Vive : heal {healAmount} BLOQUE par AntiHealShield sur P{caster->PlayerIndex}");
                    }
                    else
                    {
                        int hpBeforeHeal = caster->HP;
                        caster->HP += healAmount;
                        if (caster->HP > caster->MaxHP) caster->HP = caster->MaxHP;
                        int healed = caster->HP - hpBeforeHeal;
                        Log.Info($"[Spell] Seve Vive : heal {healed} (base {SpellRegistry.SeveViveHealBase} + HG {hgBonus} + Bleed {bleedBonus}) HP {hpBeforeHeal} -> {caster->HP}");
                    }
                    break;
                }

                case SpellId.SoulrenderDernierSouffle:
                {
                    // Heal 200 HP (bloque si AntiHealShield) + 3 HG (toujours applique).
                    if (StatusHelper.Has(caster, StatusKind.AntiHealShield))
                    {
                        Log.Info($"[Spell] Dernier Souffle : heal {SpellRegistry.DernierSouffleHealAmount} BLOQUE par AntiHealShield sur P{caster->PlayerIndex}");
                    }
                    else
                    {
                        int hpBeforeHeal = caster->HP;
                        caster->HP += SpellRegistry.DernierSouffleHealAmount;
                        if (caster->HP > caster->MaxHP) caster->HP = caster->MaxHP;
                        Log.Info($"[Spell] Dernier Souffle : heal {SpellRegistry.DernierSouffleHealAmount} sur P{caster->PlayerIndex} (HP {hpBeforeHeal} -> {caster->HP})");
                    }

                    int maxResDS = CombatantStats.GetMaxResource(caster->Class);
                    int resBeforeDS = caster->Resource;
                    caster->Resource += SpellRegistry.DernierSouffleHGGain;
                    if (caster->Resource > maxResDS) caster->Resource = maxResDS;
                    Log.Info($"[Spell] Dernier Souffle : +{SpellRegistry.DernierSouffleHGGain} HG (clamped, {resBeforeDS} -> {caster->Resource})");
                    break;
                }

                // -------------------------------------------------------------
                // 2.10.c
                // -------------------------------------------------------------

                case SpellId.SoulrenderChargeBrutale:
                {
                    // Trace la ligne caster -> target jusqu'a 1ere case occupee ou non-walkable.
                    // Inflige 180 dgts si une cible bloque. Pose Vapeur Carmin sur cases foulees.
                    // Caster termine sur la case juste AVANT l'obstacle (ou case finale si pas obstacle).
                    int sx = caster->GridX;
                    int sy = caster->GridY;
                    int dxRaw = cmd.TargetX - sx;
                    int dyRaw = cmd.TargetY - sy;
                    int adx = dxRaw < 0 ? -dxRaw : dxRaw;
                    int ady = dyRaw < 0 ? -dyRaw : dyRaw;

                    // Axe dominant : on force la ligne sur 1 axe cardinal (X ou Y).
                    int stepX = 0, stepY = 0;
                    int maxSteps;
                    if (adx >= ady)
                    {
                        stepX = dxRaw > 0 ? 1 : -1;
                        maxSteps = adx;
                    }
                    else
                    {
                        stepY = dyRaw > 0 ? 1 : -1;
                        maxSteps = ady;
                    }
                    if (maxSteps > SpellRegistry.ChargeBrutaleRange) maxSteps = SpellRegistry.ChargeBrutaleRange;

                    int finalX = sx;
                    int finalY = sy;
                    EntityRef hitTarget = EntityRef.None;
                    int hitX = -1, hitY = -1;

                    for (int step = 1; step <= maxSteps; step++)
                    {
                        int cx = sx + stepX * step;
                        int cy = sy + stepY * step;
                        if (!GridHelpers.InBounds(cx, cy)) break;
                        if (!GridHelpers.IsWalkable(f, cx, cy)) break;

                        EntityRef occ = GridHelpers.GetOccupant(f, cx, cy);
                        if (occ != EntityRef.None && occ != casterEntity)
                        {
                            // Cible bloque la charge. Caster s'arrete sur la case precedente.
                            hitTarget = occ;
                            hitX = cx;
                            hitY = cy;
                            break;
                        }

                        // Case libre : caster avance.
                        finalX = cx;
                        finalY = cy;
                    }

                    // Mouvement caster vers la case finale (non-PM).
                    if (finalX != sx || finalY != sy)
                    {
                        bool moved = MovementHelpers.MoveNonPM(f, casterEntity, caster, finalX, finalY);
                        if (moved)
                        {
                            Log.Info($"[Spell] Charge Brutale : P{caster->PlayerIndex} fonce ({sx},{sy}) -> ({finalX},{finalY})");
                        }
                    }

                    // Pose Vapeur Carmin sur toutes les cases foulees, de (sx+step) jusqu'a (finalX,finalY) inclus.
                    // La case de la cible bloquante n'est PAS impregnee (foulee = traversee par le caster).
                    {
                        for (int step = 1; step <= maxSteps; step++)
                        {
                            int cx2 = sx + stepX * step;
                            int cy2 = sy + stepY * step;
                            GridHelpers.SetTerrain(f, cx2, cy2, TerrainKind.VapeurCarmin, SpellRegistry.VapeurCarminTurns, currentTurn);
                            if (cx2 == finalX && cy2 == finalY) break;
                        }
                        Log.Info($"[Spell] Charge Brutale : Vapeur Carmin pose sur cases foulees ({SpellRegistry.VapeurCarminTurns} tour)");
                    }

                    // Damage 180 a la cible bloquante si presente.
                    if (hitTarget != EntityRef.None
                        && f.Unsafe.TryGetPointer<Combatant>(hitTarget, out Combatant* hitC))
                    {
                        int hpBeforeHit = hitC->HP;
                        // Shield absorption manuel (Charge Brutale non-pipeline).
                        int dmgLeft = SpellRegistry.ChargeBrutaleDamage;
                        // 3.2 — Densite Inerte (Bible Colossar) appliquee AVANT shield, comme le
                        // damage loop standard. Charge Brutale bypass le pipeline donc on rebranche
                        // le hook ici manuellement (3.2 fix retour Lorenzo).
                        if (hitC->Class == NymoraClass.Colossar)
                        {
                            int dmgBeforeReduc = dmgLeft;
                            dmgLeft = ColossarPassif.ApplyDamageReduction(f, hitC, dmgLeft);
                            if (dmgLeft != dmgBeforeReduc)
                            {
                                int pct = ColossarPassif.GetDamageReductionPercent(f, hitC);
                                Log.Info($"[Densite Inerte] -{pct}% dmg sur P{hitC->PlayerIndex} (Charge Brutale) : {dmgBeforeReduc} -> {dmgLeft}");
                            }
                        }
                        int shieldBefore = StatusHelper.GetMagnitude(hitC, StatusKind.ShieldActive, 0);
                        if (shieldBefore > 0)
                        {
                            int absorbed = dmgLeft > shieldBefore ? shieldBefore : dmgLeft;
                            int shieldAfter = shieldBefore - absorbed;
                            if (shieldAfter == 0) StatusHelper.Consume(hitC, StatusKind.ShieldActive);
                            else StatusHelper.SetMagnitude(hitC, StatusKind.ShieldActive, shieldAfter);
                            dmgLeft -= absorbed;
                            Log.Info($"[Spell] Charge Brutale : shield absorbe {absorbed} sur P{hitC->PlayerIndex}");
                        }
                        if (dmgLeft > 0)
                        {
                            hitC->HP -= dmgLeft;
                            if (hitC->HP < 0) hitC->HP = 0;
                            Log.Info($"[Spell] Charge Brutale : Damage {SpellRegistry.ChargeBrutaleDamage} (HP loss {dmgLeft}) sur P{hitC->PlayerIndex} ({hitX},{hitY}) HP {hpBeforeHit} -> {hitC->HP}");

                            // Gain HG cote caster (Soulrender qui inflige, max 1 par sort).
                            if (caster->Class == NymoraClass.Soulrender)
                            {
                                int maxResCB = CombatantStats.GetMaxResource(caster->Class);
                                int beforeResCB = caster->Resource;
                                caster->Resource = (beforeResCB + 1 > maxResCB) ? maxResCB : beforeResCB + 1;
                                if (caster->Resource != beforeResCB)
                                {
                                    Log.Info($"[Spell] HG +1 sur P{caster->PlayerIndex} (Charge Brutale inflige) : {beforeResCB} -> {caster->Resource}");
                                }
                            }

                            // Gain HG cible si Soulrender subit, max 1/tour adverse.
                            if (hitC->Class == NymoraClass.Soulrender && hitC->LastResourceGainOnHitTurn != currentTurn)
                            {
                                int maxResCT = CombatantStats.GetMaxResource(hitC->Class);
                                int beforeResCT = hitC->Resource;
                                hitC->Resource = (beforeResCT + 1 > maxResCT) ? maxResCT : beforeResCT + 1;
                                hitC->LastResourceGainOnHitTurn = currentTurn;
                                if (hitC->Resource != beforeResCT)
                                {
                                    Log.Info($"[Spell] HG +1 sur P{hitC->PlayerIndex} (Charge Brutale subi) : {beforeResCT} -> {hitC->Resource}");
                                }
                            }
                        }
                    }
                    break;
                }

                case SpellId.SoulrenderDetonationSanglante:
                {
                    // Le damage en croix 3 est deja gere par le pipeline generique (DamageAmount calcule dynamiquement).
                    // Ici on pose Sang Coagule sur la case CENTRE (TargetX, TargetY) pour 2 tours.
                    GridHelpers.SetTerrain(f, cmd.TargetX, cmd.TargetY, TerrainKind.SangCoagule, SpellRegistry.SangCoaguleTurns, currentTurn);
                    Log.Info($"[Spell] Detonation Sanglante : Sang Coagule pose en ({cmd.TargetX},{cmd.TargetY}) pour {SpellRegistry.SangCoaguleTurns} tours");

                    // 2.11 : si totalHG consomme == 5 -> interdit Ame Laceree + reset son cooldown.
                    // On set LastAmeLaceeUsedOnTurn = currentTurn comme si on l'avait utilisee.
                    int totalHGForInterlock = spellDef.HGCostMandatory + hgSpend;
                    if (totalHGForInterlock >= 5)
                    {
                        caster->LastAmeLaceeUsedOnTurn = currentTurn;
                        Log.Info($"[Spell] Detonation 5 HG : Ame Laceree interdite, cooldown reset au tour {currentTurn} (-{SpellRegistry.AmeLaceeCooldownTurns} tours)");
                    }
                    break;
                }

                case SpellId.SoulrenderCuree:
                {
                    // Damage 150 deja applique par pipeline. Selon issue :
                    //   - Kill : caster heal 50% HP manquants + BonusPANextTurn = 4
                    //   - Miss (cible vivante) : caster -60 HP self
                    if (wasKill)
                    {
                        int missingHP = caster->MaxHP - caster->HP;
                        int healAmount = missingHP / 2; // 50% des HP manquants
                        int hpBeforeCuree = caster->HP;
                        if (StatusHelper.Has(caster, StatusKind.AntiHealShield))
                        {
                            Log.Info($"[Spell] Curee KILL : heal {healAmount} BLOQUE par AntiHealShield sur P{caster->PlayerIndex}");
                        }
                        else
                        {
                            caster->HP += healAmount;
                            if (caster->HP > caster->MaxHP) caster->HP = caster->MaxHP;
                            Log.Info($"[Spell] Curee KILL : heal {healAmount} (50% manquants) sur P{caster->PlayerIndex} HP {hpBeforeCuree} -> {caster->HP}");
                        }
                        // BonusPANextTurn applique au prochain TurnStart du caster (TurnSystem).
                        caster->BonusPANextTurn += SpellRegistry.CureeBonusPANextTurn;
                        Log.Info($"[Spell] Curee KILL : +{SpellRegistry.CureeBonusPANextTurn} PA next turn sur P{caster->PlayerIndex} (total BonusPANextTurn={caster->BonusPANextTurn})");
                    }
                    else
                    {
                        // Cible n'est pas morte : self-damage 60 HP au caster.
                        int hpBeforeCureeMiss = caster->HP;
                        caster->HP -= SpellRegistry.CureeMissSelfDamage;
                        if (caster->HP < 0) caster->HP = 0;
                        Log.Info($"[Spell] Curee MISS : -{SpellRegistry.CureeMissSelfDamage} HP self sur P{caster->PlayerIndex} HP {hpBeforeCureeMiss} -> {caster->HP}");
                    }
                    break;
                }

                case SpellId.SoulrenderAmeLaceree:
                {
                    // 2.11 SIGNATURE. Damage 320 deja applique par le pipeline generique.
                    // Effets specifiques :
                    //   1. Heal caster = 50% des dgts qui ont passe (lastHitHPLoss, post-shield).
                    //   2. Si KILL : Sang Coagule en croix 5 cases sur la cible tuee (centre + 4 cardinales).
                    //   3. Set LastAmeLaceeUsedOnTurn pour cooldown 4 tours.

                    int healAmt = lastHitHPLoss * SpellRegistry.AmeLaceeHealPercentOfPassed / 100;
                    if (healAmt > 0)
                    {
                        if (StatusHelper.Has(caster, StatusKind.AntiHealShield))
                        {
                            Log.Info($"[Spell] Ame Laceree : heal {healAmt} BLOQUE par AntiHealShield sur P{caster->PlayerIndex}");
                        }
                        else
                        {
                            int hpBeforeAL = caster->HP;
                            caster->HP += healAmt;
                            if (caster->HP > caster->MaxHP) caster->HP = caster->MaxHP;
                            Log.Info($"[Spell] Ame Laceree : heal {healAmt} ({SpellRegistry.AmeLaceeHealPercentOfPassed}% des {lastHitHPLoss} dgts passes) sur P{caster->PlayerIndex} HP {hpBeforeAL} -> {caster->HP}");
                        }
                    }

                    if (wasKill)
                    {
                        // Croix 5 sur la cible tuee : centre + 4 cardinales.
                        GridHelpers.SetTerrain(f, killedTargetX,     killedTargetY,     TerrainKind.SangCoagule, SpellRegistry.SangCoaguleTurns, currentTurn);
                        GridHelpers.SetTerrain(f, killedTargetX + 1, killedTargetY,     TerrainKind.SangCoagule, SpellRegistry.SangCoaguleTurns, currentTurn);
                        GridHelpers.SetTerrain(f, killedTargetX - 1, killedTargetY,     TerrainKind.SangCoagule, SpellRegistry.SangCoaguleTurns, currentTurn);
                        GridHelpers.SetTerrain(f, killedTargetX,     killedTargetY + 1, TerrainKind.SangCoagule, SpellRegistry.SangCoaguleTurns, currentTurn);
                        GridHelpers.SetTerrain(f, killedTargetX,     killedTargetY - 1, TerrainKind.SangCoagule, SpellRegistry.SangCoaguleTurns, currentTurn);
                        Log.Info($"[Spell] Ame Laceree KILL : Sang Coagule croix 5 pose sur ({killedTargetX},{killedTargetY}) pour {SpellRegistry.SangCoaguleTurns} tours");
                    }

                    // Cooldown 4 tours.
                    caster->LastAmeLaceeUsedOnTurn = currentTurn;
                    Log.Info($"[Spell] Ame Laceree : cooldown {SpellRegistry.AmeLaceeCooldownTurns} tours depuis tour {currentTurn} sur P{caster->PlayerIndex}");
                    break;
                }

                case SpellId.SoulrenderCauterisation:
                {
                    // Retire tous DoT (BleedDoT et futurs Necram). Pour 2.10.c : aucun DoT actuel,
                    // donc on heal min 60 (toujours applique meme si 0 DoT retire).
                    int dotsRemoved = 0;
                    if (StatusHelper.Has(caster, StatusKind.BleedDoT))
                    {
                        StatusHelper.Consume(caster, StatusKind.BleedDoT);
                        dotsRemoved++;
                    }
                    // Futurs DoT (Necram poison etc) seront ajoutes ici en Phase 3.

                    int healAmount;
                    if (dotsRemoved == 0)
                    {
                        healAmount = SpellRegistry.CauterisationHealMin;
                    }
                    else
                    {
                        healAmount = dotsRemoved * SpellRegistry.CauterisationHealPerDoT;
                        if (healAmount < SpellRegistry.CauterisationHealMin) healAmount = SpellRegistry.CauterisationHealMin;
                        if (healAmount > SpellRegistry.CauterisationHealMax) healAmount = SpellRegistry.CauterisationHealMax;
                    }

                    if (StatusHelper.Has(caster, StatusKind.AntiHealShield))
                    {
                        Log.Info($"[Spell] Cauterisation : retire {dotsRemoved} DoT, heal {healAmount} BLOQUE par AntiHealShield sur P{caster->PlayerIndex}");
                    }
                    else
                    {
                        int hpBeforeCauter = caster->HP;
                        caster->HP += healAmount;
                        if (caster->HP > caster->MaxHP) caster->HP = caster->MaxHP;
                        Log.Info($"[Spell] Cauterisation : retire {dotsRemoved} DoT, heal {healAmount} sur P{caster->PlayerIndex} HP {hpBeforeCauter} -> {caster->HP}");
                    }
                    break;
                }

                // -------------------------------------------------------------
                // NIGHTSEER 2.15.b — TACTIQUES
                // -------------------------------------------------------------

                case SpellId.NightseerMarqueDuChasseur:
                {
                    // Pose Traque sur la cible 3 tours.
                    EntityRef target = GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY);
                    if (target != EntityRef.None
                        && f.Unsafe.TryGetPointer<Combatant>(target, out Combatant* targetC)
                        && targetC->PlayerIndex != caster->PlayerIndex)
                    {
                        MarkHelpers.ApplyMark(targetC, MarkKind.Traque,
                            SpellRegistry.MarqueDuChasseurTurns, caster->PlayerIndex, currentTurn);
                        Log.Info($"[Spell] Marque du Chasseur : Traque {SpellRegistry.MarqueDuChasseurTurns} tours sur P{targetC->PlayerIndex}");
                    }
                    break;
                }

                case SpellId.NightseerFiletDeRonces:
                {
                    // Pose un Filet (Trap) + Voile sur la case ciblee. Trigger sur entree ennemie
                    // gere par MovementSystem via FogHelpers.TryTriggerTrapOnEnter.
                    FogHelpers.PlaceTrap(f, cmd.TargetX, cmd.TargetY,
                        TrapKind.FiletRonces, caster->PlayerIndex, currentTurn);
                    // Le Filet est "embuscade Voilée" cote Bible : invisible pour l'adversaire.
                    // Duree du voile : large pour rester invisible jusqu'au declenchement (10 rounds = practical "permanent").
                    FogHelpers.ApplyVeil(f, cmd.TargetX, cmd.TargetY, caster->PlayerIndex, 10, currentTurn);
                    Log.Info($"[Spell] Filet de Ronces pose sur ({cmd.TargetX},{cmd.TargetY}) par P{caster->PlayerIndex} (Voile + Trap)");
                    break;
                }

                case SpellId.NightseerChampDeMines:
                {
                    // Bible V7.1 — voile la zone 3x3 entiere (9 cases) ET pose 3 mines dedans.
                    // Adversaire voit 9 cases sombres uniformes (1/3 chance par case = paranoia).
                    // Ordre de pose des 3 mines : centre + 2 cardinaux disponibles (placement
                    // deterministe centre sur la case visee).
                    int cx = cmd.TargetX, cy = cmd.TargetY;
                    int* candX = stackalloc int[9];
                    int* candY = stackalloc int[9];
                    candX[0] = cx;     candY[0] = cy;
                    candX[1] = cx;     candY[1] = cy - 1;
                    candX[2] = cx + 1; candY[2] = cy;
                    candX[3] = cx;     candY[3] = cy + 1;
                    candX[4] = cx - 1; candY[4] = cy;
                    candX[5] = cx + 1; candY[5] = cy - 1;
                    candX[6] = cx + 1; candY[6] = cy + 1;
                    candX[7] = cx - 1; candY[7] = cy + 1;
                    candX[8] = cx - 1; candY[8] = cy - 1;

                    // 1) Voiler les 9 cases (toutes celles in-bounds, y compris case du caster).
                    for (int i = 0; i < 9; i++)
                    {
                        int vx = candX[i], vy = candY[i];
                        if (!GridHelpers.InBounds(vx, vy)) continue;
                        FogHelpers.ApplyVeil(f, vx, vy, caster->PlayerIndex, 10, currentTurn);
                    }

                    // 2) Poser 3 mines (centre prioritaire, skip case caster).
                    int placed = 0;
                    for (int i = 0; i < 9 && placed < 3; i++)
                    {
                        int mx = candX[i], my = candY[i];
                        if (!GridHelpers.InBounds(mx, my)) continue;
                        if (mx == caster->GridX && my == caster->GridY) continue;
                        FogHelpers.PlaceTrap(f, mx, my, TrapKind.Mine, caster->PlayerIndex, currentTurn);
                        placed++;
                    }
                    Log.Info($"[Spell] Champ de Mines : zone 3x3 voilee + {placed} mines par P{caster->PlayerIndex} (centre {cx},{cy})");
                    break;
                }

                case SpellId.NightseerBourrasque:
                {
                    // Push la cible 3 cases (5 avec 1 PR) loin du caster.
                    int pushDist = hgSpend >= 1
                        ? SpellRegistry.BourrasquePushBonus1PR
                        : SpellRegistry.BourrasquePushBase;
                    EntityRef target = GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY);
                    if (target != EntityRef.None
                        && f.Unsafe.TryGetPointer<Combatant>(target, out Combatant* targetC))
                    {
                        PushAndTrigger(f, targetC, target, caster->GridX, caster->GridY, pushDist, currentTurn, caster);
                    }
                    break;
                }

                case SpellId.NightseerSouffleGlacial:
                {
                    // AoE croix 3 autour caster (gere par effectBuffer = 5 cases via CrossSmall).
                    // Damage 70 deja applique par damage loop. Ici on push +1 case + MovementMalus -1.
                    int casterX = caster->GridX;
                    int casterY = caster->GridY;
                    for (int i = 0; i < effectCount; i++)
                    {
                        int idx = effectBuffer[i];
                        int gx = idx % GridConstants.Width;
                        int gy = idx / GridConstants.Width;
                        if (gx == casterX && gy == casterY) continue; // skip caster
                        EntityRef target = GridHelpers.GetOccupant(f, gx, gy);
                        if (target == EntityRef.None) continue;
                        if (target == casterEntity) continue;
                        if (!f.Unsafe.TryGetPointer<Combatant>(target, out Combatant* targetC)) continue;

                        // Push 1 case loin du caster (depuis position courante).
                        PushAndTrigger(f, targetC, target, casterX, casterY,
                            SpellRegistry.SouffleGlacialPushDistance, currentTurn, caster);
                        // -1 PM (1 tour).
                        StatusHelper.Apply(targetC, StatusKind.MovementMalus,
                            magnitude: SpellRegistry.SouffleGlacialPMReduce, turnsLeft: 1, currentTurn);
                        Log.Info($"[Spell] Souffle Glacial : push + MovementMalus -{SpellRegistry.SouffleGlacialPMReduce} sur P{targetC->PlayerIndex}");
                    }
                    break;
                }

                // ====================================================================
                // 2.15.c — NIGHTSEER SURVIE
                // ====================================================================

                case SpellId.NightseerVoileDOmbre:
                {
                    // Untargetable 1 round actif (skip-decrement -> reste actif tout le round courant
                    // ET le round suivant cote owner, puis expire). Bible : "1 tour" = 1 round.
                    StatusHelper.Apply(caster, StatusKind.Untargetable,
                        magnitude: 0,
                        turnsLeft: SpellRegistry.VoileDOmbreTurns,
                        currentTurn);
                    Log.Info($"[Spell] Voile d'Ombre : P{caster->PlayerIndex} devient Untargetable");
                    break;
                }

                case SpellId.NightseerPasFurtif:
                {
                    // Teleport sur (cmd.TargetX, cmd.TargetY). Filter EmptyTile a deja valide
                    // que la case est vide + walkable.
                    int oldX = caster->GridX, oldY = caster->GridY;
                    GridHelpers.SetOccupant(f, oldX, oldY, EntityRef.None);
                    caster->GridX = cmd.TargetX;
                    caster->GridY = cmd.TargetY;
                    GridHelpers.SetOccupant(f, cmd.TargetX, cmd.TargetY, casterEntity);
                    Log.Info($"[Spell] Pas Furtif : P{caster->PlayerIndex} ({oldX},{oldY}) -> ({cmd.TargetX},{cmd.TargetY})");

                    // 1 PR optionnel -> Voile 2 tours sur la case d'arrivee.
                    if (hgSpend >= 1)
                    {
                        FogHelpers.ApplyVeil(f, cmd.TargetX, cmd.TargetY,
                            caster->PlayerIndex, SpellRegistry.PasFurtifVeilTurns, currentTurn);
                        Log.Info($"[Spell] Pas Furtif : Voile 2 tours pose sur ({cmd.TargetX},{cmd.TargetY}) (1 PR)");
                    }
                    break;
                }

                case SpellId.NightseerCamouflageRonces:
                {
                    // Shield 130 HP / 2 rounds + RoncesAura 70 dgts ennemis adjacents fin de round.
                    StatusHelper.Apply(caster, StatusKind.ShieldActive,
                        magnitude: SpellRegistry.CamouflageRoncesShieldHP,
                        turnsLeft: SpellRegistry.CamouflageRoncesShieldTurns,
                        currentTurn);
                    StatusHelper.Apply(caster, StatusKind.RoncesAura,
                        magnitude: SpellRegistry.CamouflageRoncesAuraDmg,
                        turnsLeft: SpellRegistry.CamouflageRoncesAuraTurns,
                        currentTurn);
                    Log.Info($"[Spell] Camouflage Ronces : Shield {SpellRegistry.CamouflageRoncesShieldHP} + RoncesAura {SpellRegistry.CamouflageRoncesAuraDmg} sur P{caster->PlayerIndex}");
                    break;
                }

                case SpellId.NightseerSeveSauvage:
                {
                    int heal = SpellRegistry.SeveSauvageHealBase;

                    // Bonus +60 si trap de ce caster declenche dans les 2 derniers rounds
                    // (ce round ou le precedent : currentTurn - LastTrapTriggeredOnTurn <= 1).
                    if (caster->LastTrapTriggeredOnTurn >= currentTurn - 1)
                    {
                        heal += SpellRegistry.SeveSauvageHealBonusTrap;
                    }

                    // Bonus +30 si au moins 1 voile actif sur la map appartenant au caster.
                    bool hasOwnVeil = false;
                    if (f.TryGetSingleton<FogSingleton>(out _))
                    {
                        var fogPtr = f.Unsafe.GetPointerSingleton<FogSingleton>();
                        for (int i = 0; i < GridConstants.Count; i++)
                        {
                            var t = fogPtr->Tiles[i];
                            if (t.VeiledTurnsLeft > 0
                                && t.VeiledByPlayer == (byte)(caster->PlayerIndex + 1))
                            {
                                hasOwnVeil = true;
                                break;
                            }
                        }
                    }
                    if (hasOwnVeil)
                    {
                        heal += SpellRegistry.SeveSauvageHealBonusVeil;
                    }

                    int hpBeforeSeve = caster->HP;
                    caster->HP += heal;
                    if (caster->HP > caster->MaxHP) caster->HP = caster->MaxHP;
                    Log.Info($"[Spell] Seve Sauvage : +{heal} HP sur P{caster->PlayerIndex} ({hpBeforeSeve} -> {caster->HP}) [base {SpellRegistry.SeveSauvageHealBase} +trap{(caster->LastTrapTriggeredOnTurn >= currentTurn - 1 ? SpellRegistry.SeveSauvageHealBonusTrap : 0)} +veil{(hasOwnVeil ? SpellRegistry.SeveSauvageHealBonusVeil : 0)}]");
                    break;
                }

                case SpellId.NightseerEvanescence:
                {
                    // Teleport sur destination + heal 150 + Voile 2 tours sur case quittee.
                    // OncePerMatch deja consomme dans le pipeline central (avant ApplySpellSpecific).
                    int oldX = caster->GridX, oldY = caster->GridY;
                    GridHelpers.SetOccupant(f, oldX, oldY, EntityRef.None);
                    caster->GridX = cmd.TargetX;
                    caster->GridY = cmd.TargetY;
                    GridHelpers.SetOccupant(f, cmd.TargetX, cmd.TargetY, casterEntity);

                    int hpBeforeEvan = caster->HP;
                    caster->HP += SpellRegistry.EvanescenceHeal;
                    if (caster->HP > caster->MaxHP) caster->HP = caster->MaxHP;

                    FogHelpers.ApplyVeil(f, oldX, oldY,
                        caster->PlayerIndex, SpellRegistry.EvanescenceVeilTurns, currentTurn);

                    Log.Info($"[Spell] Evanescence : P{caster->PlayerIndex} ({oldX},{oldY}) -> ({cmd.TargetX},{cmd.TargetY}) heal {hpBeforeEvan} -> {caster->HP} + Voile sur ({oldX},{oldY})");
                    break;
                }

                // ====================================================================
                // 2.16 — NIGHTSEER SIGNATURE TRAQUENARD
                // ====================================================================

                case SpellId.NightseerTraquenard:
                {
                    // Capture le bonus AVANT consume marque (la lecture est faite ici, le bonus dgts
                    // a deja ete applique dans le pipeline damage calc en amont).
                    bool hasBonus = TraquenardHasMarkOrOwnVeil(f, cmd.TargetX, cmd.TargetY, caster->PlayerIndex);

                    // Teleport caster sur case adjacente cible cote caster (axe principal Manhattan).
                    // La pre-validation TryCastSpell garantit qu'au moins 1 case est libre.
                    if (TryFindTraquenardLandingCell(f, caster, cmd.TargetX, cmd.TargetY, out int landX, out int landY))
                    {
                        int oldX = caster->GridX, oldY = caster->GridY;
                        GridHelpers.SetOccupant(f, oldX, oldY, EntityRef.None);
                        caster->GridX = landX;
                        caster->GridY = landY;
                        GridHelpers.SetOccupant(f, landX, landY, casterEntity);
                        Log.Info($"[Spell] Traquenard : P{caster->PlayerIndex} teleport ({oldX},{oldY}) -> ({landX},{landY}) adjacent cible ({cmd.TargetX},{cmd.TargetY})");
                    }

                    // Apply Paralysie (-3 PM, -2 PA prochain tour) + consume marque + +2 PR si bonus.
                    EntityRef trqTarget = GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY);
                    if (trqTarget != EntityRef.None
                        && f.Unsafe.TryGetPointer<Combatant>(trqTarget, out Combatant* trqC))
                    {
                        StatusHelper.Apply(trqC, StatusKind.MovementMalus,
                            magnitude: SpellRegistry.TraquenardParalysiePMMalus,
                            turnsLeft: SpellRegistry.TraquenardParalysieTurns,
                            currentTurn);
                        StatusHelper.Apply(trqC, StatusKind.ActionMalus,
                            magnitude: SpellRegistry.TraquenardParalysieAPMalus,
                            turnsLeft: SpellRegistry.TraquenardParalysieTurns,
                            currentTurn);
                        Log.Info($"[Spell] Traquenard : Paralysie -{SpellRegistry.TraquenardParalysiePMMalus} PM / -{SpellRegistry.TraquenardParalysieAPMalus} PA sur P{trqC->PlayerIndex} (1 tour)");

                        if (hasBonus)
                        {
                            MarkHelpers.ConsumeMark(trqC);
                            FogHelpers.ClearVeil(f, cmd.TargetX, cmd.TargetY);
                            int trqMaxRes = CombatantStats.GetMaxResource(caster->Class);
                            int trqBeforeRes = caster->Resource;
                            int trqGain = SpellRegistry.TraquenardPRGainOnConsumeMark;
                            caster->Resource = trqBeforeRes + trqGain > trqMaxRes
                                ? trqMaxRes
                                : trqBeforeRes + trqGain;
                            Log.Info($"[Spell] Traquenard : marque/voile consommee + +{trqGain} PR sur P{caster->PlayerIndex} : {trqBeforeRes} -> {caster->Resource}");
                        }
                    }

                    // Set cooldown.
                    caster->LastTraquenardUsedOnTurn = currentTurn;
                    Log.Info($"[Spell] Traquenard cooldown {SpellRegistry.TraquenardCooldownTurns} tours actif (utilisable a partir du tour {currentTurn + SpellRegistry.TraquenardCooldownTurns})");
                    break;
                }

                // -------------------------------------------------------------
                // COLOSSAR — 3.3.a.i
                // -------------------------------------------------------------

                case SpellId.ColossarRepresailles:
                    // 3.3.a.i — Bible : 100 dgts immediat (deja inflige par damage loop standard,
                    // + bonus adjacence Densite Inerte si applicable). Apres : applique RipostMelee
                    // 80 dgts pendant 2 tours sur le CASTER (reflect sur attaques melee subies).
                    // Bible cap 4 retours non implemente (TODO 3.3.a.iii).
                    StatusHelper.Apply(caster, StatusKind.RipostMelee,
                        magnitude: SpellRegistry.RepresaillesReflectDmg,
                        turnsLeft: SpellRegistry.RepresaillesReflectTurns,
                        currentTurn);
                    Log.Info($"[Spell] Represailles : RipostMelee {SpellRegistry.RepresaillesReflectDmg} dgts ({SpellRegistry.RepresaillesReflectTurns} tours) sur P{caster->PlayerIndex}");
                    break;
            }
        }

        /// <summary>
        /// 2.16 — True si la cible Traquenard a Traque/Empreinte OU si la case visee a un
        /// voile owned par le caster. Sert au bonus +80 dgts + gain +2 PR.
        /// </summary>
        private static bool TraquenardHasMarkOrOwnVeil(Frame f, int targetX, int targetY, int casterPlayerIndex)
        {
            EntityRef occ = GridHelpers.GetOccupant(f, targetX, targetY);
            if (occ != EntityRef.None && f.Unsafe.TryGetPointer<Combatant>(occ, out Combatant* targetC))
            {
                if (MarkHelpers.HasMark(targetC, MarkKind.Traque)) return true;
                if (MarkHelpers.HasMark(targetC, MarkKind.Empreinte)) return true;
            }
            if (FogHelpers.GetVeilOwner(f, targetX, targetY) == casterPlayerIndex) return true;
            return false;
        }

        /// <summary>
        /// 2.16 — Cherche une case adjacente (Manhattan 1) a la cible, libre + walkable.
        /// Priorite : cote du caster sur l'axe principal Manhattan (caster -> target).
        /// Fallback : autres cardinaux dans l'ordre. Retourne false si toutes bloquees.
        /// </summary>
        private static bool TryFindTraquenardLandingCell(Frame f, Combatant* caster,
            int targetX, int targetY, out int landX, out int landY)
        {
            int dx = caster->GridX - targetX;
            int dy = caster->GridY - targetY;
            int absDx = dx < 0 ? -dx : dx;
            int absDy = dy < 0 ? -dy : dy;

            int* candX = stackalloc int[4];
            int* candY = stackalloc int[4];

            if (absDx >= absDy)
            {
                candX[0] = targetX + (dx > 0 ? 1 : -1); candY[0] = targetY;
                candX[1] = targetX + (dx > 0 ? -1 : 1); candY[1] = targetY;
                candX[2] = targetX; candY[2] = targetY + 1;
                candX[3] = targetX; candY[3] = targetY - 1;
            }
            else
            {
                candX[0] = targetX; candY[0] = targetY + (dy > 0 ? 1 : -1);
                candX[1] = targetX; candY[1] = targetY + (dy > 0 ? -1 : 1);
                candX[2] = targetX + 1; candY[2] = targetY;
                candX[3] = targetX - 1; candY[3] = targetY;
            }

            for (int i = 0; i < 4; i++)
            {
                int cx = candX[i], cy = candY[i];
                if (!GridHelpers.InBounds(cx, cy)) continue;
                if (!GridHelpers.IsWalkable(f, cx, cy)) continue;
                if (GridHelpers.GetOccupant(f, cx, cy) != EntityRef.None) continue;
                landX = cx;
                landY = cy;
                return true;
            }

            landX = 0;
            landY = 0;
            return false;
        }

        /// <summary>
        /// 2.15.b — Push un combattant N cases loin du caster (axe principal). Stoppe a la
        /// 1ere case bloquante (mur, occupant, OBSTACLE depuis 3.2). Si la case finale a un Trap :
        /// declenchement via FogHelpers.TryTriggerTrapOnEnter.
        ///
        /// 3.2 — Si caster est Colossar ET le push s'arrete contre un obstacle ou bord de map :
        /// +1 FD au caster (Bible V7.1 Fondation : "+1 FD chaque fois qu'un ennemi est PUSH/PULL
        /// contre un mur, un pilier, ou un bord de map").
        ///
        /// Direction calculee depuis (casterX, casterY) -> (target.GridX, target.GridY) :
        /// signe sur l'axe dominant (Manhattan). Si delta == (0,0) : no-op.
        /// </summary>
        private static void PushAndTrigger(Frame f, Combatant* targetC, EntityRef targetEntity,
            int casterX, int casterY, int distance, int currentTurn, Combatant* caster = null)
        {
            int dx = targetC->GridX - casterX;
            int dy = targetC->GridY - casterY;
            int absDx = dx < 0 ? -dx : dx;
            int absDy = dy < 0 ? -dy : dy;
            int stepX = 0, stepY = 0;
            if (absDx >= absDy)
            {
                stepX = dx > 0 ? 1 : (dx < 0 ? -1 : 0);
            }
            else
            {
                stepY = dy > 0 ? 1 : (dy < 0 ? -1 : 0);
            }
            if (stepX == 0 && stepY == 0) return; // caster sur target case (rare)

            int curX = targetC->GridX;
            int curY = targetC->GridY;
            int steps = 0;
            // 3.2 — track le motif d'arret pour la regle Fondation Colossar.
            //   stoppedAgainstBorder : push s'arrete car case suivante hors grille.
            //   stoppedAgainstObstacle : push s'arrete car case suivante a un obstacle (Pilier/Mur).
            //   stoppedAgainstOccupant : push s'arrete contre un autre combatant (pas Bible Colossar gain).
            bool stoppedAgainstBorder = false;
            bool stoppedAgainstObstacle = false;
            for (int s = 0; s < distance; s++)
            {
                int nx = curX + stepX;
                int ny = curY + stepY;
                if (!GridHelpers.InBounds(nx, ny)) { stoppedAgainstBorder = true; break; }
                if (!GridHelpers.IsWalkable(f, nx, ny)) break;
                if (GridHelpers.GetOccupant(f, nx, ny) != EntityRef.None) break;
                if (ObstacleHelpers.HasObstacleAt(f, nx, ny)) { stoppedAgainstObstacle = true; break; }
                curX = nx;
                curY = ny;
                steps++;
            }

            // 3.2 — Bible Fondation : +1 FD au Colossar si push contre obstacle ou bord, MEME si
            // steps == 0 (la cible etait deja collee a un mur/Pilier). C'est un push qui "ecrase"
            // contre la barriere, c'est ce qui compte.
            if (caster != null && (stoppedAgainstObstacle || stoppedAgainstBorder))
            {
                string reason = stoppedAgainstObstacle ? "Push contre obstacle" : "Push contre bord";
                ColossarPassif.GainFondation(caster, reason);
            }

            if (steps == 0) return;

            // Update grid + combatant pos.
            GridHelpers.SetOccupant(f, targetC->GridX, targetC->GridY, EntityRef.None);
            targetC->GridX = curX;
            targetC->GridY = curY;
            GridHelpers.SetOccupant(f, curX, curY, targetEntity);
            Log.Info($"[Spell] Push : P{targetC->PlayerIndex} pousse de {steps} case(s) -> ({curX},{curY})");

            // Trigger trap eventuel sur la case d'arrivee.
            FogHelpers.TryTriggerTrapOnEnter(f, targetEntity, targetC, curX, curY, currentTurn);
        }

        /// <summary>
        /// Resoud un cercle Manhattan (rayon N) autour du centre. Utilise par Rugissement (rayon 3).
        /// </summary>
        private static void ResolveCircleManhattan(int centerX, int centerY, int radius, int* outBuffer, out int count)
        {
            count = 0;
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int absDx = dx < 0 ? -dx : dx;
                    int absDy = dy < 0 ? -dy : dy;
                    if (absDx + absDy > radius) continue;
                    int x = centerX + dx;
                    int y = centerY + dy;
                    if (!GridHelpers.InBounds(x, y)) continue;
                    outBuffer[count++] = GridHelpers.Index(x, y);
                }
            }
        }

        /// <summary>
        /// Empoignade (2.10.b) : pull target sur la case adjacente au caster sur la ligne
        /// caster -> target. Si target est deja adjacent : no-op (return false). Si la case
        /// "naturelle" est occupee/non-walkable : fallback sur les 4 cases cardinales du caster.
        /// Retourne true si le pull a deplace la cible, false sinon.
        /// </summary>
        private static bool PullTargetAdjacent(Frame f, Combatant* caster, EntityRef targetEntity, Combatant* targetC)
        {
            int px = caster->GridX;
            int py = caster->GridY;
            int tx = targetC->GridX;
            int ty = targetC->GridY;

            int dx = tx - px;
            int dy = ty - py;
            int absDx = dx < 0 ? -dx : dx;
            int absDy = dy < 0 ? -dy : dy;

            // Deja adjacent (distance Manhattan == 1) : no-op.
            if (absDx + absDy == 1) return false;

            // Calcule la case sur la ligne caster -> target la plus proche du caster (axe dominant).
            int newX, newY;
            if (absDx >= absDy)
            {
                newX = px + (dx > 0 ? 1 : -1);
                newY = py;
            }
            else
            {
                newX = px;
                newY = py + (dy > 0 ? 1 : -1);
            }

            // Si case "naturelle" pas dispo (hors grille / non walkable / occupee) : fallback
            // sur les 4 cases cardinales du caster.
            if (!IsCellFreeForPull(f, newX, newY))
            {
                bool found = false;
                for (int dir = 0; dir < 4; dir++)
                {
                    int trialX = px;
                    int trialY = py;
                    switch (dir)
                    {
                        case 0: trialX = px + 1; break;
                        case 1: trialX = px - 1; break;
                        case 2: trialY = py + 1; break;
                        case 3: trialY = py - 1; break;
                    }
                    if (IsCellFreeForPull(f, trialX, trialY))
                    {
                        newX = trialX;
                        newY = trialY;
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }

            // Apply move.
            GridHelpers.SetOccupant(f, tx, ty, EntityRef.None);
            targetC->GridX = newX;
            targetC->GridY = newY;
            GridHelpers.SetOccupant(f, newX, newY, targetEntity);
            return true;
        }

        private static bool IsCellFreeForPull(Frame f, int x, int y)
        {
            if (!GridHelpers.InBounds(x, y)) return false;
            if (!GridHelpers.IsWalkable(f, x, y)) return false;
            if (GridHelpers.GetOccupant(f, x, y) != EntityRef.None) return false;
            return true;
        }
    }
}
