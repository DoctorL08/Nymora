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

            // 3.3.b.iii — Provocation Bible : sorts non-ciblant le provocateur coutent +2 PA.
            // Magnitude Provoked = PlayerIndex du provocateur. Lookup combatant correspondant, si la
            // case ciblee != case du provocateur -> bump cost. Note : un sort Self du provoque (Pacte,
            // Peau de Fer, etc.) compte aussi comme "non-ciblant" donc bump.
            if (StatusHelper.Has(caster, StatusKind.Provoked))
            {
                int provocateurPi = StatusHelper.GetMagnitude(caster, StatusKind.Provoked, -1);
                if (provocateurPi >= 0)
                {
                    bool targetIsProvocateur = false;
                    var provLookup = f.Filter<Combatant>();
                    while (provLookup.NextUnsafe(out EntityRef _, out Combatant* provLookupC))
                    {
                        if (provLookupC->PlayerIndex == provocateurPi)
                        {
                            if (provLookupC->GridX == cmd.TargetX && provLookupC->GridY == cmd.TargetY)
                            {
                                targetIsProvocateur = true;
                            }
                            break;
                        }
                    }
                    if (!targetIsProvocateur)
                    {
                        int costBefore = effectivePACost;
                        effectivePACost += SpellRegistry.ProvocationCostBumpNonCible;
                        Log.Info($"[Provocation] +{SpellRegistry.ProvocationCostBumpNonCible} PA cost (target ({cmd.TargetX},{cmd.TargetY}) != provocateur P{provocateurPi}) : {costBefore} -> {effectivePACost}");
                    }
                }
            }

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
            // 3.3.d : Effondrement cooldown 4 tours apres usage. Re-castable si FD remonte a 3 ET cooldown expire.
            if (cmd.Spell == SpellId.ColossarEffondrement)
            {
                int turnsSinceUse = currentTurnForCooldown - caster->LastEffondrementUsedOnTurn;
                if (turnsSinceUse < SpellRegistry.EffondrementCooldownTurns)
                {
                    Log.Warn($"[Spell] rejet : Effondrement en cooldown ({turnsSinceUse}/{SpellRegistry.EffondrementCooldownTurns} tours depuis dernier usage tour {caster->LastEffondrementUsedOnTurn})");
                    return;
                }
                // Garde-fou : refuse re-annonce tant qu'une annonce precedente n'a pas trigger.
                if (caster->EffondrementAnnouncedOnTurn >= 0)
                {
                    Log.Warn($"[Spell] rejet : Effondrement deja annonce au tour {caster->EffondrementAnnouncedOnTurn} (en attente de trigger)");
                    return;
                }
                // 3.3.d design Lorenzo : refuse le cast si aucun ennemi vivant dans le rayon 2.
                // Le sort coute 4 PA + 3 FD = pas gachis sur cast a vide. Force un setup melee
                // (Empoignade pull, ou ennemi proche naturellement).
                {
                    int cxCk = caster->GridX;
                    int cyCk = caster->GridY;
                    int rCk = SpellRegistry.EffondrementAoeRadius;
                    bool hasEnemyInZone = false;
                    var ckFilter = f.Filter<Combatant>();
                    while (ckFilter.NextUnsafe(out EntityRef _, out Combatant* eC))
                    {
                        if (eC->PlayerIndex == caster->PlayerIndex) continue;
                        if (eC->HP <= 0) continue;
                        int dxCk = eC->GridX - cxCk;
                        int dyCk = eC->GridY - cyCk;
                        int adxCk = dxCk < 0 ? -dxCk : dxCk;
                        int adyCk = dyCk < 0 ? -dyCk : dyCk;
                        int distCk = adxCk + adyCk;
                        if (distCk > 0 && distCk <= rCk) { hasEnemyInZone = true; break; }
                    }
                    if (!hasEnemyInZone)
                    {
                        Log.Warn($"[Spell] rejet : Effondrement requiert un ennemi vivant dans le rayon {rCk} (Manhattan). Approche l'adversaire (Empoignade ?).");
                        return;
                    }
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

            // 2.15.a Detonation Onirique : option 2 PR -> portee passe de 5 a 10 (Bible V7.1).
            // L'override de RangeMax est dynamique car HGCostMaxOptional cree juste la possibilite ;
            // c'est ici qu'on materialise le bonus quand le joueur choisit effectivement 2 PR.
            int effectiveRangeMax = spellDef.RangeMax;
            if (cmd.Spell == SpellId.NightseerDetonationOnirique
                && hgSpend >= SpellRegistry.DetonationOniriquePROptionCost)
            {
                effectiveRangeMax = SpellRegistry.DetonationOniriqueRangeMaxBoosted;
            }

            if (dist < spellDef.RangeMin || dist > effectiveRangeMax)
            {
                Log.Warn($"[Spell] rejet : distance {dist} hors range [{spellDef.RangeMin},{effectiveRangeMax}]");
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

            // 3.3.b.i — Line of Sight check : Bible V7.1 "Pilier/Mur bloque lignes de vue/tir".
            // Pour les sorts directs a distance (range >= 2), on verifie qu'aucun obstacle non-OWN
            // ne se trouve sur la ligne caster -> case ciblee. Les obstacles du caster lui-meme
            // ne bloquent PAS sa LoS (sinon Colossar empeche ses propres sorts entre ses Murs).
            // Sorts en LIGNE custom (Charge Brutale, Volee d'Epines, Choc Sismique) gerent leur
            // propre arret obstacle dans leur handler -> exclus de ce check generique.
            if (SpellNeedsLineOfSight(cmd.Spell))
            {
                if (!ObstacleHelpers.HasLineOfSight(f,
                    caster->GridX, caster->GridY, cmd.TargetX, cmd.TargetY,
                    caster->PlayerIndex))
                {
                    Log.Warn($"[Spell] rejet : ligne de vue bloquee par obstacle entre ({caster->GridX},{caster->GridY}) et ({cmd.TargetX},{cmd.TargetY})");
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

            // 3.5.a.iii — Brume Toxique : pre-validation chevauchement. Refuse le cast si AU
            // MOINS UNE des 9 cases du AoE 3x3 cible chevauche une Brume existante. PA NON
            // consomme (decision design Lorenzo 2026-05-15 : pas de stack/refresh).
            if (cmd.Spell == SpellId.NecramBrumeToxique)
            {
                bool brumeOverlap = false;
                for (int bdx = -1; bdx <= 1 && !brumeOverlap; bdx++)
                {
                    for (int bdy = -1; bdy <= 1 && !brumeOverlap; bdy++)
                    {
                        int btx = cmd.TargetX + bdx;
                        int bty = cmd.TargetY + bdy;
                        if (!GridHelpers.InBounds(btx, bty)) continue;
                        if (GridHelpers.GetTerrainKind(f, btx, bty) == TerrainKind.BrumeToxique)
                        {
                            brumeOverlap = true;
                        }
                    }
                }
                if (brumeOverlap)
                {
                    Log.Warn($"[Spell] rejet : Brume Toxique chevauche une Brume existante sur AoE 3x3 centree ({cmd.TargetX},{cmd.TargetY}). PA non consomme.");
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
            else if (cmd.Spell == SpellId.ColossarOndeDeChoc)
            {
                // 3.3.a.ii — Onde de Choc : AoE rayon 1 autour caster (4 cases adj).
                ResolveCircleManhattan(caster->GridX, caster->GridY, radius: 1, effectBuffer, out effectCount);
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
            // 3.5.a.ii — Faux Decharnee : somme cumulee des marques sur cibles touchees (pour heal post-loop).
            int fauxDecharneeMarksTotal = 0;

            // 3.5.a.iii — Brume Toxique : custom handler. Pose terrain BrumeToxique sur les 9
            // cases AoE 3x3 (effectBuffer). Pour chaque occupant present ET non-Necram (decision
            // design caster + classe immunisee) : 60 dmg BYPASS shield/reduction (Bible V7.1
            // "DoT ignore boucliers, reductions, sustains") + 1 marque venin. Skip damage loop
            // standard via early return : pas de HG gain, pas de reflect, pas de Densite Inerte
            // — la Brume est une zone DoT, pas une attaque directe.
            if (cmd.Spell == SpellId.NecramBrumeToxique)
            {
                for (int bi = 0; bi < effectCount; bi++)
                {
                    int bidx = effectBuffer[bi];
                    int bcx = bidx % GridConstants.Width;
                    int bcy = bidx / GridConstants.Width;

                    // Pose terrain (override SangCoagule/VapeurCarmin si present sur la case).
                    GridHelpers.SetTerrain(f, bcx, bcy, TerrainKind.BrumeToxique,
                        SpellRegistry.BrumeToxiqueTurns, currentTurn);

                    EntityRef bocc = GridHelpers.GetOccupant(f, bcx, bcy);
                    if (bocc == EntityRef.None) continue;
                    if (bocc == casterEntity) continue;
                    if (!f.Unsafe.TryGetPointer<Combatant>(bocc, out Combatant* boccC)) continue;
                    if (boccC->Class == NymoraClass.Necram) continue; // skip Necram (design)
                    if (boccC->HP <= 0) continue;

                    int hpBefore = boccC->HP;
                    boccC->HP -= SpellRegistry.BrumeToxiqueDmgImmediate;
                    if (boccC->HP < 0) boccC->HP = 0;
                    boccC->DamageTakenThisRound += SpellRegistry.BrumeToxiqueDmgImmediate;
                    Log.Info($"[Spell] Brume Toxique pose : -{SpellRegistry.BrumeToxiqueDmgImmediate} HP bypass sur P{boccC->PlayerIndex} ({bcx},{bcy}) HP {hpBefore} -> {boccC->HP}");

                    if (boccC->HP > 0)
                    {
                        VeninHelpers.ApplyMark(f, boccC, SpellRegistry.BrumeToxiqueMarksOnHit, currentTurn);
                    }
                }
                Log.Info($"[Spell] Brume Toxique posee centree ({cmd.TargetX},{cmd.TargetY}), 9 cases, {SpellRegistry.BrumeToxiqueTurns} rounds");
                return;
            }

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
                    if (target == EntityRef.None)
                    {
                        // 3.3.d — Sort AoE damage : si la case a un obstacle ADVERSE (Faille, Pilier
                        // ennemi, Mur ennemi), on lui inflige aussi le damage de base (effectiveDmg).
                        // Bible-balance : permet a l'ennemi piégé par Effondrement de casser des
                        // Failles avec ses sorts AoE pour se créer un passage.
                        if (effectiveDmg > 0)
                        {
                            EntityRef obsHere = ObstacleHelpers.GetObstacleAt(f, cx, cy);
                            if (obsHere != EntityRef.None
                                && f.Unsafe.TryGetPointer<Obstacle>(obsHere, out Obstacle* obsData)
                                && obsData->OwnerPlayerIndex != caster->PlayerIndex)
                            {
                                ObstacleHelpers.DamageAt(f, cx, cy, effectiveDmg);
                            }
                        }
                        continue;
                    }
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
                    else if (cmd.Spell == SpellId.ColossarMarteauPunisseur)
                    {
                        // 3.3.a.ii — Bible Marteau Punisseur : 160 base, 240 si target.PA < 4
                        // (a deja cast ce tour). Applique aussi TRAUMA ActionMalus 2 prochain tour.
                        if (targetC->PA < SpellRegistry.MarteauPunisseurDepletedPAThreshold)
                        {
                            dmgThisTarget = SpellRegistry.MarteauPunisseurDmgIfDepleted;
                            StatusHelper.Apply(targetC, StatusKind.ActionMalus,
                                magnitude: SpellRegistry.MarteauPunisseurTraumaPAMagnitude,
                                turnsLeft: SpellRegistry.MarteauPunisseurTraumaTurns, currentTurn);
                            Log.Info($"[Spell] Marteau Punisseur DEPLETED : {dmgThisTarget} dgts + TRAUMA -{SpellRegistry.MarteauPunisseurTraumaPAMagnitude} PA sur P{targetC->PlayerIndex} (PA={targetC->PA})");
                        }
                    }
                    else if (cmd.Spell == SpellId.NecramMorsurePutride)
                    {
                        // 3.5.a.i — Bible Morsure Putride : 110 base + 22 par marque venin
                        // sur la cible (cap bonus +90, total max 200). Si kill : transfert
                        // des marques sur l'ennemi le plus proche (geree apres apply damage).
                        int marks = targetC->VeninStacks;
                        if (marks > 0)
                        {
                            int bonusUncapped = marks * SpellRegistry.MorsurePutrideDmgPerMark;
                            int bonus = bonusUncapped > SpellRegistry.MorsurePutrideDmgBonusCap
                                ? SpellRegistry.MorsurePutrideDmgBonusCap
                                : bonusUncapped;
                            dmgThisTarget = SpellRegistry.MorsurePutrideDmgBase + bonus;
                            Log.Info($"[Spell] Morsure Putride : {SpellRegistry.MorsurePutrideDmgBase} + {bonus} dgts ({marks} marques * {SpellRegistry.MorsurePutrideDmgPerMark}, cap {SpellRegistry.MorsurePutrideDmgBonusCap}) = {dmgThisTarget} sur P{targetC->PlayerIndex}");
                        }
                    }
                    else if (cmd.Spell == SpellId.NecramDetonationVirulente)
                    {
                        // 3.5.a.ii — Bible Detonation Virulente : 80 base + 50 par marque consommee.
                        // 4 marques = 80 + 200 = 280 dmg. Marques sont retirees en post-damage (apres
                        // que targetC->VeninStacks ait servi a calculer le bonus).
                        int marks = targetC->VeninStacks;
                        if (marks > 0)
                        {
                            int bonus = marks * SpellRegistry.DetonationVirulenteDmgPerMark;
                            dmgThisTarget = SpellRegistry.DetonationVirulenteDmgBase + bonus;
                            Log.Info($"[Spell] Detonation Virulente : {SpellRegistry.DetonationVirulenteDmgBase} + {bonus} dgts ({marks} marques * {SpellRegistry.DetonationVirulenteDmgPerMark}) = {dmgThisTarget} sur P{targetC->PlayerIndex}");
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
                            int pct = ColossarPassif.GetCombinedDamageReductionPercent(f, targetC);
                            Log.Info($"[Reduction] -{pct}% dmg sur P{targetC->PlayerIndex} : {dmgBefore} -> {dmgThisTarget}");
                        }
                    }

                    // 3.3.b.ii — Ancrage (Bible V7.1) : -50% dmg subis pendant la duree.
                    // Applique APRES Densite Inerte (cumul multiplicatif Bible-cohérent), AVANT shield.
                    // Magnitude = % de reduction (fixe 50 pour Ancrage actuel, mais extensible).
                    int anchorMag = StatusHelper.GetMagnitude(targetC, StatusKind.AnchorImmune, 0);
                    if (anchorMag > 0 && dmgThisTarget > 0)
                    {
                        int dmgBeforeAnchor = dmgThisTarget;
                        dmgThisTarget = dmgThisTarget * (100 - anchorMag) / 100;
                        Log.Info($"[Ancrage] -{anchorMag}% dmg sur P{targetC->PlayerIndex} : {dmgBeforeAnchor} -> {dmgThisTarget}");
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
                    int shieldAbsorbedThisHit = 0; // tracker pour hook Carapace Visqueuse (3.5.c.ii)
                    if (shieldBefore > 0 && dmgToShield > 0)
                    {
                        int absorbed = dmgToShield > shieldBefore ? shieldBefore : dmgToShield;
                        shieldAbsorbedThisHit = absorbed;
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

                    // 3.5.c.ii — Carapace Visqueuse hook (Bible V7.1) : si la cible porte
                    // CarapaceVisqueuse ET le shield a absorbe au moins 1 dmg de cette attaque
                    // ET le sort est ATTAQUE MELEE (Chebyshev caster-cible <= 1 au moment du dmg)
                    // -> +1 marque venin sur l'attaquant.
                    // Bible-strict : couvre Tranche-Ame (range 1), Charge Brutale post-move
                    // (caster adjacent target), Faux Decharnee AoE, Curee... Rejette les sorts
                    // distants (Crachat Acide range 4, Tir Precis range 4, etc.).
                    // Place AVANT le bloc `if (totalHPLoss > 0)` pour trigger meme si shield
                    // absorbe tout le dmg (HP_loss=0). Bible : "frappe le bouclier" = absorbed>=1.
                    // Skip si caster mort (defensif, pas de reflect avant ici donc tjrs vivant).
                    int dxCar = caster->GridX - cx; if (dxCar < 0) dxCar = -dxCar;
                    int dyCar = caster->GridY - cy; if (dyCar < 0) dyCar = -dyCar;
                    bool isMeleeAttackForCarapace = dxCar <= 1 && dyCar <= 1;
                    if (isMeleeAttackForCarapace
                        && shieldAbsorbedThisHit > 0
                        && StatusHelper.Has(targetC, StatusKind.CarapaceVisqueuse)
                        && caster->PlayerIndex != targetC->PlayerIndex
                        && caster->HP > 0)
                    {
                        int attackerStacksBefore = caster->VeninStacks;
                        VeninHelpers.ApplyMark(f, caster,
                            SpellRegistry.CarapaceVisqueuseMarksOnMeleeAttacker, currentTurn);
                        Log.Info($"[Carapace Visqueuse] P{caster->PlayerIndex} frappe melee bouclier de P{targetC->PlayerIndex} (absorbe {shieldAbsorbedThisHit}) : +{SpellRegistry.CarapaceVisqueuseMarksOnMeleeAttacker} marque sur attaquant (stacks {attackerStacksBefore} -> {caster->VeninStacks})");
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
                        // 3.3.c — tracker nb d'attaques subies ce round (Ressac Vital Bible : +30/hit).
                        targetC->HitsTakenThisRound += 1;

                        // 2.10.c : Kill detection. Tracker si au moins 1 cible est tombee a HP=0.
                        // killedTargetX/Y sert au recul Tranche-Ame (direction opposee a la cible tuee).
                        if (targetC->HP == 0 && before > 0)
                        {
                            wasKill = true;
                            killedTargetX = cx;
                            killedTargetY = cy;
                            Log.Info($"[Spell] KILL : P{targetC->PlayerIndex} tombe a HP=0 sur ({cx},{cy})");

                            // 3.5.a.i — Morsure Putride : transfert des marques venin de la cible morte
                            // vers l'ennemi du Necram (= allie de la cible morte) vivant le plus proche
                            // (Manhattan). Bible : "Si la cible meurt : toutes ses marques sont transferees
                            // sur l'unite ennemie la plus proche". En 1v1 pas d'autre cible -> marques perdues.
                            if (cmd.Spell == SpellId.NecramMorsurePutride && targetC->VeninStacks > 0)
                            {
                                VeninHelpers.TryTransferVeninOnKill(f, targetC, caster->PlayerIndex, currentTurn);
                            }
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

                        // Trigger reflect (Riposte Carmin / Represailles / Renvoi du Bouclier).
                        // Bible V7.1 :
                        //   - RipostMelee : trigger uniquement si sort melee (Riposte Carmin Soulrender = no cap ;
                        //     Represailles Colossar = cap 4)
                        //   - RipostAll   : trigger sur TOUTE attaque (melee + distance) (Renvoi du Bouclier
                        //     Colossar = cap 4). Pas de MovementMalus attaquant (Bible : juste reflect).
                        //   Cap stocke dans Combatant.RepresaillesReflectsLeft :
                        //     -1 = no cap, 0 = cap epuise (skip), >0 = trigger + decrement.
                        bool hasRipostMelee = StatusHelper.Has(targetC, StatusKind.RipostMelee);
                        bool hasRipostAll   = StatusHelper.Has(targetC, StatusKind.RipostAll);
                        if ((isMelee && hasRipostMelee) || hasRipostAll)
                        {
                            int reflectsLeft = targetC->RepresaillesReflectsLeft;
                            bool canReflect = (reflectsLeft != 0); // -1 ou >0 -> ok ; 0 -> skip
                            if (canReflect)
                            {
                                // Si RipostAll actif on prend sa magnitude (60 Bible) ; sinon RipostMelee (80/100).
                                StatusKind reflectKind = hasRipostAll ? StatusKind.RipostAll : StatusKind.RipostMelee;
                                int reflectDmg = StatusHelper.GetMagnitude(targetC, reflectKind, 100);
                                int casterBefore = caster->HP;
                                caster->HP -= reflectDmg;
                                if (caster->HP < 0) caster->HP = 0;
                                if (reflectsLeft > 0)
                                {
                                    targetC->RepresaillesReflectsLeft = reflectsLeft - 1;
                                    Log.Info($"[Spell] Reflect ({reflectKind}) : P{caster->PlayerIndex} prend {reflectDmg} dgts (HP {casterBefore} -> {caster->HP}) — retours restants {targetC->RepresaillesReflectsLeft}");
                                }
                                else
                                {
                                    Log.Info($"[Spell] Reflect ({reflectKind}, no cap) : P{caster->PlayerIndex} prend {reflectDmg} dgts (HP {casterBefore} -> {caster->HP})");
                                }

                                // L'attaquant prend MovementMalus 1 (1 tour) UNIQUEMENT pour RipostMelee
                                // (Riposte Carmin Bible : "-1 PM additionnel"). RipostAll Bible : juste reflect.
                                if (hasRipostMelee && isMelee)
                                {
                                    StatusHelper.Apply(caster, StatusKind.MovementMalus, magnitude: 1, turnsLeft: 1, currentTurn);
                                }
                            }
                            else
                            {
                                Log.Info($"[Spell] Reflect : cap 4 retours atteint sur P{targetC->PlayerIndex}, skip");
                            }
                        }

                        // 3.5.c.i — Voile de Pestilence (hook 2) : si la cible porte PestilenceAura
                        // et le sort est ATTAQUE MELEE (Chebyshev caster-cible <= 1 au moment du dmg),
                        // +1 marque venin sur l'attaquant.
                        // Bible-strict : couvre Tranche-Ame (range 1), Charge Brutale post-move (caster
                        // adjacent target), Faux Decharnee (AoE 8 voisines), Curee... Rejette les sorts
                        // distants (Crachat Acide range 4, Tir Precis range 4, etc.).
                        // Conditionne au caster encore vivant (sinon Riposte Carmin l'a tue avant).
                        // Pas de check VeninStacks cap : ApplyMark cap deja a 4 en interne.
                        int dxAtt = caster->GridX - cx; if (dxAtt < 0) dxAtt = -dxAtt;
                        int dyAtt = caster->GridY - cy; if (dyAtt < 0) dyAtt = -dyAtt;
                        bool isMeleeAttack = dxAtt <= 1 && dyAtt <= 1;
                        if (isMeleeAttack
                            && StatusHelper.Has(targetC, StatusKind.PestilenceAura)
                            && caster->PlayerIndex != targetC->PlayerIndex
                            && caster->HP > 0)
                        {
                            int attackerStacksBefore = caster->VeninStacks;
                            VeninHelpers.ApplyMark(f, caster,
                                SpellRegistry.VoilePestilenceMarksOnMeleeAttacker, currentTurn);
                            Log.Info($"[Voile Pestilence] P{caster->PlayerIndex} attaque melee P{targetC->PlayerIndex} (porteur Voile) : +{SpellRegistry.VoilePestilenceMarksOnMeleeAttacker} marque sur attaquant (stacks {attackerStacksBefore} -> {caster->VeninStacks})");
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

                    // 3.5.a.i — Crachat Acide : applique 2 marques venin sur target apres damage
                    // (Bible : "90 dgts ET 2 marques", cap 4/cible). Skip si cible morte (cas rare
                    // car 90 < 1500 HP base, mais possible si HP critique).
                    if (cmd.Spell == SpellId.NecramCrachatAcide && targetC->HP > 0)
                    {
                        VeninHelpers.ApplyMark(f, targetC, SpellRegistry.CrachatAcideMarksApplied, currentTurn);
                    }

                    // 3.5.c.iii — Drain Vital : heal Necram caster post-damage. Base 30 HP, bonus
                    // 60 HP si target.VeninStacks >= 3 au moment du cast (snapshot post-damage,
                    // marques cible NON consommees). Heal applique meme si target meurt sur les 60
                    // dmg (Bible : le siphon). Cap MaxHP standard. Skip si caster mort.
                    if (cmd.Spell == SpellId.NecramDrainVital && caster->HP > 0)
                    {
                        int targetMarks = targetC->VeninStacks;
                        int healAmount = targetMarks >= SpellRegistry.DrainVitalMarksThreshold
                            ? SpellRegistry.DrainVitalHealBonus
                            : SpellRegistry.DrainVitalHealBase;
                        int hpBeforeHeal = caster->HP;
                        caster->HP += healAmount;
                        if (caster->HP > caster->MaxHP) caster->HP = caster->MaxHP;
                        Log.Info($"[Spell] Drain Vital : P{caster->PlayerIndex} heal +{healAmount} HP (target P{targetC->PlayerIndex} marques={targetMarks}/threshold {SpellRegistry.DrainVitalMarksThreshold}) HP {hpBeforeHeal} -> {caster->HP}");
                    }

                    // 3.5.a.ii — Detonation Virulente : consomme TOUTES les marques de la cible
                    // (post-damage, le bonus a deja ete applique en pre-damage). Reset stacks=0.
                    if (cmd.Spell == SpellId.NecramDetonationVirulente && targetC->VeninStacks > 0)
                    {
                        int marksConsumed = targetC->VeninStacks;
                        VeninHelpers.RemoveAllMarks(targetC);
                        Log.Info($"[Spell] Detonation Virulente : {marksConsumed} marques consommees sur P{targetC->PlayerIndex}");
                    }

                    // 3.5.a.ii — Faux Decharnee : accumule les marques sur cibles touchees (snapshot
                    // PRE-damage pour qu'une cible morte compte quand meme). Le heal est applique
                    // post-loop apres avoir totalise toutes les cibles AoE.
                    if (cmd.Spell == SpellId.NecramFauxDecharnee)
                    {
                        fauxDecharneeMarksTotal += targetC->VeninStacks;
                    }
                }
            }

            // 3.5.a.ii — Faux Decharnee : heal Necram caster selon marques cumulees sur cibles
            // touchees (cap +120 HP = 4 marques). Applique post-loop, apres que tous les targets
            // AoE aient ete totalises. Si aucune marque sur les cibles touchees -> 0 heal.
            if (cmd.Spell == SpellId.NecramFauxDecharnee && fauxDecharneeMarksTotal > 0 && caster->HP > 0)
            {
                int healUncapped = fauxDecharneeMarksTotal * SpellRegistry.FauxDecharneeHealPerMark;
                int heal = healUncapped > SpellRegistry.FauxDecharneeHealCap
                    ? SpellRegistry.FauxDecharneeHealCap
                    : healUncapped;
                int hpBefore = caster->HP;
                caster->HP = caster->HP + heal > caster->MaxHP ? caster->MaxHP : caster->HP + heal;
                int realHeal = caster->HP - hpBefore;
                if (realHeal > 0)
                {
                    Log.Info($"[Spell] Faux Decharnee heal Necram P{caster->PlayerIndex} : +{realHeal} HP ({fauxDecharneeMarksTotal} marques * {SpellRegistry.FauxDecharneeHealPerMark}, cap {SpellRegistry.FauxDecharneeHealCap}) : {hpBefore}->{caster->HP}");
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
                    // Bible V7.1 Riposte Carmin : aucun cap de retours -> RepresaillesReflectsLeft = -1.
                    StatusHelper.Apply(caster, StatusKind.RipostMelee, magnitude: 100, turnsLeft: 1, currentTurn);
                    caster->RepresaillesReflectsLeft = -1;
                    Log.Info($"[Spell] Riposte Carmin : RipostMelee 100 dgts (1 tour, no cap) sur P{caster->PlayerIndex}");
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
                        // 3.3.b.ii — Ancrage : cible AnchorImmune ne peut pas etre tiree.
                        // AntiTeleport non plus applique (Bible : ancrage = "rien ne me deplace").
                        if (StatusHelper.Has(targetC, StatusKind.AnchorImmune))
                        {
                            Log.Info($"[Ancrage] Empoignade annulee sur P{targetC->PlayerIndex} (AnchorImmune actif)");
                            break;
                        }
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

                        // 3.3.b.i — Bible V7.1 : Pilier/Mur bloque la charge. Le caster s'arrete
                        // sur la case precedente, pas de damage (l'obstacle absorbe l'impact mais
                        // n'est pas blesse par la charge — Bible : seule la cible vivante prend dgts).
                        if (ObstacleHelpers.HasObstacleAt(f, cx, cy))
                        {
                            break;
                        }

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
                                int pct = ColossarPassif.GetCombinedDamageReductionPercent(f, hitC);
                                Log.Info($"[Reduction] -{pct}% dmg sur P{hitC->PlayerIndex} (Charge Brutale) : {dmgBeforeReduc} -> {dmgLeft}");
                            }
                        }
                        // 3.3.b.ii — Ancrage hook (Charge Brutale bypass pipeline standard).
                        int anchorMagCB = StatusHelper.GetMagnitude(hitC, StatusKind.AnchorImmune, 0);
                        if (anchorMagCB > 0 && dmgLeft > 0)
                        {
                            int dmgBeforeAnchorCB = dmgLeft;
                            dmgLeft = dmgLeft * (100 - anchorMagCB) / 100;
                            Log.Info($"[Ancrage] -{anchorMagCB}% dmg sur P{hitC->PlayerIndex} (Charge Brutale) : {dmgBeforeAnchorCB} -> {dmgLeft}");
                        }
                        int shieldBefore = StatusHelper.GetMagnitude(hitC, StatusKind.ShieldActive, 0);
                        int shieldAbsorbedByChargeBrutale = 0;
                        if (shieldBefore > 0)
                        {
                            int absorbed = dmgLeft > shieldBefore ? shieldBefore : dmgLeft;
                            shieldAbsorbedByChargeBrutale = absorbed;
                            int shieldAfter = shieldBefore - absorbed;
                            if (shieldAfter == 0) StatusHelper.Consume(hitC, StatusKind.ShieldActive);
                            else StatusHelper.SetMagnitude(hitC, StatusKind.ShieldActive, shieldAfter);
                            dmgLeft -= absorbed;
                            Log.Info($"[Spell] Charge Brutale : shield absorbe {absorbed} sur P{hitC->PlayerIndex}");
                        }

                        // 3.5.c.i — Voile de Pestilence hook (Bible V7.1) : Charge Brutale finit
                        // ADJACENT a la cible (post-move), donc c'est une attaque melee. Si la cible
                        // porte PestilenceAura -> +1 marque venin sur l'attaquant. Charge Brutale
                        // bypass le pipeline standard donc on rebranche le hook ici manuellement.
                        if (StatusHelper.Has(hitC, StatusKind.PestilenceAura)
                            && caster->PlayerIndex != hitC->PlayerIndex
                            && caster->HP > 0)
                        {
                            int attackerStacksBefore = caster->VeninStacks;
                            VeninHelpers.ApplyMark(f, caster,
                                SpellRegistry.VoilePestilenceMarksOnMeleeAttacker, currentTurn);
                            Log.Info($"[Voile Pestilence] P{caster->PlayerIndex} Charge Brutale sur P{hitC->PlayerIndex} (porteur Voile) : +{SpellRegistry.VoilePestilenceMarksOnMeleeAttacker} marque sur attaquant (stacks {attackerStacksBefore} -> {caster->VeninStacks})");
                        }

                        // 3.5.c.ii — Carapace Visqueuse hook (Bible V7.1) : Charge Brutale est melee
                        // (caster adjacent post-move). Si la cible porte CarapaceVisqueuse ET le shield
                        // a absorbe >=1 dmg -> +1 marque venin sur l'attaquant. Charge Brutale bypass
                        // le pipeline standard donc on rebranche le hook ici manuellement.
                        if (shieldAbsorbedByChargeBrutale > 0
                            && StatusHelper.Has(hitC, StatusKind.CarapaceVisqueuse)
                            && caster->PlayerIndex != hitC->PlayerIndex
                            && caster->HP > 0)
                        {
                            int attackerStacksBeforeC = caster->VeninStacks;
                            VeninHelpers.ApplyMark(f, caster,
                                SpellRegistry.CarapaceVisqueuseMarksOnMeleeAttacker, currentTurn);
                            Log.Info($"[Carapace Visqueuse] P{caster->PlayerIndex} Charge Brutale frappe bouclier P{hitC->PlayerIndex} (absorbe {shieldAbsorbedByChargeBrutale}) : +{SpellRegistry.CarapaceVisqueuseMarksOnMeleeAttacker} marque sur attaquant (stacks {attackerStacksBeforeC} -> {caster->VeninStacks})");
                        }

                        if (dmgLeft > 0)
                        {
                            hitC->HP -= dmgLeft;
                            if (hitC->HP < 0) hitC->HP = 0;
                            hitC->DamageTakenThisRound += dmgLeft;
                            hitC->HitsTakenThisRound += 1; // 3.3.c Ressac Vital tracker
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

                // 3.5.b.i — Inoculation : applique 2 marques venin sur la cible ennemie (cap 4
                // gere par VeninHelpers.ApplyMark). Pas de damage. Filter Enemy + LoS check deja
                // appliques en amont. Putrefaction Necram +1 PT (gere dans ApplyMark via hook).
                case SpellId.NecramInoculation:
                {
                    EntityRef target = GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY);
                    if (target != EntityRef.None
                        && f.Unsafe.TryGetPointer<Combatant>(target, out Combatant* targetInoc)
                        && targetInoc->PlayerIndex != caster->PlayerIndex
                        && targetInoc->HP > 0)
                    {
                        VeninHelpers.ApplyMark(f, targetInoc, SpellRegistry.InoculationMarksApplied, currentTurn);
                        Log.Info($"[Spell] Inoculation : +{SpellRegistry.InoculationMarksApplied} marques venin sur P{targetInoc->PlayerIndex} (silent, no damage)");
                    }
                    break;
                }

                // 3.5.b.i — Marque Sacrificielle : applique status MarqueSacrificielle sur la
                // cible ennemie (magnitude=20, duree 3 rounds). Hook dans VeninHelpers.TryTick
                // bonus +20 dmg/tick. Pas de damage direct. Effet neutre si cible n'a pas de
                // marques (Bible : "sans marques actives, l'effet est neutre" — bonus dort).
                case SpellId.NecramMarqueSacrificielle:
                {
                    EntityRef target = GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY);
                    if (target != EntityRef.None
                        && f.Unsafe.TryGetPointer<Combatant>(target, out Combatant* targetMS)
                        && targetMS->PlayerIndex != caster->PlayerIndex
                        && targetMS->HP > 0)
                    {
                        StatusHelper.Apply(targetMS, StatusKind.MarqueSacrificielle,
                            magnitude: SpellRegistry.MarqueSacrificielleBonusDmgPerTick,
                            turnsLeft: SpellRegistry.MarqueSacrificielleTurns,
                            currentTurn);
                        Log.Info($"[Spell] Marque Sacrificielle : +{SpellRegistry.MarqueSacrificielleBonusDmgPerTick} dgts/tick venin sur P{targetMS->PlayerIndex} pendant {SpellRegistry.MarqueSacrificielleTurns} rounds");
                    }
                    break;
                }

                // 3.5.b.ii — Symbiose Morbide : self-buff lifesteal DoT. 3 PA, range 0. Applique
                // status SymbioseMorbide sur le caster Necram (magnitude=8, duree 2 rounds).
                // Le hook heal est dans VeninHelpers.TryTick : a chaque tick venin sur un ennemi,
                // tout Necram porteur du status est soigne de min(stacks, 4) * 8 HP.
                case SpellId.NecramSymbioseMorbide:
                {
                    StatusHelper.Apply(caster, StatusKind.SymbioseMorbide,
                        magnitude: SpellRegistry.SymbioseMorbideHealPerMarkPerTick,
                        turnsLeft: SpellRegistry.SymbioseMorbideTurns,
                        currentTurn);
                    Log.Info($"[Spell] Symbiose Morbide active sur P{caster->PlayerIndex} : heal +{SpellRegistry.SymbioseMorbideHealPerMarkPerTick}/marque/tick venin pendant {SpellRegistry.SymbioseMorbideTurns} rounds (cap {SpellRegistry.SymbioseMorbideMaxMarksForHeal} marques = max +{SpellRegistry.SymbioseMorbideHealPerMarkPerTick * SpellRegistry.SymbioseMorbideMaxMarksForHeal} HP/tick)");
                    break;
                }

                // 3.5.b.iii — Pas Spectral (Bible V7.1) : 2 PA self. +2 PM ce tour (cap si refresh
                // meme sub-turn — eviter exploit re-cast pour stacker PM) + Apply PasSpectralReady
                // turnsLeft=1 magnitude=0. Le status est consume dans TurnSystem.EnterTurnEnd quand
                // ActivePlayerIndex == porteur (= fin de SON sub-turn). Tant que actif :
                // MovementSystem passe ignoreEnemyOccupants=true a A* et pose +1 marque venin par
                // ennemi present sur les cases intermediaires du path.
                case SpellId.NecramPasSpectral:
                {
                    bool alreadyActive = StatusHelper.Has(caster, StatusKind.PasSpectralReady);
                    if (!alreadyActive)
                    {
                        caster->PM += SpellRegistry.PasSpectralPMBonus;
                        Log.Info($"[Spell] Pas Spectral active sur P{caster->PlayerIndex} : +{SpellRegistry.PasSpectralPMBonus} PM (PM={caster->PM}), traversee ennemis armee pour ce sub-turn");
                    }
                    else
                    {
                        Log.Info($"[Spell] Pas Spectral re-cast sur P{caster->PlayerIndex} : refresh traversee (PM inchange, cap +{SpellRegistry.PasSpectralPMBonus} deja accorde)");
                    }
                    StatusHelper.Apply(caster, StatusKind.PasSpectralReady,
                        magnitude: 0,
                        turnsLeft: 1,
                        currentTurn);
                    break;
                }

                // 3.5.c.i — Voile de Pestilence (Bible V7.1) : 3 PA self. Apply PestilenceAura
                // turnsLeft=2 (refresh-only). 2 hooks distincts :
                //   1. Adjacence fin de sub-turn : TurnSystem.EnterTurnEnd iter ennemi finissant
                //      son sub-turn, +1 marque si Manhattan <=2 d'un Necram porteur.
                //   2. Riposte marque : damage loop SpellSystem (ce fichier, bloc reflect),
                //      +1 marque sur l'attaquant si sort melee (RangeMax==1).
                case SpellId.NecramVoilePestilence:
                {
                    StatusHelper.Apply(caster, StatusKind.PestilenceAura,
                        magnitude: 0,
                        turnsLeft: SpellRegistry.VoilePestilenceTurns,
                        currentTurn);
                    Log.Info($"[Spell] Voile de Pestilence active sur P{caster->PlayerIndex} : aura 2 rounds, +1 marque ennemi adjacent (Manhattan <={SpellRegistry.VoilePestilenceAdjacencyRange}) fin sub-turn + riposte +1 marque attaquant melee");
                    break;
                }

                // 3.5.c.ii — Carapace Visqueuse (Bible V7.1) : 3 PA self. Apply ShieldActive
                // 110 HP / 2 rounds + CarapaceVisqueuse flag 2 rounds. Hook (damage loop, apres
                // bloc absorption shield) : si attaquant melee frappe le bouclier (shield absorbe
                // >=1 dmg) -> +1 marque venin sur attaquant. Refresh-only (recast meme tour reset
                // shield a 110 HP + duree a 2 rounds, pas de stack).
                case SpellId.NecramCarapaceVisqueuse:
                {
                    StatusHelper.Apply(caster, StatusKind.ShieldActive,
                        magnitude: SpellRegistry.CarapaceVisqueuseShieldHP,
                        turnsLeft: SpellRegistry.CarapaceVisqueuseTurns,
                        currentTurn);
                    StatusHelper.Apply(caster, StatusKind.CarapaceVisqueuse,
                        magnitude: 0,
                        turnsLeft: SpellRegistry.CarapaceVisqueuseTurns,
                        currentTurn);
                    Log.Info($"[Spell] Carapace Visqueuse active sur P{caster->PlayerIndex} : Shield {SpellRegistry.CarapaceVisqueuseShieldHP} HP / {SpellRegistry.CarapaceVisqueuseTurns} rounds + flag riposte +{SpellRegistry.CarapaceVisqueuseMarksOnMeleeAttacker} marque attaquant melee qui frappe le bouclier");
                    break;
                }

                // 3.5.c.iv — Pulse Sanguin Vert (Bible V7.1) : 3 PA self. Heal Necram caster
                // base 70 + 15/marque venin somme sur ennemis vivants Manhattan <=4 (cap bonus
                // +90 HP). +30 HP additionnel si hgSpend >= 1 (1 PT optionnel via Shift+X).
                // Marques NON consommees. Cap MaxHP standard. Pas de dmg.
                case SpellId.NecramPulseSanguinVert:
                {
                    int sumMarks = 0;
                    var pulseFilter = f.Filter<Combatant>();
                    while (pulseFilter.NextUnsafe(out EntityRef _, out Combatant* pulseEnemy))
                    {
                        if (pulseEnemy->HP <= 0) continue;
                        if (pulseEnemy->PlayerIndex == caster->PlayerIndex) continue;
                        int dxP = pulseEnemy->GridX - caster->GridX; if (dxP < 0) dxP = -dxP;
                        int dyP = pulseEnemy->GridY - caster->GridY; if (dyP < 0) dyP = -dyP;
                        int distP = dxP + dyP;
                        if (distP > SpellRegistry.PulseSanguinVertMarksRange) continue;
                        sumMarks += pulseEnemy->VeninStacks;
                    }
                    int bonusUncapped = sumMarks * SpellRegistry.PulseSanguinVertHealPerMark;
                    int bonusCapped = bonusUncapped > SpellRegistry.PulseSanguinVertHealCap
                        ? SpellRegistry.PulseSanguinVertHealCap
                        : bonusUncapped;
                    int ptBonus = (hgSpend >= 1) ? SpellRegistry.PulseSanguinVertOptionalPTBonus : 0;
                    int totalHeal = SpellRegistry.PulseSanguinVertHealBase + bonusCapped + ptBonus;
                    int hpBeforePulse = caster->HP;
                    caster->HP += totalHeal;
                    if (caster->HP > caster->MaxHP) caster->HP = caster->MaxHP;
                    Log.Info($"[Spell] Pulse Sanguin Vert : P{caster->PlayerIndex} heal +{totalHeal} HP (base {SpellRegistry.PulseSanguinVertHealBase} + bonus {bonusCapped} [{sumMarks} marques sommees rayon {SpellRegistry.PulseSanguinVertMarksRange}, cap {SpellRegistry.PulseSanguinVertHealCap}] + PT {ptBonus}) HP {hpBeforePulse} -> {caster->HP}");
                    break;
                }

                // 3.5.b.iv — Contagion : propagation AoE marques venin. La cible doit etre marquee
                // (sinon no-op silencieux post-PA, Bible "Cible une unite ENNEMIE marquée").
                // Copie min(target.stacks, cap) marques sur autres ennemis rayon 3 Manhattan
                // de la cible. Cap default 3, ou 4 avec 2 PT optionnel (hgSpend >= 2). En 1v1
                // (aucun autre ennemi du caster) : +1 marque sur la cible elle-meme (boost tick).
                case SpellId.NecramContagion:
                {
                    EntityRef contTarget = GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY);
                    if (contTarget == EntityRef.None
                        || !f.Unsafe.TryGetPointer<Combatant>(contTarget, out Combatant* targetCont)
                        || targetCont->PlayerIndex == caster->PlayerIndex
                        || targetCont->HP <= 0)
                    {
                        Log.Info($"[Spell] Contagion : pas de cible valide en ({cmd.TargetX},{cmd.TargetY}), PA deja consomme");
                        break;
                    }

                    if (targetCont->VeninStacks <= 0)
                    {
                        Log.Info($"[Spell] Contagion : cible P{targetCont->PlayerIndex} non marquee, no-op");
                        break;
                    }

                    int cap = (hgSpend >= SpellRegistry.ContagionPTCostForBoost)
                        ? SpellRegistry.ContagionCapBoosted
                        : SpellRegistry.ContagionCapDefault;
                    int stacksToCopy = targetCont->VeninStacks > cap ? cap : targetCont->VeninStacks;

                    // Cherche les autres ennemis du caster dans rayon Manhattan 3 de la cible.
                    int propagated = 0;
                    var contFilter = f.Filter<Combatant>();
                    while (contFilter.NextUnsafe(out EntityRef _, out Combatant* otherC))
                    {
                        if (otherC == targetCont) continue;
                        if (otherC->PlayerIndex == caster->PlayerIndex) continue; // pas allies/self
                        if (otherC->HP <= 0) continue;
                        int dxOther = otherC->GridX - targetCont->GridX;
                        int dyOther = otherC->GridY - targetCont->GridY;
                        int absDxO = dxOther < 0 ? -dxOther : dxOther;
                        int absDyO = dyOther < 0 ? -dyOther : dyOther;
                        int distOther = absDxO + absDyO;
                        if (distOther > SpellRegistry.ContagionPropagationRadius) continue;

                        VeninHelpers.ApplyMark(f, otherC, stacksToCopy, currentTurn);
                        Log.Info($"[Spell] Contagion : +{stacksToCopy} marques copiees sur P{otherC->PlayerIndex} (rayon {distOther} de cible P{targetCont->PlayerIndex})");
                        propagated++;
                    }

                    if (propagated == 0)
                    {
                        // 1v1 fallback : +1 marque sur la cible (boost tick).
                        VeninHelpers.ApplyMark(f, targetCont, SpellRegistry.Contagion1v1FallbackMarks, currentTurn);
                        Log.Info($"[Spell] Contagion 1v1 (pas d'autres ennemis) : +{SpellRegistry.Contagion1v1FallbackMarks} marque boost sur P{targetCont->PlayerIndex}");
                    }
                    else
                    {
                        Log.Info($"[Spell] Contagion propagation : {stacksToCopy} marques sur {propagated} ennemi(s) (cap {cap}, hgSpend={hgSpend})");
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
                    // Bible V7.1 : CAP 4 RETOURS -> RepresaillesReflectsLeft = 4.
                    StatusHelper.Apply(caster, StatusKind.RipostMelee,
                        magnitude: SpellRegistry.RepresaillesReflectDmg,
                        turnsLeft: SpellRegistry.RepresaillesReflectTurns,
                        currentTurn);
                    caster->RepresaillesReflectsLeft = SpellRegistry.RepresaillesReflectMaxTriggers;
                    Log.Info($"[Spell] Represailles : RipostMelee {SpellRegistry.RepresaillesReflectDmg} dgts ({SpellRegistry.RepresaillesReflectTurns} tours, cap {SpellRegistry.RepresaillesReflectMaxTriggers} retours) sur P{caster->PlayerIndex}");
                    break;

                // -------------------------------------------------------------
                // COLOSSAR 3.3.a.ii — Onde de Choc + Choc Sismique
                // -------------------------------------------------------------

                case SpellId.ColossarOndeDeChoc:
                {
                    // Bible : 80 dgts AoE adj (deja appliques par damage loop avec AoE rayon 1).
                    // Push chaque ennemi adj de 2 cases loin du caster. Si push s'arrete contre
                    // obstacle/bord : +80 dgts + TRAUMA (-1 PA -1 PM, 1 tour).
                    int casterXOdC = caster->GridX;
                    int casterYOdC = caster->GridY;
                    int[] dxArr = { 1, -1, 0, 0 };
                    int[] dyArr = { 0, 0, 1, -1 };
                    for (int i = 0; i < 4; i++)
                    {
                        int nx = casterXOdC + dxArr[i];
                        int ny = casterYOdC + dyArr[i];
                        if (!GridHelpers.InBounds(nx, ny)) continue;
                        EntityRef adjTarget = GridHelpers.GetOccupant(f, nx, ny);
                        if (adjTarget == EntityRef.None) continue;
                        if (adjTarget == casterEntity) continue;
                        if (!f.Unsafe.TryGetPointer<Combatant>(adjTarget, out Combatant* adjC)) continue;

                        // Push 2 cases loin du caster.
                        PushAndTriggerEx(f, adjC, adjTarget, casterXOdC, casterYOdC,
                            SpellRegistry.OndeDeChocPushDistance, currentTurn, caster,
                            out bool stoppedAgainst);

                        if (stoppedAgainst)
                        {
                            // Bonus +80 dgts direct au HP (Bible : "+ 80 degats supplementaires").
                            // Bypass shield/Densite Inerte ? Bible silencieux, on traite comme dgts
                            // simples direct au HP (pas via damage loop pipeline).
                            int hpBeforeOdC = adjC->HP;
                            adjC->HP -= SpellRegistry.OndeDeChocBonusVsWall;
                            if (adjC->HP < 0) adjC->HP = 0;
                            adjC->DamageTakenThisRound += SpellRegistry.OndeDeChocBonusVsWall;
                            adjC->HitsTakenThisRound += 1; // 3.3.c Ressac Vital tracker
                            Log.Info($"[Spell] Onde de Choc BONUS WALL : +{SpellRegistry.OndeDeChocBonusVsWall} dgts sur P{adjC->PlayerIndex} (HP {hpBeforeOdC} -> {adjC->HP})");

                            // TRAUMA Bible = ActionMalus -1 PA + MovementMalus -1 PM 1 tour.
                            StatusHelper.Apply(adjC, StatusKind.ActionMalus,
                                magnitude: SpellRegistry.OndeDeChocTraumaPAMagnitude,
                                turnsLeft: SpellRegistry.OndeDeChocTraumaTurns, currentTurn);
                            StatusHelper.Apply(adjC, StatusKind.MovementMalus,
                                magnitude: SpellRegistry.OndeDeChocTraumaPMMagnitude,
                                turnsLeft: SpellRegistry.OndeDeChocTraumaTurns, currentTurn);
                            Log.Info($"[Spell] Onde de Choc TRAUMA : -{SpellRegistry.OndeDeChocTraumaPAMagnitude} PA / -{SpellRegistry.OndeDeChocTraumaPMMagnitude} PM (1 tour) sur P{adjC->PlayerIndex}");
                        }
                    }
                    break;
                }

                case SpellId.ColossarChocSismique:
                {
                    // Bible : LIGNE depuis caster vers (cmd.TargetX, cmd.TargetY). Iter cases en
                    // ligne droite (axe-dominant Manhattan), hit toutes les cibles : 130 dgts +
                    // MovementMalus -1 PM (1 tour). Si case = obstacle OWNED par caster (Pilier
                    // ou Mur) : traverse + +50 dgts a la CIBLE SUIVANTE dans la ligne.
                    int sx = caster->GridX;
                    int sy = caster->GridY;
                    int tx = cmd.TargetX;
                    int ty = cmd.TargetY;
                    int dxCs = tx - sx;
                    int dyCs = ty - sy;
                    int absDxCs = dxCs < 0 ? -dxCs : dxCs;
                    int absDyCs = dyCs < 0 ? -dyCs : dyCs;
                    int stepXCs = 0, stepYCs = 0;
                    if (absDxCs >= absDyCs) stepXCs = dxCs > 0 ? 1 : (dxCs < 0 ? -1 : 0);
                    else stepYCs = dyCs > 0 ? 1 : (dyCs < 0 ? -1 : 0);
                    if (stepXCs == 0 && stepYCs == 0) { Log.Warn("[Spell] Choc Sismique : direction nulle, no-op"); break; }

                    bool pendingThroughWallBonus = false;
                    int curXCs = sx;
                    int curYCs = sy;
                    for (int s = 0; s < SpellRegistry.ChocSismiqueRange; s++)
                    {
                        curXCs += stepXCs;
                        curYCs += stepYCs;
                        if (!GridHelpers.InBounds(curXCs, curYCs)) break;

                        // Obstacle sur la case ? OWN -> traverse + flag bonus a la cible suivante (Bible).
                        // NON-OWN (obstacle adverse) -> stop la ligne (3.3.b.i fix LoS Bible-cohérent).
                        EntityRef obsEntity = ObstacleHelpers.GetObstacleAt(f, curXCs, curYCs);
                        if (obsEntity != EntityRef.None
                            && f.Unsafe.TryGetPointer<Obstacle>(obsEntity, out Obstacle* obs))
                        {
                            if (obs->OwnerPlayerIndex == caster->PlayerIndex)
                            {
                                pendingThroughWallBonus = true;
                                Log.Info($"[Spell] Choc Sismique traverse obstacle OWN ({curXCs},{curYCs}) -> +{SpellRegistry.ChocSismiqueBonusThroughWall} dgts a la cible suivante");
                                continue; // traverse sans s'arreter
                            }
                            // Obstacle adverse : stop la ligne.
                            Log.Info($"[Spell] Choc Sismique stoppe par obstacle adverse ({curXCs},{curYCs})");
                            break;
                        }

                        // Combatant sur la case ? Hit.
                        EntityRef victim = GridHelpers.GetOccupant(f, curXCs, curYCs);
                        if (victim == EntityRef.None) continue; // case vide, continue la ligne
                        if (victim == casterEntity) continue;   // skip caster (cas degenere)
                        if (!f.Unsafe.TryGetPointer<Combatant>(victim, out Combatant* victimC)) continue;

                        int dmg = SpellRegistry.ChocSismiqueDmgBase;
                        if (pendingThroughWallBonus)
                        {
                            dmg += SpellRegistry.ChocSismiqueBonusThroughWall;
                            pendingThroughWallBonus = false;
                        }

                        // Densite Inerte si victim Colossar.
                        if (victimC->Class == NymoraClass.Colossar)
                        {
                            int dmgBeforeReducCs = dmg;
                            dmg = ColossarPassif.ApplyDamageReduction(f, victimC, dmg);
                            if (dmg != dmgBeforeReducCs)
                            {
                                int pctCs = ColossarPassif.GetCombinedDamageReductionPercent(f, victimC);
                                Log.Info($"[Reduction] -{pctCs}% dmg sur P{victimC->PlayerIndex} (Choc Sismique) : {dmgBeforeReducCs} -> {dmg}");
                            }
                        }
                        // 3.3.b.ii — Ancrage hook (Choc Sismique bypass pipeline standard).
                        int anchorMagCs = StatusHelper.GetMagnitude(victimC, StatusKind.AnchorImmune, 0);
                        if (anchorMagCs > 0 && dmg > 0)
                        {
                            int dmgBeforeAnchorCs = dmg;
                            dmg = dmg * (100 - anchorMagCs) / 100;
                            Log.Info($"[Ancrage] -{anchorMagCs}% dmg sur P{victimC->PlayerIndex} (Choc Sismique) : {dmgBeforeAnchorCs} -> {dmg}");
                        }

                        // Apply dmg direct (bypass shield - simplification 3.3.a.ii).
                        int hpBeforeCs = victimC->HP;
                        victimC->HP -= dmg;
                        if (victimC->HP < 0) victimC->HP = 0;
                        victimC->DamageTakenThisRound += dmg;
                        victimC->HitsTakenThisRound += 1; // 3.3.c Ressac Vital tracker
                        Log.Info($"[Spell] Choc Sismique : {dmg} dgts sur P{victimC->PlayerIndex} ({curXCs},{curYCs}) HP {hpBeforeCs} -> {victimC->HP}");

                        // MovementMalus -1 PM 1 tour.
                        StatusHelper.Apply(victimC, StatusKind.MovementMalus,
                            magnitude: SpellRegistry.ChocSismiquePMReduce,
                            turnsLeft: SpellRegistry.ChocSismiquePMTurns, currentTurn);

                        // Continue la ligne (Bible : toutes les cibles touchees).
                    }
                    break;
                }

                // -------------------------------------------------------------
                // 3.3.b.i — Colossar Tactiques : Pilier + Mur de Pierre
                // -------------------------------------------------------------

                case SpellId.ColossarPilier:
                {
                    // Bible V7.1 : pose 1 Pilier 200 HP, reste jusqu'a destruction (HP <= 0).
                    // Pas de timer d'expiration -> expiresOnTurn = 0 (convention persistent
                    // du framework Obstacle.qtn).
                    // Le filter EmptyTile (SpellDef) garantit deja que la case n'a pas de combatant ;
                    // SpawnObstacle refuse en plus si obstacle deja present + log warn defensif.
                    // +1 FD est branche dans SpawnObstacle (cf 3.2 hook GainFondation).
                    EntityRef pillarEntity = ObstacleHelpers.SpawnObstacle(
                        f,
                        ObstacleKind.Pillar, SpellRegistry.PilierHP,
                        cmd.TargetX, cmd.TargetY,
                        owner: casterEntity, ownerPlayerIndex: caster->PlayerIndex,
                        expiresOnTurn: 0); // 0 = persistent (Bible : reste jusqu'a destruction)
                    if (pillarEntity == EntityRef.None)
                    {
                        Log.Warn($"[Spell] Pilier : SpawnObstacle a echoue sur ({cmd.TargetX},{cmd.TargetY})");
                    }
                    break;
                }

                case SpellId.ColossarMurDePierre:
                {
                    // Bible : pose une LIGNE de 3 cases de Mur (150 HP / 2 tours / case) centree sur
                    // (cmd.TargetX, cmd.TargetY), ORIENTEE PERPENDICULAIREMENT a l'axe caster->cible.
                    // Cases occupees ou deja obstacle : skip silencieux (SpawnObstacle refuse + log warn).
                    // Chaque spawn reussi declenche +1 FD via le hook (max 3 FD posables d'un coup).
                    int wsx = caster->GridX;
                    int wsy = caster->GridY;
                    int wdx = cmd.TargetX - wsx;
                    int wdy = cmd.TargetY - wsy;
                    int wadx = wdx < 0 ? -wdx : wdx;
                    int wady = wdy < 0 ? -wdy : wdy;
                    // Axe principal caster->cible -> orientation perpendiculaire pour la ligne du mur.
                    // Si l'axe principal est X (horizontal) -> mur sur axe Y (vertical) et inversement.
                    int wPerpStepX, wPerpStepY;
                    if (wadx >= wady)
                    {
                        // Axe caster->cible majoritairement X -> mur perpendiculaire en Y.
                        wPerpStepX = 0;
                        wPerpStepY = 1;
                    }
                    else
                    {
                        // Axe caster->cible majoritairement Y -> mur perpendiculaire en X.
                        wPerpStepX = 1;
                        wPerpStepY = 0;
                    }

                    // 3.3.b.iii — option Bible : 1 FD depense (hgSpend >= 1) -> 5 segments au lieu de 3.
                    int murSegments = (hgSpend >= 1)
                        ? SpellRegistry.MurDePierreSegmentsBoosted    // 5
                        : SpellRegistry.MurDePierreSegmentsBase;      // 3
                    int segmentsSpawned = 0;
                    for (int offset = -(murSegments / 2);
                             offset <= murSegments / 2;
                             offset++)
                    {
                        int wx = cmd.TargetX + wPerpStepX * offset;
                        int wy = cmd.TargetY + wPerpStepY * offset;
                        EntityRef wallEntity = ObstacleHelpers.SpawnObstacle(
                            f,
                            ObstacleKind.Wall, SpellRegistry.MurDePierreSegmentHP,
                            wx, wy,
                            owner: casterEntity, ownerPlayerIndex: caster->PlayerIndex,
                            expiresOnTurn: currentTurn + SpellRegistry.MurDePierreTurns);
                        if (wallEntity != EntityRef.None) segmentsSpawned++;
                    }
                    Log.Info($"[Spell] Mur de Pierre : {segmentsSpawned}/{murSegments} segments poses (centre {cmd.TargetX},{cmd.TargetY}, axe perp {wPerpStepX},{wPerpStepY}, option boost FD={hgSpend})");
                    break;
                }

                // -------------------------------------------------------------
                // 3.3.b.iii — Colossar Tactiques Bible-correct (refacto rétroactif)
                // -------------------------------------------------------------

                // Ancrage Bible : 2 PA, range 4, ENEMY. Cible : -2 PM 2 tours + immune push/pull/teleport
                // 1 tour. Pas de damage. Anti-mobilite ultime (anti-teleport Ghostra notamment).
                case SpellId.ColossarAncrage:
                {
                    EntityRef ancrageTarget = GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY);
                    if (ancrageTarget == EntityRef.None
                        || !f.Unsafe.TryGetPointer<Combatant>(ancrageTarget, out Combatant* ancrageC))
                    {
                        Log.Warn($"[Spell] Ancrage : pas de cible sur ({cmd.TargetX},{cmd.TargetY}), no-op");
                        break;
                    }
                    StatusHelper.Apply(ancrageC, StatusKind.MovementMalus,
                        magnitude: SpellRegistry.AncrageMovementMalusMag,
                        turnsLeft: SpellRegistry.AncrageMovementMalusTurns, currentTurn);
                    StatusHelper.Apply(ancrageC, StatusKind.AnchorImmune,
                        magnitude: 0,                       // pas de dmg reduc, juste immune deplacement
                        turnsLeft: SpellRegistry.AncrageImmuneTurns, currentTurn);
                    Log.Info($"[Spell] Ancrage : P{ancrageC->PlayerIndex} -{SpellRegistry.AncrageMovementMalusMag} PM ({SpellRegistry.AncrageMovementMalusTurns}T) + immune push/pull/tp ({SpellRegistry.AncrageImmuneTurns}T)");
                    break;
                }

                // Provocation Bible : 2 PA, range 5, 1 tour. Apply Provoked + -1 PM. Effets passifs :
                // sorts non-ciblant le caster coutent +2 PA pour la cible (hook EffectiveStats), et
                // 100 dmg auto si pas adjacent au caster en fin de SON tour (hook TurnSystem.EnterTurnEnd).
                case SpellId.ColossarProvocation:
                {
                    EntityRef provTarget = GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY);
                    if (provTarget == EntityRef.None
                        || !f.Unsafe.TryGetPointer<Combatant>(provTarget, out Combatant* provC))
                    {
                        Log.Warn("[Spell] Provocation : pas de cible sur la case visee, no-op");
                        break;
                    }
                    StatusHelper.Apply(provC, StatusKind.Provoked,
                        magnitude: caster->PlayerIndex,     // PlayerIndex provocateur (lookup dans hooks)
                        turnsLeft: SpellRegistry.ProvocationTurns, currentTurn);
                    StatusHelper.Apply(provC, StatusKind.MovementMalus,
                        magnitude: SpellRegistry.ProvocationMovementMalusMag,
                        turnsLeft: SpellRegistry.ProvocationMovementMalusTurns, currentTurn);
                    Log.Info($"[Spell] Provocation : P{provC->PlayerIndex} provoque par P{caster->PlayerIndex} pour {SpellRegistry.ProvocationTurns}T (-{SpellRegistry.ProvocationMovementMalusMag} PM, +{SpellRegistry.ProvocationCostBumpNonCible} PA cost sorts non-ciblant, {SpellRegistry.ProvocationAutoDamageNotAdj} dmg auto si pas adjacent fin tour)");
                    break;
                }

                // Brisure Bible : 3 PA range 2, ENEMY. 90 dgts (pipeline standard avec Densite Inerte/
                // Ancrage applies en amont). En PLUS : retire 1 buff/bouclier de la cible. Si pas de
                // buff trouve : applique TRAUMA -2 PA. Priorite Bible : ShieldActive (Peau de Fer/
                // Stoicisme) > RoncesAura (Camouflage Ronces) > AnchorImmune (Stoicisme immune part) >
                // BuffNextOffensiveDmgPercent (Pacte) > RipostMelee (Riposte/Représailles) >
                // RageInsatiableActive. Le 90 dmg est applique par le pipeline (IsOffensive=1).
                case SpellId.ColossarBrisure:
                {
                    EntityRef brisureTarget = GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY);
                    if (brisureTarget == EntityRef.None
                        || !f.Unsafe.TryGetPointer<Combatant>(brisureTarget, out Combatant* brisureC))
                    {
                        Log.Warn($"[Spell] Brisure : pas de cible sur ({cmd.TargetX},{cmd.TargetY}), no-op effet buff");
                        break;
                    }
                    bool buffRemoved = false;
                    if (StatusHelper.Has(brisureC, StatusKind.ShieldActive))
                    {
                        StatusHelper.Consume(brisureC, StatusKind.ShieldActive);
                        Log.Info($"[Spell] Brisure : retire ShieldActive sur P{brisureC->PlayerIndex}");
                        buffRemoved = true;
                    }
                    else if (StatusHelper.Has(brisureC, StatusKind.RoncesAura))
                    {
                        StatusHelper.Consume(brisureC, StatusKind.RoncesAura);
                        Log.Info($"[Spell] Brisure : retire RoncesAura sur P{brisureC->PlayerIndex}");
                        buffRemoved = true;
                    }
                    else if (StatusHelper.Has(brisureC, StatusKind.AnchorImmune))
                    {
                        StatusHelper.Consume(brisureC, StatusKind.AnchorImmune);
                        Log.Info($"[Spell] Brisure : retire AnchorImmune sur P{brisureC->PlayerIndex}");
                        buffRemoved = true;
                    }
                    else if (StatusHelper.Has(brisureC, StatusKind.BuffNextOffensiveDmgPercent))
                    {
                        StatusHelper.Consume(brisureC, StatusKind.BuffNextOffensiveDmgPercent);
                        Log.Info($"[Spell] Brisure : retire BuffNextOffensiveDmgPercent (Pacte de Sang) sur P{brisureC->PlayerIndex}");
                        buffRemoved = true;
                    }
                    else if (StatusHelper.Has(brisureC, StatusKind.RipostMelee))
                    {
                        StatusHelper.Consume(brisureC, StatusKind.RipostMelee);
                        Log.Info($"[Spell] Brisure : retire RipostMelee sur P{brisureC->PlayerIndex}");
                        buffRemoved = true;
                    }
                    else if (StatusHelper.Has(brisureC, StatusKind.RipostAll))
                    {
                        // 3.3.c — Renvoi du Bouclier
                        StatusHelper.Consume(brisureC, StatusKind.RipostAll);
                        Log.Info($"[Spell] Brisure : retire RipostAll (Renvoi du Bouclier) sur P{brisureC->PlayerIndex}");
                        buffRemoved = true;
                    }
                    else if (StatusHelper.Has(brisureC, StatusKind.DamageReductionPercent))
                    {
                        // 3.3.c — Garde Protectrice
                        StatusHelper.Consume(brisureC, StatusKind.DamageReductionPercent);
                        Log.Info($"[Spell] Brisure : retire DamageReductionPercent (Garde Protectrice) sur P{brisureC->PlayerIndex}");
                        buffRemoved = true;
                    }
                    else if (StatusHelper.Has(brisureC, StatusKind.RageInsatiableActive))
                    {
                        StatusHelper.Consume(brisureC, StatusKind.RageInsatiableActive);
                        Log.Info($"[Spell] Brisure : retire RageInsatiableActive sur P{brisureC->PlayerIndex}");
                        buffRemoved = true;
                    }

                    if (!buffRemoved)
                    {
                        // Pas de buff -> TRAUMA -2 PA prochain tour
                        StatusHelper.Apply(brisureC, StatusKind.ActionMalus,
                            magnitude: SpellRegistry.BrisureTraumaPAMag,
                            turnsLeft: SpellRegistry.BrisureTraumaTurns, currentTurn);
                        Log.Info($"[Spell] Brisure : pas de buff sur P{brisureC->PlayerIndex} -> TRAUMA -{SpellRegistry.BrisureTraumaPAMag} PA prochain tour");
                    }
                    break;
                }

                // -------------------------------------------------------------
                // COLOSSAR 3.3.c — SURVIE (handlers)
                // -------------------------------------------------------------

                case SpellId.ColossarStoicisme:
                {
                    // Bible V7.1 : Shield 200 HP / 2 tours + immune push/pull/tp 2 tours.
                    // Tracker StoicismeExpiresOnTurn set a currentTurn + 2 ; TurnSystem fin de round
                    // verifie a expiration si shield.Magnitude > 0 -> heal 80.
                    StatusHelper.Apply(caster, StatusKind.ShieldActive,
                        magnitude: SpellRegistry.StoicismeShieldHP,
                        turnsLeft: SpellRegistry.StoicismeShieldTurns,
                        currentTurn);
                    // AnchorImmune Magnitude=0 (pas de reduction dmg ici, juste immune push/pull/tp).
                    // Bible Ancrage utilise AnchorImmune avec Magnitude=0 aussi (reduction = 0 dans hook damage).
                    StatusHelper.Apply(caster, StatusKind.AnchorImmune,
                        magnitude: 0,
                        turnsLeft: SpellRegistry.StoicismeImmuneTurns,
                        currentTurn);
                    caster->StoicismeExpiresOnTurn = currentTurn + SpellRegistry.StoicismeShieldTurns;
                    Log.Info($"[Spell] Stoicisme : Shield {SpellRegistry.StoicismeShieldHP} + immune push/pull/tp {SpellRegistry.StoicismeImmuneTurns} tours sur P{caster->PlayerIndex} (expiry tour {caster->StoicismeExpiresOnTurn})");
                    break;
                }

                case SpellId.ColossarGardeProtectrice:
                {
                    // Bible V7.1 : -30% dmg subis / 2 tours. Cap combine 50% avec Densite Inerte
                    // (additif clamp via ColossarPassif.GetCombinedDamageReductionPercent).
                    StatusHelper.Apply(caster, StatusKind.DamageReductionPercent,
                        magnitude: SpellRegistry.GardeProtectricePercent,
                        turnsLeft: SpellRegistry.GardeProtectriceTurns,
                        currentTurn);
                    Log.Info($"[Spell] Garde Protectrice : -{SpellRegistry.GardeProtectricePercent}% dmg subis / {SpellRegistry.GardeProtectriceTurns} tours sur P{caster->PlayerIndex} (cap combine {SpellRegistry.MaxCombinedDamageReductionPct}%)");
                    break;
                }

                case SpellId.ColossarRessacVital:
                {
                    // Bible V7.1 : heal 80 + 30/hit subi tour precedent (max +120 = 4 hits cap).
                    int hitsCounted = caster->HitsTakenLastRound;
                    if (hitsCounted > SpellRegistry.RessacVitalHitsCap) hitsCounted = SpellRegistry.RessacVitalHitsCap;
                    int bonusHeal = hitsCounted * SpellRegistry.RessacVitalHealPerHit;
                    int totalHeal = SpellRegistry.RessacVitalHealBase + bonusHeal;
                    int hpBeforeRessac = caster->HP;
                    caster->HP += totalHeal;
                    if (caster->HP > caster->MaxHP) caster->HP = caster->MaxHP;
                    Log.Info($"[Spell] Ressac Vital : +{totalHeal} HP sur P{caster->PlayerIndex} ({hpBeforeRessac} -> {caster->HP}) [base {SpellRegistry.RessacVitalHealBase} + {hitsCounted}/{SpellRegistry.RessacVitalHitsCap} hits last round x{SpellRegistry.RessacVitalHealPerHit}]");
                    break;
                }

                case SpellId.ColossarRenvoiDuBouclier:
                {
                    // Bible V7.1 : RipostAll 60 dgts (melee + distance) / 1 tour / cap 4 retours.
                    // Cap reuse Combatant.RepresaillesReflectsLeft (set a 4).
                    StatusHelper.Apply(caster, StatusKind.RipostAll,
                        magnitude: SpellRegistry.RenvoiBouclierReflectDmg,
                        turnsLeft: SpellRegistry.RenvoiBouclierTurns,
                        currentTurn);
                    caster->RepresaillesReflectsLeft = SpellRegistry.RenvoiBouclierMaxTriggers;
                    Log.Info($"[Spell] Renvoi du Bouclier : RipostAll {SpellRegistry.RenvoiBouclierReflectDmg} dgts ({SpellRegistry.RenvoiBouclierTurns} tour, cap {SpellRegistry.RenvoiBouclierMaxTriggers} retours) sur P{caster->PlayerIndex}");
                    break;
                }

                case SpellId.ColossarSoinLourd:
                {
                    // Bible V7.1 : 3 PA range 3, heal 150 HP self/allie. MVP 1v1 : self-only
                    // (SpellDef Filter=Self range 0). Le case en 2v2/3v3 sera ajoute en Phase 6.
                    int hpBeforeSoin = caster->HP;
                    caster->HP += SpellRegistry.SoinLourdHeal;
                    if (caster->HP > caster->MaxHP) caster->HP = caster->MaxHP;
                    Log.Info($"[Spell] Soin Lourd : +{SpellRegistry.SoinLourdHeal} HP sur P{caster->PlayerIndex} ({hpBeforeSoin} -> {caster->HP})");
                    break;
                }

                // -------------------------------------------------------------
                // COLOSSAR 3.3.d — SIGNATURE EFFONDREMENT (handler cast)
                // -------------------------------------------------------------

                case SpellId.ColossarEffondrement:
                {
                    // Bible V7.1 + design Lorenzo (swap anti-fuite) : ANNONCE 1 tour a l'avance.
                    // Snapshot au cast : ennemi LE PLUS PROCHE actuellement dans le rayon 2.
                    // Au trigger N+1, meme si cet ennemi a quitte la zone, il sera teleporte
                    // AU CENTRE des Failles (= case ex-caster), et le caster ira sur la case
                    // actuelle de l'ennemi (= position fuite). Mindgame "le Colossar dicte".
                    //
                    // FD : les 3 HG mandatory (= 3 FD) ont deja ete consommes par le pipeline standard
                    // de cost. FD revient automatiquement a 0 vu qu'il etait au cap 3.
                    caster->EffondrementAnnouncedOnTurn = currentTurn;
                    caster->LastEffondrementUsedOnTurn = currentTurn;

                    // Snapshot ennemi le plus proche dans rayon 2 (Manhattan).
                    EntityRef targetSnapshot = EntityRef.None;
                    int bestDist = int.MaxValue;
                    int cxCast = caster->GridX;
                    int cyCast = caster->GridY;
                    int eRadius = SpellRegistry.EffondrementAoeRadius;
                    var enemyScanFilter = f.Filter<Combatant>();
                    while (enemyScanFilter.NextUnsafe(out EntityRef eEntity, out Combatant* eC))
                    {
                        if (eC->PlayerIndex == caster->PlayerIndex) continue;
                        if (eC->HP <= 0) continue;
                        int dxE = eC->GridX - cxCast;
                        int dyE = eC->GridY - cyCast;
                        int adX = dxE < 0 ? -dxE : dxE;
                        int adY = dyE < 0 ? -dyE : dyE;
                        int distE = adX + adY;
                        if (distE == 0 || distE > eRadius) continue;
                        if (distE < bestDist)
                        {
                            bestDist = distE;
                            targetSnapshot = eEntity;
                        }
                    }
                    caster->EffondrementTargetEntity = targetSnapshot;
                    if (targetSnapshot != EntityRef.None)
                    {
                        Log.Info($"[Spell] Effondrement ANNONCE par P{caster->PlayerIndex} (tour {currentTurn}). Cible snapshot dist {bestDist}. Trigger au prochain sub-turn (swap au trigger).");
                    }
                    else
                    {
                        Log.Info($"[Spell] Effondrement ANNONCE par P{caster->PlayerIndex} (tour {currentTurn}). Aucun ennemi en zone au cast -> pas de swap, juste Failles + buff au trigger.");
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// 3.3.b.i — Liste explicite des sorts qui requierent une ligne de vue claire
        /// caster -> case ciblee. Bible V7.1 : "Pilier/Mur bloque lignes de vue/tir".
        ///
        /// Sont concernes les sorts DIRECTS a distance (range >= 2) qui visent une case
        /// precise (Single/AoE centree sur target). Sont EXCLUS :
        ///   - Sorts melee (range 1, pas d'intermediaire) : Tranche-Ame, Ouvre-Plaie,
        ///     Frappe Lourde, Represailles, Ame Laceree.
        ///   - Sorts Self / AoE caster (Filter Self) : Pacte de Sang, Rugissement, Souffle
        ///     Glacial, Onde de Choc, Peau de Fer, Rage Insatiable, Riposte Carmin, etc.
        ///   - Sorts en LIGNE custom qui gerent leur propre arret obstacle dans leur handler :
        ///     Charge Brutale, Volee d'Epines, Choc Sismique.
        ///   - Teleport (Bible : "ignore les obstacles") : Pas Furtif, Evanescence, Traquenard.
        ///   - Sorts qui posent quelque chose a courte portee non bloque (Pilier 3.3.b range 1).
        /// </summary>
        private static bool SpellNeedsLineOfSight(SpellId id)
        {
            switch (id)
            {
                // Soulrender distance.
                case SpellId.SoulrenderMarqueDeCarnage:    // range 5, single target
                case SpellId.SoulrenderEmpoignade:         // range 3, pull
                case SpellId.SoulrenderDetonationSanglante: // range 4, AoE croix
                case SpellId.SoulrenderCuree:              // range 2, single target
                // Nightseer distance.
                case SpellId.NightseerTirPrecis:           // range 6
                case SpellId.NightseerFrappeDeLOmbre:      // range 3
                case SpellId.NightseerDetonationOnirique:  // range 5, AoE 2x2
                case SpellId.NightseerSalveMortelle:       // range 6, croix 5
                case SpellId.NightseerMarqueDuChasseur:    // range 5
                case SpellId.NightseerFiletDeRonces:       // range 4, pose trap
                case SpellId.NightseerChampDeMines:        // range 3, AoE 3x3
                case SpellId.NightseerBourrasque:          // range 5, push
                // Colossar distance.
                case SpellId.ColossarMarteauPunisseur:     // range 2
                case SpellId.ColossarMurDePierre:          // range 2 (pose, mais Bible : on doit voir l'emplacement)
                // Necram tactiques (3.5.b.i / 3.5.b.iv).
                case SpellId.NecramInoculation:            // range 5, apply marque
                case SpellId.NecramMarqueSacrificielle:    // range 5, apply status
                case SpellId.NecramContagion:              // range 5, propagation marques
                    return true;
                default:
                    return false;
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
            PushAndTriggerEx(f, targetC, targetEntity, casterX, casterY, distance, currentTurn, caster, out _);
        }

        /// <summary>
        /// 3.3.a.ii — Variante de PushAndTrigger qui expose le motif d'arret au caller.
        /// Utilise par Onde de Choc Colossar (Bible : +80 dgts + TRAUMA si push s'arrete contre
        /// mur/Pilier/bord). PushAndTrigger classique = wrapper appelant avec out _.
        /// </summary>
        private static void PushAndTriggerEx(Frame f, Combatant* targetC, EntityRef targetEntity,
            int casterX, int casterY, int distance, int currentTurn, Combatant* caster,
            out bool stoppedAgainstObstacleOrBorder)
        {
            stoppedAgainstObstacleOrBorder = false; // assigned avant tout early return
            // 3.3.b.ii — Ancrage : cible AnchorImmune ne peut pas etre poussee.
            // Pas de FD gain pour le caster Colossar (pas de "push contre obstacle" survenu).
            if (StatusHelper.Has(targetC, StatusKind.AnchorImmune))
            {
                Log.Info($"[Ancrage] Push annule sur P{targetC->PlayerIndex} (AnchorImmune actif)");
                return;
            }
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
            stoppedAgainstObstacleOrBorder = stoppedAgainstObstacle || stoppedAgainstBorder;
            if (caster != null && stoppedAgainstObstacleOrBorder)
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
