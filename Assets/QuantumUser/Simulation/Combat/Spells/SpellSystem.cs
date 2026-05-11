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

        private static void TryCastSpell(Frame f, int playerIndex, CastSpellCommand cmd, int activePlayerIndex)
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

            int effectivePACost = EffectiveStats.GetPACost(spellDef, caster);
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
            // Pacte de Sang +50% : applique a tout sort OFFENSIF (DamageAmount > 0).
            // Consume apres le damage loop (1 seul cast offensif).
            int pacteBuffPct = StatusHelper.GetMagnitude(caster, StatusKind.BuffNextOffensiveDmgPercent, 0);
            if (effectiveDmg > 0 && pacteBuffPct > 0)
            {
                effectiveDmg += effectiveDmg * pacteBuffPct / 100;
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

            // ===== Damage loop + Riposte trigger + gain HG =====
            bool casterHitSomething = false;
            bool isMelee = spellDef.RangeMax == 1; // pour trigger Riposte (Bible : attaque MELEE)

            if (effectiveDmg > 0)
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

                    int before = targetC->HP;
                    targetC->HP -= effectiveDmg;
                    if (targetC->HP < 0) targetC->HP = 0;
                    casterHitSomething = true;
                    Log.Info($"[Spell] Damage {effectiveDmg} sur P{targetC->PlayerIndex} ({cx},{cy}) HP {before} -> {targetC->HP}");

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
            }

            // ===== Consume Pacte buff si utilise =====
            if (effectiveDmg > 0 && pacteBuffPct > 0)
            {
                StatusHelper.Consume(caster, StatusKind.BuffNextOffensiveDmgPercent);
                Log.Info($"[Spell] BuffNextOffensiveDmgPercent consume sur P{caster->PlayerIndex} (+{pacteBuffPct}%)");
            }

            // ===== Effets specifiques par sort (apres damage) =====
            ApplySpellSpecificEffects(f, cmd, spellDef, caster, casterEntity, casterHitSomething, hgSpend, currentTurn, effectBuffer, effectCount);

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

            Log.Info($"[Spell] P{playerIndex} cast {cmd.Spell} target=({cmd.TargetX},{cmd.TargetY}) PA restant={caster->PA}");
        }

        /// <summary>
        /// Applique les effets non-damage specifiques au sort : statuses, self-effects.
        /// Le damage / gain HG / Riposte trigger sont deja faits dans la boucle principale.
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
            int effectCount)
        {
            switch (cmd.Spell)
            {
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
    }
}
