using System;

namespace Nymora.Core.Data
{
    /// <summary>
    /// Position discrete sur la grille de combat (15x17 cases).
    /// Immuable. Coordonnees entieres uniquement (pas de float — compatible Quantum determinisme).
    /// </summary>
    public readonly struct Position2D : IEquatable<Position2D>
    {
        public readonly int X;
        public readonly int Y;

        public Position2D(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static Position2D Zero => new Position2D(0, 0);
        public static Position2D Up => new Position2D(0, 1);
        public static Position2D Down => new Position2D(0, -1);
        public static Position2D Left => new Position2D(-1, 0);
        public static Position2D Right => new Position2D(1, 0);

        /// <summary>
        /// Distance de Manhattan (somme des differences absolues).
        /// Utilisee pour les portees de sorts et le pathfinding sur grille carree.
        /// </summary>
        public int ManhattanDistance(Position2D other)
        {
            int dx = X - other.X;
            int dy = Y - other.Y;
            if (dx < 0) dx = -dx;
            if (dy < 0) dy = -dy;
            return dx + dy;
        }

        public static Position2D operator +(Position2D a, Position2D b) => new Position2D(a.X + b.X, a.Y + b.Y);
        public static Position2D operator -(Position2D a, Position2D b) => new Position2D(a.X - b.X, a.Y - b.Y);
        public static bool operator ==(Position2D a, Position2D b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(Position2D a, Position2D b) => !(a == b);

        public bool Equals(Position2D other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Position2D p && Equals(p);
        public override int GetHashCode() => unchecked(X * 397 ^ Y);
        public override string ToString() => $"({X}, {Y})";
    }
}
