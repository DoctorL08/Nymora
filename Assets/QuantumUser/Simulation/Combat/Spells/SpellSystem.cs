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

            // Refonte 29 mai : round courant pour le passif "-1 PA sur le 1er sort du tour".
            int turnForPACost = f.TryGetSingleton<CombatState>(out var statePA) ? statePA.TurnNumber : 0;
            int effectivePACost = EffectiveStats.GetPACost(spellDef, caster, targetHPRatio, turnForPACost);

            // Patch 7 juin — Provocation : le surcout +1 PA (sur TOUS les sorts du provoque) est
            //   desormais applique DANS EffectiveStats.GetPACost (centralise, plus d'exception cible).

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

            // 3.5.c.v — Cocon Putride : conditionnel HP < 30% MaxHP (Bible V7.1 panic signature).
            // Check avant consommation PA pour eviter de "perdre" le cast (style Dernier Souffle).
            // Le check OncePerMatch est gere par le systeme generique (OncePerMatchBit/Flags).
            if (cmd.Spell == SpellId.NecramCoconPutride)
            {
                if (caster->HP * 100 >= caster->MaxHP * SpellRegistry.CoconPutrideHpThresholdPct)
                {
                    Log.Warn($"[Spell] rejet : Cocon Putride requiert HP < {SpellRegistry.CoconPutrideHpThresholdPct}% (actuel {caster->HP}/{caster->MaxHP})");
                    return;
                }
            }

            // 3.7.c.iv — Dernier Pas : conditionnel HP < 30% MaxHP (Bible V7.1 panic-button Ghostra).
            // Check avant consommation PA (style Cocon Putride / Dernier Souffle / Evanescence).
            // Rejet propre si HP >= 30% : PA NON consume, OncePerMatchBit NON consume.
            if (cmd.Spell == SpellId.GhostraDernierPas)
            {
                if (caster->HP * 100 >= caster->MaxHP * SpellRegistry.DernierPasHpThresholdPct)
                {
                    Log.Warn($"[Spell] rejet : Dernier Pas requiert HP < {SpellRegistry.DernierPasHpThresholdPct}% (actuel {caster->HP}/{caster->MaxHP})");
                    return;
                }
            }

            // 3.7.d — Execution Spectrale : pre-PA gates SIGNATURE Ghostra.
            //   (1) Requiert 3 leurres actifs (Bible 3/3 LEURRES requirement). Rejet propre
            //       si CountActive != 3 (peut etre 0, 1 ou 2 - pas de leurres dispos).
            //   (2) Requiert cooldown expire (4 tours). currentTurn - LastUsed >= 4.
            //   Si gates OK -> cast accepte, PA consume + custom path handler (le check dorsal
            //   est dans le handler, pas en pre-PA, car Bible "RATE consume leurres + PA + cooldown").
            if (cmd.Spell == SpellId.GhostraExecutionSpectrale)
            {
                int activeDecoys = DecoyHelpers.CountActive(caster);
                if (activeDecoys != SpellRegistry.ExecutionSpectraleRequiredDecoys)
                {
                    Log.Warn($"[Spell] rejet : Execution Spectrale requiert {SpellRegistry.ExecutionSpectraleRequiredDecoys} leurres actifs (actuel {activeDecoys})");
                    return;
                }
                int currentTurnES = f.TryGetSingleton<CombatState>(out var stateES) ? stateES.TurnNumber : 0;
                int cooldownDelta = currentTurnES - caster->LastExecutionSpectraleUsedOnTurn;
                if (cooldownDelta < SpellRegistry.ExecutionSpectraleCooldownTurns)
                {
                    Log.Warn($"[Spell] rejet : Execution Spectrale en cooldown ({SpellRegistry.ExecutionSpectraleCooldownTurns - cooldownDelta} tours restants, dernier cast T{caster->LastExecutionSpectraleUsedOnTurn})");
                    return;
                }
            }

            // 3.5.c.vi — Virus Fatal (SIGNATURE Necram) : cooldown 4 tours apres usage. Pattern
            // identique Ame Laceree / Traquenard / Effondrement. Check AVANT consume PA pour
            // eviter de "perdre" le cast. Gate Resource >= 6 (PT cap) gere par le pipeline
            // generique HGCostMandatory=6 plus bas.
            if (cmd.Spell == SpellId.NecramVirusFatal)
            {
                int currentTurnVF = f.TryGetSingleton<CombatState>(out var stateVF) ? stateVF.TurnNumber : 0;
                int turnsSinceUseVF = currentTurnVF - caster->LastVirusFatalUsedOnTurn;
                if (turnsSinceUseVF < SpellRegistry.VirusFatalCooldownTurns)
                {
                    Log.Warn($"[Spell] rejet : Virus Fatal en cooldown ({turnsSinceUseVF}/{SpellRegistry.VirusFatalCooldownTurns} tours depuis dernier usage tour {caster->LastVirusFatalUsedOnTurn})");
                    return;
                }
            }

            // 3.7.b.ii — Pas dans l'Ombre : cap 1x/tour (decision Lorenzo). Reject AVANT consume PA.
            // Pattern Permutation : check LastPasDansLOmbreOnTurn == currentTurn.
            if (cmd.Spell == SpellId.GhostraPasDansLOmbre)
            {
                int currentTurnPDO = f.TryGetSingleton<CombatState>(out var statePDO) ? statePDO.TurnNumber : 0;
                if (caster->LastPasDansLOmbreOnTurn == currentTurnPDO)
                {
                    Log.Warn($"[Spell] rejet : Pas dans l'Ombre deja utilise ce tour (round {currentTurnPDO}, cap 1x/tour)");
                    return;
                }
            }

            // Patch 7 juin — sorts INTERDITS au TOUR 1 (decision Lorenzo) : Pas dans l'Ombre (Ghostra),
            //   Pas Furtif (Nightseer) + Affut (Marque du Chasseur, Nightseer). Reject AVANT consume PA
            //   (le tour n'est pas gache). TurnNumber demarre a 1 au 1er round (cf TurnSystem).
            if (cmd.Spell == SpellId.GhostraPasDansLOmbre
                || cmd.Spell == SpellId.NightseerPasFurtif
                || cmd.Spell == SpellId.NightseerMarqueDuChasseur)
            {
                int currentTurnT1 = f.TryGetSingleton<CombatState>(out var stateT1) ? stateT1.TurnNumber : 0;
                if (currentTurnT1 <= 1)
                {
                    Log.Warn($"[Spell] rejet : {cmd.Spell} interdit au tour 1 (round {currentTurnT1})");
                    return;
                }
            }

            // 3.7.b — Éveil Spectral (ex-Dague Lancée slot 93) : le cap 2x/tour passe désormais par
            //   le moteur générique (SpellDef.MaxUsesPerTurn). Gate dédié de présence du leurre
            //   plus bas (après validation de la cible ennemie).

            // 3.7.a.iii — Frappe Fantome : pre-check case libre adjacente target. Reject AVANT consume PA
            // si aucune case dispo (4 cardinaux Manhattan=1 autour target tous occupes/obstacles/hors grille).
            // Priorite case DORSALE (derriere target.Facing) pour combo dorsal garanti.
            // Le teleport effectif a lieu plus loin (juste avant la boucle damage), apres consume PA.
            if (cmd.Spell == SpellId.GhostraFrappeFantome)
            {
                // Resolve target pour avoir target.Facing (pre-check ne consomme rien).
                EntityRef ffPrecheckTarget = GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY);
                IsoFacing ffPrecheckFacing = IsoFacing.SE; // default fallback safe (anyway target sera resolu de toute facon)
                if (ffPrecheckTarget != EntityRef.None
                    && f.Unsafe.TryGetPointer<Combatant>(ffPrecheckTarget, out Combatant* ffPrecheckTargetC))
                {
                    ffPrecheckFacing = ffPrecheckTargetC->Facing;
                }

                if (!TryFindFreeCellAdjacentToTarget(f, cmd.TargetX, cmd.TargetY, ffPrecheckFacing,
                        out int _ffPrecheckX, out int _ffPrecheckY))
                {
                    Log.Warn($"[Spell] rejet : Frappe Fantome impossible, aucune case libre adjacente target ({cmd.TargetX},{cmd.TargetY})");
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
                // Refonte 29 mai : plus d'annonce différée -> plus de garde-fou "déjà annoncé".
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

            // ===== Cout en ressource de classe (HG/PR/FD/PT) : mandatory + optionnel AUTO =====
            // Brique juin 2026 (decision Lorenzo : "depense si dispo") — le bind manuel "Shift+X"
            //   ayant ete retire le 17 mai, cmd.HGSpend arrive TOUJOURS a 0 cote joueur -> les bonus
            //   de consommation (Ouvre-Plaie +120, Seve Vive +60, Detonation Sanglante, Mur de Pierre
            //   5 segments, Pas Furtif voile, Symbiose +30 HP, Pas dans l'Ombre leurre, Detonation
            //   Onirique portee 10, Contagion) ne partaient JAMAIS, et la ressource n'etait pas
            //   consommee. On depense desormais AUTOMATIQUEMENT le maximum optionnel finançable APRES
            //   le cout obligatoire. Trade-off assume : on ne peut plus "banker" sa ressource pour la
            //   signature (Ame Laceree 5 HG / Effondrement 5 FD / Virus Fatal 6 PT) en castant ces
            //   sorts a bas cout. cmd.HGSpend conserve comme PLAFOND optionnel : un appelant (IA / futur
            //   UI) qui met HGSpend > 0 borne l'auto ; 0 (cas joueur actuel) = auto au max.
            int optionalBudget = caster->Resource - spellDef.HGCostMandatory; // ressource restante apres mandatory
            if (optionalBudget < 0) optionalBudget = 0;
            int hgSpend = spellDef.HGCostMaxOptional;
            if (cmd.HGSpend > 0 && cmd.HGSpend < hgSpend) hgSpend = cmd.HGSpend; // plafond explicite si fourni
            if (hgSpend > optionalBudget) hgSpend = optionalBudget;            // borne par ce qui est finançable
            if (hgSpend < 0) hgSpend = 0;
            int totalHgCost = spellDef.HGCostMandatory + hgSpend;
            if (caster->Resource < totalHgCost)
            {
                // Ne peut pas payer le cout OBLIGATOIRE (l'optionnel est deja borne au finançable).
                Log.Warn($"[Spell] rejet : ressource {caster->Resource} < cout obligatoire {spellDef.HGCostMandatory} ({cmd.Spell})");
                return;
            }

            // Range Manhattan caster -> target.
            int dx = cmd.TargetX - caster->GridX;
            int dy = cmd.TargetY - caster->GridY;
            int absDx = dx < 0 ? -dx : dx;
            int absDy = dy < 0 ? -dy : dy;
            int dist = absDx + absDy;

            // Equilibrage 6 juin : option "2 PR -> portee 10" de Detonation Onirique RETIREE (trop cheat).
            //   La portee reste fixe (RangeMax du SpellDef). Le bonus de phase P2+ ci-dessous s'applique
            //   toujours normalement.
            int effectiveRangeMax = spellDef.RangeMax;
            // Patch 7 juin — le +1 portée de la phase 2 Nightseer a été RETIRÉ (décision Lorenzo).
            //   La phase 2 ne donne plus que le +30 dégâts flat (cf NightseerPassif.FlatDamageBonusActive).
            // Affut (patch 7 juin, ex-Marque du Chasseur) : +2 portee sur les sorts a distance tant
            //   que le self-buff AffutActive est actif (RangeMax >= 1 exclut les sorts self).
            if (spellDef.RangeMax >= 1 && StatusHelper.Has(caster, StatusKind.AffutActive))
            {
                effectiveRangeMax += SpellRegistry.AffutRangeBonus;
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
                // PATCH 22 mai (test designer) + brique juin 2026 — Murs/Piliers/Failles ciblables
                // par les sorts de degats (toutes classes). Le filtre Enemy/AnyUnit rejette une case
                // sans unite, mais on veut pouvoir viser un OBSTACLE pour le detruire (Bible : Pilier/
                // Mur destructibles). Vaut pour l'obstacle ADVERSE (le detruire) ET — depuis brique juin
                // 2026 — pour l'obstacle OWN : le Colossar peut TAPER SES PROPRES Piliers/Murs/Failles
                // (degager une Faille genante, abattre un Mur, casser un Pilier -> Densite Inerte +30 HP).
                // La boucle damage offensive (plus bas) applique les degats a l'obstacle present (own
                // ou adverse) sur la case d'effet.
                bool offensiveObstacleTarget =
                    spellDef.IsOffensive != 0
                    && (spellDef.Filter == TargetingFilter.Enemy || spellDef.Filter == TargetingFilter.AnyUnit)
                    && ObstacleHelpers.HasObstacleAt(f, cmd.TargetX, cmd.TargetY);

                // PATCH #6 — un sort cible-ennemi peut viser une case occupee par un LEURRE Ghostra
                // ennemi (Bible : leurres indiscernables -> ciblables comme la vraie Ghostra). La
                // resolution (consommation du leurre) est geree plus bas (boucle damage / interception
                // non-offensive). Vaut pour les sorts offensifs ET non-offensifs (marques/debuffs).
                bool targetIsEnemyDecoy =
                    (spellDef.Filter == TargetingFilter.Enemy || spellDef.Filter == TargetingFilter.AnyUnit)
                    && DecoyHelpers.HasEnemyDecoyAt(f, playerIndex, cmd.TargetX, cmd.TargetY);

                if (!offensiveObstacleTarget && !targetIsEnemyDecoy)
                {
                    Log.Warn($"[Spell] rejet : ({cmd.TargetX},{cmd.TargetY}) ne match pas filter {spellDef.Filter}");
                    return;
                }
                if (offensiveObstacleTarget)
                    Log.Info($"[Spell] cible obstacle (own ou adverse) autorisee sur ({cmd.TargetX},{cmd.TargetY}) (sort offensif {cmd.Spell})");
                else
                    Log.Info($"[Spell] cible leurre ennemi autorisee sur ({cmd.TargetX},{cmd.TargetY}) ({cmd.Spell})");
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

            // Sorts en LIGNE DROITE cardinale : la cible doit etre alignee avec le caster (meme
            // ligne OU meme colonne). Couvre Choc Sismique, Charge Brutale ET Volée d'Épines
            // (cf SpellIsStraightLine). Sans ce garde, une cible diagonale est acceptee a portee
            // Manhattan puis snappee sur l'axe dominant -> "range diagonale" incoherente.
            // (dx/dy deja calcules plus haut = cmd.Target - caster->Grid.)
            if (SpellIsStraightLine(cmd.Spell) && dx != 0 && dy != 0)
            {
                Log.Warn($"[Spell] rejet : {cmd.Spell} cible non alignee (ligne droite cardinale requise) dx={dx} dy={dy}");
                return;
            }

            // Refonte 29 mai + brique juin 2026 — Éboulement (ex-Soin Lourd) : requiert un de TES
            //   obstacles sur la case ciblée. Avant, seul un Pilier était accepté ; on étend aux Murs
            //   et Failles (le handler détruit l'obstacle quel que soit son Kind, l'AoE 150 + le push
            //   s'appliquent pareil ; seul le +30 HP Densité Inerte reste réservé au Pilier dans
            //   DestroyObstacle). Reject AVANT consommation PA (pas de cast à vide).
            if (cmd.Spell == SpellId.ColossarSoinLourd)
            {
                EntityRef ebObs = ObstacleHelpers.GetObstacleAt(f, cmd.TargetX, cmd.TargetY);
                bool ownObstacle = ebObs != EntityRef.None
                    && f.Unsafe.TryGetPointer<Obstacle>(ebObs, out Obstacle* ebObsC)
                    && ebObsC->OwnerPlayerIndex == caster->PlayerIndex
                    && ebObsC->HP > 0;
                if (!ownObstacle)
                {
                    Log.Warn($"[Spell] rejet : Éboulement requiert un de tes obstacles (Pilier/Mur/Faille) sur ({cmd.TargetX},{cmd.TargetY})");
                    return;
                }
            }

            // Fix 2 juin — Garde ANTI-TELEPORT : un caster sous AnchorImmune (Ancrage / Stoicisme) ou
            //   AntiTeleport (Rugissement) ne peut PAS lancer un sort qui le teleporte (Bible : "rien
            //   ne me deplace"). Avant, ces statuts ne bloquaient que les deplacements SUBIS, pas les
            //   self-teleports -> NS ancre se TP quand meme. Reject AVANT consommation PA.
            if (SpellIsSelfTeleport(cmd.Spell)
                && (StatusHelper.Has(caster, StatusKind.AnchorImmune)
                    || StatusHelper.Has(caster, StatusKind.AntiTeleport)))
            {
                Log.Warn($"[Spell] rejet : {cmd.Spell} bloque (caster P{caster->PlayerIndex} sous Ancrage/AntiTeleport — rien ne le deplace). PA non consomme.");
                return;
            }

            // Fix 2 juin — Pilier : pas de pose sur une case qui porte une EMBUCHE (piege) ou un
            //   LEURRE. Reject AVANT consommation PA (pose unique -> sinon tour gaspille). Le Mur
            //   (multi-segments) saute juste les segments concernes cote SpawnObstacle, donc pas de
            //   pre-check ici pour lui (pose partielle acceptable). SpawnObstacle garde la regle par-case.
            if (cmd.Spell == SpellId.ColossarPilier)
            {
                if (FogHelpers.GetTrapOwner(f, cmd.TargetX, cmd.TargetY) != -1
                    || DecoyHelpers.HasAnyDecoyAt(f, cmd.TargetX, cmd.TargetY))
                {
                    Log.Warn($"[Spell] rejet : Pilier sur ({cmd.TargetX},{cmd.TargetY}) impossible (embuche ou leurre present). PA non consomme.");
                    return;
                }
            }

            // Fix 2 juin — pose d'EMBUCHE directe (Filet de Ronces, Piège Bondissant) : pas sur une
            //   case occupee par un combattant (joueur), un obstacle (Pilier/Mur/Faille) ou un leurre.
            //   Reject AVANT consommation PA (pose unique -> sinon tour gaspille). Le Champ de Mines
            //   (cluster) saute les cases concernees cote PlaceTrap (pose partielle acceptable).
            if (cmd.Spell == SpellId.NightseerFiletDeRonces || cmd.Spell == SpellId.NightseerSouffleGlacial)
            {
                if (GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY) != EntityRef.None
                    || ObstacleHelpers.HasObstacleAt(f, cmd.TargetX, cmd.TargetY)
                    || DecoyHelpers.HasAnyDecoyAt(f, cmd.TargetX, cmd.TargetY)
                    || FogHelpers.GetTrapKind(f, cmd.TargetX, cmd.TargetY) != TrapKind.None) // #12 (5 juin) : pas sur un piège existant
                {
                    Log.Warn($"[Spell] rejet : {cmd.Spell} sur ({cmd.TargetX},{cmd.TargetY}) impossible (case occupee : joueur, obstacle, leurre ou piege). PA non consomme.");
                    return;
                }
            }

            // Refonte 29 mai — Détonation Virulente : requiert une cible ennemie MARQUÉE (venin)
            //   vivante. Reject AVANT consommation PA (pas de tick à vide).
            if (cmd.Spell == SpellId.NecramDetonationVirulente)
            {
                EntityRef dvGateTarget = GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY);
                bool dvHasMarked = dvGateTarget != EntityRef.None
                    && f.Unsafe.TryGetPointer<Combatant>(dvGateTarget, out Combatant* dvGateC)
                    && dvGateC->PlayerIndex != caster->PlayerIndex
                    && dvGateC->HP > 0
                    && dvGateC->VeninStacks > 0;
                if (!dvHasMarked)
                {
                    Log.Warn($"[Spell] rejet : Détonation Virulente requiert une cible marquée (venin) vivante en ({cmd.TargetX},{cmd.TargetY})");
                    return;
                }
            }

            // 3.7.b — Éveil Spectral (refonte 30 mai) : requiert un de tes leurres ADJACENT (1 case)
            //   à la cible ennemie. Reject AVANT consommation PA. On mémorise le leurre choisi
            //   (dorsal prioritaire) dans eveilLeurreX/Y + eveilDorsal pour que le pipeline de
            //   dégâts calcule le bonus dorsal + la Plaie depuis le LEURRE (et non depuis la Ghostra).
            // #20 (5 juin) — Éveil Spectral REWORK auto-TP : on PLANIFIE (sans muter) le téléport d'un de
            //   tes leurres sur une case libre adjacente (dorsal-prioritaire) à la cible. Reject pré-PA si
            //   aucun leurre OU aucune case adjacente libre. Le déplacement réel est fait APRÈS le commit
            //   du cast (plus bas) pour ne pas bouger un leurre si un gate ultérieur (cap/relance) rejette.
            int eveilLeurreX = -1, eveilLeurreY = -1;
            bool eveilDorsal = false;
            int eveilSlot = -1;
            if (cmd.Spell == SpellId.GhostraEveilSpectral)
            {
                EntityRef esTarget = GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY);
                if (esTarget == EntityRef.None
                    || !f.Unsafe.TryGetPointer<Combatant>(esTarget, out Combatant* esTargetC)
                    || esTargetC->PlayerIndex == caster->PlayerIndex
                    || esTargetC->HP <= 0
                    || !DecoyHelpers.TryPlanEveilAutoTeleport(f, caster, esTargetC, out eveilSlot, out eveilLeurreX, out eveilLeurreY, out eveilDorsal))
                {
                    Log.Warn($"[Spell] rejet : Éveil Spectral requiert au moins un leurre actif ET une case libre adjacente à la cible en ({cmd.TargetX},{cmd.TargetY})");
                    return;
                }
                Log.Info($"[Éveil Spectral] plan auto-TP leurre slot {eveilSlot} -> ({eveilLeurreX},{eveilLeurreY}) dorsal={eveilDorsal} sur cible ({cmd.TargetX},{cmd.TargetY})");
            }

            // 3.7.c.ii — Voile Spectral (rework) : requiert au moins 1 leurre actif à téléporter.
            //   Reject AVANT consommation PA (la cible ennemie est déjà validée par MatchesFilter).
            if (cmd.Spell == SpellId.GhostraVoileSpectral && DecoyHelpers.CountActive(caster) <= 0)
            {
                Log.Warn($"[Spell] rejet : Voile Spectral requiert au moins 1 leurre actif");
                return;
            }

            // 3.7.c — Communion Spectrale : requiert au moins 1 leurre actif à consommer.
            //   Reject AVANT consommation PA (pas de heal à vide).
            if (cmd.Spell == SpellId.GhostraCommunionSpectrale && DecoyHelpers.CountActive(caster) <= 0)
            {
                Log.Warn($"[Spell] rejet : Communion Spectrale requiert au moins 1 leurre actif");
                return;
            }

            // Refonte 29 mai — GATE GENERIQUE limites/relances (cap Nx/tour + relance N tours).
            //   Declare par sort dans SpellDef.MaxUsesPerTurn / CooldownTurns. Reject AVANT
            //   consommation des PA (pas de cast "perdu"). No-op si les deux valent 0 (defaut).
            {
                int genTurn = f.TryGetSingleton<CombatState>(out var stGen) ? stGen.TurnNumber : 0;
                if (SpellLimitsHelper.CapReached(caster, cmd.Spell, spellDef.MaxUsesPerTurn, genTurn))
                {
                    Log.Warn($"[Spell] rejet : {cmd.Spell} cap {spellDef.MaxUsesPerTurn}x/tour atteint (round {genTurn})");
                    return;
                }
                if (SpellLimitsHelper.OnCooldown(caster, cmd.Spell, spellDef.CooldownTurns, genTurn, out int genRem))
                {
                    Log.Warn($"[Spell] rejet : {cmd.Spell} en relance ({genRem} tour(s) restant(s))");
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

            // Refonte 29 mai — COMMIT limites/relances : journalise le cast (caps Nx/tour) et
            //   arme la relance si CooldownTurns > 0. Apres consommation PA = cast valide.
            SpellLimitsHelper.RecordCast(caster, cmd.Spell, currentTurn);
            if (spellDef.CooldownTurns > 0)
            {
                SpellLimitsHelper.SetCooldown(caster, cmd.Spell, currentTurn);
            }

            // #20/#21 (5 juin) — mutations de leurres Ghostra APRÈS le commit du cast (PA + cap/relance
            //   validés), AVANT le calcul de dégâts (pour que CountOwnDecoysAdjacent soit à jour).
            if (cmd.Spell == SpellId.GhostraEveilSpectral && eveilSlot >= 0)
            {
                // #20 : auto-TP du leurre choisi sur la case adjacente (dorsal-prioritaire) planifiée au gate.
                caster->Decoys[eveilSlot].PosX = eveilLeurreX;
                caster->Decoys[eveilSlot].PosY = eveilLeurreY;
                Log.Info($"[Éveil Spectral] leurre slot {eveilSlot} auto-TP -> ({eveilLeurreX},{eveilLeurreY})");
            }
            else if (cmd.Spell == SpellId.GhostraVoileSpectral)
            {
                // #21 : TP de TOUS les leurres autour de la cible (les dégâts/leurre adjacent sont calculés
                //   juste après dans la section effectiveDmg, sur les positions POST-TP).
                int movedV = DecoyHelpers.TeleportAllDecoysAround(f, caster, cmd.TargetX, cmd.TargetY);
                Log.Info($"[Voile Spectral] {movedV} leurre(s) téléporté(s) autour de la cible ({cmd.TargetX},{cmd.TargetY})");
            }

            // ===== Calcul damage effectif (buffs + HG variants) =====
            int effectiveDmg = spellDef.DamageAmount;
            // 3.7.a — Nuée Spectrale (refonte 30 mai) : burst cible-unique qui SCALE avec les leurres.
            //   base 100 + 70 par leurre ACTIF + 30 par leurre ADJACENT à la cible. Ne consomme pas.
            //   Le scaling REMPLACE le bonus dorsal (skip dorsal + Plaie plus bas pour ce sort).
            if (cmd.Spell == SpellId.GhostraNueeSpectrale)
            {
                int nueeActive = DecoyHelpers.CountActive(caster);
                int nueeAdjacent = DecoyHelpers.CountOwnDecoysAdjacent(caster, cmd.TargetX, cmd.TargetY);
                effectiveDmg = SpellRegistry.NueeSpectraleBaseDamage
                             + SpellRegistry.NueeSpectralePerLeurre * nueeActive
                             + SpellRegistry.NueeSpectralePerAdjacent * nueeAdjacent;
                Log.Info($"[Nuée Spectrale] dmg {effectiveDmg} = {SpellRegistry.NueeSpectraleBaseDamage} + {SpellRegistry.NueeSpectralePerLeurre}x{nueeActive} leurres + {SpellRegistry.NueeSpectralePerAdjacent}x{nueeAdjacent} adjacents");
            }
            // #21 (5 juin) — Voile Spectral : 60 dmg par leurre désormais ADJACENT à la cible (le TP a été
            //   fait juste au-dessus, après commit), cap 180 (= 3 leurres). Passe par le pipeline standard
            //   (boucliers/réductions de la cible). 0 leurre adjacent (cas rare, tous bloqués) -> 0 dmg.
            if (cmd.Spell == SpellId.GhostraVoileSpectral)
            {
                int voileAdj = DecoyHelpers.CountOwnDecoysAdjacent(caster, cmd.TargetX, cmd.TargetY);
                effectiveDmg = voileAdj * SpellRegistry.VoileSpectralDmgPerAdjacent;
                if (effectiveDmg > SpellRegistry.VoileSpectralDmgMax) effectiveDmg = SpellRegistry.VoileSpectralDmgMax;
                Log.Info($"[Voile Spectral] dmg {effectiveDmg} = {SpellRegistry.VoileSpectralDmgPerAdjacent} x {voileAdj} leurres adjacents (cap {SpellRegistry.VoileSpectralDmgMax})");
            }
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
            // Patch 7 juin — FIX cast == preview : les buffs OFFENSIFS du caster (Pacte %, Peau de Fer
            //   flat mêlée, Frénésie %, Affût %, Sang Bouillant flat) ne sont PLUS appliqués ici en
            //   amont sur effectiveDmg. Ils étaient PERDUS par les sorts qui recalculent dmgThisTarget
            //   par cible (Tir Précis Traqué -> 210, Frappe +120) ou à DamageAmount=0 (Salve), d'où
            //   "Affût n'applique pas le +10% au cast" alors que le preview (FinalizeOffensive) l'affichait.
            //   Ils sont désormais appliqués PAR CIBLE via ApplyOffensiveCasterBuffs (après les bonus flat
            //   phase/Densité/Marque), exactement comme SpellPreview.FinalizeOffensive.
            //   On garde ici les déclarations servant à la CONSOMMATION post-loop (Pacte / Sang Bouillant),
            //   + une valeur buffée pour les cibles SANS bonus per-cellule (leurres / obstacles).
            int pacteBuffPct = StatusHelper.GetMagnitude(caster, StatusKind.BuffNextOffensiveDmgPercent, 0);
            int nextStrikeBonus = StatusHelper.GetMagnitude(caster, StatusKind.NextStrikeBonus, 0);
            int effectiveDmgBuffed = ApplyOffensiveCasterBuffs(caster, spellDef.RangeMax == 1, effectiveDmg);

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

            // ===== PATCH #6 — sort NON-offensif ciblant directement un leurre ennemi =====
            // Les sorts OFFENSIFS gerent le leurre dans la boucle damage ci-dessous (par case d'effet,
            // AoE incluse). Pour un sort NON-offensif a cible ennemi (marque/debuff), viser la case
            // d'un leurre = interaction : Standard/RepliqueFantome consommes (le faux est revele,
            // heal Bible), Protective survit (encaisse 0 dmg). PA deja consomme (cast "rate" sur un faux).
            if (spellDef.IsOffensive == 0
                && (spellDef.Filter == TargetingFilter.Enemy || spellDef.Filter == TargetingFilter.AnyUnit)
                && GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY) == EntityRef.None
                && DecoyHelpers.TryFindEnemyDecoyForCaster(f, caster->PlayerIndex, cmd.TargetX, cmd.TargetY,
                    out Combatant* nbDecoyG, out int nbDecoySlot))
            {
                bool nbDestroyed = DecoyHelpers.HitDecoyByEnemyAction(nbDecoyG, nbDecoySlot, 0);
                Log.Info($"[Spell] {cmd.Spell} (non-offensif) cible leurre ennemi P{nbDecoyG->PlayerIndex} en ({cmd.TargetX},{cmd.TargetY}) -> {(nbDestroyed ? "DETRUIT" : "Protective survit")}");
                return;
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
            int volEpinesLastHitX = -1;            // Volee d'Epines : derniere case touchee pour pose Filet
            int volEpinesLastHitY = -1;
            // 3.5.a.ii — Faux Decharnee : somme cumulee des marques sur cibles touchees (pour heal post-loop).
            int fauxDecharneeMarksTotal = 0;

            // Refonte 29 mai — Brume Toxique SIMPLIFIÉE : pose terrain BrumeToxique sur les 9 cases
            // AoE 3x3 (effectBuffer). Plus de dégâts directs : pour chaque occupant ennemi déjà dans
            // la zone, juste +1 marque venin (le tick majoré dans la zone fait le reste). Zone 3 tours.
            if (cmd.Spell == SpellId.NecramBrumeToxique)
            {
                for (int bi = 0; bi < effectCount; bi++)
                {
                    int bidx = effectBuffer[bi];
                    int bcx = bidx % GridConstants.Width;
                    int bcy = bidx / GridConstants.Width;

                    GridHelpers.SetTerrain(f, bcx, bcy, TerrainKind.BrumeToxique,
                        SpellRegistry.BrumeToxiqueTurns, currentTurn, caster->PlayerIndex);

                    EntityRef bocc = GridHelpers.GetOccupant(f, bcx, bcy);
                    if (bocc == EntityRef.None || bocc == casterEntity) continue;
                    if (!f.Unsafe.TryGetPointer<Combatant>(bocc, out Combatant* boccC)) continue;
                    if (boccC->Class == NymoraClass.Necram || boccC->HP <= 0) continue;
                    VeninHelpers.ApplyMark(f, boccC, SpellRegistry.BrumeToxiqueMarksOnHit, currentTurn);
                }
                Log.Info($"[Spell] Brume Toxique posée centrée ({cmd.TargetX},{cmd.TargetY}), 9 cases, {SpellRegistry.BrumeToxiqueTurns} rounds (zone marques + tick majoré, sans dégâts directs)");
                return;
            }

            // 3.7.a.iii — Frappe Fantome : teleport caster sur case libre adjacente target AVANT
            // la boucle damage, pour que le dorsal Ghostra (calcule depuis caster.GridX/Y vs
            // target.Facing) bénéficie de la nouvelle position. Priorite case DORSALE (Lorenzo
            // amendement 16 mai) -> combo F (Dague Lancee 90°) -> T (Frappe Fantome) garantit
            // un teleport dans le dos. Le pre-check pre-PA a deja garanti qu'au moins une case
            // est libre, on rappelle le helper pour recuperer les coords.
            if (cmd.Spell == SpellId.GhostraFrappeFantome)
            {
                // Resolve target.Facing au moment du teleport (post-Dague Lancee = facing pivot 90°).
                EntityRef ffMainTarget = GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY);
                IsoFacing ffMainFacing = IsoFacing.SE;
                if (ffMainTarget != EntityRef.None
                    && f.Unsafe.TryGetPointer<Combatant>(ffMainTarget, out Combatant* ffMainTargetC))
                {
                    ffMainFacing = ffMainTargetC->Facing;
                }

                if (TryFindFreeCellAdjacentToTarget(f, cmd.TargetX, cmd.TargetY, ffMainFacing,
                        out int ffTpX, out int ffTpY))
                {
                    int ffOldX = caster->GridX, ffOldY = caster->GridY;
                    bool ffMoved = MovementHelpers.MoveNonPM(f, casterEntity, caster, ffTpX, ffTpY);
                    if (ffMoved)
                    {
                        // Apres teleport, MoveNonPM update caster.Facing depuis delta(ffOldX,ffOldY -> ffTpX,ffTpY).
                        // Mais Bible : Ghostra doit regarder LA TARGET, pas continuer sa direction de teleport.
                        // On override caster.Facing pour pointer vers target depuis la nouvelle position.
                        caster->Facing = FacingHelpers.FacingFromGridDelta(cmd.TargetX - ffTpX, cmd.TargetY - ffTpY);
                        Log.Info($"[Spell] Frappe Fantome : P{caster->PlayerIndex} teleport ({ffOldX},{ffOldY}) -> ({ffTpX},{ffTpY}) face target ({cmd.TargetX},{cmd.TargetY})");
                    }
                    else
                    {
                        // Edge case : MoveNonPM a echoue malgre le pre-check. On laisse le pipeline
                        // continuer (damage applique depuis position d'origine, pas de teleport).
                        Log.Warn($"[Spell] Frappe Fantome : teleport ({ffTpX},{ffTpY}) refuse par MoveNonPM (race condition ?), damage applique depuis position d'origine ({ffOldX},{ffOldY})");
                    }
                }
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
                        // PATCH #6 — leurre Ghostra ENNEMI sur une case d'effet (cible directe OU
                        // case d'AoE) : le sort le touche. Standard/RepliqueFantome 1-shot ;
                        // Protective encaisse effectiveDmg (detruit a 0 HP). Heal Bible selon Kind.
                        if (DecoyHelpers.TryFindEnemyDecoyForCaster(f, caster->PlayerIndex, cx, cy,
                                out Combatant* aoeDecoyG, out int aoeDecoySlot))
                        {
                            bool aoeDecoyDestroyed = DecoyHelpers.HitDecoyByEnemyAction(aoeDecoyG, aoeDecoySlot, effectiveDmgBuffed);
                            Log.Info($"[Spell] {cmd.Spell} touche leurre ennemi P{aoeDecoyG->PlayerIndex} en ({cx},{cy}) dmg={effectiveDmgBuffed} -> {(aoeDecoyDestroyed ? "DETRUIT" : "encaisse")}");
                            continue;
                        }

                        // 3.3.d — Sort AoE/offensif sur une case-obstacle : on lui inflige le damage
                        // de base (effectiveDmg). Couvre deux cas :
                        //   - obstacle ADVERSE : l'ennemi piégé par Effondrement casse des Failles
                        //     (ou un Pilier/Mur ennemi) avec ses sorts pour se créer un passage.
                        //   - obstacle OWN (brique juin 2026) : le Colossar peut désormais TAPER SES
                        //     PROPRES Piliers / Murs / Failles (dégager une Faille gênante, abattre un
                        //     Mur, ou détruire un Pilier pour Densité Inerte +30 HP). Avant, l'owner-
                        //     check excluait ses propres obstacles -> cast à vide (PA perdu, rien cassé).
                        // DamageAt est kind/owner-agnostique : il décrémente l'obstacle présent et le
                        // détruit à 0 HP (Densité Inerte se branche dans DestroyObstacle si Pilier own).
                        if (effectiveDmgBuffed > 0 && ObstacleHelpers.HasObstacleAt(f, cx, cy))
                        {
                            ObstacleHelpers.DamageAt(f, cx, cy, effectiveDmgBuffed);
                        }
                        continue;
                    }
                    if (!f.Unsafe.TryGetPointer<Combatant>(target, out Combatant* targetC)) continue;
                    if (target == casterEntity) continue; // pas d'auto-damage offensif

                    // 2.15.a — Nightseer per-cell damage variants. dmgThisTarget part du BASE non buffé
                    //   (effectiveDmg) ; les buffs offensifs du caster (Pacte/Peau de Fer/Frénésie/Affût/
                    //   Sang Bouillant) sont appliqués PLUS BAS, après les bonus flat (phase/Densité/Marque),
                    //   via ApplyOffensiveCasterBuffs (patch 7 juin, parité avec SpellPreview).
                    int dmgThisTarget = effectiveDmg;
                    if (cmd.Spell == SpellId.NightseerTirPrecis)
                    {
                        if (MarkHelpers.HasMark(targetC, MarkKind.Traque))
                        {
                            dmgThisTarget = SpellRegistry.TirPrecisDmgIfTraque;
                        }
                    }
                    else if (cmd.Spell == SpellId.NightseerFrappeDeLOmbre)
                    {
                        // Patch 7 juin — EXECUTEUR : 160 base + 120 si la cible est TRAQUÉ (= 280),
                        //   consomme Traqué. Ne pose PLUS Traqué, plus de bonus PM. Récompense le setup.
                        if (MarkHelpers.HasMark(targetC, MarkKind.Traque))
                        {
                            dmgThisTarget += SpellRegistry.FrappeDeLOmbreDmgBonusTraque;
                            MarkHelpers.ConsumeMark(targetC);
                            Log.Info($"[Spell] Frappe de l'Ombre : {dmgThisTarget} dgts (TRAQUÉ consommé, +{SpellRegistry.FrappeDeLOmbreDmgBonusTraque}) sur P{targetC->PlayerIndex}");
                        }
                        else
                        {
                            Log.Info($"[Spell] Frappe de l'Ombre : {dmgThisTarget} dgts (cible non Traqué) sur P{targetC->PlayerIndex}");
                        }
                    }
                    else if (cmd.Spell == SpellId.NightseerSalveMortelle)
                    {
                        // 6 juin — bonus "cible Traqué" RETIRÉ (choix Lorenzo). Salve = 200 centre /
                        //   120 autour ; le seul bonus restant est la zone des pièges (+50/piège non
                        //   consommé, géré dans le switch d'effets via ApplyZoneTrapBonusNoConsume).
                        bool isCenter = (cx == cmd.TargetX && cy == cmd.TargetY);
                        dmgThisTarget = isCenter
                            ? SpellRegistry.SalveMortelleDmgCenter
                            : SpellRegistry.SalveMortelleDmgSide;
                    }
                    // Détonation Onirique : 170 dgts de base (DamageAmount). Le SEUL bonus est +30 par
                    //   piège détoné (FogHelpers.DetonateOwnTrapsInArea, dans le switch d'effets). L'ancien
                    //   "+80 si la zone couvre un piège" a été RETIRÉ (6 juin, choix Lorenzo).
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
                    // Refonte 29 mai — Détonation Virulente : retirée de la boucle standard. C'est
                    //   désormais un TICK VENIN complet instantané (bypass shield + réduction, sans
                    //   consommer les marques), appliqué dans son handler dédié (ApplySpellSpecificEffects).
                    else if (cmd.Spell == SpellId.GhostraLameSpectrale)
                    {
                        // 3.7.a.i.2 — Bible Lame Spectrale : 170 base + 60 si target a PlaieOuverte (NON consommee).
                        // Bonus dorsal applique generiquement plus bas (bloc Ghostra).
                        if (StatusHelper.Has(targetC, StatusKind.PlaieOuverte))
                        {
                            dmgThisTarget += SpellRegistry.LameSpectralePlaieBonus;
                            Log.Info($"[Spell] Lame Spectrale +{SpellRegistry.LameSpectralePlaieBonus} dgts (PlaieOuverte sur P{targetC->PlayerIndex})");
                        }
                    }
                    else if (cmd.Spell == SpellId.GhostraLameVoraceSpectrale)
                    {
                        // 3.7.a.i.2 — Bible Lame Vorace : 130 base + 60 si PlaieOuverte (NON consommee).
                        // Bible "La Plaie Ouverte n'est PAS consommee" : on garde le status actif. Bonus dorsal
                        // applique generiquement plus bas.
                        if (StatusHelper.Has(targetC, StatusKind.PlaieOuverte))
                        {
                            dmgThisTarget += SpellRegistry.LameVoracePlaieBonus;
                            Log.Info($"[Spell] Lame Vorace Spectrale +{SpellRegistry.LameVoracePlaieBonus} dgts (PlaieOuverte sur P{targetC->PlayerIndex}, non consommee)");
                        }
                    }
                    else if (cmd.Spell == SpellId.GhostraSaigneAme)
                    {
                        // 3.7.a.ii — Bible Saigne-Ame : 200 base + 70 si target a PlaieOuverte
                        // (la plaie est CONSOMMEE post-damage sur cible survivante — consume gere
                        // dans le post-damage handler car on a besoin de wasKill pour discriminer).
                        // Bonus dorsal applique generiquement plus bas.
                        if (StatusHelper.Has(targetC, StatusKind.PlaieOuverte))
                        {
                            dmgThisTarget += SpellRegistry.SaigneAmePlaieBonus;
                            Log.Info($"[Spell] Saigne-Ame +{SpellRegistry.SaigneAmePlaieBonus} dgts (PlaieOuverte sur P{targetC->PlayerIndex}, sera consomme si survit)");
                        }
                    }

                    // 3.7.a — Bonus dorsal Ghostra (Bible Angle Mort) : applique aux sorts OFFENSIFS
                    // Ghostra uniquement (IsOffensive==1). +0 si Angle 1 (0 leurre) ou hit non dorsal,
                    // +50 si Angle 2 (1-2 leurres) dorsal, +80 si Angle 3 (3 leurres) dorsal.
                    //   Le gate IsOffensive évite tout dorsal/Marque accidentel sur les sorts non
                    //   offensifs ciblant un ennemi (Marque de l'Ombre, Voile Spectral rework).
                    if (caster->Class == NymoraClass.Ghostra && spellDef.IsOffensive == 1)
                    {
                        // 3.7.b — Éveil Spectral : dorsal calculé depuis le LEURRE (eveilDorsal), pas
                        //   depuis la Ghostra. Nuée Spectrale : AUCUN bonus dorsal (le scaling leurres
                        //   EST le bonus). Les autres sorts gardent le dorsal caster standard.
                        int dorsalBonus;
                        if (cmd.Spell == SpellId.GhostraNueeSpectrale || cmd.Spell == SpellId.GhostraVoileSpectral)
                            dorsalBonus = 0; // #21 : Voile = scaling par leurre adjacent, pas de dorsal caster
                        else if (cmd.Spell == SpellId.GhostraEveilSpectral)
                            dorsalBonus = eveilDorsal ? GhostraPassif.GetDorsalBonusForGhostra(caster) : 0;
                        else
                            dorsalBonus = GhostraPassif.GetDorsalBonusIfApplicable(caster, targetC);
                        if (dorsalBonus > 0)
                        {
                            dmgThisTarget += dorsalBonus;
                            Log.Info($"[Angle Mort] +{dorsalBonus} dmg DORSAL sur P{targetC->PlayerIndex} (sort {cmd.Spell}) -> total {dmgThisTarget}");
                        }

                        // 3.7.b.v — Marque de l'Ombre : si la cible porte le status, tous les sorts
                        //   Ghostra sur elle gagnent +20 dgts (magnitude du status). Bible "Buff de
                        //   pression : +20 sur tous les sorts Ghostra pendant 2 tours".
                        if (StatusHelper.Has(targetC, StatusKind.MarqueDeLOmbre))
                        {
                            int markBonus = StatusHelper.GetMagnitude(targetC, StatusKind.MarqueDeLOmbre, SpellRegistry.MarqueDeLOmbreDmgBonus);
                            dmgThisTarget += markBonus;
                            Log.Info($"[Marque de l'Ombre] +{markBonus} dmg sur P{targetC->PlayerIndex} (sort {cmd.Spell}) -> total {dmgThisTarget}");
                        }
                    }

                    // Refonte 29 mai — Nightseer Passif phasé P2+ : +30 dégâts flat sur les sorts
                    //   offensifs si le Nightseer est en phase >= 2 (PR 3-4+). Avant les réductions.
                    if (caster->Class == NymoraClass.Nightseer
                        && dmgThisTarget > 0
                        && NightseerPassif.FlatDamageBonusActive(caster->Resource))
                    {
                        dmgThisTarget += NightseerPassif.FlatDamageBonus;
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

                    // Patch 7 juin — buffs OFFENSIFS du caster (Pacte %, Peau de Fer flat mêlée, Frénésie %,
                    //   Affût %, Sang Bouillant flat) appliqués PAR CIBLE sur le total per-cellule (base +
                    //   bonus flat phase/Densité/Marque). Ordre identique à SpellPreview.FinalizeOffensive
                    //   -> cast == preview, y compris pour Tir Précis Traqué / Frappe / Salve (qui
                    //   recalculaient dmgThisTarget et perdaient le +10% Affût appliqué en amont).
                    if (dmgThisTarget > 0)
                    {
                        dmgThisTarget = ApplyOffensiveCasterBuffs(caster, spellDef.RangeMax == 1, dmgThisTarget);
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

                    // 3.7.c.iii — Réplique Protectrice : si target est Ghostra ET porte un decoy
                    // Protective vivant -> redirige 40% du dmg incoming (post-reduction%, AVANT
                    // shield Ghostra) sur le 1er decoy Protective trouve. Bible-strict : on retire
                    // toujours 40% du dmg Ghostra, le decoy absorbe min(40%, decoyHP) — le surplus
                    // s'evapore (le decoy a absorbe ce qu'il pouvait). Si decoy meurt suite a la
                    // redirection -> DestroyByEnemyAction (heal +60 HP Ghostra, Bible-conforme).
                    if (targetC->Class == NymoraClass.Ghostra && dmgThisTarget > 0)
                    {
                        int protSlotRP = -1;
                        for (int sRP = 0; sRP < DecoyHelpers.MaxDecoys; sRP++)
                        {
                            if (targetC->Decoys[sRP].Kind == DecoyKind.Protective && targetC->Decoys[sRP].HP > 0)
                            {
                                protSlotRP = sRP;
                                break;
                            }
                        }
                        if (protSlotRP >= 0)
                        {
                            int redirectRP = dmgThisTarget * SpellRegistry.RepliqueProtectriceRedirectPercent / 100;
                            if (redirectRP > 0)
                            {
                                int decoyHPBeforeRP = targetC->Decoys[protSlotRP].HP;
                                int absorbedRP = redirectRP > decoyHPBeforeRP ? decoyHPBeforeRP : redirectRP;
                                int decoyHPAfterRP = decoyHPBeforeRP - absorbedRP;
                                var slotRP = targetC->Decoys[protSlotRP];
                                slotRP.HP = decoyHPAfterRP;
                                targetC->Decoys[protSlotRP] = slotRP;

                                int dmgBeforeRP = dmgThisTarget;
                                dmgThisTarget -= redirectRP;
                                if (dmgThisTarget < 0) dmgThisTarget = 0;

                                Log.Info($"[Réplique Protectrice] P{targetC->PlayerIndex} redirige {redirectRP} dmg ({SpellRegistry.RepliqueProtectriceRedirectPercent}%) -> decoy slot {protSlotRP} absorbe {absorbedRP} (HP {decoyHPBeforeRP}->{decoyHPAfterRP}, surplus {redirectRP - absorbedRP} evapore). dmg Ghostra {dmgBeforeRP} -> {dmgThisTarget}");

                                if (decoyHPAfterRP <= 0)
                                {
                                    DecoyHelpers.DestroyByEnemyAction(targetC, protSlotRP);
                                }
                            }
                        }
                    }

                    // Shield absorption (2.10.b) : ShieldActive absorbe avant HP.
                    // 2.11 Passif RAGE OUVERTE : si target <40% HP pre-damage ET caster Soulrender ET
                    // sort melee -> 50% des dgts bypass shield direct au HP. L'autre 50% va shield -> HP overflow.
                    int targetHPRatioPreDmg = targetC->MaxHP > 0 ? (targetC->HP * 100 / targetC->MaxHP) : 100;
                    int shieldBefore = StatusHelper.GetMagnitude(targetC, StatusKind.ShieldActive, 0);
                    // Refonte 29 mai : le palier Appel du Sang <40% ne donne PLUS de bypass bouclier
                    //   (ni +1 PM). Il est remplace par un VOL DE VIE applique plus bas (apres HP loss).

                    // Refonte 29 mai — Passif phasé P3 : le Nightseer ignore 50% des boucliers
                    //   (= 50% des dgts bypass shield direct au HP) si en phase >= 3 (PR 5). Plus lié
                    //   à la marque Traqué (c'est un effet de palier).
                    int oeilTraqueBypass = 0;
                    if (caster->Class == NymoraClass.Nightseer
                        && shieldBefore > 0
                        && NightseerPassif.ShieldIgnoreActive(caster->Resource))
                    {
                        oeilTraqueBypass = dmgThisTarget * NightseerPassif.ShieldIgnorePct / 100;
                        Log.Info($"[Spell] Prescience P3 : {oeilTraqueBypass} dgts ignorent le bouclier de P{targetC->PlayerIndex} ({NightseerPassif.ShieldIgnorePct}%)");
                    }

                    int totalShieldBypass = oeilTraqueBypass; // Refonte 29 mai : plus de bypass Rage Ouverte
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

                    // 3.7.c.i — Linceul d'Ombres hook (Bible V7.1 ligne 1173) : si target
                    // porte LinceulDOmbres ET attaque MELEE (Chebyshev caster-cible <= 1)
                    // -> renvoie Magnitude dgts (40) sur l'attaquant. PIPELINE STANDARD :
                    // applique reduction% (Densite Inerte + Garde Protectrice cap 50%)
                    // puis shield attaquant si present. Trigger meme si le shield Linceul
                    // a absorbe TOUT le dmg incoming (Bible "toute attaque melee subie" :
                    // on est entre dans le damage loop avec dmgThisTarget > 0 donc l'attaque
                    // touche). isMeleeAttackForCarapace reutilise (Chebyshev Bible-strict).
                    // Guard `dmgThisTarget > 0` : evite double-trigger sur les sorts custom
                    // path qui ont DamageAmount=0 dans SpellDef et appliquent le dmg eux-memes
                    // (Charge Brutale, Empoignade...). Le hook standard ne doit trigger QUE
                    // pour les sorts qui passent reellement par le damage loop standard. Le
                    // custom path rebranche le hook manuellement (Charge Brutale ligne ~1690).
                    if (isMeleeAttackForCarapace
                        && dmgThisTarget > 0
                        && StatusHelper.Has(targetC, StatusKind.LinceulDOmbres)
                        && caster->PlayerIndex != targetC->PlayerIndex
                        && caster->HP > 0)
                    {
                        int ripostDmgBase = StatusHelper.GetMagnitude(targetC, StatusKind.LinceulDOmbres,
                            SpellRegistry.LinceulDOmbresRipostMeleeDmg);
                        int reductionPctRiposte = ColossarPassif.GetCombinedDamageReductionPercent(f, caster);
                        int ripostDmg = ColossarPassif.ApplyDamageReduction(f, caster, ripostDmgBase);
                        int ripostShieldBefore = StatusHelper.GetMagnitude(caster, StatusKind.ShieldActive, 0);
                        int ripostShieldAbsorbed = 0;
                        if (ripostShieldBefore > 0 && ripostDmg > 0)
                        {
                            int absorbed = ripostDmg > ripostShieldBefore ? ripostShieldBefore : ripostDmg;
                            ripostShieldAbsorbed = absorbed;
                            int after = ripostShieldBefore - absorbed;
                            if (after == 0) StatusHelper.Consume(caster, StatusKind.ShieldActive);
                            else StatusHelper.SetMagnitude(caster, StatusKind.ShieldActive, after);
                            ripostDmg -= absorbed;
                        }
                        int casterBeforeRiposte = caster->HP;
                        if (ripostDmg > 0)
                        {
                            caster->HP -= ripostDmg;
                            if (caster->HP < 0) caster->HP = 0;
                        }
                        Log.Info($"[Linceul d'Ombres] P{targetC->PlayerIndex} renvoie {ripostDmgBase} dgts melee a P{caster->PlayerIndex} (reduction {reductionPctRiposte}%, shield absorbe {ripostShieldAbsorbed} -> HP loss {ripostDmg}, HP {casterBeforeRiposte} -> {caster->HP})");
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

                        // Refonte 29 mai — Passif Appel du Sang VOL DE VIE : si caster Soulrender et
                        //   cible <40% PV (pre-dmg), heal 20% des dgts qui passent. Remplace l'ancien
                        //   +1 PM / bypass bouclier. Via HealHelper (respecte AntiHealShield + ÷2 soin).
                        if (caster->Class == NymoraClass.Soulrender
                            && caster->PlayerIndex != targetC->PlayerIndex
                            && targetHPRatioPreDmg < SpellRegistry.AppelDuSangPalierRageOuverte
                            && caster->HP > 0)
                        {
                            int lifesteal = totalHPLoss * SpellRegistry.AppelDuSangLifestealPct / 100;
                            int healedLS = HealHelper.ApplyHeal(caster, lifesteal);
                            if (healedLS > 0)
                                Log.Info($"[Appel du Sang] Vol de vie {SpellRegistry.AppelDuSangLifestealPct}% : +{healedLS} HP sur P{caster->PlayerIndex} (cible <{SpellRegistry.AppelDuSangPalierRageOuverte}% PV)");
                        }

                        // Refonte 29 mai — SANG BOUILLANT (hook victime) : si le combattant qui subit
                        //   les degats porte SangBouillantActive ET survit -> +1 HG + sa prochaine
                        //   frappe gagne +30 (NextStrikeBonus). SangBouillantActive n'est porte que par
                        //   un Soulrender (Resource = HG).
                        if (targetC->HP > 0 && StatusHelper.Has(targetC, StatusKind.SangBouillantActive))
                        {
                            int maxResSB = CombatantStats.GetMaxResource(targetC->Class);
                            int hgBeforeSB = targetC->Resource;
                            targetC->Resource += SpellRegistry.SangBouillantHGPerHit;
                            if (targetC->Resource > maxResSB) targetC->Resource = maxResSB;
                            StatusHelper.Apply(targetC, StatusKind.NextStrikeBonus,
                                magnitude: SpellRegistry.SangBouillantNextStrikeBonus, turnsLeft: 99, currentTurn);
                            Log.Info($"[Sang Bouillant] P{targetC->PlayerIndex} subit des degats -> +{targetC->Resource - hgBeforeSB} HG + prochaine frappe +{SpellRegistry.SangBouillantNextStrikeBonus}");
                        }

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

                        // Refonte 29 mai — l'ancien hook riposte mêlée de Voile de Pestilence est RETIRÉ
                        // (Voile devient Nuée de Spores, un buff offensif sans riposte).

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

                    // 3.5.c.iii — Drain Vital (refonte 29 mai) : 40 dmg (pipeline) + heal Necram caster
                    // = 40 HP par marque venin sur la cible (cap 160 = 4 marques), marques NON
                    // consommees. Heal applique meme si la cible meurt sur les 40 dmg. Cap MaxHP.
                    if (cmd.Spell == SpellId.NecramDrainVital && caster->HP > 0)
                    {
                        int targetMarks = targetC->VeninStacks;
                        int healAmount = targetMarks * SpellRegistry.DrainVitalHealPerMark;
                        if (healAmount > SpellRegistry.DrainVitalHealMaxBonus) healAmount = SpellRegistry.DrainVitalHealMaxBonus;
                        if (healAmount > 0)
                        {
                            int hpBeforeHeal = caster->HP;
                            caster->HP += healAmount;
                            if (caster->HP > caster->MaxHP) caster->HP = caster->MaxHP;
                            Log.Info($"[Spell] Drain Vital : P{caster->PlayerIndex} heal +{healAmount} HP ({targetMarks} marques * {SpellRegistry.DrainVitalHealPerMark}, cap {SpellRegistry.DrainVitalHealMaxBonus}) HP {hpBeforeHeal} -> {caster->HP}");
                        }
                    }

                    // Refonte 29 mai — Détonation Virulente ne consomme PLUS les marques (rework :
                    //   tick à la demande). Effet appliqué dans son handler dédié.

                    // 3.7.a — Passif Ghostra Angle Mort : si caster Ghostra ET hit dorsal ET Angle 2+
                    // -> applique PlaieOuverte auto (40/tour x 2 rounds Bible). Helper no-op si:
                    // - caster != Ghostra
                    // - target mort
                    // - hit non dorsal (FacingHelpers.IsDorsalHit false)
                    // - Angle 1 (0 leurre actif)
                    // S'applique aux 5 sorts offensifs Ghostra (Lame Spec / Frappe Fantome / Lame
                    // Vorace / Saigne-Ame / Danse des Lames). Pas aux sorts tactiques/survie.
                    if (caster->Class == NymoraClass.Ghostra
                        && targetC->HP > 0
                        && spellDef.IsOffensive == 1
                        && cmd.Spell != SpellId.GhostraNueeSpectrale  // Nuée : pas de Plaie (pas de mécanique dorsale)
                        && cmd.Spell != SpellId.GhostraVoileSpectral) // #21 : Voile non plus (dégâts = scaling par leurre)
                    {
                        // 3.7.b — Éveil Spectral : Plaie évaluée depuis le LEURRE (dorsal du leurre),
                        //   pas depuis la Ghostra. Les autres sorts gardent le dorsal caster standard.
                        if (cmd.Spell == SpellId.GhostraEveilSpectral)
                            GhostraPassif.ApplyPlaieOuverteFromPosition(f, caster, targetC, eveilLeurreX, eveilLeurreY, currentTurn);
                        else
                            GhostraPassif.ApplyPlaieOuverteIfAngle2Plus(f, caster, targetC, currentTurn);
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

            // Refonte 29 mai — ancienne génération PR "+1 par hit Traqué" RETIRÉE (nouvelle économie
            //   = pièges posés/déclenchés + marques appliquées, cf NightseerPassif).

            // 2.15.a — Volee d'Epines : pose un Filet de Ronces DERRIERE la derniere cible touchee.
            // Refonte juin 2026 (demande Lorenzo) : au lieu de poser sur la case de la cible, on
            // pose UNE CASE PLUS LOIN dans le sens du tir (en s'eloignant du caster) -> coupe la
            // retraite de l'ennemi (Bible PRESSION : "foncer dans le filet ou contourner"). Si la
            // case derriere est hors grille / non walkable (mur, bord), fallback sur la case de la
            // cible. Si aucune cible touchee (ligne tiree dans le vide), pas de Filet pose.
            if (cmd.Spell == SpellId.NightseerVoleeDEpines && volEpinesLastHitX >= 0)
            {
                // Sens cardinal du tir = signe(target - caster). Le tir etant valide en ligne droite
                // (SpellIsStraightLine), exactement un des deux axes est non nul.
                int fireDirX = cmd.TargetX == caster->GridX ? 0 : (cmd.TargetX > caster->GridX ? 1 : -1);
                int fireDirY = cmd.TargetY == caster->GridY ? 0 : (cmd.TargetY > caster->GridY ? 1 : -1);
                int trapX = volEpinesLastHitX + fireDirX;
                int trapY = volEpinesLastHitY + fireDirY;
                if (!GridHelpers.IsWalkable(f, trapX, trapY))
                {
                    trapX = volEpinesLastHitX;
                    trapY = volEpinesLastHitY;
                }
                FogHelpers.PlaceTrap(f, trapX, trapY, TrapKind.FiletRonces, caster->PlayerIndex, currentTurn);
                Log.Info($"[Spell] Volee d'Epines : Filet de Ronces pose DERRIERE la cible sur ({trapX},{trapY}) par P{caster->PlayerIndex} (cible ({volEpinesLastHitX},{volEpinesLastHitY}), sens ({fireDirX},{fireDirY}))");
            }

            // ===== Consume Pacte buff si utilise =====
            if (effectiveDmg > 0 && pacteBuffPct > 0)
            {
                StatusHelper.Consume(caster, StatusKind.BuffNextOffensiveDmgPercent);
                Log.Info($"[Spell] BuffNextOffensiveDmgPercent consume sur P{caster->PlayerIndex} (+{pacteBuffPct}%)");
            }

            // ===== Consume Sang Bouillant NextStrikeBonus si la frappe a touche =====
            if (nextStrikeBonus > 0 && casterHitSomething)
            {
                StatusHelper.Consume(caster, StatusKind.NextStrikeBonus);
                Log.Info($"[Spell] Sang Bouillant : NextStrikeBonus +{nextStrikeBonus} consomme sur P{caster->PlayerIndex}");
            }

            // 2.11 Passif LE CRI : si target <20% HP post-hit, pose Sang Coagule sur croix 5
            // (caster + 4 cardinales). Une fois par cast peu importe le nb de cibles.
            if (castTriggeredLeCri)
            {
                int cx0 = caster->GridX;
                int cy0 = caster->GridY;
                GridHelpers.SetTerrain(f, cx0,     cy0,     TerrainKind.SangCoagule, SpellRegistry.SangCoaguleTurns, currentTurn, caster->PlayerIndex);
                GridHelpers.SetTerrain(f, cx0 + 1, cy0,     TerrainKind.SangCoagule, SpellRegistry.SangCoaguleTurns, currentTurn, caster->PlayerIndex);
                GridHelpers.SetTerrain(f, cx0 - 1, cy0,     TerrainKind.SangCoagule, SpellRegistry.SangCoaguleTurns, currentTurn, caster->PlayerIndex);
                GridHelpers.SetTerrain(f, cx0,     cy0 + 1, TerrainKind.SangCoagule, SpellRegistry.SangCoaguleTurns, currentTurn, caster->PlayerIndex);
                GridHelpers.SetTerrain(f, cx0,     cy0 - 1, TerrainKind.SangCoagule, SpellRegistry.SangCoaguleTurns, currentTurn, caster->PlayerIndex);
                Log.Info($"[Spell] LE CRI ! Cible <{SpellRegistry.AppelDuSangPalierLeCri}% HP, Sang Coagule pose en croix 5 autour P{caster->PlayerIndex} ({cx0},{cy0})");
            }

            // Refonte 29 mai — NUÉE DE SPORES (ex-Voile de Pestilence) : tant que le Necram porte
            //   PestilenceAura, chacun de ses sorts visant un ENNEMI pose +1 marque venin BONUS sur
            //   la cible. Appliqué AVANT ApplySpellSpecificEffects (avant un éventuel swap Échange).
            if (caster->Class == NymoraClass.Necram
                && StatusHelper.Has(caster, StatusKind.PestilenceAura))
            {
                EntityRef nueeTarget = GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY);
                if (nueeTarget != EntityRef.None
                    && f.Unsafe.TryGetPointer<Combatant>(nueeTarget, out Combatant* nueeC)
                    && nueeC->PlayerIndex != caster->PlayerIndex
                    && nueeC->HP > 0)
                {
                    int nueeBefore = nueeC->VeninStacks;
                    VeninHelpers.ApplyMark(f, nueeC, 1, currentTurn);
                    if (nueeC->VeninStacks != nueeBefore)
                        Log.Info($"[Nuée de Spores] +1 marque bonus sur P{nueeC->PlayerIndex} ({nueeBefore}->{nueeC->VeninStacks})");
                }
            }

            // ===== Effets specifiques par sort (apres damage) =====
            ApplySpellSpecificEffects(f, cmd, spellDef, caster, casterEntity,
                casterHitSomething, hgSpend, currentTurn,
                effectBuffer, effectCount,
                wasKill, killedTargetX, killedTargetY, lastHitHPLoss);

            // ===== Frenesie (ex-Rage Insatiable, refonte 29 mai) : +1 HG par sort OFFENSIF =====
            //   (le +10% dgts est applique en amont dans le bloc effectiveDmg). Plus de regen PA,
            //   plus de +1 PA cost. "chaque offensif" = pas de cap par tour.
            if (spellDef.IsOffensive != 0 && StatusHelper.Has(caster, StatusKind.RageInsatiableActive))
            {
                int maxResFr = CombatantStats.GetMaxResource(caster->Class);
                int hgBeforeFr = caster->Resource;
                caster->Resource += SpellRegistry.FrenesieHGPerOffensive;
                if (caster->Resource > maxResFr) caster->Resource = maxResFr;
                if (caster->Resource != hgBeforeFr)
                    Log.Info($"[Spell] Frenesie : +{SpellRegistry.FrenesieHGPerOffensive} HG sur P{caster->PlayerIndex} ({hgBeforeFr} -> {caster->Resource})");
            }

            // 3.7.b.iii — Update caster.Facing au cast pour les sorts non-Self.
            //   Quantum est source-of-truth pour le Facing depuis 3.7.a.i. La View (CombatantRenderer.ResolveFacing)
            //   lit self.Facing directement, donc le perso doit "regarder" sa cible cote sim au moment du cast
            //   (sinon sprite reste oriente comme avant le cast). Auparavant cette logique etait
            //   un hack View-only ; on la promote en Quantum pour cohérence cross-frame.
            if (spellDef.Filter != TargetingFilter.Self
                && (cmd.TargetX != caster->GridX || cmd.TargetY != caster->GridY))
            {
                caster->Facing = FacingHelpers.FacingFromGridDelta(cmd.TargetX - caster->GridX, cmd.TargetY - caster->GridY);
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
                    // Refonte 29 mai : si 1 HG depense ET cible touchee -> soins/boucliers RECUS
                    //   par la cible reduits de 50% (÷2) pendant 1 tour (HealReductionPercent),
                    //   au lieu du blocage total AntiHealShield 2 tours d'avant.
                    if (hgSpend >= 1 && casterHitSomething)
                    {
                        EntityRef target = GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY);
                        if (target != EntityRef.None
                            && f.Unsafe.TryGetPointer<Combatant>(target, out Combatant* targetC))
                        {
                            StatusHelper.Apply(targetC, StatusKind.HealReductionPercent,
                                magnitude: SpellRegistry.OuvrePlaieHealReductionPct, turnsLeft: 1, currentTurn);
                            Log.Info($"[Spell] Ouvre-Plaie : soins/boucliers ÷2 ({SpellRegistry.OuvrePlaieHealReductionPct}%) 1 tour sur P{targetC->PlayerIndex}");
                        }
                    }
                    break;

                case SpellId.SoulrenderPacteDeSang:
                    // Patch 7 juin : -80 HP self, +2 HG self (etait 3), buff +25% next offensif (etait 50%) 1 tour.
                    int hpBefore = caster->HP;
                    caster->HP -= SpellRegistry.PacteDeSangSelfDamage;
                    if (caster->HP < 0) caster->HP = 0;
                    Log.Info($"[Spell] Pacte de Sang : self-damage {SpellRegistry.PacteDeSangSelfDamage} (HP {hpBefore} -> {caster->HP})");

                    int maxRes = CombatantStats.GetMaxResource(caster->Class);
                    int resBefore = caster->Resource;
                    caster->Resource += SpellRegistry.PacteDeSangHGGain;
                    if (caster->Resource > maxRes) caster->Resource = maxRes;
                    Log.Info($"[Spell] Pacte de Sang : +{SpellRegistry.PacteDeSangHGGain} HG (clamped, {resBefore} -> {caster->Resource})");

                    StatusHelper.Apply(caster, StatusKind.BuffNextOffensiveDmgPercent, magnitude: SpellRegistry.PacteDeSangDmgPercent, turnsLeft: 1, currentTurn);
                    Log.Info($"[Spell] Pacte de Sang : BuffNextOffensiveDmgPercent +{SpellRegistry.PacteDeSangDmgPercent}% (1 tour) sur P{caster->PlayerIndex}");
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

                case SpellId.SoulrenderRageInsatiable: // FRENESIE (refonte 29 mai)
                    // Frenesie 2 tours : chaque sort offensif +1 HG + +10% dgts (geres dans le
                    //   pipeline). Magnitude inutilisee (0). "1x actif" = refresh natif (recast
                    //   remet 2 tours, pas de stack). Identifiant enum/status conserve.
                    StatusHelper.Apply(caster, StatusKind.RageInsatiableActive, magnitude: 0, turnsLeft: 2, currentTurn);
                    Log.Info($"[Spell] Frenesie : active 2 tours sur P{caster->PlayerIndex}");
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
                        // Refonte 29 mai : le 90 dgts est applique par le pipeline (IsOffensive=1)
                        //   AVANT ce handler. Si la cible est morte du coup, plus rien a faire.
                        if (targetC->HP <= 0) break;

                        // 3.3.b.ii — Ancrage : cible AnchorImmune ne peut pas etre tiree.
                        if (StatusHelper.Has(targetC, StatusKind.AnchorImmune))
                        {
                            Log.Info($"[Ancrage] Empoignade : pull annule sur P{targetC->PlayerIndex} (AnchorImmune). -2 PM applique quand meme.");
                        }
                        else
                        {
                            int beforeX = targetC->GridX;
                            int beforeY = targetC->GridY;
                            bool pulled = PullTargetAdjacent(f, caster, target, targetC);
                            if (pulled)
                                Log.Info($"[Spell] Empoignade : P{targetC->PlayerIndex} tire ({beforeX},{beforeY}) -> ({targetC->GridX},{targetC->GridY})");
                            else
                                Log.Info($"[Spell] Empoignade : P{targetC->PlayerIndex} deja adjacent ou pas de case libre (no-op move)");
                        }
                        // Refonte 29 mai : -2 PM (MovementMalus) au lieu de l'AntiTeleport.
                        StatusHelper.Apply(targetC, StatusKind.MovementMalus,
                            magnitude: SpellRegistry.EmpoignadePMMalus, turnsLeft: 1, currentTurn);
                        Log.Info($"[Spell] Empoignade : -{SpellRegistry.EmpoignadePMMalus} PM sur P{targetC->PlayerIndex}");
                    }
                    break;
                }

                case SpellId.SoulrenderPeauDeFer:
                    // ShieldActive 2 tours, magnitude = 200 HP de shield (÷2 si HealReductionPercent
                    //   actif sur le caster — Ouvre-Plaie). Bonus +30 dgts melee lu au runtime.
                    {
                        int pdfShield = HealHelper.EffectiveShieldGain(caster, SpellRegistry.PeauDeFerShieldHP);
                        StatusHelper.Apply(caster, StatusKind.ShieldActive,
                            magnitude: pdfShield,
                            turnsLeft: SpellRegistry.PeauDeFerShieldTurns,
                            currentTurn);
                        Log.Info($"[Spell] Peau de Fer : ShieldActive {pdfShield} HP / {SpellRegistry.PeauDeFerShieldTurns} tours sur P{caster->PlayerIndex}");
                    }
                    break;

                case SpellId.SoulrenderSeveVive:
                {
                    int healAmount = SpellRegistry.SeveViveHealBase;
                    int hgBonus = (hgSpend >= 1) ? SpellRegistry.SeveViveHealBonusHG : 0;
                    healAmount += hgBonus;
                    // Fix 5 juin (B4) : BleedDoT etait un statut MORT (aucun sort ne l'applique)
                    //   -> le bonus +50 ne partait JAMAIS. On detecte les VRAIS DoT du jeu (meme
                    //   definition que Voile Spectral) : venin Necram (VeninStacks) + Plaie Ouverte.
                    bool isBleeding = caster->VeninStacks > 0
                        || StatusHelper.Has(caster, StatusKind.PlaieOuverte)
                        || StatusHelper.Has(caster, StatusKind.BleedDoT);
                    int bleedBonus = isBleeding ? SpellRegistry.SeveViveHealBonusBleed : 0;
                    healAmount += bleedBonus;

                    // Via HealHelper : respecte AntiHealShield (bloque) + HealReductionPercent (÷2 Ouvre-Plaie).
                    int healedSV = HealHelper.ApplyHeal(caster, healAmount);
                    Log.Info($"[Spell] Seve Vive : heal {healedSV} (demande {healAmount} = base {SpellRegistry.SeveViveHealBase} + HG {hgBonus} + Bleed {bleedBonus}) sur P{caster->PlayerIndex}");
                    break;
                }

                case SpellId.SoulrenderDernierSouffle:
                {
                    // Heal 200 HP via HealHelper (AntiHealShield bloque, HealReductionPercent ÷2) + 3 HG (toujours).
                    int healedDS = HealHelper.ApplyHeal(caster, SpellRegistry.DernierSouffleHealAmount);
                    Log.Info($"[Spell] Dernier Souffle : heal {healedDS} (demande {SpellRegistry.DernierSouffleHealAmount}) sur P{caster->PlayerIndex}");

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

                        // 3.7.a.i.4 — Bible V7.1 Ghostra : un leurre stoppe la Charge (visuellement
                        // indiscernable de la vraie). Le caster s'arrete sur la case precedente.
                        // Patch 5 juin (Lorenzo) — les leurres ont desormais des HP : la charge leur
                        // inflige ses degats (HitDecoyByEnemyAction) au lieu de les detruire en 1 coup.
                        // Le leurre survit si sa reserve d'HP > degats charge (200/250 vs 180). Heal
                        // Bible-conforme applique seulement s'il tombe a 0 (DestroyByEnemyAction interne).
                        if (DecoyHelpers.TryFindEnemyDecoyForCaster(f, caster->PlayerIndex, cx, cy,
                            out Combatant* decoyGhostra, out int decoySlot))
                        {
                            bool cbDecoyDestroyed = DecoyHelpers.HitDecoyByEnemyAction(
                                decoyGhostra, decoySlot, SpellRegistry.ChargeBrutaleDamage);
                            Log.Info($"[Spell] Charge Brutale : stoppee par leurre Ghostra P{decoyGhostra->PlayerIndex} en ({cx},{cy}) — {SpellRegistry.ChargeBrutaleDamage} dgts au leurre ({(cbDecoyDestroyed ? "DETRUIT" : "survit")})");
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
                            GridHelpers.SetTerrain(f, cx2, cy2, TerrainKind.VapeurCarmin, SpellRegistry.VapeurCarminTurns, currentTurn, caster->PlayerIndex);
                            if (cx2 == finalX && cy2 == finalY) break;
                        }
                        Log.Info($"[Spell] Charge Brutale : Vapeur Carmin pose sur cases foulees ({SpellRegistry.VapeurCarminTurns} tour)");
                    }

                    // Fix #5 (5 juin) — Charge Brutale declenche les pieges sur TOUTES les cases TRAVERSEES.
                    //   La case finale est deja geree par MoveNonPM ci-dessus ; ici on couvre les cases
                    //   INTERMEDIAIRES (sx+step jusqu'a AVANT finalX/finalY). Owner-filtre. Borne maxSteps
                    //   (anti-boucle deterministe).
                    if (finalX != sx || finalY != sy)
                    {
                        int cbtx = sx, cbty = sy;
                        for (int cbStep = 0; cbStep < maxSteps; cbStep++)
                        {
                            cbtx += stepX; cbty += stepY;
                            if (cbtx == finalX && cbty == finalY) break; // landing deja declenche par MoveNonPM
                            if (caster->HP <= 0) break;
                            FogHelpers.TryTriggerTrapOnEnter(f, casterEntity, caster, cbtx, cbty, currentTurn);
                        }
                    }

                    // Patch 5 juin — Charge Brutale ne touche QUE si elle finit au corps a corps. Si un
                    //   piege REPULSANT (Bondissant) du Nightseer a catapulte le caster hors de sa case
                    //   d'arrivee prevue (finalX/finalY, adjacente a la cible) pendant MoveNonPM / les
                    //   triggers de cases traversees, la charge est INTERROMPUE -> pas de degats. Avant,
                    //   les 180 partaient meme apres ejection. On verifie aussi que le caster est vivant
                    //   (mort possible sur un Filet/Mine traverse).
                    bool chargeConnected = caster->HP > 0
                                           && caster->GridX == finalX && caster->GridY == finalY;
                    if (hitTarget != EntityRef.None && !chargeConnected)
                    {
                        Log.Info($"[Spell] Charge Brutale : INTERROMPUE — caster en ({caster->GridX},{caster->GridY}) != arrivee prevue ({finalX},{finalY}) (piege repulsant ?) ou mort -> aucun degat sur la cible.");
                    }

                    // Damage 180 a la cible bloquante si presente ET si la charge a connecte (melee).
                    if (hitTarget != EntityRef.None
                        && chargeConnected
                        && f.Unsafe.TryGetPointer<Combatant>(hitTarget, out Combatant* hitC))
                    {
                        int hpBeforeHit = hitC->HP;
                        // Shield absorption manuel (Charge Brutale non-pipeline).
                        // Fix 5 juin (#1) — CB bypassait le pipeline offensif generique (~ligne 704) :
                        // Pacte de Sang +50% / Frenesie / Sang Bouillant n'etaient JAMAIS appliques
                        // (alors que le HUD les prevoit via SpellPreview). On les rebranche ici comme
                        // on rebranche deja les hooks DEFENSIFS plus bas. isMelee=false (portee 4).
                        int dmgLeft = ApplyOffensiveCasterBuffs(caster, isMelee: false, SpellRegistry.ChargeBrutaleDamage);
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
                        // 3.7.c.iii — Réplique Protectrice hook (Charge Brutale custom path) :
                        // si hitC est Ghostra ET porte un decoy Protective vivant, redirige 40%
                        // du dmgLeft (post-reduction% deja appliquee dans le path CB) AVANT le
                        // shield Ghostra. Charge Brutale bypass le pipeline standard donc on
                        // rebranche le hook manuellement.
                        if (hitC->Class == NymoraClass.Ghostra && dmgLeft > 0)
                        {
                            int protSlotCB = -1;
                            for (int sCB = 0; sCB < DecoyHelpers.MaxDecoys; sCB++)
                            {
                                if (hitC->Decoys[sCB].Kind == DecoyKind.Protective && hitC->Decoys[sCB].HP > 0)
                                {
                                    protSlotCB = sCB;
                                    break;
                                }
                            }
                            if (protSlotCB >= 0)
                            {
                                int redirectCB = dmgLeft * SpellRegistry.RepliqueProtectriceRedirectPercent / 100;
                                if (redirectCB > 0)
                                {
                                    int decoyHPBeforeCB = hitC->Decoys[protSlotCB].HP;
                                    int absorbedCB = redirectCB > decoyHPBeforeCB ? decoyHPBeforeCB : redirectCB;
                                    int decoyHPAfterCB = decoyHPBeforeCB - absorbedCB;
                                    var slotCB = hitC->Decoys[protSlotCB];
                                    slotCB.HP = decoyHPAfterCB;
                                    hitC->Decoys[protSlotCB] = slotCB;

                                    int dmgBeforeCB = dmgLeft;
                                    dmgLeft -= redirectCB;
                                    if (dmgLeft < 0) dmgLeft = 0;

                                    Log.Info($"[Réplique Protectrice] P{hitC->PlayerIndex} Charge Brutale redirige {redirectCB} dmg ({SpellRegistry.RepliqueProtectriceRedirectPercent}%) -> decoy slot {protSlotCB} absorbe {absorbedCB} (HP {decoyHPBeforeCB}->{decoyHPAfterCB}). dmg Ghostra {dmgBeforeCB} -> {dmgLeft}");

                                    if (decoyHPAfterCB <= 0)
                                    {
                                        DecoyHelpers.DestroyByEnemyAction(hitC, protSlotCB);
                                    }
                                }
                            }
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

                        // Refonte 29 mai — ancien hook riposte Voile de Pestilence (Charge Brutale)
                        // RETIRÉ (Voile devient Nuée de Spores, sans riposte).

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

                        // 3.7.c.i — Linceul d'Ombres hook (Bible V7.1) : Charge Brutale est melee
                        // (caster adjacent post-move = Chebyshev <= 1 implicite). Si la cible porte
                        // LinceulDOmbres -> renvoie 40 dgts sur attaquant (PIPELINE STANDARD reduction%
                        // + shield attaquant). Charge Brutale bypass le pipeline standard donc on
                        // rebranche le hook ici manuellement. Trigger meme si shield Linceul absorbe
                        // tout (l'attaque a touche : on est entre dans ce bloc avec dmg>=0 incoming).
                        if (StatusHelper.Has(hitC, StatusKind.LinceulDOmbres)
                            && caster->PlayerIndex != hitC->PlayerIndex
                            && caster->HP > 0)
                        {
                            int ripostDmgBaseCB = StatusHelper.GetMagnitude(hitC, StatusKind.LinceulDOmbres,
                                SpellRegistry.LinceulDOmbresRipostMeleeDmg);
                            int reductionPctRiposteCB = ColossarPassif.GetCombinedDamageReductionPercent(f, caster);
                            int ripostDmgCB = ColossarPassif.ApplyDamageReduction(f, caster, ripostDmgBaseCB);
                            int ripostShieldBeforeCB = StatusHelper.GetMagnitude(caster, StatusKind.ShieldActive, 0);
                            int ripostShieldAbsorbedCB = 0;
                            if (ripostShieldBeforeCB > 0 && ripostDmgCB > 0)
                            {
                                int absorbed = ripostDmgCB > ripostShieldBeforeCB ? ripostShieldBeforeCB : ripostDmgCB;
                                ripostShieldAbsorbedCB = absorbed;
                                int after = ripostShieldBeforeCB - absorbed;
                                if (after == 0) StatusHelper.Consume(caster, StatusKind.ShieldActive);
                                else StatusHelper.SetMagnitude(caster, StatusKind.ShieldActive, after);
                                ripostDmgCB -= absorbed;
                            }
                            int casterBeforeRiposteCB = caster->HP;
                            if (ripostDmgCB > 0)
                            {
                                caster->HP -= ripostDmgCB;
                                if (caster->HP < 0) caster->HP = 0;
                            }
                            Log.Info($"[Linceul d'Ombres] P{hitC->PlayerIndex} Charge Brutale renvoie {ripostDmgBaseCB} dgts melee a P{caster->PlayerIndex} (reduction {reductionPctRiposteCB}%, shield absorbe {ripostShieldAbsorbedCB} -> HP loss {ripostDmgCB}, HP {casterBeforeRiposteCB} -> {caster->HP})");
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

                            // Fix 5 juin (#1) — conso des buffs one-shot que Charge Brutale vient
                            // d'appliquer (Pacte de Sang + Sang Bouillant) : la conso generique
                            // (~ligne 1555) ne fire pas pour ce custom path (effectiveDmg==0) -> sans ca
                            // le buff se reporterait sur le sort suivant (double-dip). Frenesie (duree)
                            // n'est pas consommee.
                            ConsumeOffensiveOneShotBuffs(caster);
                        }
                    }
                    break;
                }

                case SpellId.SoulrenderDetonationSanglante:
                {
                    // Le damage en croix 3 est deja gere par le pipeline generique (DamageAmount calcule dynamiquement).
                    // Ici on pose Sang Coagule sur la case CENTRE (TargetX, TargetY) pour 2 tours.
                    GridHelpers.SetTerrain(f, cmd.TargetX, cmd.TargetY, TerrainKind.SangCoagule, SpellRegistry.SangCoaguleTurns, currentTurn, caster->PlayerIndex);
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

                case SpellId.SoulrenderCuree: // EVENTRATION (refonte 29 mai, ex-Curee)
                {
                    // 220 dgts deja appliques par le pipeline. Pose Plaie Ouverte 50/tour x 3 rounds
                    //   sur la cible vivante (gros DoT). Plus de kill-heal / miss-selfdamage.
                    //   Bloque si la cible est DotImmune (Voile Spectral Ghostra).
                    EntityRef evTarget = GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY);
                    if (evTarget != EntityRef.None
                        && f.Unsafe.TryGetPointer<Combatant>(evTarget, out Combatant* evTargetC)
                        && evTargetC->HP > 0
                        && evTargetC->PlayerIndex != caster->PlayerIndex
                        && !StatusHelper.Has(evTargetC, StatusKind.DotImmune))
                    {
                        StatusHelper.Apply(evTargetC, StatusKind.PlaieOuverte,
                            magnitude: SpellRegistry.EventrationPlaieDmgPerTurn,
                            turnsLeft: SpellRegistry.EventrationPlaieTurns, currentTurn);
                        Log.Info($"[Spell] Eventration : Plaie Ouverte {SpellRegistry.EventrationPlaieDmgPerTurn}/tour x {SpellRegistry.EventrationPlaieTurns}t sur P{evTargetC->PlayerIndex}");
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
                    int healedAL = HealHelper.ApplyHeal(caster, healAmt);
                    if (healAmt > 0)
                        Log.Info($"[Spell] Ame Laceree : heal {healedAL} ({SpellRegistry.AmeLaceeHealPercentOfPassed}% des {lastHitHPLoss} dgts passes, demande {healAmt}) sur P{caster->PlayerIndex}");

                    if (wasKill)
                    {
                        // Croix 5 sur la cible tuee : centre + 4 cardinales.
                        GridHelpers.SetTerrain(f, killedTargetX,     killedTargetY,     TerrainKind.SangCoagule, SpellRegistry.SangCoaguleTurns, currentTurn, caster->PlayerIndex);
                        GridHelpers.SetTerrain(f, killedTargetX + 1, killedTargetY,     TerrainKind.SangCoagule, SpellRegistry.SangCoaguleTurns, currentTurn, caster->PlayerIndex);
                        GridHelpers.SetTerrain(f, killedTargetX - 1, killedTargetY,     TerrainKind.SangCoagule, SpellRegistry.SangCoaguleTurns, currentTurn, caster->PlayerIndex);
                        GridHelpers.SetTerrain(f, killedTargetX,     killedTargetY + 1, TerrainKind.SangCoagule, SpellRegistry.SangCoaguleTurns, currentTurn, caster->PlayerIndex);
                        GridHelpers.SetTerrain(f, killedTargetX,     killedTargetY - 1, TerrainKind.SangCoagule, SpellRegistry.SangCoaguleTurns, currentTurn, caster->PlayerIndex);
                        Log.Info($"[Spell] Ame Laceree KILL : Sang Coagule croix 5 pose sur ({killedTargetX},{killedTargetY}) pour {SpellRegistry.SangCoaguleTurns} tours");
                    }

                    // Cooldown 4 tours.
                    caster->LastAmeLaceeUsedOnTurn = currentTurn;
                    Log.Info($"[Spell] Ame Laceree : cooldown {SpellRegistry.AmeLaceeCooldownTurns} tours depuis tour {currentTurn} sur P{caster->PlayerIndex}");
                    break;
                }

                case SpellId.SoulrenderCauterisation: // SANG BOUILLANT (refonte 29 mai)
                    // Plus d'anti-DoT cleanse (le bleed reste menaçant, decision Lorenzo). A la place :
                    //   applique SangBouillantActive 2 rounds. Pendant la duree, chaque fois que le
                    //   Soulrender SUBIT des degats -> +1 HG + sa prochaine frappe gagne +30 dgts
                    //   (NextStrikeBonus). Geres dans la boucle de degats (cote victime) + le calcul
                    //   offensif. "1x actif" = refresh natif (recast remet 2 rounds, pas de stack).
                    StatusHelper.Apply(caster, StatusKind.SangBouillantActive, magnitude: 0,
                        turnsLeft: SpellRegistry.SangBouillantTurns, currentTurn);
                    Log.Info($"[Spell] Sang Bouillant : actif {SpellRegistry.SangBouillantTurns} rounds sur P{caster->PlayerIndex}");
                    break;

                // Patch 5 juin — Salve Mortelle ET Détonation Onirique "déclenchent tes embûches" sous
                //   leur zone d'effet. Décision Lorenzo « détoner tous (chaîne) » : TOUS les pièges du
                //   Nightseer sous l'AoE partent, qu'un ennemi soit dessus ou non (ennemi présent =
                //   dégâts/catapulte + Traqué + chaîne mines + PR ; case vide = piège consommé sans
                //   effet). Avant : Salve ne déclenchait que sur un ennemi occupant, Détonation rien du
                //   tout. Les dégâts directs (Salve 200/120 + Traqué, Détonation 170 + 80 si pièges)
                //   sont déjà appliqués par le damage loop ; ici on ne gère que la détonation des pièges.
                // Distinction nette (6 juin, choix Lorenzo) :
                //   - Détonation Onirique (croix de 5) = setup/pression : DÉTONE tes pièges sous la zone
                //     (+30 + Traqué) et GÉNÈRE du PR (coût 0 PR).
                //   - Salve Mortelle (carré 3x3) = finisher d'exécution : NE détone PAS les pièges,
                //     DÉPENSE 3 PR, gros burst + bonus Traqué renforcé (+90), relance 2 tours. Ses
                //     dégâts (200/120 + 90 Traqué) sont gérés dans le damage loop ; ici on ajoute le
                //     bonus pièges : +50 par piège du caster sous la zone, SANS les consommer (ils
                //     restent posés -> ≠ Détonation qui les détone).
                case SpellId.NightseerSalveMortelle:
                    FogHelpers.ApplyZoneTrapBonusNoConsume(f, effectBuffer, effectCount,
                        caster->PlayerIndex, SpellRegistry.SalveMortelleTrapBonusDmg);
                    break;
                case SpellId.NightseerDetonationOnirique:
                    FogHelpers.DetonateOwnTrapsInArea(f, effectBuffer, effectCount, caster->PlayerIndex, currentTurn);
                    break;

                // -------------------------------------------------------------
                // NIGHTSEER 2.15.b — TACTIQUES
                // -------------------------------------------------------------

                case SpellId.NightseerMarqueDuChasseur:
                {
                    // AFFUT (patch 7 juin) : self-buff +2 portee / +10% dgts pendant 2 tours.
                    //   Ne pose PLUS Traqué. Magnitude = % dgts (lu par tooltips/preview). Relance 3 tours
                    //   (moteur generique). +1 PR (sort de setup, conserve l'economie de ressource).
                    StatusHelper.Apply(caster, StatusKind.AffutActive,
                        magnitude: SpellRegistry.AffutDmgBonusPct, turnsLeft: SpellRegistry.AffutTurns, currentTurn);
                    Log.Info($"[Spell] Affût : +{SpellRegistry.AffutRangeBonus} portée / +{SpellRegistry.AffutDmgBonusPct}% dgts pendant {SpellRegistry.AffutTurns} tours sur P{caster->PlayerIndex}");
                    NightseerPassif.GainPrescienceForPlayer(f, caster->PlayerIndex, currentTurn, "Affût (setup)");
                    break;
                }

                case SpellId.NightseerVoileDOmbre:
                {
                    // REPLI ÉPINEUX (patch 7 juin) : repousse de 3 cases TOUS les ennemis adjacents
                    //   (Manhattan 1) loin du Nightseer, puis heal 100. Survie / désengagement.
                    int casterXRE = caster->GridX;
                    int casterYRE = caster->GridY;
                    int[] dxRE = { 1, -1, 0, 0 };
                    int[] dyRE = { 0, 0, 1, -1 };
                    for (int i = 0; i < 4; i++)
                    {
                        int nx = casterXRE + dxRE[i];
                        int ny = casterYRE + dyRE[i];
                        if (!GridHelpers.InBounds(nx, ny)) continue;
                        EntityRef adjTarget = GridHelpers.GetOccupant(f, nx, ny);
                        if (adjTarget == EntityRef.None || adjTarget == casterEntity) continue;
                        if (!f.Unsafe.TryGetPointer<Combatant>(adjTarget, out Combatant* adjC)) continue;
                        if (adjC->PlayerIndex == caster->PlayerIndex) continue; // skip alliés (1v1 = N/A)
                        PushAndTrigger(f, adjC, adjTarget, casterXRE, casterYRE,
                            SpellRegistry.RepliEpineuxPush, currentTurn, caster);
                        Log.Info($"[Spell] Repli Épineux : push {SpellRegistry.RepliEpineuxPush} sur P{adjC->PlayerIndex}");
                    }
                    int healedRE = HealHelper.ApplyHeal(caster, SpellRegistry.RepliEpineuxHeal);
                    Log.Info($"[Spell] Repli Épineux : heal {healedRE} sur P{caster->PlayerIndex}");
                    break;
                }

                // 3.5.b.i — Inoculation : applique 2 marques venin sur la cible ennemie (cap 4
                // gere par VeninHelpers.ApplyMark). Patch 8 juin : les 30 dgts directs sont appliques
                // par le pipeline generique (IsOffensive=1 / DamageAmount=30) ; ici on ajoute les marques.
                // Filter Enemy + LoS check deja appliques en amont. Putrefaction Necram +1 PT (hook ApplyMark).
                case SpellId.NecramInoculation:
                {
                    EntityRef target = GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY);
                    if (target != EntityRef.None
                        && f.Unsafe.TryGetPointer<Combatant>(target, out Combatant* targetInoc)
                        && targetInoc->PlayerIndex != caster->PlayerIndex
                        && targetInoc->HP > 0)
                    {
                        VeninHelpers.ApplyMark(f, targetInoc, SpellRegistry.InoculationMarksApplied, currentTurn);
                        Log.Info($"[Spell] Inoculation : +{SpellRegistry.InoculationMarksApplied} marques venin sur P{targetInoc->PlayerIndex} (+{SpellRegistry.InoculationDmg} dgts via pipeline)");
                    }
                    break;
                }

                // Refonte 29 mai — DÉTONATION VIRULENTE : TICK VENIN complet instantané sur la cible
                //   (stacks * clock Floraison + Marque Sacrificielle), bypass shield + réduction,
                //   SANS consommer les marques (rejouable chaque tour, cap 1x). Déclenche Symbiose.
                case SpellId.NecramDetonationVirulente:
                {
                    EntityRef dvTarget = GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY);
                    if (dvTarget != EntityRef.None
                        && f.Unsafe.TryGetPointer<Combatant>(dvTarget, out Combatant* dvC)
                        && dvC->PlayerIndex != caster->PlayerIndex
                        && dvC->HP > 0
                        && dvC->VeninStacks > 0)
                    {
                        // FIX MIROIR v141 : palier base sur le venin subi par la cible (pool du caster).
                        int dvDensity = VeninHelpers.GetDensityOnTeam(f, dvC->PlayerIndex);
                        int dvPerMark = VeninHelpers.GetTickDmgPerMark(dvDensity);
                        int dvMarqueSac = StatusHelper.GetMagnitude(dvC, StatusKind.MarqueSacrificielle, 0);
                        int dvTotal = dvC->VeninStacks * dvPerMark + dvMarqueSac;

                        int dvHpBefore = dvC->HP;
                        dvC->HP -= dvTotal; // bypass shield + réduction (comme un tick venin standard)
                        if (dvC->HP < 0) dvC->HP = 0;
                        dvC->DamageTakenThisRound += dvTotal;
                        dvC->HitsTakenThisRound += 1;
                        Log.Info($"[Spell] Détonation Virulente : tick instantané {dvC->VeninStacks} marques * {dvPerMark} + {dvMarqueSac} (MarqueSac) = -{dvTotal} HP sur P{dvC->PlayerIndex} (marques NON consommées, HP {dvHpBefore}->{dvC->HP})");

                        // Hook Symbiose Morbide (heal flat par tick venin) sur les Necram porteurs.
                        var dvSymFilter = f.Filter<Combatant>();
                        while (dvSymFilter.NextUnsafe(out EntityRef _, out Combatant* dvNec))
                        {
                            if (dvNec->Class != NymoraClass.Necram || dvNec->HP <= 0) continue;
                            // FIX MIROIR v141 : seul le caster (proprietaire du venin) est soigne.
                            if (dvNec->PlayerIndex != caster->PlayerIndex) continue;
                            int dvHealPerTick = StatusHelper.GetMagnitude(dvNec, StatusKind.SymbioseMorbide, 0);
                            if (dvHealPerTick <= 0) continue;
                            int dvNecBefore = dvNec->HP;
                            dvNec->HP = dvNec->HP + dvHealPerTick > dvNec->MaxHP ? dvNec->MaxHP : dvNec->HP + dvHealPerTick;
                            if (dvNec->HP != dvNecBefore)
                                Log.Info($"[Symbiose Morbide] Détonation Virulente : Necram P{dvNec->PlayerIndex} heal +{dvNec->HP - dvNecBefore} HP");
                        }
                    }
                    else
                    {
                        Log.Info($"[Spell] Détonation Virulente : aucune cible marquée vivante en ({cmd.TargetX},{cmd.TargetY}) — sans effet.");
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
                // Refonte 29 mai : status SymbioseMorbide sur le caster Necram (heal FLAT +15 HP par
                // tick venin sur un ennemi, n'echelonne plus par marque), duree 2 rounds. Hook dans
                // VeninHelpers.TryTick.
                case SpellId.NecramSymbioseMorbide:
                {
                    StatusHelper.Apply(caster, StatusKind.SymbioseMorbide,
                        magnitude: SpellRegistry.SymbioseMorbideHealPerMarkPerTick,
                        turnsLeft: SpellRegistry.SymbioseMorbideTurns,
                        currentTurn);
                    Log.Info($"[Spell] Symbiose Morbide active sur P{caster->PlayerIndex} : heal FLAT +{SpellRegistry.SymbioseMorbideHealPerMarkPerTick} HP/tick venin pendant {SpellRegistry.SymbioseMorbideTurns} rounds");
                    break;
                }

                // 3.5.b.iii — Pas Spectral (Bible V7.1) : 2 PA self. +2 PM ce tour (cap si refresh
                // meme sub-turn — eviter exploit re-cast pour stacker PM) + Apply PasSpectralReady
                // turnsLeft=1 magnitude=0. Le status est consume dans TurnSystem.EnterTurnEnd quand
                // ActivePlayerIndex == porteur (= fin de SON sub-turn). Tant que actif :
                // MovementSystem passe ignoreEnemyOccupants=true a A* et pose +1 marque venin par
                // ennemi present sur les cases intermediaires du path.
                case SpellId.NecramPasSpectral: // ÉCHANGE SPECTRAL (refonte 29 mai)
                {
                    // 80 dgts déjà appliqués par le pipeline. Ici : SWAP de place caster <-> cible.
                    //   Bloqué si la cible est morte du coup ou AnchorImmune (Ancrage).
                    EntityRef esTarget = GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY);
                    if (esTarget != EntityRef.None
                        && f.Unsafe.TryGetPointer<Combatant>(esTarget, out Combatant* esTargetC)
                        && esTargetC->PlayerIndex != caster->PlayerIndex
                        && esTargetC->HP > 0)
                    {
                        if (StatusHelper.Has(esTargetC, StatusKind.AnchorImmune))
                        {
                            Log.Info($"[Ancrage] Échange Spectral : swap annulé sur P{esTargetC->PlayerIndex} (AnchorImmune). 80 dgts appliqués quand même.");
                        }
                        else
                        {
                            int esCasterX = caster->GridX, esCasterY = caster->GridY;
                            int esTargetX = esTargetC->GridX, esTargetY = esTargetC->GridY;
                            GridHelpers.SetOccupant(f, esCasterX, esCasterY, EntityRef.None);
                            GridHelpers.SetOccupant(f, esTargetX, esTargetY, EntityRef.None);
                            caster->GridX = esTargetX; caster->GridY = esTargetY;
                            esTargetC->GridX = esCasterX; esTargetC->GridY = esCasterY;
                            caster->Facing = FacingHelpers.FacingFromGridDelta(esCasterX - esTargetX, esCasterY - esTargetY);
                            esTargetC->Facing = FacingHelpers.FacingFromGridDelta(esTargetX - esCasterX, esTargetY - esCasterY);
                            GridHelpers.SetOccupant(f, esTargetX, esTargetY, casterEntity);
                            GridHelpers.SetOccupant(f, esCasterX, esCasterY, esTarget);
                            Log.Info($"[Spell] Échange Spectral : swap P{caster->PlayerIndex} <-> P{esTargetC->PlayerIndex} ({esCasterX},{esCasterY}) <-> ({esTargetX},{esTargetY})");
                            // Fix #5 — un SWAP est un deplacement force pour les DEUX : declenche les pieges
                            //   ennemis sur les 2 cases d'arrivee (owner-filtre dans TryTriggerTrapOnEnter).
                            FogHelpers.TryTriggerTrapOnEnter(f, casterEntity, caster, esTargetX, esTargetY, currentTurn);
                            FogHelpers.TryTriggerTrapOnEnter(f, esTarget, esTargetC, esCasterX, esCasterY, currentTurn);
                        }
                    }
                    break;
                }

                // NUÉE DE SPORES (ex-Voile de Pestilence, refonte 29 mai) : 3 PA self. Apply
                //   PestilenceAura 2 rounds (refresh-only). Hook unique : tant qu'actif, chaque sort
                //   du Necram visant un ennemi pose +1 marque venin bonus (cf bloc post-cast TryCastSpell).
                case SpellId.NecramVoilePestilence:
                {
                    StatusHelper.Apply(caster, StatusKind.PestilenceAura,
                        magnitude: 0,
                        turnsLeft: SpellRegistry.VoilePestilenceTurns,
                        currentTurn);
                    Log.Info($"[Spell] Nuée de Spores active sur P{caster->PlayerIndex} : {SpellRegistry.VoilePestilenceTurns} rounds, +1 marque bonus sur tes sorts visant un ennemi");
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

                // 3.7.c.i — Linceul d'Ombres (Bible V7.1 ligne 1173) : 3 PA self. Apply
                // ShieldActive 130 HP / 2 rounds + LinceulDOmbres flag (Magnitude=40
                // dgts riposte) / 2 rounds. Hook damage loop standard + Charge Brutale
                // custom : si target porte LinceulDOmbres ET attaque melee -> renvoie
                // 40 dgts sur attaquant (pipeline reduction% + shield attaquant).
                // Refresh-only (recast meme tour reset shield + duree). Bible
                // "Anti-Soulrender qui charge".
                case SpellId.GhostraLinceulDOmbres:
                {
                    StatusHelper.Apply(caster, StatusKind.ShieldActive,
                        magnitude: SpellRegistry.LinceulDOmbresShieldHP,
                        turnsLeft: SpellRegistry.LinceulDOmbresTurns,
                        currentTurn);
                    StatusHelper.Apply(caster, StatusKind.LinceulDOmbres,
                        magnitude: SpellRegistry.LinceulDOmbresRipostMeleeDmg,
                        turnsLeft: SpellRegistry.LinceulDOmbresTurns,
                        currentTurn);
                    Log.Info($"[Spell] Linceul d'Ombres active sur P{caster->PlayerIndex} : Shield {SpellRegistry.LinceulDOmbresShieldHP} HP / {SpellRegistry.LinceulDOmbresTurns} rounds + flag riposte {SpellRegistry.LinceulDOmbresRipostMeleeDmg} dgts melee (pipeline reduction+shield)");
                    break;
                }

                // 3.7.c.ii — Voile Spectral (REWORK 30 mai) : SETUP. Téléporte tous les leurres
                //   actifs de la Ghostra autour de l'ennemi ciblé (cmd.TargetX/Y). Cardinales en
                //   priorité (Manhattan 1 -> enchaîne Éveil/Nuée dorsal), diagonales en secours.
                //   Plus de cleanse anti-DoT. Gate >=1 leurre fait en amont (pré-PA).
                // 3.7.c.ii — Voile Spectral (rework #21, 5 juin) : le TP des leurres autour de la cible ET
                //   les 60 dmg/leurre adjacent sont désormais gérés AVANT le damage loop (mutation après
                //   commit + section effectiveDmg) pour compter les leurres POST-TP. Plus de handler ici.

                // 3.7.c.iii — Réplique Protectrice (Bible V7.1 ligne 1187) : 3 PA range 3
                // case vide. Pose un DecoyKind.Protective (HP=200) qui redirige 40% des dgts
                // subis par la Ghostra (hook dans damage loop apres reduction%, avant shield).
                // Si decoy meurt -> heal +60 HP Ghostra (DestroyByEnemyAction).
                // Validation cap 3 leurres + case vide gere par DecoyHelpers.TrySpawn (rejet
                // silencieux si cap atteint ou case occupee, PA deja consume - choix MVP).
                case SpellId.GhostraRepliqueProtectrice:
                {
                    // #22 (5 juin) : poser un 4e leurre (cap 3 atteint) vire le PLUS ANCIEN au lieu de partir dans le vide.
                    bool spawned = DecoyHelpers.TrySpawnEvictingOldest(f, caster, cmd.TargetX, cmd.TargetY,
                        DecoyKind.Protective, currentTurn);
                    if (spawned)
                    {
                        Log.Info($"[Spell] Réplique Protectrice : P{caster->PlayerIndex} pose decoy PROTECTIVE en ({cmd.TargetX},{cmd.TargetY}) HP={DecoyHelpers.ProtectiveDecoyMaxHP} (redirection {SpellRegistry.RepliqueProtectriceRedirectPercent}% dmg subis, heal +{DecoyHelpers.RepliqueProtectriceHealOnDestroy} HP si detruit)");
                    }
                    else
                    {
                        Log.Warn($"[Spell] Réplique Protectrice rejete : DecoyHelpers.TrySpawn refuse (cap atteint, case occupee, ou hors grille) — PA deja consume");
                    }
                    break;
                }

                // 3.7.c.iv — Dernier Pas (Bible V7.1 ligne 1194) : 4 PA self panic-button.
                // Gate HP<30% deja verifie en pre-PA. Effet en 3 temps :
                //   (1) Heal +200 HP cap MaxHP.
                //   (2) Teleport sur case vide (cmd.TargetX, cmd.TargetY) via MoveNonPM.
                //   (3) Pose DecoyKind.Standard sur case quittee. Cap 3 : si atteint, destroy
                //       le leurre LE PLUS ANCIEN (min SpawnedOnTurn) puis spawn nouveau.
                //       Bloque PAS l'effet (panic-button doit toujours laisser un leurre).
                case SpellId.GhostraDernierPas:
                {
                    // (1) Heal
                    int hpBeforeDP = caster->HP;
                    int hpAfterDP = hpBeforeDP + SpellRegistry.DernierPasHealAmount;
                    if (hpAfterDP > caster->MaxHP) hpAfterDP = caster->MaxHP;
                    caster->HP = hpAfterDP;
                    int realHealDP = hpAfterDP - hpBeforeDP;
                    Log.Info($"[Spell] Dernier Pas : heal P{caster->PlayerIndex} +{realHealDP} HP ({hpBeforeDP} -> {hpAfterDP})");

                    // (2) Teleport : capture position d'origine avant move pour pose leurre apres.
                    int dpOldX = caster->GridX;
                    int dpOldY = caster->GridY;
                    bool dpMoved = MovementHelpers.MoveNonPM(f, casterEntity, caster, cmd.TargetX, cmd.TargetY);
                    if (!dpMoved)
                    {
                        // Edge case : teleport rejete (case occupee surprise post-validation). On laisse
                        // le heal applique mais skip pose leurre (case d'origine = case actuelle Ghostra).
                        Log.Warn($"[Spell] Dernier Pas : teleport ({cmd.TargetX},{cmd.TargetY}) refuse par MoveNonPM. Heal applique mais pas de pose leurre.");
                        break;
                    }
                    Log.Info($"[Spell] Dernier Pas : P{caster->PlayerIndex} teleport ({dpOldX},{dpOldY}) -> ({cmd.TargetX},{cmd.TargetY})");

                    // (3) Pose leurre Standard sur case quittee. Cap 3 : évince le plus ancien (#22, helper partagé).
                    bool dpDecoySpawned = DecoyHelpers.TrySpawnEvictingOldest(f, caster, dpOldX, dpOldY,
                        DecoyKind.Standard, currentTurn);
                    if (dpDecoySpawned)
                    {
                        Log.Info($"[Spell] Dernier Pas : pose leurre Standard en ({dpOldX},{dpOldY}) (case quittee)");
                    }
                    else
                    {
                        Log.Warn($"[Spell] Dernier Pas : pose leurre Standard rejetee en ({dpOldX},{dpOldY}) (edge case TrySpawn)");
                    }
                    break;
                }

                // 3.7.c — Communion Spectrale (refonte 30 mai, ex-Pas de l'Au-Dela slot 100) :
                //   Consomme 1 leurre actif (premier slot) -> heal 150 (cap MaxHP, bloque par
                //   AntiHealShield via HealHelper). Gate >=1 leurre fait en amont (pré-PA).
                case SpellId.GhostraCommunionSpectrale:
                {
                    int comSlot = -1;
                    for (int i = 0; i < DecoyHelpers.MaxDecoys; i++)
                    {
                        if (caster->Decoys[i].Kind != DecoyKind.None) { comSlot = i; break; }
                    }
                    if (comSlot >= 0)
                    {
                        DecoyHelpers.DestroyAtSlot(caster, comSlot);
                        int healedCom = HealHelper.ApplyHeal(caster, SpellRegistry.CommunionHeal);
                        Log.Info($"[Spell] Communion Spectrale : consomme leurre slot {comSlot} -> heal {healedCom} HP (demande {SpellRegistry.CommunionHeal}) sur P{caster->PlayerIndex}");
                    }
                    else
                    {
                        Log.Warn($"[Spell] Communion Spectrale : aucun leurre à consommer (PA déjà consommé) — cas rare");
                    }
                    break;
                }

                // 3.7.d — Execution Spectrale (Bible V7.1 ligne 1071) SIGNATURE Ghostra.
                //   Pre-PA gates (3 leurres + cooldown) deja valides. PA deja consume (pipeline).
                //   Pipeline custom (DamageAmount=0 dans SpellDef) :
                //     (1) Capture positions des leurres slots 0 et 1 (re-spawn potentiel sur kill).
                //     (2) Destroy 3 leurres inconditionnellement (Bible "consomme TOUS les leurres actifs").
                //     (3) Set cooldown (LastExecutionSpectraleUsedOnTurn = currentTurn).
                //     (4) Check FacingHelpers.IsDorsalHit :
                //         - false : log RATE, break (PA + 3 leurres + cooldown deja consommes).
                //         - true  : apply 350 dmg direct HP (bypass shield/reduction, decision Lorenzo
                //                   Bible-stricte "350 dmg" net signature) + apply PlaieOuverte refresh
                //                   mag=50 turnsLeft=3 (override plaie standard 40/2). Si kill :
                //                   heal +100 caster (respect AntiHealShield) + TrySpawn 2 Standard
                //                   aux positions slots 0/1 capturees.
                case SpellId.GhostraExecutionSpectrale:
                {
                    // Resolve target ennemi adjacent (Filter=Enemy range 1 - validation pipeline standard).
                    EntityRef esTargetEntity = GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY);
                    if (esTargetEntity == EntityRef.None
                        || !f.Unsafe.TryGetPointer<Combatant>(esTargetEntity, out Combatant* esTarget)
                        || esTarget->HP <= 0)
                    {
                        Log.Warn($"[Spell] Execution Spectrale : aucune cible vivante en ({cmd.TargetX},{cmd.TargetY}) - PA consume mais effet skip");
                        // Pas de consume leurres / cooldown si target invalide (edge case post-validation).
                        break;
                    }

                    // (1) Capture positions des leurres slots 0 et 1 pour re-spawn potentiel.
                    int esCapturedDecoy0X = caster->Decoys[0].PosX;
                    int esCapturedDecoy0Y = caster->Decoys[0].PosY;
                    int esCapturedDecoy1X = caster->Decoys[1].PosX;
                    int esCapturedDecoy1Y = caster->Decoys[1].PosY;

                    // (2) Destroy les 3 leurres inconditionnellement.
                    DecoyHelpers.DestroyAtSlot(caster, 0);
                    DecoyHelpers.DestroyAtSlot(caster, 1);
                    DecoyHelpers.DestroyAtSlot(caster, 2);
                    Log.Info($"[Spell] Execution Spectrale : P{caster->PlayerIndex} consomme 3 leurres (positions capturees pour kill bonus : ({esCapturedDecoy0X},{esCapturedDecoy0Y}) + ({esCapturedDecoy1X},{esCapturedDecoy1Y}))");

                    // (3) Set cooldown inconditionnellement.
                    caster->LastExecutionSpectraleUsedOnTurn = currentTurn;
                    Log.Info($"[Spell] Execution Spectrale : cooldown {SpellRegistry.ExecutionSpectraleCooldownTurns} tours depuis T{currentTurn} sur P{caster->PlayerIndex}");

                    // (4) Check dorsal.
                    bool esIsDorsal = FacingHelpers.IsDorsalHit(caster, esTarget);
                    if (!esIsDorsal)
                    {
                        Log.Warn($"[Spell] Execution Spectrale RATE : cible P{esTarget->PlayerIndex} pas dorsale (facing {esTarget->Facing}). PA + 3 leurres + cooldown consommes, 0 dmg, 0 plaie (Bible-stricte 'le coup le plus risque du jeu').");
                        break;
                    }

                    // (4a) Apply 350 dmg direct (bypass pipeline shield/reduction Bible-stricte signature).
                    int esHpBefore = esTarget->HP;
                    int esDmg = SpellRegistry.ExecutionSpectraleDamage;
                    esTarget->HP -= esDmg;
                    if (esTarget->HP < 0) esTarget->HP = 0;
                    int esRealDmg = esHpBefore - esTarget->HP;
                    esTarget->DamageTakenThisRound += esRealDmg;
                    Log.Info($"[Spell] Execution Spectrale DORSAL HIT : -{esRealDmg} HP direct sur P{esTarget->PlayerIndex} ({cmd.TargetX},{cmd.TargetY}) HP {esHpBefore} -> {esTarget->HP}");

                    // (4b) Apply PlaieOuverte refresh override (50/3) - ecrase plaie standard si presente.
                    if (esTarget->HP > 0)
                    {
                        StatusHelper.Apply(esTarget, StatusKind.PlaieOuverte,
                            magnitude: SpellRegistry.ExecutionSpectralePlaieDmgPerTurn,
                            turnsLeft: SpellRegistry.ExecutionSpectralePlaieTurns,
                            currentTurn);
                        Log.Info($"[Spell] Execution Spectrale : PlaieOuverte refresh applique sur P{esTarget->PlayerIndex} ({SpellRegistry.ExecutionSpectralePlaieDmgPerTurn}/tour x {SpellRegistry.ExecutionSpectralePlaieTurns} rounds)");
                    }

                    // (4c) Check kill : heal +100 + re-spawn 2 leurres.
                    if (esTarget->HP <= 0)
                    {
                        // Heal caster (respect AntiHealShield comme les autres signatures).
                        if (StatusHelper.Has(caster, StatusKind.AntiHealShield))
                        {
                            Log.Info($"[Spell] Execution Spectrale KILL : heal {SpellRegistry.ExecutionSpectraleKillHeal} BLOQUE par AntiHealShield sur P{caster->PlayerIndex}");
                        }
                        else
                        {
                            int esCasterHpBefore = caster->HP;
                            caster->HP += SpellRegistry.ExecutionSpectraleKillHeal;
                            if (caster->HP > caster->MaxHP) caster->HP = caster->MaxHP;
                            int esRealHeal = caster->HP - esCasterHpBefore;
                            Log.Info($"[Spell] Execution Spectrale KILL : heal P{caster->PlayerIndex} +{esRealHeal} HP ({esCasterHpBefore} -> {caster->HP})");
                        }

                        // Re-spawn 2 leurres Standard aux positions slots 0/1 captees.
                        // TrySpawn rejette silencieusement si la case n'est plus libre (edge case
                        // post-consume : un ennemi a bouge sur la case entre temps - tres rare).
                        bool esRespawn0 = DecoyHelpers.TrySpawn(f, caster, esCapturedDecoy0X, esCapturedDecoy0Y,
                            DecoyKind.Standard, currentTurn);
                        bool esRespawn1 = DecoyHelpers.TrySpawn(f, caster, esCapturedDecoy1X, esCapturedDecoy1Y,
                            DecoyKind.Standard, currentTurn);
                        Log.Info($"[Spell] Execution Spectrale KILL : re-spawn 2 leurres Standard (slot 0 @({esCapturedDecoy0X},{esCapturedDecoy0Y}) success={esRespawn0}, slot 1 @({esCapturedDecoy1X},{esCapturedDecoy1Y}) success={esRespawn1})");
                    }
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

                // 3.5.c.v — Cocon Putride (Bible V7.1 panic signature) : 4 PA self, gate HP <30%
                // verifie en amont. Heal Necram 220 HP (cap MaxHP) + applique +1 marque venin sur
                // tous ennemis vivants Manhattan <=4 du caster. 1x/match via OncePerMatchBit
                // (gere par le systeme generique). En 1v1 : marque 1 ennemi. En 2v2/3v3 :
                // marque jusqu'a 3 ennemis -> boost massif Putrefaction (cap +2 PT/tour Necram
                // respecte par GainPutrefactionFromMarkApply via ApplyMark).
                case SpellId.NecramCoconPutride:
                {
                    int hpBeforeCocon = caster->HP;
                    caster->HP += SpellRegistry.CoconPutrideHealAmount;
                    if (caster->HP > caster->MaxHP) caster->HP = caster->MaxHP;
                    Log.Info($"[Spell] Cocon Putride : P{caster->PlayerIndex} heal +{SpellRegistry.CoconPutrideHealAmount} HP HP {hpBeforeCocon} -> {caster->HP}");

                    int enemiesMarked = 0;
                    var coconFilter = f.Filter<Combatant>();
                    while (coconFilter.NextUnsafe(out EntityRef _, out Combatant* coconEnemy))
                    {
                        if (coconEnemy->HP <= 0) continue;
                        if (coconEnemy->PlayerIndex == caster->PlayerIndex) continue;
                        int dxC = coconEnemy->GridX - caster->GridX; if (dxC < 0) dxC = -dxC;
                        int dyC = coconEnemy->GridY - caster->GridY; if (dyC < 0) dyC = -dyC;
                        int distC = dxC + dyC;
                        if (distC > SpellRegistry.CoconPutrideMarksRange) continue;
                        int stacksBeforeCocon = coconEnemy->VeninStacks;
                        VeninHelpers.ApplyMark(f, coconEnemy, SpellRegistry.CoconPutrideMarksPerEnemy, currentTurn);
                        enemiesMarked++;
                        Log.Info($"[Cocon Putride] P{caster->PlayerIndex} marque P{coconEnemy->PlayerIndex} (Manhattan {distC} <= {SpellRegistry.CoconPutrideMarksRange}) : stacks {stacksBeforeCocon} -> {coconEnemy->VeninStacks}");
                    }
                    Log.Info($"[Spell] Cocon Putride : {enemiesMarked} ennemi(s) marque(s) dans rayon {SpellRegistry.CoconPutrideMarksRange} du Necram P{caster->PlayerIndex}");
                    break;
                }

                // 3.5.c.vi — Virus Fatal (SIGNATURE Necram, Bible V7.1 lignes 855-866) : declenche
                // un tick venin instantane * 3 sur la cible (multiplicateur Floraison applique).
                // 6 PT deja consommes par le pipeline generique (HGCostMandatory=6 -> Resource=0).
                // Damage = (stacks * GetTickDmgPerMark(densityGlobal) + MarqueSacBonus) * 3.
                // Bypass shield + reduction (comme TryTick standard). Hook Symbiose Morbide x3.
                // Si cible survit : VeninStacks = 0 (consommees). Si cible meurt : marques
                // transferees sur ennemi vivant le plus proche via TryTransferVeninOnKill
                // (Bible "marques restent disponibles pour Contagion / Detonation Virulente sur
                // d'autres cibles"). En 1v1 = perdues silencieusement. Set cooldown apres.
                case SpellId.NecramVirusFatal:
                {
                    EntityRef vfTargetEntity = GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY);
                    if (vfTargetEntity == EntityRef.None
                        || !f.Unsafe.TryGetPointer<Combatant>(vfTargetEntity, out Combatant* vfTarget))
                    {
                        Log.Warn($"[Spell] Virus Fatal : pas de cible vivante en ({cmd.TargetX},{cmd.TargetY})");
                        caster->LastVirusFatalUsedOnTurn = currentTurn; // cooldown applique meme si miss (PT consommee deja)
                        break;
                    }
                    if (vfTarget->HP <= 0)
                    {
                        Log.Warn($"[Spell] Virus Fatal : cible deja morte");
                        caster->LastVirusFatalUsedOnTurn = currentTurn;
                        break;
                    }
                    if (vfTarget->VeninStacks <= 0)
                    {
                        Log.Warn($"[Spell] Virus Fatal : cible sans marques venin (cast gaspille). Cooldown active, PT consommees.");
                        caster->LastVirusFatalUsedOnTurn = currentTurn;
                        break;
                    }

                    int vfStacks = vfTarget->VeninStacks;
                    // FIX MIROIR v141 : palier base sur le venin subi par la cible (pool du caster).
                    int vfDensity = VeninHelpers.GetDensityOnTeam(f, vfTarget->PlayerIndex);
                    int vfDmgPerMark = VeninHelpers.GetTickDmgPerMark(vfDensity);
                    int vfBaseDmg = vfStacks * vfDmgPerMark;
                    int vfMarqueSacBonus = StatusHelper.GetMagnitude(vfTarget, StatusKind.MarqueSacrificielle, 0);
                    // Refonte 29 mai : x2.5 (= * 5 / 2) au lieu de x3.
                    int vfTotalDmg = (vfBaseDmg + vfMarqueSacBonus) * SpellRegistry.VirusFatalMultNum / SpellRegistry.VirusFatalMultDen;

                    // Bypass shield + reduction (comme tick venin standard via VeninHelpers.TryTick).
                    int vfHpBefore = vfTarget->HP;
                    vfTarget->HP -= vfTotalDmg;
                    if (vfTarget->HP < 0) vfTarget->HP = 0;
                    vfTarget->DamageTakenThisRound += vfTotalDmg;
                    bool vfKilled = (vfTarget->HP <= 0);

                    Log.Info($"[Virus Fatal] P{caster->PlayerIndex} -> P{vfTarget->PlayerIndex} : {vfStacks} marques * {vfDmgPerMark} dmg/marque (density {vfDensity}) + {vfMarqueSacBonus} (MarqueSac) tout x1.5 = -{vfTotalDmg} HP (HP {vfHpBefore} -> {vfTarget->HP}, killed={vfKilled})");

                    // Symbiose Morbide hook (refonte 29 mai : heal FLAT par tick) : le méga-tick Virus
                    // Fatal applique le heal flat x2.5.
                    var vfSymFilter = f.Filter<Combatant>();
                    while (vfSymFilter.NextUnsafe(out EntityRef _, out Combatant* vfNec))
                    {
                        if (vfNec->Class != NymoraClass.Necram) continue;
                        if (vfNec->HP <= 0) continue;
                        // FIX MIROIR v141 : seul le caster (proprietaire du venin) est soigne.
                        if (vfNec->PlayerIndex != caster->PlayerIndex) continue;
                        int vfHealPerTick = StatusHelper.GetMagnitude(vfNec, StatusKind.SymbioseMorbide, 0);
                        if (vfHealPerTick <= 0) continue;
                        int vfHealAmount = vfHealPerTick * SpellRegistry.VirusFatalMultNum / SpellRegistry.VirusFatalMultDen;
                        int vfNecHpBefore = vfNec->HP;
                        vfNec->HP = vfNec->HP + vfHealAmount > vfNec->MaxHP ? vfNec->MaxHP : vfNec->HP + vfHealAmount;
                        int vfRealHeal = vfNec->HP - vfNecHpBefore;
                        if (vfRealHeal > 0)
                        {
                            Log.Info($"[Symbiose Morbide] Virus Fatal x1.5 : Necram P{vfNec->PlayerIndex} heal +{vfRealHeal} HP : {vfNecHpBefore}->{vfNec->HP}");
                        }
                    }

                    // Marques : consommees si survit, transferees si tuee (Bible).
                    if (vfKilled)
                    {
                        VeninHelpers.TryTransferVeninOnKill(f, vfTarget, caster->PlayerIndex, currentTurn);
                    }
                    else
                    {
                        vfTarget->VeninStacks = 0;
                        Log.Info($"[Virus Fatal] Marques consommees sur P{vfTarget->PlayerIndex} (VeninStacks -> 0)");
                    }

                    // Set cooldown (Bible : Reutilisable si PT remonte a 6 ET cooldown expire).
                    caster->LastVirusFatalUsedOnTurn = currentTurn;
                    Log.Info($"[Virus Fatal] Cooldown active sur P{caster->PlayerIndex} jusqu'au tour {currentTurn + SpellRegistry.VirusFatalCooldownTurns}");
                    break;
                }

                // -------------------------------------------------------------
                // 3.7.b — GHOSTRA Tactiques
                // -------------------------------------------------------------

                // Réplique Fantôme (3.7.b.i) — Bible V7.1 ligne 1127 (amendee 16 mai sur la duree) :
                //   "Pose un Leurre sur une case vide a 4 cases. Le Leurre est visuellement
                //    identique a la Ghostra. Dure DecoyHelpers.LifetimeRounds rounds (=4 amende
                //    le 16 mai par Lorenzo, Bible orig disait 2 mais pas le temps de combo) ou
                //    jusqu'a interaction. Si le Leurre survit la duree complete, la Ghostra
                //    regagne 80 HP. Si le Leurre est detruit par un sort adverse, +40 HP."
                //
                // Implementation : wrappe DecoyHelpers.TrySpawn(DecoyKind.RepliqueFantome).
                // Le helper rejette (logs warn) si cap 3 atteint, case occupee, obstacle, leurre
                // deja la. Heal lifecycle gere dans DecoyHelpers (TickLifetime +80 / DestroyByEnemyAction +40).
                // SpellSystem core a deja consomme le PA AVANT d'appeler ce handler — si le spawn
                // echoue, le PA reste consomme (Bible : decision joueur, on n'annule pas).
                case SpellId.GhostraRepliqueFantome:
                {
                    // #22 (5 juin) : poser un 4e leurre (cap 3 atteint) vire le PLUS ANCIEN au lieu de partir dans le vide.
                    bool spawned = DecoyHelpers.TrySpawnEvictingOldest(f, caster,
                        cmd.TargetX, cmd.TargetY,
                        DecoyKind.RepliqueFantome, currentTurn);
                    if (spawned)
                    {
                        Log.Info($"[Spell] Réplique Fantôme : P{caster->PlayerIndex} pose un leurre RepliqueFantome en ({cmd.TargetX},{cmd.TargetY}) (Bible V7.1)");
                    }
                    else
                    {
                        Log.Warn($"[Spell] Réplique Fantôme : pose echouee en ({cmd.TargetX},{cmd.TargetY}) — PA deja consomme (cap atteint / case invalide)");
                    }
                    break;
                }

                // Pas dans l'Ombre (3.7.b.ii) — Bible V7.1 ligne 1134 :
                //   "Teleporte la Ghostra jusqu'a 5 cases. Si une case adjacente a l'arrivee
                //    contient une cible ennemie : la cible PIVOTE pour faire face a la Ghostra.
                //    Cout optionnel : laisser un leurre sur la case quittee (compte dans le cap 3)."
                //
                // Note ordre : on pose le leurre AVANT de bouger (depuis l'ancienne case caster),
                // puis on teleporte. Si on faisait l'inverse, la case quittee serait deja libere
                // pour le leurre mais ce serait equivalent — choisi l'ordre "pose avant move"
                // pour traiter le cap 3 de maniere coherente (si cap deja a 3, refuse pose mais
                // teleport quand meme). Caster Ghostra elle-meme bloque la case, donc TrySpawn
                // refuse avec un check ghostra->GridX==posX (cf DecoyHelpers.TrySpawn). On utilise
                // donc le pattern : memorize (oldX, oldY) AVANT move, move via MoveNonPM, puis
                // tente TrySpawn sur (oldX, oldY) maintenant que la case est libre.
                case SpellId.GhostraPasDansLOmbre:
                {
                    int oldPDOX = caster->GridX;
                    int oldPDOY = caster->GridY;

                    // Teleport via MoveNonPM : valide case vide, pas obstacle, pas leurre.
                    // Update Facing depuis dx/dy automatiquement.
                    bool tpOk = MovementHelpers.MoveNonPM(f, casterEntity, caster, cmd.TargetX, cmd.TargetY);
                    if (!tpOk)
                    {
                        Log.Warn($"[Spell] Pas dans l'Ombre : teleport echec sur ({cmd.TargetX},{cmd.TargetY}) — PA deja consomme");
                        break;
                    }
                    // Cap 1x/tour : mark used (CombatantRenderer lit ce field pour l'anim teleport).
                    caster->LastPasDansLOmbreOnTurn = currentTurn;
                    Log.Info($"[Spell] Pas dans l'Ombre : P{caster->PlayerIndex} ({oldPDOX},{oldPDOY}) -> ({cmd.TargetX},{cmd.TargetY}) facing={caster->Facing}");

                    // Pivot enemies adj Manhattan <=1 a l'arrivee : Facing target = direction Ghostra (= -direction target->Ghostra).
                    // Itere les 4 cases cardinales (Bible-strict "adjacente" = Manhattan 1).
                    int* adjDx = stackalloc int[4] { 1, -1, 0, 0 };
                    int* adjDy = stackalloc int[4] { 0, 0, 1, -1 };
                    int pivoted = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        int ax = cmd.TargetX + adjDx[i];
                        int ay = cmd.TargetY + adjDy[i];
                        if (!GridHelpers.InBounds(ax, ay)) continue;
                        EntityRef adjOcc = GridHelpers.GetOccupant(f, ax, ay);
                        if (adjOcc == EntityRef.None) continue;
                        if (!f.Unsafe.TryGetPointer<Combatant>(adjOcc, out Combatant* adjC)) continue;
                        if (adjC->PlayerIndex == caster->PlayerIndex) continue; // skip allies/self
                        if (adjC->HP <= 0) continue;
                        // Facing target = direction qui pointe vers Ghostra (caster).
                        IsoFacing newFacing = FacingHelpers.FacingFromGridDelta(cmd.TargetX - ax, cmd.TargetY - ay);
                        adjC->Facing = newFacing;
                        Log.Info($"[Spell] Pas dans l'Ombre : P{adjC->PlayerIndex} pivot -> {newFacing} (face Ghostra en {cmd.TargetX},{cmd.TargetY})");
                        pivoted++;
                    }
                    if (pivoted == 0)
                    {
                        Log.Info($"[Spell] Pas dans l'Ombre : aucune cible adjacente a pivoter");
                    }

                    // Fix 5 juin — pose TOUJOURS un leurre Standard sur la case quittée. Le toggle Shift+H
                    //   a disparu (bind retiré le 17 mai) et la conso auto du 3 juin confondait l'"option"
                    //   avec la JAUGE de leurres du Ghostra (Resource = nb de leurres) -> le leurre ne popait
                    //   que si un leurre existait DÉJÀ. Refus silencieux au cap 3 (le slot EST la ressource ;
                    //   pas d'évinçage ici, contrairement aux vrais spawners — cf #22).
                    bool spawnedPDO = DecoyHelpers.TrySpawn(f, caster, oldPDOX, oldPDOY, DecoyKind.Standard, currentTurn);
                    if (spawnedPDO)
                    {
                        Log.Info($"[Spell] Pas dans l'Ombre : leurre Standard posé sur case quittée ({oldPDOX},{oldPDOY})");
                    }
                    else
                    {
                        Log.Info($"[Spell] Pas dans l'Ombre : pose leurre case quittée ({oldPDOX},{oldPDOY}) refusée (cap 3 atteint)");
                    }
                    break;
                }

                // Permutation (3.7.b refonte 30 mai, ex-Volte-Face slot 90) :
                //   Swap instantane de position entre la Ghostra et un de ses leurres cible. Le
                //   filtre TileWithLure (valide en amont, pre-PA) garantit qu'un leurre OWN occupe
                //   la case ciblee. Cap 2x/tour gere par le moteur generique. Aucun degat.
                case SpellId.GhostraPermutation:
                {
                    int permSlot = DecoyHelpers.FindSlotAtPosition(caster, cmd.TargetX, cmd.TargetY);
                    if (permSlot < 0)
                    {
                        Log.Warn($"[Spell] Permutation : aucun leurre own en ({cmd.TargetX},{cmd.TargetY}) — PA deja consomme (cas rare)");
                        break;
                    }
                    DecoyHelpers.PermuteToSlot(f, casterEntity, caster, permSlot);
                    break;
                }

                // Marque de l'Ombre (3.7.b.v) — Bible V7.1 ligne 1155 :
                //   "Pendant 2 tours, tous les sorts de la Ghostra sur la cible gagnent +20 degats.
                //    Si la cible est touchee en dorsal pendant ces 2 tours : applique automatiquement
                //    PLAIE OUVERTE."
                //
                // Aucun damage direct. Apply status MarqueDeLOmbre 2 rounds magnitude=20. Les 2 hooks
                // (bonus +20 dmg + PlaieOuverte auto dorsal) sont gerees ailleurs :
                //   - Bonus +20 dmg : dans le pipeline damage Ghostra (bloc Ghostra ci-dessus).
                //   - PlaieOuverte auto dorsal : dans GhostraPassif.ApplyPlaieOuverteIfAngle2Plus
                //     (etendu pour bypass requirement Angle 2+ si target marquee).
                case SpellId.GhostraMarqueDeLOmbre:
                {
                    EntityRef moTarget = GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY);
                    if (moTarget != EntityRef.None
                        && f.Unsafe.TryGetPointer<Combatant>(moTarget, out Combatant* moTargetC)
                        && moTargetC->HP > 0
                        && moTargetC->PlayerIndex != caster->PlayerIndex)
                    {
                        StatusHelper.Apply(moTargetC, StatusKind.MarqueDeLOmbre,
                            magnitude: SpellRegistry.MarqueDeLOmbreDmgBonus,
                            turnsLeft: SpellRegistry.MarqueDeLOmbreDurationRounds,
                            currentTurn);
                        Log.Info($"[Spell] Marque de l'Ombre : pose sur P{moTargetC->PlayerIndex} ({SpellRegistry.MarqueDeLOmbreDmgBonus} dmg buff x {SpellRegistry.MarqueDeLOmbreDurationRounds} rounds, PlaieOuverte auto dorsal Ghostra)");
                    }
                    else
                    {
                        Log.Warn($"[Spell] Marque de l'Ombre : pas de cible ennemie vivante en ({cmd.TargetX},{cmd.TargetY}), PA deja consomme");
                    }
                    break;
                }

                // Éveil Spectral (3.7.b refonte 30 mai, ex-Dague Lancée slot 93) : AUCUN effet
                //   post-dégâts ici. Tout est géré dans TryCastSpell (boucle de dégâts) : base 100
                //   + bonus dorsal + Plaie calculés depuis la position du leurre (eveilLeurreX/Y/
                //   eveilDorsal). Le leurre n'est PAS consommé. Pas de case dédiée nécessaire.

                // Frappe Fantome (3.7.a.iii) — Bible V7.1 ligne 1095 :
                //   "Si la cible avait ete VOLTE-FACE ou que sa direction a ete modifiee ce tour :
                //    APPLIQUE PLAIE OUVERTE (40/tour x 2t)."
                //
                // Le teleport + damage 200 (+dorsal) sont deja appliques en amont. Ici on regarde
                // le flag target.LastFacingForcedOnTurn : si == currentTurn (set par Volte-Face dans
                // ce meme tour), on applique PlaieOuverte. La cible doit etre vivante (HP > 0) pour
                // recevoir le status (sinon no-op sur cadavre).
                case SpellId.GhostraFrappeFantome:
                {
                    EntityRef ffPostTarget = GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY);
                    if (ffPostTarget != EntityRef.None
                        && f.Unsafe.TryGetPointer<Combatant>(ffPostTarget, out Combatant* ffPostTargetC)
                        && ffPostTargetC->HP > 0
                        && ffPostTargetC->LastFacingForcedOnTurn == currentTurn)
                    {
                        // 3.7.c.ii — Voile Spectral : si la cible porte DotImmune, skip apply
                        // PlaieOuverte. Bible "immunisee a toute nouvelle application de DoT".
                        if (StatusHelper.Has(ffPostTargetC, StatusKind.DotImmune))
                        {
                            Log.Info($"[Spell] Frappe Fantome : SKIP PlaieOuverte sur P{ffPostTargetC->PlayerIndex} (DotImmune Voile Spectral actif)");
                        }
                        else
                        {
                            StatusHelper.Apply(ffPostTargetC, StatusKind.PlaieOuverte,
                                magnitude: GhostraPassif.PlaieOuverteDmgPerTurn,
                                turnsLeft: GhostraPassif.PlaieOuverteDurationRounds,
                                currentTurn);
                            Log.Info($"[Spell] Frappe Fantome : PLAIE OUVERTE applique sur P{ffPostTargetC->PlayerIndex} (direction forcee ce tour, {GhostraPassif.PlaieOuverteDmgPerTurn}/tour x {GhostraPassif.PlaieOuverteDurationRounds}t)");
                        }
                    }
                    break;
                }

                // Saigne-Ame (3.7.a.ii) — Bible V7.1 ligne 1109 :
                //   "Finisher conditionnel. 4 PA, range 2. 200 dgts + 70 si la cible a PLAIE
                //    OUVERTE (consomme la plaie). Si la cible meurt : la Ghostra regagne 60 HP."
                //
                // Damage 200 (+70 PlaieOuverte +dorsal) deja applique par pipeline generique.
                // Ici on gere :
                //   1. Si target SURVIT et avait PlaieOuverte -> consume (Bible : "consomme la plaie").
                //   2. Si target MEURT (wasKill) -> caster heal +60 HP (cap MaxHP, bloque par
                //      AntiHealShield comme tous les heals).
                // Note : si target meurt, la plaie disparait avec elle, pas besoin de consume.
                case SpellId.GhostraSaigneAme:
                {
                    if (wasKill)
                    {
                        int hpBeforeSaigne = caster->HP;
                        if (StatusHelper.Has(caster, StatusKind.AntiHealShield))
                        {
                            Log.Info($"[Spell] Saigne-Ame KILL : heal {SpellRegistry.SaigneAmeHealOnKill} BLOQUE par AntiHealShield sur P{caster->PlayerIndex}");
                        }
                        else
                        {
                            caster->HP += SpellRegistry.SaigneAmeHealOnKill;
                            if (caster->HP > caster->MaxHP) caster->HP = caster->MaxHP;
                            Log.Info($"[Spell] Saigne-Ame KILL : heal +{SpellRegistry.SaigneAmeHealOnKill} HP sur P{caster->PlayerIndex} {hpBeforeSaigne} -> {caster->HP}");
                        }
                    }
                    else
                    {
                        // Cible survit : consume PlaieOuverte (Bible).
                        EntityRef saTarget = GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY);
                        if (saTarget != EntityRef.None
                            && f.Unsafe.TryGetPointer<Combatant>(saTarget, out Combatant* saTargetC)
                            && saTargetC->HP > 0
                            && StatusHelper.Has(saTargetC, StatusKind.PlaieOuverte))
                        {
                            StatusHelper.Consume(saTargetC, StatusKind.PlaieOuverte);
                            Log.Info($"[Spell] Saigne-Ame : PlaieOuverte CONSOMMEE sur P{saTargetC->PlayerIndex} (Bible finisher)");
                        }
                    }
                    break;
                }

                // Refonte 29 mai — CONTAGION (auto-propagation) : rend la cible ennemie CONTAGIOUS
                //   pendant 2 rounds. À la fin de chaque tour de la cible, elle prend +1 marque venin
                //   auto (hook TurnSystem.EnterTurnEnd). Pas besoin que la cible soit déjà marquée.
                case SpellId.NecramContagion:
                {
                    EntityRef contTarget = GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY);
                    if (contTarget != EntityRef.None
                        && f.Unsafe.TryGetPointer<Combatant>(contTarget, out Combatant* targetCont)
                        && targetCont->PlayerIndex != caster->PlayerIndex
                        && targetCont->HP > 0)
                    {
                        StatusHelper.Apply(targetCont, StatusKind.Contagious, magnitude: 0,
                            turnsLeft: SpellRegistry.ContagionTurns, currentTurn);
                        Log.Info($"[Spell] Contagion : P{targetCont->PlayerIndex} contagieux {SpellRegistry.ContagionTurns} rounds (+1 marque venin auto en fin de son tour)");
                    }
                    else
                    {
                        Log.Info($"[Spell] Contagion : pas de cible valide en ({cmd.TargetX},{cmd.TargetY}), PA déjà consommé");
                    }
                    break;
                }

                case SpellId.NightseerFiletDeRonces:
                {
                    // Refonte 29 mai — pose un Filet (Trap). VISIBLE par défaut ; PlaceTrap applique le
                    //   voile (invisible) uniquement si le Nightseer est en phase 3. Trigger sur entrée
                    //   ennemie via MovementSystem -> FogHelpers.TryTriggerTrapOnEnter.
                    FogHelpers.PlaceTrap(f, cmd.TargetX, cmd.TargetY,
                        TrapKind.FiletRonces, caster->PlayerIndex, currentTurn);
                    Log.Info($"[Spell] Filet de Ronces posé sur ({cmd.TargetX},{cmd.TargetY}) par P{caster->PlayerIndex}");
                    break;
                }

                case SpellId.NightseerChampDeMines:
                {
                    // Refonte 29 mai — pose 3 mines VISIBLES par défaut (invisibles si phase 3, géré
                    //   par PlaceTrap). Ordre : centre + cardinaux disponibles (placement déterministe).
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

                    // Refonte 29 mai — plus de voile uniforme sur la zone : les mines sont VISIBLES
                    //   par défaut (PlaceTrap applique le voile par mine uniquement si phase 3).
                    // Poser 3 mines (centre prioritaire, skip case caster).
                    int placed = 0;
                    for (int i = 0; i < 9 && placed < 3; i++)
                    {
                        int mx = candX[i], my = candY[i];
                        if (!GridHelpers.InBounds(mx, my)) continue;
                        if (mx == caster->GridX && my == caster->GridY) continue;
                        FogHelpers.PlaceTrap(f, mx, my, TrapKind.Mine, caster->PlayerIndex, currentTurn);
                        placed++;
                    }
                    Log.Info($"[Spell] Champ de Mines : {placed} mines posées par P{caster->PlayerIndex} (centre {cx},{cy}, visibles sauf phase 3)");
                    break;
                }

                case SpellId.NightseerBourrasque:
                {
                    // Refonte 29 mai — push DIRECTIONNEL : la cible est poussée dans le sens
                    //   (TargetX,TargetY) -> (DirX,DirY) du 2e clic (réduit à une cardinale).
                    //   Fallback "loin du caster" si pas de direction fournie (DirX/DirY absent).
                    // Equilibrage juin : push FIXE 2 cases, l'option "1 PR -> 4 cases" a ete retiree
                    //   (HGCostMaxOptional=0 -> hgSpend toujours 0, plus aucune depense de PR).
                    int pushDist = SpellRegistry.BourrasquePushBase;
                    EntityRef target = GridHelpers.GetOccupant(f, cmd.TargetX, cmd.TargetY);
                    if (target != EntityRef.None
                        && f.Unsafe.TryGetPointer<Combatant>(target, out Combatant* targetC))
                    {
                        int fromX = caster->GridX, fromY = caster->GridY; // fallback
                        int dirDX = cmd.DirX - cmd.TargetX;
                        int dirDY = cmd.DirY - cmd.TargetY;
                        bool hasDir = cmd.DirX >= 0 && cmd.DirY >= 0 && (dirDX != 0 || dirDY != 0);
                        if (hasDir)
                        {
                            int adx = dirDX < 0 ? -dirDX : dirDX;
                            int ady = dirDY < 0 ? -dirDY : dirDY;
                            int sx = 0, sy = 0;
                            if (adx >= ady) sx = dirDX > 0 ? 1 : -1;
                            else            sy = dirDY > 0 ? 1 : -1;
                            // "from" = case derrière la cible dans le sens opposé -> push vers la cardinale.
                            fromX = cmd.TargetX - sx;
                            fromY = cmd.TargetY - sy;
                        }
                        PushAndTrigger(f, targetC, target, fromX, fromY, pushDist, currentTurn, caster);
                    }
                    break;
                }

                case SpellId.NightseerSouffleGlacial: // PIÈGE BONDISSANT (refonte 29 mai)
                {
                    // Pose un piège-catapulte sur (TargetX,TargetY) avec la direction d'éjection
                    //   choisie au 2e clic (cmd.DirX/DirY -> cardinale). Au déclenchement (FogHelpers),
                    //   l'ennemi est éjecté de 3 cases dans ce sens. Visible (invisible en phase 3).
                    byte trapDir = 0; // 0 = aucune (fallback : la cible ne sera pas éjectée)
                    int dirDX = cmd.DirX - cmd.TargetX;
                    int dirDY = cmd.DirY - cmd.TargetY;
                    if (cmd.DirX >= 0 && cmd.DirY >= 0 && (dirDX != 0 || dirDY != 0))
                    {
                        int adx = dirDX < 0 ? -dirDX : dirDX;
                        int ady = dirDY < 0 ? -dirDY : dirDY;
                        if (adx >= ady) trapDir = (byte)(dirDX > 0 ? 1 : 2);
                        else            trapDir = (byte)(dirDY > 0 ? 3 : 4);
                    }
                    FogHelpers.PlaceTrap(f, cmd.TargetX, cmd.TargetY, TrapKind.Bondissant,
                        caster->PlayerIndex, currentTurn, trapDir);
                    Log.Info($"[Spell] Piège Bondissant posé en ({cmd.TargetX},{cmd.TargetY}) par P{caster->PlayerIndex}, dir={trapDir}");
                    break;
                }

                // ====================================================================
                // 2.15.c — NIGHTSEER SURVIE
                // ====================================================================

                // Refonte 29 mai — Voile d'Ombre supprimé (ID réutilisé par Flèche Traçante, qui est
                //   offensive : dégâts gérés dans le damage override, pas de handler dédié ici).

                case SpellId.NightseerPasFurtif:
                {
                    // Teleport sur (cmd.TargetX, cmd.TargetY). Filter EmptyTile a deja valide
                    // que la case est vide + walkable.
                    int oldX = caster->GridX, oldY = caster->GridY;
                    GridHelpers.SetOccupant(f, oldX, oldY, EntityRef.None);
                    caster->GridX = cmd.TargetX;
                    caster->GridY = cmd.TargetY;
                    caster->Facing = FacingHelpers.FacingFromGridDelta(cmd.TargetX - oldX, cmd.TargetY - oldY); // 3.7.a.i.0
                    GridHelpers.SetOccupant(f, cmd.TargetX, cmd.TargetY, casterEntity);
                    Log.Info($"[Spell] Pas Furtif : P{caster->PlayerIndex} ({oldX},{oldY}) -> ({cmd.TargetX},{cmd.TargetY})");
                    // Fix #5 — le teleport declenche un piege ennemi sur la case d'arrivee (owner-filtre).
                    FogHelpers.TryTriggerTrapOnEnter(f, casterEntity, caster, cmd.TargetX, cmd.TargetY, currentTurn);

                    // Refonte 29 mai — 1 PR optionnel -> pose un Filet de Ronces sur la case QUITTÉE
                    //   (cadeau d'adieu), au lieu de l'ancien voile. Visible par défaut (cf phase).
                    if (hgSpend >= 1)
                    {
                        FogHelpers.PlaceTrap(f, oldX, oldY, TrapKind.FiletRonces, caster->PlayerIndex, currentTurn);
                        Log.Info($"[Spell] Pas Furtif : Filet de Ronces posé sur la case quittée ({oldX},{oldY}) (1 PR)");
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

                    // Refonte 29 mai — bonus voile retiré (les pièges ne voilent plus). Reste : base + trap.
                    int hpBeforeSeve = caster->HP;
                    caster->HP += heal;
                    if (caster->HP > caster->MaxHP) caster->HP = caster->MaxHP;
                    Log.Info($"[Spell] Sève Sauvage : +{heal} HP sur P{caster->PlayerIndex} ({hpBeforeSeve} -> {caster->HP}) [base {SpellRegistry.SeveSauvageHealBase} +trap{(caster->LastTrapTriggeredOnTurn >= currentTurn - 1 ? SpellRegistry.SeveSauvageHealBonusTrap : 0)}]");
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
                    caster->Facing = FacingHelpers.FacingFromGridDelta(cmd.TargetX - oldX, cmd.TargetY - oldY); // 3.7.a.i.0
                    GridHelpers.SetOccupant(f, cmd.TargetX, cmd.TargetY, casterEntity);
                    // Fix #5 — le teleport declenche un piege ennemi sur la case d'arrivee (owner-filtre).
                    FogHelpers.TryTriggerTrapOnEnter(f, casterEntity, caster, cmd.TargetX, cmd.TargetY, currentTurn);

                    int hpBeforeEvan = caster->HP;
                    caster->HP += SpellRegistry.EvanescenceHeal;
                    if (caster->HP > caster->MaxHP) caster->HP = caster->MaxHP;

                    // Refonte 29 mai — pose un PIÈGE (Filet de Ronces) sur la case quittée, au lieu d'un voile.
                    FogHelpers.PlaceTrap(f, oldX, oldY, TrapKind.FiletRonces, caster->PlayerIndex, currentTurn);

                    Log.Info($"[Spell] Évanescence : P{caster->PlayerIndex} ({oldX},{oldY}) -> ({cmd.TargetX},{cmd.TargetY}) heal {hpBeforeEvan} -> {caster->HP} + Filet sur ({oldX},{oldY})");
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
                        caster->Facing = FacingHelpers.FacingFromGridDelta(landX - oldX, landY - oldY); // 3.7.a.i.0
                        GridHelpers.SetOccupant(f, landX, landY, casterEntity);
                        Log.Info($"[Spell] Traquenard : P{caster->PlayerIndex} teleport ({oldX},{oldY}) -> ({landX},{landY}) adjacent cible ({cmd.TargetX},{cmd.TargetY})");
                        // Fix #5 — le teleport declenche un piege ennemi sur la case d'arrivee (owner-filtre).
                        FogHelpers.TryTriggerTrapOnEnter(f, casterEntity, caster, landX, landY, currentTurn);
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
                    // Equilibrage juin : le Mur donne +2 FD FLAT (gainFondation:false par segment ci-dessous
                    //   -> plus de +1/segment, soit +3 a +5 avant ; grant unique +2 apres la pose).
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
                            expiresOnTurn: currentTurn + SpellRegistry.MurDePierreTurns,
                            gainFondation: false); // equilibrage juin : pas de +1 FD/segment, grant +2 flat plus bas
                        if (wallEntity != EntityRef.None) segmentsSpawned++;
                    }
                    Log.Info($"[Spell] Mur de Pierre : {segmentsSpawned}/{murSegments} segments poses (centre {cmd.TargetX},{cmd.TargetY}, axe perp {wPerpStepX},{wPerpStepY}, option boost FD={hgSpend})");
                    // Equilibrage juin : +2 FD FLAT par Mur pose (au lieu de +1 par segment).
                    if (segmentsSpawned > 0)
                    {
                        ColossarPassif.GainFondation(caster, "Mur de Pierre (pose)", SpellRegistry.MurDePierreFondationGain);
                    }
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
                    Log.Info($"[Spell] Provocation : P{provC->PlayerIndex} provoque par P{caster->PlayerIndex} pour {SpellRegistry.ProvocationTurns}T (-{SpellRegistry.ProvocationMovementMalusMag} PM, +{SpellRegistry.ProvocationCostBump} PA cost TOUS sorts, {SpellRegistry.ProvocationAutoDamageNotAdj} dmg auto si pas adjacent fin tour)");
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

                case SpellId.ColossarSoinLourd: // EBOULEMENT (refonte 29 mai)
                {
                    // Les 150 AoE (rayon 1 autour de l'obstacle) sont déjà appliqués par le pipeline
                    //   (CircleSmall). Ici : détruis l'obstacle ciblé (Pilier/Mur/Faille ; +30 HP via
                    //   passif Densité Inerte UNIQUEMENT si c'est un Pilier — géré dans DestroyObstacle)
                    //   puis push les ennemis survivants autour, loin de l'obstacle.
                    int obsX = cmd.TargetX;
                    int obsY = cmd.TargetY;
                    EntityRef ebTargetObs = ObstacleHelpers.GetObstacleAt(f, obsX, obsY);
                    if (ebTargetObs != EntityRef.None)
                    {
                        ObstacleHelpers.DestroyObstacle(f, ebTargetObs); // +30 HP owner si Pilier (passif destruction)
                        Log.Info($"[Spell] Éboulement : obstacle détruit en ({obsX},{obsY}) par P{caster->PlayerIndex}");
                    }

                    int[] ebDx = { 1, -1, 0, 0 };
                    int[] ebDy = { 0, 0, 1, -1 };
                    for (int i = 0; i < 4; i++)
                    {
                        int nx = obsX + ebDx[i];
                        int ny = obsY + ebDy[i];
                        if (!GridHelpers.InBounds(nx, ny)) continue;
                        EntityRef occ = GridHelpers.GetOccupant(f, nx, ny);
                        if (occ == EntityRef.None || occ == casterEntity) continue;
                        if (!f.Unsafe.TryGetPointer<Combatant>(occ, out Combatant* ebC)) continue;
                        if (ebC->PlayerIndex == caster->PlayerIndex || ebC->HP <= 0) continue;
                        PushAndTrigger(f, ebC, occ, obsX, obsY,
                            SpellRegistry.EboulementPushDistance, currentTurn, caster);
                    }
                    break;
                }

                // -------------------------------------------------------------
                // COLOSSAR 3.3.d — SIGNATURE EFFONDREMENT (handler cast)
                // -------------------------------------------------------------

                case SpellId.ColossarEffondrement:
                {
                    // Refonte 29 mai : déclenchement IMMÉDIAT (plus d'annonce différée). Le swap
                    //   anti-fuite cible l'ennemi le plus proche dans le rayon 2 AU MOMENT DU CAST.
                    //   FD (5) déjà consommés par le pipeline standard de cost.
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
                    Log.Info($"[Spell] Effondrement IMMÉDIAT par P{caster->PlayerIndex} (tour {currentTurn}). Cible snapshot dist {(targetSnapshot != EntityRef.None ? bestDist : -1)}.");

                    // Refonte 29 mai : déclenche tout de suite (200 AoE + éjection + Failles + buff).
                    TurnSystem.TriggerEffondrement(f, caster, currentTurn);
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
        // public (PATCH 22 mai) : la View (TargetingPreviewView) reutilise cette MEME liste pour
        // griser les cases dont la ligne de vue est bloquee par un obstacle. Source unique.
        public static bool SpellNeedsLineOfSight(SpellId id)
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
                // NightseerMarqueDuChasseur -> AFFUT (patch 7 juin) : self-buff, plus de LoS requise.
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
                // Ghostra tactiques (3.7.b)
                //   NB : Permutation (swap avec son propre leurre, type teleport) est EXCLUE — elle
                //   ignore les obstacles comme les autres teleports (cf Pas Furtif/Evanescence).
                case SpellId.GhostraMarqueDeLOmbre:        // range 4, ENEMY, buff pression 2 rounds
                // Ghostra offensifs distance (3.7.a.ii / 3.7.a.iii)
                //   NB : Éveil Spectral (ex-Dague) est EXCLU — c'est un leurre adjacent qui frappe
                //   au corps-à-corps, pas un tir depuis la Ghostra : pas de LoS Ghostra requise.
                case SpellId.GhostraSaigneAme:             // range 2, ENEMY, finisher PlaieOuverte
                case SpellId.GhostraFrappeFantome:         // range 4, ENEMY, teleport + 200 dmg
                // Ghostra survie pose-leurre (3.7.c.iii)
                case SpellId.GhostraRepliqueProtectrice:   // range 3, EmptyTile, pose decoy Protective
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Fix 5 juin (#1) — Applique les buffs OFFENSIFS du caster a un degat brut : Pacte de Sang
        /// (+%), Peau de Fer (+30 si melee & ShieldActive), Frenesie (+%), Sang Bouillant (flat).
        /// MIRROR du bloc generique (~ligne 704) — sert aux paths CUSTOM qui bypassent le damage loop
        /// standard (Charge Brutale) pour que ces buffs s'appliquent comme partout + que la preview
        /// (SpellPreview.FinalizeOffensive) colle au degat reel. Ne CONSOMME rien (cf
        /// <see cref="ConsumeOffensiveOneShotBuffs"/>). isMelee = spellDef.RangeMax == 1 (Peau de Fer
        /// = melee only, Bible V7.1).
        /// </summary>
        private static int ApplyOffensiveCasterBuffs(Combatant* caster, bool isMelee, int rawDamage)
        {
            if (rawDamage <= 0) return rawDamage;
            int dmg = rawDamage;
            // Pacte de Sang : multiplicateur +% (Bible 50%).
            int pacteBuffPct = StatusHelper.GetMagnitude(caster, StatusKind.BuffNextOffensiveDmgPercent, 0);
            if (pacteBuffPct > 0) dmg += dmg * pacteBuffPct / 100;
            // Peau de Fer : +30 melee si le caster a un shield actif.
            if (isMelee && StatusHelper.GetMagnitude(caster, StatusKind.ShieldActive, 0) > 0)
                dmg += SpellRegistry.PeauDeFerMeleeDmgBonus;
            // Frenesie : +% dgts offensifs tant que RageInsatiableActive.
            if (StatusHelper.Has(caster, StatusKind.RageInsatiableActive))
                dmg += dmg * SpellRegistry.FrenesieDmgBonusPct / 100;
            // Affut (patch 7 juin, Nightseer) : +% dgts offensifs tant que AffutActive.
            if (StatusHelper.Has(caster, StatusKind.AffutActive))
                dmg += dmg * SpellRegistry.AffutDmgBonusPct / 100;
            // Sang Bouillant : bonus FLAT "prochaine frappe +X" (NextStrikeBonus).
            int nextStrikeBonus = StatusHelper.GetMagnitude(caster, StatusKind.NextStrikeBonus, 0);
            if (nextStrikeBonus > 0) dmg += nextStrikeBonus;
            return dmg;
        }

        /// <summary>
        /// Fix 5 juin (#1) — Consomme les buffs offensifs ONE-SHOT (Pacte de Sang + Sang Bouillant)
        /// apres qu'un path CUSTOM (Charge Brutale) a inflige ses degats : la conso generique
        /// (~ligne 1555) ne fire pas pour ces sorts (effectiveDmg==0 / casterHitSomething jamais mis).
        /// Frenesie (buff de duree) N'EST PAS consommee. A appeler une seule fois, apres le hit.
        /// </summary>
        private static void ConsumeOffensiveOneShotBuffs(Combatant* caster)
        {
            if (StatusHelper.GetMagnitude(caster, StatusKind.BuffNextOffensiveDmgPercent, 0) > 0)
                StatusHelper.Consume(caster, StatusKind.BuffNextOffensiveDmgPercent);
            if (StatusHelper.GetMagnitude(caster, StatusKind.NextStrikeBonus, 0) > 0)
                StatusHelper.Consume(caster, StatusKind.NextStrikeBonus);
        }

        /// <summary>
        /// Fix 2 juin — Sorts qui TELEPORTENT le CASTER (il se deplace lui-meme sans marcher).
        /// Servent au garde anti-teleport : un caster sous AnchorImmune (Ancrage Colossar /
        /// Stoicisme) ou AntiTeleport (Rugissement Soulrender) ne peut PAS les lancer ("rien ne me
        /// deplace"). Avant, ces statuts ne bloquaient que les deplacements SUBIS (push/pull/swap
        /// imposes), pas les self-teleports -> un Nightseer ancre pouvait quand meme se TP (bug vu NS vs Colossar).
        ///
        /// Permutation (swap Ghostra<->son leurre) et Frappe Fantome (TP + frappe) incluses : le
        /// caster s'y deplace, donc bloquees aussi sous ancrage.
        /// </summary>
        public static bool SpellIsSelfTeleport(SpellId id)
        {
            switch (id)
            {
                case SpellId.NightseerPasFurtif:
                case SpellId.NightseerEvanescence:
                case SpellId.NightseerTraquenard:
                case SpellId.GhostraDernierPas:
                case SpellId.GhostraPasDansLOmbre:
                case SpellId.GhostraFrappeFantome:
                case SpellId.GhostraPermutation:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// True si le sort tire/agit en LIGNE DROITE cardinale depuis le caster : la cible doit
        /// etre alignee (meme ligne OU meme colonne). Couvre la shape Line (resolution generique
        /// TargetingResolver) ET les sorts a resolution de ligne CUSTOM (Shape SingleTile mais
        /// handler en ligne : Choc Sismique, Charge Brutale). Sans ce garde, une cible diagonale
        /// est acceptee a portee Manhattan puis snappee sur l'axe dominant -> ciblage incoherent.
        /// Utilise par la validation du cast (sim) ET le preview de portee (View) pour rester en phase.
        /// NB : Mur de Pierre est EXCLU (pose libre dans le diamant, mur oriente perpendiculairement,
        /// pas un tir en ligne depuis le caster).
        /// </summary>
        public static bool SpellIsStraightLine(SpellId id)
        {
            switch (id)
            {
                case SpellId.ColossarChocSismique:     // ligne 4 (Shape SingleTile, handler custom)
                case SpellId.SoulrenderChargeBrutale:  // ligne 4 (refonte 29 mai, Shape SingleTile, handler custom)
                case SpellId.NightseerVoleeDEpines:    // ligne 5 (Shape Line, resolution generique)
                case SpellId.SoulrenderEmpoignade:     // patch 7 juin : pull en LIGNE DROITE uniquement
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 2.16 — True si la cible Traquenard a Traque/Empreinte OU si la case visee a un
        /// voile owned par le caster. Sert au bonus +80 dgts + gain +2 PR.
        /// </summary>
        // Refonte 29 mai — Traquenard : bonus +80 si la cible est TRAQUÉ (marque unique).
        //   Voilé/Empreinté supprimés du gameplay. (casterPlayerIndex conservé pour la signature.)
        private static bool TraquenardHasMarkOrOwnVeil(Frame f, int targetX, int targetY, int casterPlayerIndex)
        {
            EntityRef occ = GridHelpers.GetOccupant(f, targetX, targetY);
            if (occ != EntityRef.None && f.Unsafe.TryGetPointer<Combatant>(occ, out Combatant* targetC))
            {
                if (MarkHelpers.HasMark(targetC, MarkKind.Traque)) return true;
            }
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
                if (ObstacleHelpers.HasObstacleAt(f, cx, cy)) continue; // Fix 2 juin : pas d'atterrissage sur Faille/Pilier/Mur
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
            int pushFromX = targetC->GridX, pushFromY = targetC->GridY; // 3.7.a.i.0
            GridHelpers.SetOccupant(f, targetC->GridX, targetC->GridY, EntityRef.None);
            targetC->GridX = curX;
            targetC->GridY = curY;
            // 3.7.a.i.0 — Update Facing depuis le delta du push.
            targetC->Facing = FacingHelpers.FacingFromGridDelta(curX - pushFromX, curY - pushFromY);
            GridHelpers.SetOccupant(f, curX, curY, targetEntity);
            Log.Info($"[Spell] Push : P{targetC->PlayerIndex} pousse de {steps} case(s) -> ({curX},{curY})");

            // FIX 30 mai — pieges declenches AU PASSAGE sur TOUTE la trajectoire de poussee, pas
            // seulement la case d'arrivee. Avant, un piege traverse en cours de poussee (Bourrasque,
            // Onde de Choc, Eboulement) etait ignore tant que la cible ne s'arretait pas pile dessus.
            // On rejoue la trajectoire depuis la case de depart (pushFromX/Y) dans le sens
            // (stepX,stepY) et on declenche chaque case intermediaire, puis la case finale.
            int passX = pushFromX, passY = pushFromY;
            for (int s = 1; s < steps; s++)
            {
                if (targetC->HP <= 0) break;
                passX += stepX;
                passY += stepY;
                FogHelpers.TryTriggerTrapOnEnter(f, targetEntity, targetC, passX, passY, currentTurn);
            }

            // Trigger trap eventuel sur la case d'arrivee.
            if (targetC->HP > 0)
            {
                FogHelpers.TryTriggerTrapOnEnter(f, targetEntity, targetC, curX, curY, currentTurn);
            }
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
            // 3.7.a.i.0 — Update Facing depuis delta du pull.
            targetC->Facing = FacingHelpers.FacingFromGridDelta(newX - tx, newY - ty);
            GridHelpers.SetOccupant(f, newX, newY, targetEntity);
            return true;
        }

        private static bool IsCellFreeForPull(Frame f, int x, int y)
        {
            if (!GridHelpers.InBounds(x, y)) return false;
            if (!GridHelpers.IsWalkable(f, x, y)) return false;
            if (GridHelpers.GetOccupant(f, x, y) != EntityRef.None) return false;
            if (ObstacleHelpers.HasObstacleAt(f, x, y)) return false; // Fix 2 juin : Faille/Pilier/Mur = case solide (pull/Frappe Fantome n'y depose pas)
            return true;
        }

        /// <summary>
        /// 3.7.a.iii — Frappe Fantome : trouve une case libre adjacente Manhattan=1 a la target.
        /// AMENDEMENT 16 mai (Lorenzo) : priorise la case DORSALE (derriere target.Facing) pour
        /// GARANTIR le dorsal sur la Frappe Fantome -> combo Dague Lancee 90° -> Frappe Fantome
        /// dans le dos en teleport.
        ///
        /// Ordre de priorite (en partant de la direction Opposite(target.Facing) puis rotate cw) :
        ///   1. back   = Opposite(target.Facing)   -> dorsal GARANTI
        ///   2. side1  = RotateClockwise(back)     -> perpendiculaire (90°)
        ///   3. side2  = RotateClockwise(front)    -> perpendiculaire (-90°)
        ///   4. front  = target.Facing             -> face target (PIRE cas, pas dorsal)
        ///
        /// Retourne false si les 4 cardinaux sont hors grille / non walkable / occupes.
        /// </summary>
        private static bool TryFindFreeCellAdjacentToTarget(Frame f, int targetX, int targetY,
            IsoFacing targetFacing, out int outX, out int outY)
        {
            // Ordre dorsal -> side1 -> side2 -> front.
            IsoFacing back = FacingHelpers.Opposite(targetFacing);
            IsoFacing side1 = FacingHelpers.RotateClockwise(back);
            IsoFacing front = FacingHelpers.RotateClockwise(side1);
            IsoFacing side2 = FacingHelpers.RotateClockwise(front);

            // Test back en premier (dorsal garanti).
            if (TryCellFromFacing(f, targetX, targetY, back, out outX, out outY)) return true;
            if (TryCellFromFacing(f, targetX, targetY, side1, out outX, out outY)) return true;
            if (TryCellFromFacing(f, targetX, targetY, side2, out outX, out outY)) return true;
            if (TryCellFromFacing(f, targetX, targetY, front, out outX, out outY)) return true;

            outX = -1; outY = -1; return false;
        }

        /// <summary>
        /// Helper : convertit une IsoFacing en delta grille et teste si la case correspondante
        /// adjacente a (targetX, targetY) est libre. Utilise par TryFindFreeCellAdjacentToTarget.
        /// </summary>
        private static bool TryCellFromFacing(Frame f, int targetX, int targetY, IsoFacing facing,
            out int outX, out int outY)
        {
            FacingHelpers.IsoFacingToGridDelta(facing, out int dx, out int dy);
            int cx = targetX + dx;
            int cy = targetY + dy;
            if (IsCellFreeForPull(f, cx, cy)) { outX = cx; outY = cy; return true; }
            outX = -1; outY = -1; return false;
        }
    }
}
