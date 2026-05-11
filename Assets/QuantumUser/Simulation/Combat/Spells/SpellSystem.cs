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
            // Detonation Sanglante (2.10.c) : 60 + 40 par HG total consomme (mandatory + optional).
            // Avec HGSpend=0 -> 60+80=140. Avec HGSpend=3 (max) -> 60+200=260.
            if (cmd.Spell == SpellId.SoulrenderDetonationSanglante)
            {
                int totalHGForDetonation = spellDef.HGCostMandatory + hgSpend;
                effectiveDmg = SpellRegistry.DetonationBaseDamage
                             + SpellRegistry.DetonationDamagePerHG * totalHGForDetonation;
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

                    // Shield absorption (2.10.b) : ShieldActive absorbe avant HP.
                    // L'excedent passe au HP. Si Magnitude tombe a 0 : Consume status.
                    int dmgRemaining = effectiveDmg;
                    int shieldBefore = StatusHelper.GetMagnitude(targetC, StatusKind.ShieldActive, 0);
                    if (shieldBefore > 0)
                    {
                        int absorbed = dmgRemaining > shieldBefore ? shieldBefore : dmgRemaining;
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
                        dmgRemaining -= absorbed;
                    }

                    if (dmgRemaining > 0)
                    {
                        int before = targetC->HP;
                        targetC->HP -= dmgRemaining;
                        if (targetC->HP < 0) targetC->HP = 0;
                        casterHitSomething = true;
                        Log.Info($"[Spell] Damage {effectiveDmg} (HP loss {dmgRemaining}) sur P{targetC->PlayerIndex} ({cx},{cy}) HP {before} -> {targetC->HP}");

                        // 2.10.c : Kill detection. Tracker si au moins 1 cible est tombee a HP=0.
                        // killedTargetX/Y sert au recul Tranche-Ame (direction opposee a la cible tuee).
                        if (targetC->HP == 0 && before > 0)
                        {
                            wasKill = true;
                            killedTargetX = cx;
                            killedTargetY = cy;
                            Log.Info($"[Spell] KILL : P{targetC->PlayerIndex} tombe a HP=0 sur ({cx},{cy})");
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

            // ===== Consume Pacte buff si utilise =====
            if (effectiveDmg > 0 && pacteBuffPct > 0)
            {
                StatusHelper.Consume(caster, StatusKind.BuffNextOffensiveDmgPercent);
                Log.Info($"[Spell] BuffNextOffensiveDmgPercent consume sur P{caster->PlayerIndex} (+{pacteBuffPct}%)");
            }

            // ===== Effets specifiques par sort (apres damage) =====
            ApplySpellSpecificEffects(f, cmd, spellDef, caster, casterEntity,
                casterHitSomething, hgSpend, currentTurn,
                effectBuffer, effectCount,
                wasKill, killedTargetX, killedTargetY);

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
        ///
        /// 2.10.c : prend wasKill + killedTargetX/Y en parametre pour les effets conditionnels
        /// au kill (recul Tranche-Ame, Curee kill chain).
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
            int killedTargetY)
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

                    // TODO 2.11 : si totalHG consomme == 5 -> interdit Ame Laceree + reset son cooldown.
                    // Implementer quand la signature arrive en 2.11.
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
