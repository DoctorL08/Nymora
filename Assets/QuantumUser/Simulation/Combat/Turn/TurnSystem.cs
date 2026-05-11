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

            // Tirage d'initiative deterministe (Bible V7.1 : random tour 1, alternance ensuite).
            // f.RNG->Next(0, max) retourne un int dans [0, max) - donc [0, 2) = 0 ou 1.
            state->ActivePlayerIndex = f.RNG->Next(0, TurnConstants.PlayerCount);

            // Transition immediate vers le 1er TurnStart. La FSM termine son init au prochain Update.
            state->CurrentPhase = CombatPhase.TurnStart;

            Log.Info($"[TurnSystem] Initiative: Joueur P{state->ActivePlayerIndex} commence");
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
            state->TurnNumber += 1;
            state->TurnTimerTicks = TurnConstants.GetTurnDurationTicks(f);

            // Reset PA/PM du joueur actif (Bible V7.1 : debut de tour = ressources fraiches).
            // HP et ressources de classe (HG, PR, FD, PT, RM) NE sont PAS resets.
            int activePlayer = state->ActivePlayerIndex;
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* combatant))
            {
                if (combatant->PlayerIndex == activePlayer)
                {
                    combatant->PA = combatant->MaxPA;
                    combatant->PM = combatant->MaxPM;

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
                }
            }

            Log.Info($"[TurnSystem] Tour {state->TurnNumber} - Joueur P{activePlayer} (timer {state->TurnTimerTicks} ticks)");
            state->CurrentPhase = CombatPhase.TurnActive;
        }

        private static void TickTurnActive(Frame f, CombatState* state)
        {
            state->TurnTimerTicks -= 1;
            if (state->TurnTimerTicks <= 0)
            {
                state->TurnTimerTicks = 0;
                state->CurrentPhase = CombatPhase.TurnEnd;
            }
        }

        private static void EnterTurnEnd(Frame f, CombatState* state)
        {
            // 2.10.a : decremente les statuses de tous les combattants. La regle
            // "skip si AppliedOnTurn == currentTurn" assure une semantic intuitive
            // pour les durees Bible V7.1 (cf StatusHelper.DecrementAllOnTurnEnd).
            StatusHelper.DecrementAllOnTurnEnd(f, state->TurnNumber);

            // Alternance stricte des 2 joueurs (1v1 en Phase 2). Pour 2v2/3v3 (Phase 6),
            // la rotation devra suivre l'ordre d'initiative et non un simple modulo.
            state->ActivePlayerIndex = (state->ActivePlayerIndex + 1) % TurnConstants.PlayerCount;
            state->CurrentPhase = CombatPhase.TurnStart;
        }
    }
}
