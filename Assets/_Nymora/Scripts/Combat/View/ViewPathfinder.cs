using Quantum;

namespace Nymora.Combat.View
{
    /// <summary>
    /// FIX trajectoire (preview ≠ déplacement réel ≠ logique) — Pathfinder 4-connexité côté
    /// View, SOURCE DE VÉRITÉ UNIQUE partagée par :
    ///   - l'animation cell-by-cell de l'avatar (CombatantRenderer)
    ///   - le preview de trajectoire au survol (MovementRangePreview, path jaune)
    /// pour que la trajectoire case-par-case PRÉVISUALISÉE / ANIMÉE soit EXACTEMENT celle que la
    /// simulation a empruntée.
    ///
    /// Refonte 30 mai — l'ancien moteur était un BFS forward FIFO. À coût égal (chemins en L sur
    /// grille ouverte) le BFS et l'A* de la sim (AStarPathfinder) choisissaient des branches
    /// DIFFÉRENTES : l'avatar marchait visuellement sur une case (ex. un piège) que l'A* sim
    /// n'avait pas foulée → le piège ne se déclenchait pas (« il marche pile dessus, rien »).
    ///
    /// Désormais ce helper exécute le MÊME A* que la sim :
    ///   - même heuristique Manhattan, même tie-break (index grille croissant),
    ///   - même ordre des voisins {E, O, N, S},
    ///   - mêmes prédicats de blocage (GridHelpers.IsWalkable / GetOccupant,
    ///     ObstacleHelpers.HasObstacleAt, DecoyHelpers.HasAnyDecoyAt) lus sur la MÊME Frame.
    /// → le chemin trouvé est bit-identique à celui de AStarPathfinder, donc ce que le joueur
    ///   VOIT correspond exactement à ce que la logique déclenche.
    ///
    /// Buffers statiques réutilisés (le combat View tourne sur le main thread, appels non
    /// ré-entrants) → zéro alloc heap par appel. 100% View, lecture seule de la Frame.
    /// </summary>
    public static class ViewPathfinder
    {
        public const int Width = 15;
        public const int Height = 17;
        public const int Count = Width * Height;

        private const int Unreachable = -1;
        private const int Infinity = int.MaxValue;

        // Buffers réutilisés (reset au début de chaque appel) — miroir des stackalloc de la sim.
        private static readonly int[] _gScore = new int[Count];
        private static readonly int[] _cameFrom = new int[Count];
        private static readonly bool[] _closed = new bool[Count];
        private static readonly int[] _openSet = new int[Count];

        // Ordre des voisins IDENTIQUE à AStarPathfinder (sim) : droite, gauche, haut, bas.
        private static readonly int[] DX = { 1, -1, 0, 0 };
        private static readonly int[] DY = { 0, 0, 1, -1 };

