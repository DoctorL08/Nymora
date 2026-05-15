namespace Quantum
{
    using Photon.Deterministic;

    /// <summary>
    /// FSM du tour de combat (Bible V7.1).
    ///
    /// OnInit : cree le singleton CombatState, tire l'initiative deterministe via f.RNG,
    /// transition immediate vers TurnStart pour demarrer le 1er tour.
    ///
    /// Update : chaque tick, fait avancer la FSM
    ///   - TurnStart : reset PA/PM du joueur actif, increment TurnNumber, init timer, -> TurnActive
    ///   - TurnActive : decrement TurnTimerTicks, quand <= 0 -> TurnEnd
    ///   - TurnEnd : swap ActivePlayerIndex, -> TurnStart (sauf condition de victoire en Phase 2.x)
    ///   - MatchEnd : no-op (combat termine)
    ///
    /// En 2.3 le swap est purement automatique au timer. L'input "End Turn" volontaire
    /// arrivera en 2.4 avec le systeme de mouvement.
    /// </summary>
    public unsafe class TurnSystem : SystemMainThread
    {
        public override void OnInit(Frame f)
        {
            var state = f.Unsafe.GetOrAddSingletonPointer<CombatState>(EntityRef.None);
            state->CurrentPhase = CombatPhase.PreMatch;
            state->TurnNumber = 0;
            state->TurnTimerTicks = 0;
            state->SubTurnInRound = 0; // 2.14 : 1er sous-tour du round 1
            state->WinnerPlayerIndex = -1; // 2.16.c.i : -1 = match en cours / pas de winner

            // Tirage d'initiative deterministe (Bible V7.1 : random tour 1, alternance ensuite).
            // f.RNG->Next(0, max) retourne un int dans [0, max) - donc [0, 2) = 0 ou 1.
            state->ActivePlayerIndex = f.RNG->Next(0, TurnConstants.PlayerCount);

            // Transition immediate vers le 1er TurnStart. La FSM termine son init au prochain Update.
            state->CurrentPhase = CombatPhase.TurnStart;

            Log.Info($"[TurnSystem] Initiative: Joueur P{state->ActivePlayerIndex} commence le round 1");
        }

        public override void Update(Frame f)
        {
            var state = f.Unsafe.GetPointerSingleton<CombatState>();

            switch (state->CurrentPhase)
            {
                case CombatPhase.TurnStart:
                    EnterTurnStart(f, state);
                    break;

                case CombatPhase.TurnActive:
                    TickTurnActive(f, state);
                    break;

                case CombatPhase.TurnEnd:
                    EnterTurnEnd(f, state);
                    break;

                // PreMatch est juste un sas avant l'OnInit, ne devrait pas arriver en Update.
                // MatchEnd : no-op.
                case CombatPhase.PreMatch:
                case CombatPhase.MatchEnd:
                default:
                    break;
            }
        }

        private static void EnterTurnStart(Frame f, CombatState* state)
        {
            // 2.14 — TurnNumber incremente UNIQUEMENT au 1er sous-tour du round (semantique Dofus).
            // SubTurnInRound vaut 0 au debut de chaque round (set par EnterTurnEnd ou OnInit).
            if (state->SubTurnInRound == 0)
            {
                state->TurnNumber += 1;
                // 2.15.a — Reset DamageTakenThisRound pour tous les combattants (Bible Prescience).
                // Doit etre fait au debut du round, pas en fin (sinon le check de fin de round
                // est fait sur des donnees deja remises a zero).
                var resetFilter = f.Filter<Combatant>();
                while (resetFilter.NextUnsafe(out EntityRef _, out Combatant* c))
                {
                    c->DamageTakenThisRound = 0;
                }
            }
            state->TurnTimerTicks = TurnConstants.GetTurnDurationTicks(f);

            // Reset PA/PM du joueur actif (Bible V7.1 : debut de tour = ressources fraiches).
            // HP et ressources de classe (HG, PR, FD, PT, RM) NE sont PAS resets.
            int activePlayer = state->ActivePlayerIndex;
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* combatant))
            {
                if (combatant->PlayerIndex == activePlayer)
                {
                    // 2.10.c : BonusPANextTurn (Curee kill) ajoute au reset PA, puis consume.
                    combatant->PA = combatant->MaxPA + combatant->BonusPANextTurn;
                    if (combatant->BonusPANextTurn > 0)
                    {
                        Log.Info($"[TurnSystem] BonusPANextTurn +{combatant->BonusPANextTurn} PA sur P{combatant->PlayerIndex} (Curee kill chain)");
                        combatant->BonusPANextTurn = 0;
                    }
                    combatant->PM = combatant->MaxPM;

                    // 3.3.c : Snapshot Ressac Vital. Au debut du sub-turn du combattant actif,
                    // on copie les hits subis au sub-turn adverse precedent dans LastRound
                    // (consultes par Ressac Vital), puis on reset le compteur courant.
                    // "Tour precedent" Bible = dernier sub-turn ou ce combatant n'etait pas actif.
                    combatant->HitsTakenLastRound = combatant->HitsTakenThisRound;
                    combatant->HitsTakenThisRound = 0;

                    // 2.10.a : MovementMalus (Rugissement -1/-2, Riposte Carmin -1)
                    // reduit le PM disponible pour CE tour. Le status reste actif jusqu'a
                    // son TurnEnd ; il sera decrement la et expire normalement.
                    int pmMalus = StatusHelper.GetMagnitude(combatant, StatusKind.MovementMalus, 0);
                    if (pmMalus > 0)
                    {
                        combatant->PM -= pmMalus;
                        if (combatant->PM < 0) combatant->PM = 0;
                        Log.Info($"[TurnSystem] MovementMalus -{pmMalus} PM applique sur P{combatant->PlayerIndex} (PM={combatant->PM}/{combatant->MaxPM})");
                    }

                    // 2.16 : ActionMalus (Traquenard Paralysie -2 PA) reduit les PA pour CE tour.
                    // Pattern miroir de MovementMalus.
                    int paMalus = StatusHelper.GetMagnitude(combatant, StatusKind.ActionMalus, 0);
                    if (paMalus > 0)
                    {
                        combatant->PA -= paMalus;
                        if (combatant->PA < 0) combatant->PA = 0;
                        Log.Info($"[TurnSystem] ActionMalus -{paMalus} PA applique sur P{combatant->PlayerIndex} (PA={combatant->PA}/{combatant->MaxPA})");
                    }

                    // 2.10.c : Sang Coagule tick. Si le combatant actif est sur une case
                    // SangCoagule au demarrage de son tour, il subit 30 dgts (Bible V7.1).
                    if (GridHelpers.GetTerrainKind(f, combatant->GridX, combatant->GridY) == TerrainKind.SangCoagule)
                    {
                        int hpBefore = combatant->HP;
                        combatant->HP -= 30;
                        if (combatant->HP < 0) combatant->HP = 0;
                        Log.Info($"[TurnSystem] Sang Coagule tick : -30 HP sur P{combatant->PlayerIndex} ({combatant->GridX},{combatant->GridY}) HP {hpBefore} -> {combatant->HP}");
                    }

                    // 2.11 Passif Appel du Sang RAGE OUVERTE : si Soulrender actif et au moins
                    // 1 ennemi vivant a HP < 40% MaxHP -> +1 PM permanent (au reset TurnStart).
                    // Bible : "+1 PM permanent jusqu'a fin du tour suivant la kill" — pour 2.11 on
                    // applique simplement "+1 PM tant qu'un ennemi est <40%". La nuance "fin du
                    // tour suivant la kill" se gere implicitement (1v1 = pas d'autres ennemis).
                    if (combatant->Class == NymoraClass.Soulrender)
                    {
                        bool enemyBelow40 = false;
                        var enemyFilter = f.Filter<Combatant>();
                        while (enemyFilter.NextUnsafe(out EntityRef _, out Combatant* other))
                        {
                            if (other->PlayerIndex == combatant->PlayerIndex) continue;
                            if (other->HP <= 0) continue;
                            if (other->MaxHP <= 0) continue;
                            if (other->HP * 100 < other->MaxHP * 40)
                            {
                                enemyBelow40 = true;
                                break;
                            }
                        }
                        if (enemyBelow40)
                        {
                            combatant->PM += 1;
                            Log.Info($"[TurnSystem] Rage Ouverte : +1 PM sur P{combatant->PlayerIndex} (ennemi <40% HP) -> PM={combatant->PM}");
                        }
                    }

                    // 3.3.d — Effondrement : trigger differe. Si le Colossar a annonce au tour
                    // precedent, on declenche maintenant : 200 dgts AoE rayon 2 + ejection
                    // ennemis + Failles 2 tours + apply EffondrementActive 2T + DamageReductionPercent 30/2T.
                    // Doit s'executer AVANT le buff +1 PM (qui depend de EffondrementActive).
                    if (combatant->Class == NymoraClass.Colossar
                        && combatant->EffondrementAnnouncedOnTurn >= 0
                        && state->TurnNumber == combatant->EffondrementAnnouncedOnTurn + 1)
                    {
                        TriggerEffondrement(f, combatant, state->TurnNumber);
                        combatant->EffondrementAnnouncedOnTurn = -1; // consume tracker
                    }

                    // 3.3.d — Buff Effondrement : +1 PM si actif. Hook similaire a Rage Ouverte.
                    // Le -1 PA cost se gere dans EffectiveStats.GetPACost. Le -30% dgts subis via
                    // DamageReductionPercent (cap combine 50% avec Densite Inerte/Garde Prot).
                    if (StatusHelper.Has(combatant, StatusKind.EffondrementActive))
                    {
                        combatant->PM += 1;
                        Log.Info($"[TurnSystem] Effondrement actif : +1 PM sur P{combatant->PlayerIndex} -> PM={combatant->PM}");
                    }

                    // 3.4 — Passif Necram "La Floraison" : tick venin sur le porteur,
                    // regen Necram tier 2+, halo toxique sur ennemis adjacents tier 2+,
                    // reset PutrefactionMarksGainedThisTurn pour le Necram. Appel
                    // unique sur le combattant actif (sub-turn start hook).
                    NecramPassif.OnSubTurnStart(f, combatant, state->TurnNumber);
                }
            }

            Log.Info($"[TurnSystem] Round {state->TurnNumber} (sub {state->SubTurnInRound + 1}/{TurnConstants.PlayerCount}) - Joueur P{activePlayer} (timer {state->TurnTimerTicks} ticks)");
            state->CurrentPhase = CombatPhase.TurnActive;
        }

        private static void TickTurnActive(Frame f, CombatState* state)
        {
            // 2.13.a : End Turn manuel via EndTurnCommand. Seul ActivePlayerIndex peut
            // declencher la transition ; toute autre source = rejet silencieux.
            if (f.GetPlayerCommand(state->ActivePlayerIndex) is EndTurnCommand)
            {
                Log.Info($"[TurnSystem] End Turn manuel par P{state->ActivePlayerIndex} (tour {state->TurnNumber}, timer restant {state->TurnTimerTicks} ticks)");
                state->TurnTimerTicks = 0;
                state->CurrentPhase = CombatPhase.TurnEnd;
                return;
            }

            state->TurnTimerTicks -= 1;
            if (state->TurnTimerTicks <= 0)
            {
                state->TurnTimerTicks = 0;
                state->CurrentPhase = CombatPhase.TurnEnd;
            }
        }

        private static void EnterTurnEnd(Frame f, CombatState* state)
        {
            // 3.3.b.iii — Provocation Bible hook : si le combattant actif (qui vient de finir SON
            // sub-turn) porte Provoked ET n'est pas adjacent (Manhattan 1) au provocateur stocke
            // dans Magnitude (= PlayerIndex), il prend 100 dgts auto. Appel AVANT decrementation
            // statuses (sinon Provoked aurait deja decremente).
            {
                var activeFilter = f.Filter<Combatant>();
                while (activeFilter.NextUnsafe(out EntityRef _, out Combatant* actC))
                {
                    if (actC->PlayerIndex != state->ActivePlayerIndex) continue;
                    if (actC->HP <= 0) break; // mort, skip
                    if (!StatusHelper.Has(actC, StatusKind.Provoked)) break;
                    int provocPi = StatusHelper.GetMagnitude(actC, StatusKind.Provoked, -1);
                    if (provocPi < 0) break;
                    // Lookup position provocateur (combatant avec ce PlayerIndex).
                    var provocFilter = f.Filter<Combatant>();
                    while (provocFilter.NextUnsafe(out EntityRef _, out Combatant* provocC))
                    {
                        if (provocC->PlayerIndex != provocPi) continue;
                        if (provocC->HP <= 0) break;
                        int dxProv = provocC->GridX - actC->GridX;
                        int dyProv = provocC->GridY - actC->GridY;
                        int absDxP = dxProv < 0 ? -dxProv : dxProv;
                        int absDyP = dyProv < 0 ? -dyProv : dyProv;
                        int distProv = absDxP + absDyP;
                        if (distProv > 1)
                        {
                            int dmgProv = SpellRegistry.ProvocationAutoDamageNotAdj;
                            int hpBeforeProv = actC->HP;
                            actC->HP -= dmgProv;
                            if (actC->HP < 0) actC->HP = 0;
                            actC->DamageTakenThisRound += dmgProv;
                            Log.Info($"[Provocation] P{actC->PlayerIndex} pas adjacent au provocateur P{provocPi} (dist {distProv}), -{dmgProv} HP auto fin tour : {hpBeforeProv} -> {actC->HP}");
                        }
                        break;
                    }
                    break;
                }
            }

            // 3.5.a.iii — Brume Toxique fin de tour : si le combattant actif (qui vient de finir
            // son sub-turn) est sur une case BrumeToxique -> +1 marque venin (sans dgts, Bible
            // V7.1). Skip Necram (decision design Lorenzo 2026-05-15). Trigger a CHAQUE
            // sub-turn (pas seulement dernier du round) car la Bible parle de "qui finit son
            // tour" = par unite, pas par round.
            {
                var brumeFilter = f.Filter<Combatant>();
                while (brumeFilter.NextUnsafe(out EntityRef _, out Combatant* actBrume))
                {
                    if (actBrume->PlayerIndex != state->ActivePlayerIndex) continue;
                    if (actBrume->HP <= 0) break; // mort, skip
                    if (actBrume->Class == NymoraClass.Necram) break;
                    if (GridHelpers.GetTerrainKind(f, actBrume->GridX, actBrume->GridY) != TerrainKind.BrumeToxique) break;
                    VeninHelpers.ApplyMark(f, actBrume, SpellRegistry.BrumeToxiqueMarksOnHit, state->TurnNumber);
                    Log.Info($"[TurnSystem] Brume Toxique fin de tour : +1 marque sur P{actBrume->PlayerIndex} ({actBrume->GridX},{actBrume->GridY})");
                    break;
                }
            }

            // 3.5.c.i — Voile de Pestilence (hook adjacence) : a la fin du sub-turn de l'ennemi
            // (= player actif qui finit son tour), iter les Necrams porteurs de PestilenceAura
            // ennemis ; si Manhattan <= 2 entre Necram et l'ennemi finissant -> +1 marque venin
            // sur l'ennemi. Skip si le Necram porteur est mort (HP <= 0).
            {
                var endingFilter = f.Filter<Combatant>();
                while (endingFilter.NextUnsafe(out EntityRef _, out Combatant* ending))
                {
                    if (ending->PlayerIndex != state->ActivePlayerIndex) continue;
                    if (ending->HP <= 0) break;
                    // Iter tous les combattants porteurs de PestilenceAura ennemis du finissant.
                    var veiledFilter = f.Filter<Combatant>();
                    while (veiledFilter.NextUnsafe(out EntityRef _, out Combatant* veiled))
                    {
                        if (veiled->PlayerIndex == ending->PlayerIndex) continue;
                        if (veiled->HP <= 0) continue;
                        if (!StatusHelper.Has(veiled, StatusKind.PestilenceAura)) continue;
                        int dxV = ending->GridX - veiled->GridX;
                        int dyV = ending->GridY - veiled->GridY;
                        int adxV = dxV < 0 ? -dxV : dxV;
                        int adyV = dyV < 0 ? -dyV : dyV;
                        int distV = adxV + adyV;
                        if (distV > SpellRegistry.VoilePestilenceAdjacencyRange) continue;
                        VeninHelpers.ApplyMark(f, ending,
                            SpellRegistry.VoilePestilenceMarksOnAdjacency, state->TurnNumber);
                        Log.Info($"[Voile Pestilence] P{ending->PlayerIndex} fin sub-turn a Manhattan {distV} du Necram P{veiled->PlayerIndex} : +{SpellRegistry.VoilePestilenceMarksOnAdjacency} marque venin");
                    }
                    break;
                }
            }

            // 3.5.b.iii — Pas Spectral : consume PasSpectralReady a la fin du sub-turn du porteur.
            // Le status est pose avec turnsLeft=1 + AppliedOnTurn=currentTurn donc DecrementAllOnTurnEnd
            // le skipperait au last sub-turn et il ne s'eteindrait que round+1, ce qui violerait
            // la regle Bible "+2 PM CE tour". On le retire ici manuellement pour qu'il dure
            // strictement le sub-turn courant du porteur.
            {
                var pasSpectralFilter = f.Filter<Combatant>();
                while (pasSpectralFilter.NextUnsafe(out EntityRef _, out Combatant* actPS))
                {
                    if (actPS->PlayerIndex != state->ActivePlayerIndex) continue;
                    if (!StatusHelper.Has(actPS, StatusKind.PasSpectralReady)) break;
                    StatusHelper.Consume(actPS, StatusKind.PasSpectralReady);
                    Log.Info($"[TurnSystem] Pas Spectral consume sur P{actPS->PlayerIndex} (fin sub-turn)");
                    break;
                }
            }

            // 2.14 — Decrementation UNIQUEMENT a la fin du dernier sous-tour du round
            // (semantique Dofus : "Bible 2 tours" = "2 rounds complets actifs"). Si on
            // decremente a chaque sous-tour, un status "1 tour" expire apres 1 swap de
            // joueur — ce qui n'est PAS l'intention design Bible V7.1.
            bool isLastSubTurnOfRound = state->SubTurnInRound == TurnConstants.PlayerCount - 1;
            if (isLastSubTurnOfRound)
            {
                // 2.10.a : statuses (Pacte +50%, Riposte Carmin, Peau de Fer, etc.).
                // 2.10.c : terrains (Vapeur Carmin, Sang Coagule).
                // 2.14   : voiles + marques Nightseer.
                // Tous skippent leur 1ere decrementation grace a "AppliedOnTurn == currentTurn".
                // 2.15.c — RoncesAura tick : AVANT decrementation pour que le status soit encore
                // actif, et AVANT Prescience pour que les dgts d'aura comptent dans DamageTakenThisRound.
                TickRoncesAura(f);

                // 3.3.c — Stoicisme tick fin de round. Si StoicismeExpiresOnTurn == currentTurn ET
                // ShieldActive.Magnitude > 0 (= shield a survecu 2 tours sans etre brise) -> heal 80.
                // Reset StoicismeExpiresOnTurn a -1 dans tous les cas (consume tracker).
                // Appel AVANT DecrementAllOnTurnEnd pour que le ShieldActive soit encore lisible.
                TickStoicismeHeal(f, state->TurnNumber);

                StatusHelper.DecrementAllOnTurnEnd(f, state->TurnNumber);
                GridHelpers.DecrementAllTerrainsOnTurnEnd(f, state->TurnNumber);
                FogHelpers.DecrementAllVeilsOnTurnEnd(f, state->TurnNumber);
                MarkHelpers.DecrementAllMarksOnTurnEnd(f, state->TurnNumber);

                // 2.15.a — Passif Prescience Nightseer (Bible "L'Œil qui n'est pas") :
                //   +1 PR par round ou le Nightseer N'A PAS pris de degats directs.
                //   -1 PR si degats directs subis ce round.
                //   Cap a CombatantStats.GetMaxResource(Nightseer) = 4.
                //   Plancher : 0 PR.
                var prescienceFilter = f.Filter<Combatant>();
                while (prescienceFilter.NextUnsafe(out EntityRef _, out Combatant* ns))
                {
                    if (ns->Class != NymoraClass.Nightseer) continue;
                    if (ns->HP <= 0) continue; // mort, on ne touche pas
                    int maxResource = CombatantStats.GetMaxResource(ns->Class);
                    int beforeRes = ns->Resource;
                    if (ns->DamageTakenThisRound == 0)
                    {
                        // Pas de degats ce round -> +1 PR
                        ns->Resource = beforeRes + 1 > maxResource ? maxResource : beforeRes + 1;
                        if (ns->Resource != beforeRes)
                        {
                            Log.Info($"[Prescience] +1 PR sur P{ns->PlayerIndex} (no damage round {state->TurnNumber}) : {beforeRes} -> {ns->Resource}");
                        }
                    }
                    else
                    {
                        // A pris des degats -> -1 PR
                        ns->Resource = beforeRes - 1 < 0 ? 0 : beforeRes - 1;
                        if (ns->Resource != beforeRes)
                        {
                            Log.Info($"[Prescience] -1 PR sur P{ns->PlayerIndex} (dgts subis {ns->DamageTakenThisRound} round {state->TurnNumber}) : {beforeRes} -> {ns->Resource}");
                        }
                    }
                }
            }

            // 2.16.c.i — MATCH END check : si au moins un combattant a HP=0, fin du combat.
            // Le vainqueur est le dernier vivant ; en cas de double KO (e.g. Sang Coagule tick
            // simultane), WinnerPlayerIndex reste -1 (draw).
            int aliveCount = 0;
            int lastAlivePlayer = -1;
            var matchEndFilter = f.Filter<Combatant>();
            while (matchEndFilter.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                if (c->HP > 0)
                {
                    aliveCount++;
                    lastAlivePlayer = c->PlayerIndex;
                }
            }

            if (aliveCount <= 1)
            {
                state->WinnerPlayerIndex = aliveCount == 1 ? lastAlivePlayer : -1;
                state->CurrentPhase = CombatPhase.MatchEnd;
                string verdict = state->WinnerPlayerIndex >= 0
                    ? $"Winner: P{state->WinnerPlayerIndex}"
                    : "Draw (double KO)";
                Log.Info($"[TurnSystem] MATCH END — {verdict} (round {state->TurnNumber}, aliveCount={aliveCount})");
                return;
            }

            // Avance le compteur de sous-tour (wrap a 0 au debut du round suivant).
            state->SubTurnInRound = (state->SubTurnInRound + 1) % TurnConstants.PlayerCount;

            // Alternance stricte des 2 joueurs (1v1 en Phase 2). Pour 2v2/3v3 (Phase 6),
            // la rotation devra suivre l'ordre d'initiative et non un simple modulo.
            state->ActivePlayerIndex = (state->ActivePlayerIndex + 1) % TurnConstants.PlayerCount;
            state->CurrentPhase = CombatPhase.TurnStart;
        }

        /// <summary>
        /// 2.15.c — Camouflage Ronces : tick fin de round. Pour chaque Combatant avec status
        /// RoncesAura (Magnitude > 0), inflige Magnitude dgts a tous les Combatants ENNEMIS
        /// adjacents (Manhattan 1) ET applique Empreinte 2 tours (Bible V7.1 :
        /// "70 degats + EMPREINTE"). Aussi increment DamageTakenThisRound pour le check Prescience.
        ///
        /// Note shield : le status ShieldActive (Camouflage Ronces ou Peau de Fer) du PORTEUR
        /// de l'aura n'absorbe PAS les dgts d'aura (l'aura est offensive emise par lui-meme).
        /// Le shield des CIBLES ennemies absorbe normalement (cf damage loop SpellSystem). Pour
        /// 2.15.c on garde simple : aura ignore les shields ennemis (pas un sort, ne traverse
        /// pas le pipeline). A revoir si Bible specifie autrement en playtest.
        /// </summary>
        private static void TickRoncesAura(Frame f)
        {
            if (!f.TryGetSingleton<CombatState>(out var stateLocal)) return;
            int currentTurn = stateLocal.TurnNumber;

            var auraFilter = f.Filter<Combatant>();
            while (auraFilter.NextUnsafe(out EntityRef _, out Combatant* aura))
            {
                if (aura->HP <= 0) continue;
                int auraMag = StatusHelper.GetMagnitude(aura, StatusKind.RoncesAura, 0);
                if (auraMag <= 0) continue;

                var enemyFilter = f.Filter<Combatant>();
                while (enemyFilter.NextUnsafe(out EntityRef _, out Combatant* enemy))
                {
                    if (enemy->PlayerIndex == aura->PlayerIndex) continue;
                    if (enemy->HP <= 0) continue;
                    int adx = enemy->GridX - aura->GridX;
                    int ady = enemy->GridY - aura->GridY;
                    if (adx < 0) adx = -adx;
                    if (ady < 0) ady = -ady;
                    if (adx + ady != 1) continue;

                    int hpBefore = enemy->HP;
                    enemy->HP -= auraMag;
                    if (enemy->HP < 0) enemy->HP = 0;
                    enemy->DamageTakenThisRound += auraMag;
                    enemy->HitsTakenThisRound += 1; // 3.3.c : compte aussi pour Ressac Vital
                    // Bible V7.1 : EMPREINTE 2 tours sur l'ennemi adjacent.
                    MarkHelpers.ApplyMark(enemy, MarkKind.Empreinte,
                        SpellRegistry.CamouflageRoncesAuraEmpreinteTurns,
                        aura->PlayerIndex, currentTurn);
                    Log.Info($"[RoncesAura] -{auraMag} HP + Empreinte sur P{enemy->PlayerIndex} (aura P{aura->PlayerIndex}) : {hpBefore} -> {enemy->HP}");
                }
            }
        }

        /// <summary>
        /// 3.3.c — Stoicisme : tick fin de round. Si StoicismeExpiresOnTurn == currentTurn ET
        /// le ShieldActive porte encore une magnitude > 0 (= shield n'a pas ete brise pendant
        /// les 2 tours), heal +80 HP (Bible V7.1). Reset StoicismeExpiresOnTurn a -1 dans tous
        /// les cas (consume tracker, evite double-heal si plusieurs casts qui se chevauchent).
        ///
        /// Appel AVANT DecrementAllOnTurnEnd pour que le ShieldActive soit encore lisible
        /// (sinon il aurait expire ce tour-ci et Magnitude serait nettoyee).
        /// </summary>
        private static void TickStoicismeHeal(Frame f, int currentTurn)
        {
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                if (c->HP <= 0) continue;
                if (c->StoicismeExpiresOnTurn != currentTurn) continue;
                // Tour d'expiration atteint : check si shield encore vivant.
                int shieldMag = StatusHelper.GetMagnitude(c, StatusKind.ShieldActive, 0);
                if (shieldMag > 0)
                {
                    int hpBefore = c->HP;
                    c->HP += SpellRegistry.StoicismeHealIfSurvived;
                    if (c->HP > c->MaxHP) c->HP = c->MaxHP;
                    Log.Info($"[Stoicisme] Shield survecu ({shieldMag} HP residuel) : +{SpellRegistry.StoicismeHealIfSurvived} HP sur P{c->PlayerIndex} ({hpBefore} -> {c->HP})");
                }
                else
                {
                    Log.Info($"[Stoicisme] Shield brise avant expiration sur P{c->PlayerIndex}, pas de heal bonus");
                }
                c->StoicismeExpiresOnTurn = -1; // consume tracker
            }
        }

        /// <summary>
        /// 3.3.d — Effondrement : trigger differe. Design Lorenzo (Bible "ejection" + swap) :
        ///   Sequence en 2 phases pour le SWAP de la cible snapshot :
        ///   1. EJECTION : on calcule la case d'ejection de la cible (Bible "case libre la plus
        ///      proche", direction axe-dominant caster->target, push jusqu'a sortir du rayon).
        ///      Cette case est GARANTIE hors zone (sauf grid edge ou blocking).
        ///   2. SWAP : ennemi -> case ex-caster (centre Failles), caster -> case d'ejection.
        ///      Resultat : ennemi pige au centre, Colossar dehors avec +1 PM utile.
        ///
        ///   - 200 dgts sur la cible swap (damage compute avec shield/reductions).
        ///   - Pose Failles dans le rayon 2 autour de l'ANCIEN centre (= case ex-caster). Skip
        ///     la case caster post-swap pour ne pas l'enfermer.
        ///   - Apply EffondrementActive (buff 2T : +1 PM / -1 PA cost / -30% dgts subis).
        ///   - Cas degenere : pas de cible snapshot ou cible morte -> pas de swap, juste Failles
        ///     autour de la case caster + buff.
        /// </summary>
        private static void TriggerEffondrement(Frame f, Combatant* caster, int currentTurn)
        {
            int casterStartX = caster->GridX;
            int casterStartY = caster->GridY;
            int radius = SpellRegistry.EffondrementAoeRadius;
            int dmg = SpellRegistry.EffondrementDamage;
            int expireAtTurn = currentTurn + SpellRegistry.EffondrementFailleTurns;

            Log.Info($"[Effondrement] TRIGGER par P{caster->PlayerIndex} en ({casterStartX},{casterStartY}) rayon {radius}, dmg {dmg}");

            EntityRef targetEntity = caster->EffondrementTargetEntity;
            caster->EffondrementTargetEntity = EntityRef.None; // consume tracker

            int failleCenterX = casterStartX;
            int failleCenterY = casterStartY;
            int casterEndX = casterStartX;
            int casterEndY = casterStartY;
            bool swapDone = false;

            if (targetEntity != EntityRef.None
                && f.Unsafe.TryGetPointer<Combatant>(targetEntity, out Combatant* target)
                && target->HP > 0)
            {
                int targetStartX = target->GridX;
                int targetStartY = target->GridY;

                // -- Phase 1 : EJECTION --
                // Calcule la "case libre la plus proche hors zone" pour la target, en partant
                // de sa position COURANTE (qu'elle ait fui ou non depuis le cast).
                // Direction = axe-dominant caster->target (cohérent avec PushAndTrigger / Onde de Choc).
                int dx = targetStartX - casterStartX;
                int dy = targetStartY - casterStartY;
                int absDx = dx < 0 ? -dx : dx;
                int absDy = dy < 0 ? -dy : dy;
                int distCurrent = absDx + absDy;
                int stepX = 0, stepY = 0;
                if (absDx >= absDy) stepX = dx > 0 ? 1 : (dx < 0 ? -1 : 0);
                else                stepY = dy > 0 ? 1 : (dy < 0 ? -1 : 0);
                if (stepX == 0 && stepY == 0) stepX = 1; // fallback si target == caster (impossible normal)

                int ejectX = targetStartX;
                int ejectY = targetStartY;
                if (distCurrent <= radius)
                {
                    // Target en zone : push jusqu'a sortir du rayon, stop a la 1ere case bloquante.
                    int probeX = targetStartX;
                    int probeY = targetStartY;
                    int probeDist = distCurrent;
                    while (probeDist <= radius)
                    {
                        int nextX = probeX + stepX;
                        int nextY = probeY + stepY;
                        if (!GridHelpers.InBounds(nextX, nextY)) break;
                        if (ObstacleHelpers.HasObstacleAt(f, nextX, nextY)) break;
                        EntityRef occ = GridHelpers.GetOccupant(f, nextX, nextY);
                        if (occ != EntityRef.None && occ != targetEntity) break;
                        probeX = nextX;
                        probeY = nextY;
                        // Recalc dist depuis casterStart
                        int adx2 = probeX - casterStartX; if (adx2 < 0) adx2 = -adx2;
                        int ady2 = probeY - casterStartY; if (ady2 < 0) ady2 = -ady2;
                        probeDist = adx2 + ady2;
                    }
                    ejectX = probeX;
                    ejectY = probeY;
                }
                // else : target deja hors zone (a fui apres l'annonce) -> ejection = pos actuelle.

                // -- Phase 2 : SWAP target<->caster en utilisant ejectXY pour le caster --
                // Clear les cases initiales avant move (eviter double-occupant temporaire).
                GridHelpers.SetOccupant(f, casterStartX, casterStartY, EntityRef.None);
                GridHelpers.SetOccupant(f, targetStartX, targetStartY, EntityRef.None);

                target->GridX = casterStartX;
                target->GridY = casterStartY;
                caster->GridX = ejectX;
                caster->GridY = ejectY;

                EntityRef casterEntityRef = ResolveEntityFromPlayerIndex(f, caster->PlayerIndex);
                GridHelpers.SetOccupant(f, casterStartX, casterStartY, targetEntity);
                GridHelpers.SetOccupant(f, ejectX, ejectY, casterEntityRef);

                casterEndX = ejectX;
                casterEndY = ejectY;
                swapDone = true;
                Log.Info($"[Effondrement] EJECTION+SWAP : P{target->PlayerIndex} ({targetStartX},{targetStartY}) ejecte vers ({ejectX},{ejectY}), puis swap -> centre ({casterStartX},{casterStartY}). Caster -> ({ejectX},{ejectY}).");

                // Damage 200 sur la target (au centre Failles).
                int finalDmg = ColossarPassif.ApplyDamageReduction(f, target, dmg);
                int shieldMag = StatusHelper.GetMagnitude(target, StatusKind.ShieldActive, 0);
                int hpLoss = finalDmg;
                if (shieldMag > 0 && finalDmg > 0)
                {
                    int absorbed = finalDmg > shieldMag ? shieldMag : finalDmg;
                    int newShield = shieldMag - absorbed;
                    if (newShield == 0) StatusHelper.Consume(target, StatusKind.ShieldActive);
                    else StatusHelper.SetMagnitude(target, StatusKind.ShieldActive, newShield);
                    hpLoss = finalDmg - absorbed;
                }
                int hpBefore = target->HP;
                target->HP -= hpLoss;
                if (target->HP < 0) target->HP = 0;
                target->DamageTakenThisRound += hpLoss;
                target->HitsTakenThisRound += 1; // 3.3.c Ressac Vital tracker
                Log.Info($"[Effondrement] {finalDmg} dmg (HP loss {hpLoss}) sur P{target->PlayerIndex} (centre Failles {casterStartX},{casterStartY}) HP {hpBefore} -> {target->HP}");
            }
            else
            {
                Log.Info($"[Effondrement] Pas de swap (cible snapshot morte/absente). Failles autour case caster + buff uniquement.");
            }

            // Pose Failles dans le rayon 2 autour du centre (= ancienne case caster, qui est
            // maintenant celle de l'ennemi swap ou simplement la case caster sans swap).
            // SKIP la case caster post-swap pour qu'il puisse y rester (Bible +1 PM utile).
            for (int dyF = -radius; dyF <= radius; dyF++)
            {
                for (int dxF = -radius; dxF <= radius; dxF++)
                {
                    int absDxF = dxF < 0 ? -dxF : dxF;
                    int absDyF = dyF < 0 ? -dyF : dyF;
                    if (absDxF + absDyF > radius) continue;
                    if (dxF == 0 && dyF == 0) continue; // skip centre (= position cible swap ou caster sans swap)
                    int fx = failleCenterX + dxF;
                    int fy = failleCenterY + dyF;
                    if (!GridHelpers.InBounds(fx, fy)) continue;
                    if (fx == casterEndX && fy == casterEndY) continue; // skip case caster post-swap
                    if (GridHelpers.GetOccupant(f, fx, fy) != EntityRef.None) continue;
                    if (ObstacleHelpers.HasObstacleAt(f, fx, fy)) continue;
                    ObstacleHelpers.SpawnObstacle(f,
                        ObstacleKind.Faille, SpellRegistry.EffondrementFailleHP,
                        fx, fy,
                        owner: EntityRef.None, ownerPlayerIndex: caster->PlayerIndex,
                        expiresOnTurn: expireAtTurn);
                }
            }

            // Buff caster (EffondrementActive + DamageReductionPercent 30%).
            StatusHelper.Apply(caster, StatusKind.EffondrementActive,
                magnitude: 0,
                turnsLeft: SpellRegistry.EffondrementBuffTurns,
                currentTurn);
            StatusHelper.Apply(caster, StatusKind.DamageReductionPercent,
                magnitude: SpellRegistry.EffondrementDmgReductionPct,
                turnsLeft: SpellRegistry.EffondrementBuffTurns,
                currentTurn);
            Log.Info($"[Effondrement] Buff applique sur P{caster->PlayerIndex} : EffondrementActive + DamageReductionPercent {SpellRegistry.EffondrementDmgReductionPct}% / {SpellRegistry.EffondrementBuffTurns} tours. Caster final pos ({casterEndX},{casterEndY}), swap={swapDone}");
        }

        /// <summary>
        /// Helper utilitaire pour retrouver l'EntityRef d'un combatant par PlayerIndex (nécessaire
        /// pour le swap d'Effondrement : on n'a que le pointer Combatant* du caster, pas son EntityRef).
        /// </summary>
        private static EntityRef ResolveEntityFromPlayerIndex(Frame f, int playerIndex)
        {
            var filter = f.Filter<Combatant>();
            while (filter.Next(out EntityRef entity, out Combatant c))
            {
                if (c.PlayerIndex == playerIndex) return entity;
            }
            return EntityRef.None;
        }
    }
}
