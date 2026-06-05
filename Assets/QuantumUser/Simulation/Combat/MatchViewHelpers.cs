namespace Quantum
{
    /// <summary>
    /// #23 (5 juin) — Helpers READ-ONLY pour l'affichage de l'outline d'appartenance en match
    /// MIROIR (deux joueurs de la meme classe). Encapsule l'acces unsafe au frame pour que la View
    /// (MirrorOwnershipOutlineView) reste sans pointeurs. La sim ne s'en sert pas.
    /// </summary>
    public static unsafe class MatchViewHelpers
    {
        /// <summary>True si les deux joueurs (PlayerIndex 0 et 1) ont la MEME classe — seul cas ou
        /// les objets poses (pieges, leurres, obstacles, terrain) sont visuellement ambigus.</summary>
        public static bool IsMirrorMatch(Frame f)
        {
            if (f == null) return false;
            NymoraClass c0 = NymoraClass.None, c1 = NymoraClass.None;
            bool has0 = false, has1 = false;
            var it = f.Filter<Combatant>();
            while (it.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                if (c->PlayerIndex == 0) { c0 = c->Class; has0 = true; }
                else if (c->PlayerIndex == 1) { c1 = c->Class; has1 = true; }
            }
            return has0 && has1 && c0 != NymoraClass.None && c0 == c1;
        }

        /// <summary>Owner (PlayerIndex) de l'objet present sur la case, -1 si aucun. Priorite
        /// piege > terrain > obstacle > leurre (les objets sont quasi exclusifs par case). Sert a
        /// colorer l'outline d'equipe. Read-only.</summary>
        public static int GetObjectOwnerAt(Frame f, int x, int y)
        {
            int owner = FogHelpers.GetTrapOwner(f, x, y); // -1 si pas de piege
            if (owner < 0 && GridHelpers.GetTerrainKind(f, x, y) != TerrainKind.None)
                owner = FogHelpers.GetTerrainOwner(f, x, y);
            if (owner < 0) owner = ObstacleHelpers.GetObstacleOwnerAt(f, x, y);
            if (owner < 0) owner = DecoyHelpers.GetDecoyOwnerAt(f, x, y);
            return owner;
        }
    }
}
