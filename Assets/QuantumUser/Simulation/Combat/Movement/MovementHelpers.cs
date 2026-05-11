namespace Quantum
{
    /// <summary>
    /// Helpers de mouvement statiques (deterministes, sans PM).
    ///
    /// Pour 2.10.c :
    ///   - MoveNonPM : deplace une entite sans toucher PM. Utilise par Charge Brutale,
    ///     recul Tranche-Ame, et potentiellement futurs sorts qui deplacent gratuitement
    ///     (Empoignade pull pourrait etre refactore ici en Phase 7 polish).
    /// </summary>
    public static unsafe class MovementHelpers
    {
        /// <summary>
        /// Deplace une entite a (toX, toY) sans consommer de PM. Valide :
        ///   - dans la grille
        ///   - case walkable
        ///   - case non occupee
        ///   - destination differente de la case actuelle
        ///
        /// Retourne true si le deplacement a reussi, false sinon. Le caller ne paie aucun PM.
        /// </summary>
        public static bool MoveNonPM(Frame f, EntityRef entity, Combatant* combatant, int toX, int toY)
        {
            if (!GridHelpers.InBounds(toX, toY)) return false;
            if (!GridHelpers.IsWalkable(f, toX, toY)) return false;
            if (GridHelpers.GetOccupant(f, toX, toY) != EntityRef.None) return false;

            int fromX = combatant->GridX;
            int fromY = combatant->GridY;
            if (fromX == toX && fromY == toY) return false; // no-op

            GridHelpers.SetOccupant(f, fromX, fromY, EntityRef.None);
            combatant->GridX = toX;
            combatant->GridY = toY;
            GridHelpers.SetOccupant(f, toX, toY, entity);
            return true;
        }
    }
}
