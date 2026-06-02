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
            // Fix 2 juin — une case-OBSTACLE (Faille d'Effondrement, Pilier, Mur) est solide : on ne
            // peut ni y teleporter ni y etre pousse/tire. IsWalkable ne teste que le flag statique de
            // la tuile (pas les obstacles dynamiques) -> check explicite ici. Bloque l'exploit "TP sur
            // Faille pour fuir l'ulti Colossar" et tout move-spell qui atterrirait sur un obstacle.
            if (ObstacleHelpers.HasObstacleAt(f, toX, toY)) return false;
            // 3.7.a.i.4 — TOUT leurre Ghostra bloque la destination (Bible-strict).
            // Applique a tout sort-move (Charge Brutale, recul Tranche-Ame, etc.).
            if (DecoyHelpers.HasAnyDecoyAt(f, toX, toY)) return false;

            int fromX = combatant->GridX;
            int fromY = combatant->GridY;
            if (fromX == toX && fromY == toY) return false; // no-op

            // 3.7.a.i.0 — Update Facing depuis le delta du move (cohérent avec MovementSystem.ApplyMove).
            // Critique pour le dorsal hit Ghostra : tout sort qui deplace via MoveNonPM (Charge Brutale,
            // recul Tranche-Ame, etc.) doit aussi mettre a jour le Facing, sinon target.Facing reste
            // bloque sur sa valeur initiale.
            combatant->Facing = FacingHelpers.FacingFromGridDelta(toX - fromX, toY - fromY);

            GridHelpers.SetOccupant(f, fromX, fromY, EntityRef.None);
            combatant->GridX = toX;
            combatant->GridY = toY;
            GridHelpers.SetOccupant(f, toX, toY, entity);
            return true;
        }
    }
}