        /// <summary>
        /// Plus court chemin 4-connexité de (sx,sy) inclus à (tx,ty) inclus, écrit dans
        /// <paramref name="outPath"/> (du start au target). Retourne false si pas de chemin
        /// ou si la longueur dépasse <paramref name="outPath"/>.Length.
        /// Identique (mêmes cases, même ordre) au chemin que AStarPathfinder calcule côté sim.
        /// </summary>
        /// <param name="self">Entité qui se déplace : sa propre case ne bloque pas le chemin.</param>
        /// <param name="ignoreEnemyOccupants">Pas Spectral (Necram) / Pas de l'Au-Delà (Ghostra) :
        /// traverse les combatants ET les leurres (les obstacles Pilier/Mur/Faille restent bloquants).</param>
        public static bool TryFindPath(
            Frame frame,
            int sx, int sy, int tx, int ty,
            EntityRef self,
            System.Span<(int x, int y)> outPath,
            out int outLen,
            bool ignoreEnemyOccupants = false)
        {
            outLen = 0;
            if (sx < 0 || sx >= Width || sy < 0 || sy >= Height) return false;
            if (tx < 0 || tx >= Width || ty < 0 || ty >= Height) return false;
            if (sx == tx && sy == ty) return false;

            int startIdx = sy * Width + sx;
            int targetIdx = ty * Width + tx;

            for (int i = 0; i < Count; i++)
            {
                _gScore[i] = Infinity;
                _cameFrom[i] = Unreachable;
                _closed[i] = false;
            }

            _gScore[startIdx] = 0;
            _openSet[0] = startIdx;
            int openCount = 1;

            bool found = false;
            while (openCount > 0)
            {
                // Sélection du meilleur fScore (tie-break index croissant) — identique à la sim.
                int bestPos = 0;
                int bestIdx = _openSet[0];
                int bestF = _gScore[bestIdx] + Manhattan(bestIdx, tx, ty);
                for (int i = 1; i < openCount; i++)
                {
                    int idx = _openSet[i];
                    int fScore = _gScore[idx] + Manhattan(idx, tx, ty);
                    if (fScore < bestF || (fScore == bestF && idx < bestIdx))
                    {
                        bestF = fScore;
                        bestIdx = idx;
                        bestPos = i;
                    }
                }

                int currentIdx = bestIdx;
                if (currentIdx == targetIdx) { found = true; break; }

                _openSet[bestPos] = _openSet[openCount - 1];
                openCount--;
                _closed[currentIdx] = true;

                int cx = currentIdx % Width;
                int cy = currentIdx / Width;

                for (int dir = 0; dir < 4; dir++)
                {
                    int nx = cx + DX[dir];
                    int ny = cy + DY[dir];
                    if (nx < 0 || nx >= Width || ny < 0 || ny >= Height) continue;

                    int nIdx = ny * Width + nx;
                    if (_closed[nIdx]) continue;
                    if (!GridHelpers.IsWalkable(frame, nx, ny)) continue;

                    // Occupant bloque (sauf start/target, ou Pas Spectral / Pas de l'Au-Delà).
                    if (!ignoreEnemyOccupants && nIdx != startIdx && nIdx != targetIdx
                        && GridHelpers.GetOccupant(frame, nx, ny) != EntityRef.None) continue;
                    // Obstacle Pilier/Mur/Faille bloque toujours.
                    if (ObstacleHelpers.HasObstacleAt(frame, nx, ny)) continue;
                    // Leurre Ghostra bloque (sauf traversée Pas Spectral / Pas de l'Au-Delà).
                    if (!ignoreEnemyOccupants && DecoyHelpers.HasAnyDecoyAt(frame, nx, ny)) continue;

                    int tentativeG = _gScore[currentIdx] + 1;
                    if (tentativeG >= _gScore[nIdx]) continue;

                    _gScore[nIdx] = tentativeG;
                    _cameFrom[nIdx] = currentIdx;

                    bool alreadyInOpen = false;
                    for (int j = 0; j < openCount; j++)
                    {
                        if (_openSet[j] == nIdx) { alreadyInOpen = true; break; }
                    }
                    if (!alreadyInOpen && openCount < Count)
                    {
                        _openSet[openCount++] = nIdx;
                    }
                }
            }

            if (!found) return false;

            // Longueur du chemin, START INCLUS (convention View, à l'inverse de la sim qui exclut
            // le start — mais les cases intermédiaires sont strictement les mêmes).
            int len = 1;
            int cur = targetIdx;
            while (cur != startIdx)
            {
                cur = _cameFrom[cur];
                if (cur < 0) return false;
                len++;
            }
            if (len > outPath.Length) return false;

            cur = targetIdx;
            for (int i = len - 1; i >= 0; i--)
            {
                outPath[i] = (cur % Width, cur / Width);
                if (i > 0) cur = _cameFrom[cur];
            }
            outLen = len;
            return true;
        }

        private static int Manhattan(int idx, int targetX, int targetY)
        {
            int x = idx % Width;
            int y = idx / Width;
            int dx = x - targetX; if (dx < 0) dx = -dx;
            int dy = y - targetY; if (dy < 0) dy = -dy;
            return dx + dy;
        }

        /// <summary>
        /// Case bloquante pour le path View — conservée pour compat (MovementRangePreview l'appelle
        /// encore pour le grisé de portée). Miroir des prédicats de la sim.
        /// </summary>
        public static bool IsBlocked(Frame frame, int x, int y, EntityRef self, bool ignoreEnemyOccupants = false)
        {
            if (ObstacleHelpers.HasObstacleAt(frame, x, y)) return true;
            if (!ignoreEnemyOccupants)
            {
                EntityRef occ = GridHelpers.GetOccupant(frame, x, y);
                if (occ != EntityRef.None && occ != self) return true;
                if (DecoyHelpers.HasAnyDecoyAt(frame, x, y)) return true;
            }
            return false;
        }
    }
}
