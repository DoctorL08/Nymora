namespace Quantum
{
    using Photon.Deterministic;

    /// <summary>
    /// Traite les MoveCommands envoyees par les joueurs. Bouge le Combatant d'1 case
    /// (4-connexite Manhattan) si toutes les validations passent : phase TurnActive,
    /// joueur actif, PM > 0, case adjacente, walkable, non occupee.
    ///
    /// 2.4 : mouvement 1 case a la fois. Le pathfinding A* multi-cases (clic sur case
    /// lointaine = chemin auto cellule par cellule) c'est la brique 2.5.
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

            // Cherche le Combatant du joueur
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

            // Adjacence Manhattan : distance 4-connexite == 1 (pas de diagonale en 2.4)
            int dx = targetX - combatant->GridX;
            int dy = targetY - combatant->GridY;
            int absDx = dx < 0 ? -dx : dx;
            int absDy = dy < 0 ? -dy : dy;
            if (absDx + absDy != 1)
            {
                Log.Warn($"[Movement] rejet : non-adjacent (dx={dx}, dy={dy})");
                return;
            }

            // Application : libere ancienne case, deplace, occupe nouvelle case, decrement PM
            GridHelpers.SetOccupant(f, combatant->GridX, combatant->GridY, EntityRef.None);
            combatant->GridX = targetX;
            combatant->GridY = targetY;
            combatant->PM -= 1;
            GridHelpers.SetOccupant(f, targetX, targetY, combatantEntity);

            Log.Info($"[Movement] P{playerIndex} -> ({targetX},{targetY}) PM restant={combatant->PM}");
        }
    }
}
