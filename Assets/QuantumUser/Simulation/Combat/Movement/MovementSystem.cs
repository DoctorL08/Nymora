namespace Quantum
{
    using Photon.Deterministic;

    /// <summary>
    /// Traite les MoveCommands envoyees par les joueurs.
    ///
    /// 2.5 : appelle A* pour calculer le chemin optimal vers la case cible.
    /// - Si distance Manhattan == 1 : application directe (skip A*, optim)
    /// - Sinon : A* deterministe (cf AStarPathfinder), application si path.length <= PM
    ///
    /// Application synchrone en 1 tick : combattant teleporte a la destination,
    /// PM decrement par la longueur du chemin. Le View lerp en ligne droite vers
    /// la nouvelle case (anim case-par-case = Phase 2.10+ si necessaire).
    /// </summary>
    public unsafe class MovementSystem : SystemMainThread
    {
        public override void Update(Frame f)
        {
            if (!f.TryGetSingleton<CombatState>(out var state)) return;
            if (state.CurrentPhase != CombatPhase.TurnActive) return;

            for (int playerIndex = 0; playerIndex < TurnConstants.PlayerCount; playerIndex++)
            {
                if (f.GetPlayerCommand(playerIndex) is MoveCommand cmd)
                {
                    TryMoveCombatant(f, playerIndex, cmd.TargetX, cmd.TargetY, state.ActivePlayerIndex);
                }
            }
        }

        private static void TryMoveCombatant(Frame f, int playerIndex, int targetX, int targetY, int activePlayerIndex)
        {
            if (playerIndex != activePlayerIndex)
            {
                Log.Warn($"[Movement] rejet : ce n'est pas le tour de P{playerIndex}");
                return;
            }

            EntityRef combatantEntity = EntityRef.None;
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef entity, out Combatant* c))
            {
                if (c->PlayerIndex == playerIndex)
                {
                    combatantEntity = entity;
                    break;
                }
            }

            if (combatantEntity == EntityRef.None)
            {
                Log.Warn($"[Movement] rejet : pas de Combatant pour P{playerIndex}");
                return;
            }

            var combatant = f.Unsafe.GetPointer<Combatant>(combatantEntity);

            if (combatant->PM <= 0)
            {
                Log.Warn("[Movement] rejet : PM=0");
                return;
            }

            // No-op silencieux si clic sur la propre case du combattant.
            if (targetX == combatant->GridX && targetY == combatant->GridY) return;

            if (!GridHelpers.InBounds(targetX, targetY))
            {
                Log.Warn($"[Movement] rejet : ({targetX},{targetY}) hors grille");
                return;
            }
            if (!GridHelpers.IsWalkable(f, targetX, targetY))
            {
                Log.Warn($"[Movement] rejet : ({targetX},{targetY}) non walkable");
                return;
            }
            if (GridHelpers.GetOccupant(f, targetX, targetY) != EntityRef.None)
            {
                Log.Warn($"[Movement] rejet : ({targetX},{targetY}) deja occupee");
                return;
            }

            // Heuristique rapide : si meme la distance Manhattan optimale depasse PM, inutile d'appeler A*.
            int dx = targetX - combatant->GridX;
            int dy = targetY - combatant->GridY;
            int absDx = dx < 0 ? -dx : dx;
            int absDy = dy < 0 ? -dy : dy;
            int manhattan = absDx + absDy;
            if (manhattan > combatant->PM)
            {
                Log.Warn($"[Movement] rejet : distance optimale {manhattan} > PM {combatant->PM}");
                return;
            }

            // Cas adjacent (1 case) : skip A* pour optimiser
            if (manhattan == 1)
            {
                ApplyMove(f, combatant, combatantEntity, targetX, targetY, 1);
                return;
            }

            // Cas multi-cases : A* deterministe
            int* pathBuffer = stackalloc int[GridConstants.Count];
            if (!AStarPathfinder.TryFindPath(
                    f,
                    combatant->GridX, combatant->GridY,
                    targetX, targetY,
                    combatant->PM,
                    pathBuffer,
                    out int pathLength))
            {
                Log.Warn($"[Movement] rejet : pas de chemin <= PM={combatant->PM} vers ({targetX},{targetY})");
                return;
            }

            ApplyMove(f, combatant, combatantEntity, targetX, targetY, pathLength);
        }

        private static void ApplyMove(Frame f, Combatant* combatant, EntityRef entity, int targetX, int targetY, int cost)
        {
            GridHelpers.SetOccupant(f, combatant->GridX, combatant->GridY, EntityRef.None);
            combatant->GridX = targetX;
            combatant->GridY = targetY;
            combatant->PM -= cost;
            GridHelpers.SetOccupant(f, targetX, targetY, entity);

            Log.Info($"[Movement] P{combatant->PlayerIndex} -> ({targetX},{targetY}) cost={cost} PM restant={combatant->PM}");
        }
    }
}
