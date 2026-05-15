using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 4.3.b — A* tile-based 4-connexite (haut/bas/gauche/droite), heuristique Manhattan.
    /// Cote View pur (float-free dans la logique, juste int + cost int) — pas pour la simulation Quantum.
    /// 400 nodes max (20x20) -> open list O(n) trivialement perf-acceptable.
    /// </summary>
    public static class HubPathfinder
    {
        /// <summary>
        /// Retourne un chemin de (sx,sy) vers (tx,ty) exclus de la case de depart, inclus la cible.
        /// Null si pas de chemin, ou si (tx,ty) hors grille / non walkable / identique a start.
        /// isWalkable est appelee pour chaque case candidate ; (sx,sy) et (tx,ty) testes aussi.
        /// </summary>
        public static List<(int gx, int gy)> FindPath(
            int sx, int sy, int tx, int ty,
            int width, int height,
            Func<int, int, bool> isWalkable)
        {
            if (sx == tx && sy == ty) return null;
            if (!InBounds(tx, ty, width, height)) return null;
            if (isWalkable != null && !isWalkable(tx, ty)) return null;

            var open = new List<Node>();
            var allNodes = new Dictionary<int, Node>(64);
            int startKey = Key(sx, sy, width);
            var startNode = new Node { X = sx, Y = sy, G = 0, H = Manhattan(sx, sy, tx, ty), ParentKey = -1 };
            startNode.F = startNode.G + startNode.H;
            open.Add(startNode);
            allNodes[startKey] = startNode;

            var closed = new HashSet<int>();

            while (open.Count > 0)
            {
                // Pop min F
                int bestIdx = 0;
                for (int i = 1; i < open.Count; i++)
                {
                    if (open[i].F < open[bestIdx].F) bestIdx = i;
                }
                Node current = open[bestIdx];
                open.RemoveAt(bestIdx);

                int curKey = Key(current.X, current.Y, width);
                if (closed.Contains(curKey)) continue;
                closed.Add(curKey);

                if (current.X == tx && current.Y == ty)
                {
                    return Reconstruct(allNodes, current, width);
                }

                // 4-connexite
                TryNeighbor(current.X + 1, current.Y, current, tx, ty, width, height, isWalkable, open, closed, allNodes);
                TryNeighbor(current.X - 1, current.Y, current, tx, ty, width, height, isWalkable, open, closed, allNodes);
                TryNeighbor(current.X, current.Y + 1, current, tx, ty, width, height, isWalkable, open, closed, allNodes);
                TryNeighbor(current.X, current.Y - 1, current, tx, ty, width, height, isWalkable, open, closed, allNodes);
            }

            return null;
        }

        private static void TryNeighbor(
            int nx, int ny, Node parent, int tx, int ty,
            int width, int height,
            Func<int, int, bool> isWalkable,
            List<Node> open, HashSet<int> closed, Dictionary<int, Node> allNodes)
        {
            if (!InBounds(nx, ny, width, height)) return;
            int nKey = Key(nx, ny, width);
            if (closed.Contains(nKey)) return;
            if (isWalkable != null && !isWalkable(nx, ny)) return;

            int tentativeG = parent.G + 1;
            if (allNodes.TryGetValue(nKey, out Node existing))
            {
                if (tentativeG >= existing.G) return;
                existing.G = tentativeG;
                existing.F = tentativeG + existing.H;
                existing.ParentKey = Key(parent.X, parent.Y, width);
                allNodes[nKey] = existing;
                open.Add(existing);
                return;
            }

            var node = new Node
            {
                X = nx, Y = ny,
                G = tentativeG,
                H = Manhattan(nx, ny, tx, ty),
                ParentKey = Key(parent.X, parent.Y, width),
            };
            node.F = node.G + node.H;
            allNodes[nKey] = node;
            open.Add(node);
        }

        private static List<(int, int)> Reconstruct(Dictionary<int, Node> allNodes, Node end, int width)
        {
            var path = new List<(int, int)>();
            Node cur = end;
            while (cur.ParentKey != -1)
            {
                path.Add((cur.X, cur.Y));
                if (!allNodes.TryGetValue(cur.ParentKey, out cur)) break;
            }
            path.Reverse();
            return path;
        }

        private static bool InBounds(int x, int y, int w, int h) => x >= 0 && y >= 0 && x < w && y < h;

        private static int Manhattan(int ax, int ay, int bx, int by) => Mathf.Abs(ax - bx) + Mathf.Abs(ay - by);

        private static int Key(int x, int y, int width) => y * width + x;

        private struct Node
        {
            public int X, Y;
            public int G, H, F;
            public int ParentKey; // -1 = start
        }
    }
}
