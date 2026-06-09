namespace Quantum
{
    /// <summary>
    /// Resolveur de ciblage Nymora.
    ///
    /// 3 methodes :
    ///   - ResolveCastableCells : quelles cases le caster peut viser (range Manhattan)
    ///   - ResolveEffectCells   : quelles cases sont dans la zone d'effet (shape)
    ///   - MatchesFilter        : la case match-elle le filter (occupant type)
    ///
    /// Cote View : le preview combine les 3 pour montrer les cases cliquables + zone d'effet
    ///             au survol. Cote simu : le SpellSystem (2.7) combinera les 3 pour valider
    ///             un cast et appliquer les effets.
    ///
    /// Shapes implementes en 2.6 : SingleTile, CrossSmall, Line, CircleSmall.
    /// Les autres (CrossMedium/Large, Square3x3/5x5, LineThrough, Cone, CircleMedium/Large)
    /// retournent count=0 + Log.Warn pour signaler qu'il faut les implementer quand un sort
    /// en aura besoin. Cf brique 2.10 (sorts Soulrender) et 2.13 (sorts Nightseer).
    /// </summary>
    public static unsafe class TargetingResolver
    {
        // =================================================================
        // SAFE wrappers (int[] managed) — utilisables par le View Unity sans
        // necessiter d'asmdef avec allowUnsafeCode: true.
        // =================================================================
        public static void ResolveCastableCells(
            Frame f,
            int casterX, int casterY,
            int rangeMin, int rangeMax,
            int[] outBuffer,
            out int count)
        {
            fixed (int* buf = outBuffer)
            {
                ResolveCastableCells(f, casterX, casterY, rangeMin, rangeMax, buf, out count);
            }
        }

        public static void ResolveEffectCells(
            Frame f,
            int casterX, int casterY,
            int targetX, int targetY,
            TargetingShape shape,
            int[] outBuffer,
            out int count)
        {
            fixed (int* buf = outBuffer)
            {
                ResolveEffectCells(f, casterX, casterY, targetX, targetY, shape, buf, out count);
            }
        }

        // =================================================================
        // UNSAFE versions (int*) — preferees cote simu pour zero alloc (stackalloc).
        // =================================================================

        // -----------------------------------------------------------------
        // Castable cells : cases visables par le caster (range Manhattan)
        // -----------------------------------------------------------------
        public static void ResolveCastableCells(
            Frame f,
            int casterX, int casterY,
            int rangeMin, int rangeMax,
            int* outBuffer,
            out int count)
        {
            count = 0;
            if (rangeMax < rangeMin) return;

            for (int y = 0; y < GridConstants.Height; y++)
            {
                for (int x = 0; x < GridConstants.Width; x++)
                {
                    int dx = x - casterX; if (dx < 0) dx = -dx;
                    int dy = y - casterY; if (dy < 0) dy = -dy;
                    int dist = dx + dy;
                    if (dist < rangeMin || dist > rangeMax) continue;

                    outBuffer[count++] = GridHelpers.Index(x, y);
                }
            }
        }

        // -----------------------------------------------------------------
        // Effect cells : cases impactees par le sort centre sur (targetX, targetY)
        // -----------------------------------------------------------------
        public static void ResolveEffectCells(
            Frame f,
            int casterX, int casterY,
            int targetX, int targetY,
            TargetingShape shape,
            int* outBuffer,
            out int count)
        {
            count = 0;

            switch (shape)
            {
                case TargetingShape.SingleTile:
                    AddIfInBounds(targetX, targetY, outBuffer, ref count);
                    break;

                case TargetingShape.CrossSmall:
                    AddIfInBounds(targetX, targetY, outBuffer, ref count);
                    AddIfInBounds(targetX + 1, targetY, outBuffer, ref count);
                    AddIfInBounds(targetX - 1, targetY, outBuffer, ref count);
                    AddIfInBounds(targetX, targetY + 1, outBuffer, ref count);
                    AddIfInBounds(targetX, targetY - 1, outBuffer, ref count);
                    break;

                case TargetingShape.Line:
                    AddLineFromCaster(casterX, casterY, targetX, targetY, outBuffer, ref count, throughUnits: false);
                    break;

                case TargetingShape.CircleSmall:
                    AddCircleManhattan(targetX, targetY, radius: 1, outBuffer, ref count);
                    break;

                // 2.15.b — Square3x3 (= 9 cases : centre + 8 voisins). Utilise par Champ de Mines.
                case TargetingShape.Square3x3:
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            AddIfInBounds(targetX + dx, targetY + dy, outBuffer, ref count);
                    break;

                // Shapes pas encore implementees — a faire quand un sort en aura besoin.
                case TargetingShape.None:
                    break;

                case TargetingShape.CrossMedium:
                case TargetingShape.CrossLarge:
                case TargetingShape.Square5x5:
                case TargetingShape.LineThrough:
                case TargetingShape.Cone:
                case TargetingShape.CircleMedium:
                case TargetingShape.CircleLarge:
                    Log.Warn($"[TargetingResolver] Shape {shape} non implementee en 2.6 — a faire selon les besoins (sorts Phase 2.10+/2.13).");
                    break;
            }
        }

        // -----------------------------------------------------------------
        // Filter : est-ce que cette case match le filtre attendu ?
        // -----------------------------------------------------------------
        public static bool MatchesFilter(
            Frame f,
            int cellX, int cellY,
            TargetingFilter filter,
            EntityRef casterEntity,
            int casterPlayerIndex)
        {
            if (!GridHelpers.InBounds(cellX, cellY)) return false;

            EntityRef occupant = GridHelpers.GetOccupant(f, cellX, cellY);
            bool occupied = occupant != EntityRef.None;
            bool isCaster = occupied && occupant == casterEntity;

            // 5.1 (2v2/3v3) — allie/ennemi se decide sur l'EQUIPE (TeamId), plus le PlayerIndex.
            //   On lit le TeamId du caster via casterEntity (fallback casterPlayerIndex = 1v1).
            bool isAlly = false;
            bool isEnemy = false;
            if (occupied && !isCaster)
            {
                if (f.Unsafe.TryGetPointer<Combatant>(occupant, out Combatant* other))
                {
                    int casterTeamId = casterPlayerIndex;
                    if (f.Unsafe.TryGetPointer<Combatant>(casterEntity, out Combatant* casterC))
                        casterTeamId = casterC->TeamId;
                    isAlly = other->TeamId == casterTeamId;
                    isEnemy = other->TeamId != casterTeamId;
                }
            }

            switch (filter)
            {
                case TargetingFilter.None:
                    return false;
                case TargetingFilter.Self:
                    return isCaster;
                case TargetingFilter.Ally:
                    return isAlly;
                case TargetingFilter.AllyIncludingSelf:
                    return isCaster || isAlly;
                case TargetingFilter.Enemy:
                    return isEnemy;
                case TargetingFilter.AnyUnit:
                    return occupied;
                case TargetingFilter.EmptyTile:
                    // Fix 2 juin — une case avec OBSTACLE (Faille d'Effondrement, Pilier, Mur) n'est PAS
                    // vide : sans ce check, les TP filtre EmptyTile (Dernier Pas, Pas dans l'Ombre...)
                    // pouvaient atterrir sur une Faille pour s'echapper de l'ulti Colossar. Les poses
                    // d'obstacle (Pilier/Mur, aussi EmptyTile) rejettent deja les cases occupees cote
                    // SpawnObstacle ; ce filtre les bloque maintenant aussi proprement avant les PA.
                    return !occupied
                        && GridHelpers.IsWalkable(f, cellX, cellY)
                        && !ObstacleHelpers.HasObstacleAt(f, cellX, cellY);
                case TargetingFilter.TileWithObstacle:
                    // PATCH 22 mai (test designer) — case occupee par un obstacle (Pilier/Mur/Faille).
                    return ObstacleHelpers.HasObstacleAt(f, cellX, cellY);
                case TargetingFilter.TileWithLure:
                    // Permutation Ghostra (refonte 30 mai) : valide UNIQUEMENT une case occupee par
                    // un leurre appartenant AU CASTER (swap Ghostra<->leurre). Les leurres ne sont
                    // pas dans la grid occupancy -> check dedie DecoyHelpers.HasOwnDecoyAt.
                    return DecoyHelpers.HasOwnDecoyAt(f, casterPlayerIndex, cellX, cellY);
                case TargetingFilter.AnyTile:
                    return true;
                default:
                    return false;
            }
        }

        // =================================================================
        // Helpers prives
        // =================================================================

        private static void AddIfInBounds(int x, int y, int* outBuffer, ref int count)
        {
            if (!GridHelpers.InBounds(x, y)) return;
            outBuffer[count++] = GridHelpers.Index(x, y);
        }

        private static void AddCircleManhattan(int centerX, int centerY, int radius, int* outBuffer, ref int count)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int absDx = dx < 0 ? -dx : dx;
                    int absDy = dy < 0 ? -dy : dy;
                    if (absDx + absDy > radius) continue;
                    AddIfInBounds(centerX + dx, centerY + dy, outBuffer, ref count);
                }
            }
        }

        private static void AddLineFromCaster(int casterX, int casterY, int targetX, int targetY, int* outBuffer, ref int count, bool throughUnits)
        {
            // Force la ligne sur 1 axe cardinal : si target n'est pas alignee, on prend l'axe dominant.
            int dx = targetX - casterX;
            int dy = targetY - casterY;
            int absDx = dx < 0 ? -dx : dx;
            int absDy = dy < 0 ? -dy : dy;

            int stepX = 0, stepY = 0;
            int length;

            if (absDx >= absDy)
            {
                stepX = dx > 0 ? 1 : -1;
                length = absDx;
            }
            else
            {
                stepY = dy > 0 ? 1 : -1;
                length = absDy;
            }

            // En 2.6 : Line va simplement jusqu'au target (ou bord de grille). Le stop sur
            // unite (Line classique) vs traversee (LineThrough) sera ajoute quand un sort
            // concret en aura besoin (Phase 2.10+). Pour faire le check correctement il
            // faudra passer une Frame en parametre.
            int cx = casterX;
            int cy = casterY;
            for (int i = 0; i < length; i++)
            {
                cx += stepX;
                cy += stepY;
                if (!GridHelpers.InBounds(cx, cy)) break;
                outBuffer[count++] = GridHelpers.Index(cx, cy);
            }
        }
    }
}
