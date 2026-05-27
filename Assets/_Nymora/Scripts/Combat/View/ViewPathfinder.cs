using Quantum;

namespace Nymora.Combat.View
{
    /// <summary>
    /// FIX trajectoire (preview ≠ déplacement réel) — Pathfinder 4-connexité côté View,
    /// SOURCE DE VÉRITÉ UNIQUE partagée par :
    ///   - l'animation cell-by-cell de l'avatar (CombatantRenderer)
    ///   - le preview de trajectoire au survol (MovementRangePreview, path jaune)
    /// pour que la trajectoire case-par-case PRÉVISUALISÉE soit EXACTEMENT celle que l'avatar
    /// parcourt. Avant ce helper, chacun avait sa propre reconstruction (BFS-prédécesseur côté
    /// anim vs descente gloutonne côté preview) → tie-break différents → chemins divergents à
    /// coût égal (cas fréquent sur grille ouverte).
    ///
    /// BFS forward (FIFO) avec prédécesseur. Ordre des voisins {E, O, N, S} FIXE (identique à
    /// AStarPathfinder côté sim) → chemin déterministe et reproductible. Buffers statiques
    /// réutilisés (le combat View tourne sur le main thread, appels non ré-entrants) → zéro
    /// alloc heap par appel.
    ///
    /// 100% View, lecture seule de la Frame. Aucune mutation sim → rien à versionner.
    /// </summary>
    public static class ViewPathfinder
    {
        public const int Width = 15;
        public const int Height = 17;
        public const int Count = Width * Height;

        // Buffers réutilisés (reset au début de chaque appel).
        private static readonly int[] _prev = new int[Count];
        private static readonly bool[] _visited = new bool[Count];
        private static readonly int[] _queue = new int[Count];

        // Ordre des voisins IDENTIQUE à AStarPathfinder (sim) : droite, gauche, haut, bas.
        private static readonly int[] DX = { 1, -1, 0, 0 };
        private static readonly int[] DY = { 0, 0, 1, -1 };

        /// <summary>
        /// Plus court chemin 4-connexité de (sx,sy) inclus à (tx,ty) inclus, écrit dans
        /// <paramref name="outPath"/> (du start au target). Retourne false si pas de chemin
        /// ou si la longueur dépasse <paramref name="outPath"/>.Length.
        /// </summary>
        /// <param name="self">Entité qui se déplace : sa propre case ne bloque pas le chemin.</param>
        /// <param name="ignoreEnemyOccupants">Pas Spectral (Necram) / Pas de l'Au-Delà (Ghostra) :
        /// traverse les combatants (les obstacles Pilier/Mur/Faille restent bloquants).</param>
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

            for (int i = 0; i < Count; i++) { _prev[i] = -1; _visited[i] = false; }

            int startIdx = sy * Width + sx;
            int targetIdx = ty * Width + tx;
            _visited[startIdx] = true;
            int head = 0, tail = 0;
            _queue[tail++] = startIdx;

            bool found = false;
            while (head < tail)
            {
                int idx = _queue[head++];
                if (idx == targetIdx) { found = true; break; }
                int cx = idx % Width;
                int cy = idx / Width;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + DX[d];
                    int ny = cy + DY[d];
                    if (nx < 0 || nx >= Width || ny < 0 || ny >= Height) continue;
                    int nidx = ny * Width + nx;
                    if (_visited[nidx]) continue;
                    // La case cible est traversable même si elle "bloque" (la sim a déjà validé
                    // qu'elle est libre). Les cases intermédiaires checkent obstacle/combatant.
                    if (nidx != targetIdx && IsBlocked(frame, nx, ny, self, ignoreEnemyOccupants)) continue;
                    _visited[nidx] = true;
                    _prev[nidx] = idx;
                    _queue[tail++] = nidx;
                }
            }

            if (!found) return false;

            // Compte la longueur (target inclus) pour valider la capacité du buffer.
            int len = 1;
            int cur = targetIdx;
            while (cur != startIdx)
            {
                cur = _prev[cur];
                if (cur < 0) return false;
                len++;
            }
            if (len > outPath.Length) return false;

            // Remplit du start au target.
            cur = targetIdx;
            for (int i = len - 1; i >= 0; i--)
            {
                outPath[i] = (cur % Width, cur / Width);
                if (i > 0) cur = _prev[cur];
            }
            outLen = len;
            return true;
        }

        /// <summary>
        /// Case bloquante pour le path View : un obstacle actif (Pilier/Mur/Faille HP&gt;0) ou un
        /// combatant vivant autre que <paramref name="self"/> (sauf <paramref name="ignoreEnemyOccupants"/>).
        /// </summary>
        public static bool IsBlocked(Frame frame, int x, int y, EntityRef self, bool ignoreEnemyOccupants = false)
        {
            var obsFilter = frame.Filter<Obstacle>();
            while (obsFilter.Next(out _, out Obstacle obs))
            {
                if (obs.HP <= 0) continue;
                if (obs.GridX == x && obs.GridY == y) return true;
            }
            if (!ignoreEnemyOccupants)
            {
                var combFilter = frame.Filter<Combatant>();
                while (combFilter.Next(out EntityRef ent, out Combatant c))
                {
                    if (ent == self) continue;
                    if (c.HP <= 0) continue;
                    if (c.GridX == x && c.GridY == y) return true;
                }
            }
            return false;
        }
    }
}
