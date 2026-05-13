namespace Quantum
{
    using Photon.Deterministic;

    /// <summary>
    /// AI brain Nymora (Bloc E Phase 2).
    ///
    /// 2.16.a.i — squelette : EndTurn auto si bot actif (delai 30 ticks).
    /// 2.16.a.ii — ajoute TryGreedyMove : bot Soulrender s'approche de l'ennemi
    ///             en utilisant tout son PM (1 move par tour, all-in).
    /// 2.16.a.iii — ajoutera EvaluateSpell + boucle de cast greedy.
    ///
    /// Pourquoi pas un MoveCommand simule ? Les DeterministicCommand viennent de
    /// l'input client. Une IA simu-side mute directement la primitive d'etat
    /// (GridX/Y, PM, SetOccupant) — meme effet que MovementSystem.ApplyMove. Le
    /// trigger trap (FogHelpers.TryTriggerTrapOnEnter) est appele en post-move
    /// pour conserver les semantiques marques/pieges Nightseer.
    ///
    /// Ordre des systems : AISystem AVANT TurnSystem dans SystemSetup pour que
    /// TurnTimerTicks=0 soit visible au meme tick par TickTurnActive.
    /// </summary>
    public unsafe class AISystem : SystemMainThread
    {
        public override void Update(Frame f)
        {
            var state = f.Unsafe.GetPointerSingleton<CombatState>();

            // L'IA n'agit que quand son joueur est actif ET le tour est en cours.
            if (state->CurrentPhase != CombatPhase.TurnActive) return;
            if (state->ActivePlayerIndex != AIConstants.BotPlayerIndex) return;

            // Recupere l'entity du bot (1 seul en 1v1). On extrait juste l'EntityRef
            // dans le filter pour eviter de garder un Combatant* vivant a travers
            // d'autres appels (pattern miroir de MovementSystem.TryMoveCombatant).
            EntityRef botEntity = EntityRef.None;
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef e, out Combatant* c))
            {
                if (c->PlayerIndex == AIConstants.BotPlayerIndex)
                {
                    botEntity = e;
                    break;
                }
            }
            if (botEntity == EntityRef.None) return;

            var bot = f.Unsafe.GetPointer<Combatant>(botEntity);
            if (bot->HP <= 0) return; // bot mort, TurnSystem gere MatchEnd

            // Phase action : 1 tentative de move greedy par tick. Idempotent — apres
            // un move reussi, PM=0 fait que les ticks suivants sont no-op.
            TryGreedyMove(f, botEntity, bot);

            // Phase delay : end turn apres BotEndTurnDelayTicks ecoules. Laisse au
            // joueur humain le temps de voir le move + l'etat avant de passer au sien.
            int totalDuration = TurnConstants.GetTurnDurationTicks(f);
            int elapsed = totalDuration - state->TurnTimerTicks;
            if (elapsed < AIConstants.BotEndTurnDelayTicks) return;

            Log.Info($"[AI] Bot P{state->ActivePlayerIndex} termine son tour (PM restant {bot->PM})");
            state->TurnTimerTicks = 0;
        }

        /// <summary>
        /// Selectionne la meilleure case de destination dans la portee PM du bot et
        /// applique le move directement. No-op si PM=0 ou si aucune case ne fait
        /// strictement mieux que rester sur place.
        ///
        /// Algorithme :
        ///   1. Enumere toutes les cases (dx, dy) avec |dx| + |dy| <= PM
        ///   2. Filtre : InBounds + Walkable + non occupee
        ///   3. Verifie le chemin via A* (path length <= PM)
        ///   4. Ajoute le cout Vapeur Carmin (+1 PM sur case arrivee)
        ///   5. Score la destination via AIEvaluator.ScoreMoveDestination
        ///   6. Garde le meilleur (tie-break index grille croissant)
        ///   7. Applique le move + trigger trap si meilleur que stay-still
        /// </summary>
        private static void TryGreedyMove(Frame f, EntityRef botEntity, Combatant* bot)
        {
            if (bot->PM <= 0) return;

            int startX = bot->GridX;
            int startY = bot->GridY;
            int maxRange = bot->PM;

            int currentScore = AIEvaluator.ScoreMoveDestination(f, bot, startX, startY);

            int bestScore = currentScore;
            int bestX = startX;
            int bestY = startY;
            int bestCost = 0;
            int bestIdx = GridHelpers.Index(startX, startY);

            int* pathBuf = stackalloc int[GridConstants.Count];

            for (int dy = -maxRange; dy <= maxRange; dy++)
            {
                int absDy = dy < 0 ? -dy : dy;
                for (int dx = -maxRange; dx <= maxRange; dx++)
                {
                    int absDx = dx < 0 ? -dx : dx;
                    int manhattan = absDx + absDy;
                    if (manhattan == 0) continue;
                    if (manhattan > maxRange) continue;

                    int tx = startX + dx;
                    int ty = startY + dy;
                    if (!GridHelpers.InBounds(tx, ty)) continue;
                    if (!GridHelpers.IsWalkable(f, tx, ty)) continue;
                    if (GridHelpers.GetOccupant(f, tx, ty) != EntityRef.None) continue;

                    int extraCost = GridHelpers.GetTerrainKind(f, tx, ty) == TerrainKind.VapeurCarmin ? 1 : 0;
                    if (manhattan + extraCost > maxRange) continue;

                    if (!AStarPathfinder.TryFindPath(f, startX, startY, tx, ty, maxRange, pathBuf, out int pathLen))
                        continue;

                    int totalCost = pathLen + extraCost;
                    if (totalCost > maxRange) continue;

                    int score = AIEvaluator.ScoreMoveDestination(f, bot, tx, ty);
                    int idx = GridHelpers.Index(tx, ty);

                    if (score > bestScore || (score == bestScore && idx < bestIdx))
                    {
                        bestScore = score;
                        bestX = tx;
                        bestY = ty;
                        bestCost = totalCost;
                        bestIdx = idx;
                    }
                }
            }

            if (bestX == startX && bestY == startY) return;
            if (bestScore <= currentScore) return;

            Log.Info($"[AI] Bot P{bot->PlayerIndex} se deplace ({startX},{startY}) -> ({bestX},{bestY}) cost={bestCost} score={bestScore}");
            GridHelpers.SetOccupant(f, startX, startY, EntityRef.None);
            bot->GridX = bestX;
            bot->GridY = bestY;
            bot->PM -= bestCost;
            GridHelpers.SetOccupant(f, bestX, bestY, botEntity);

            int currentTurn = f.TryGetSingleton<CombatState>(out var st) ? st.TurnNumber : 0;
            FogHelpers.TryTriggerTrapOnEnter(f, botEntity, bot, bestX, bestY, currentTurn);
        }
    }
}
