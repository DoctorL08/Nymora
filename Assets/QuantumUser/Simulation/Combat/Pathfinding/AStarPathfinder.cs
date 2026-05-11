namespace Quantum
{
    /// <summary>
    /// A* deterministe sur la grille 15x17 (4-connexite Manhattan).
    ///
    /// Garanties :
    ///   - Determinisme : tie-break par index grille croissant (y*Width + x) en cas
    ///     d'egalite fScore. Donc l'ordre d'expansion est totalement reproductible.
    ///   - Zero allocation heap : tous les buffers sont stackalloc (4 KB stack/appel
    ///     pour 255 cases, OK).
    ///   - Pas de float ni FP : tout en int (heuristique Manhattan).
    ///
    /// Utilisation cote MovementSystem :
    ///   int* path = stackalloc int[GridConstants.Count];
    ///   if (AStarPathfinder.TryFindPath(f, sx, sy, tx, ty, maxSteps, path, out int len)) { ... }
    /// </summary>
    public static unsafe class AStarPathfinder
    {
        private const int Width = GridConstants.Width;
        private const int Height = GridConstants.Height;
        private const int Count = GridConstants.Count;

        private const int Unreachable = -1;
        private const int Infinity = int.MaxValue;

        // 4-connexite : droite, gauche, haut, bas. L'ordre fixe l'expansion deterministe.
        private static readonly int[] DX = { 1, -1, 0, 0 };
        private static readonly int[] DY = { 0, 0, 1, -1 };

        /// <summary>
        /// Cherche le chemin le plus court (4-connexite) de (startX,startY) a (targetX,targetY).
        /// Le start est exclu du buffer de sortie, le target est inclus a la fin.
        /// Retourne false si pas de chemin OU longueur > maxSteps OU target invalide.
        /// </summary>
        /// <param name="pathOutBuffer">buffer de sortie, doit contenir au moins maxSteps elements (indices grille).</param>
        /// <param name="pathLength">longueur du chemin trouve (0 si return false).</param>
        public static bool TryFindPath(
            Frame f,
            int startX, int startY,
            int targetX, int targetY,
            int maxSteps,
            int* pathOutBuffer,
            out int pathLength)
        {
            pathLength = 0;

            if (!GridHelpers.InBounds(startX, startY)) return false;
            if (!GridHelpers.InBounds(targetX, targetY)) return false;
            if (!GridHelpers.IsWalkable(f, targetX, targetY)) return false;
            if (GridHelpers.GetOccupant(f, targetX, targetY) != EntityRef.None) return false;

            int startIdx = GridHelpers.Index(startX, startY);
            int targetIdx = GridHelpers.Index(targetX, targetY);
            if (startIdx == targetIdx) return false;

            // Buffers stackalloc (~4 KB total pour 255 cases)
            int* gScore = stackalloc int[Count];
            int* cameFrom = stackalloc int[Count];
            bool* closed = stackalloc bool[Count];
            int* openSet = stackalloc int[Count];
            int openCount = 0;

            for (int i = 0; i < Count; i++)
            {
                gScore[i] = Infinity;
                cameFrom[i] = Unreachable;
                closed[i] = false;
            }

            gScore[startIdx] = 0;
            openSet[openCount++] = startIdx;

            while (openCount > 0)
            {
                // Selection du meilleur fScore (tie-break index croissant pour determinisme)
                int bestPos = 0;
                int bestIdx = openSet[0];
                int bestF = gScore[bestIdx] + ManhattanHeuristic(bestIdx, targetX, targetY);
                for (int i = 1; i < openCount; i++)
                {
                    int idx = openSet[i];
                    int fScore = gScore[idx] + ManhattanHeuristic(idx, targetX, targetY);
                    if (fScore < bestF || (fScore == bestF && idx < bestIdx))
                    {
                        bestF = fScore;
                        bestIdx = idx;
                        bestPos = i;
                    }
                }

                int currentIdx = bestIdx;

                if (currentIdx == targetIdx)
                {
                    return ReconstructPath(currentIdx, startIdx, cameFrom, maxSteps, pathOutBuffer, out pathLength);
                }

                // Retire le current du open set (swap avec dernier, plus rapide qu'un shift)
                openSet[bestPos] = openSet[openCount - 1];
                openCount--;
                closed[currentIdx] = true;

                int cx = currentIdx % Width;
                int cy = currentIdx / Width;

                for (int dir = 0; dir < 4; dir++)
                {
                    int nx = cx + DX[dir];
                    int ny = cy + DY[dir];

                    if (!GridHelpers.InBounds(nx, ny)) continue;

                    int nIdx = GridHelpers.Index(nx, ny);
                    if (closed[nIdx]) continue;
                    if (!GridHelpers.IsWalkable(f, nx, ny)) continue;

                    // Une case occupee bloque le passage, SAUF si c'est notre case de
                    // depart (le combattant qui se deplace) ou la case cible (deja
                    // validee comme libre plus haut, mais redondance defensive).
                    if (nIdx != startIdx && nIdx != targetIdx
                        && GridHelpers.GetOccupant(f, nx, ny) != EntityRef.None) continue;

                    int tentativeG = gScore[currentIdx] + 1;
                    if (tentativeG >= gScore[nIdx]) continue;

                    gScore[nIdx] = tentativeG;
                    cameFrom[nIdx] = currentIdx;

                    // Ajoute au open set si pas deja present
                    bool alreadyInOpen = false;
                    for (int j = 0; j < openCount; j++)
                    {
                        if (openSet[j] == nIdx) { alreadyInOpen = true; break; }
                    }
                    if (!alreadyInOpen && openCount < Count)
                    {
                        openSet[openCount++] = nIdx;
                    }
                }
            }

            return false;
        }

        private static int ManhattanHeuristic(int idx, int targetX, int targetY)
        {
            int x = idx % Width;
            int y = idx / Width;
            int dx = x - targetX; if (dx < 0) dx = -dx;
            int dy = y - targetY; if (dy < 0) dy = -dy;
            return dx + dy;
        }

        private static bool ReconstructPath(
            int targetIdx,
            int startIdx,
            int* cameFrom,
            int maxSteps,
            int* pathOutBuffer,
            out int pathLength)
        {
            // Phase 1 : compte la longueur du chemin (sans ecrire)
            int len = 0;
            int cur = targetIdx;
            while (cur != startIdx)
            {
                len++;
                if (len > maxSteps)
                {
                    pathLength = 0;
                    return false;
                }
                cur = cameFrom[cur];
                if (cur == Unreachable)
                {
                    // Securite : ne devrait jamais arriver si A* a trouve le target,
                    // mais on protege contre une cameFrom corrompue.
                    pathLength = 0;
                    return false;
                }
            }

            // Phase 2 : remonte le chemin et l'ecrit dans le bon ordre (start exclu, target inclus)
            cur = targetIdx;
            for (int i = len - 1; i >= 0; i--)
            {
                pathOutBuffer[i] = cur;
                cur = cameFrom[cur];
            }
            pathLength = len;
            return true;
        }
    }
}
