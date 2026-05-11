namespace Quantum
{
    using Photon.Deterministic;

    // Constantes structurelles de la grille de combat (Bible V7.1).
    // Volontairement hardcodees ici car elles fixent la taille du fixed array
    // dans Grid.qtn (array<Tile>[255]) — une modification implique regeneration
    // du DSL Quantum. Ce n'est PAS de la donnee de gameplay tunable.
    public static class GridConstants
    {
        public const int Width = 15;
        public const int Height = 17;
        public const int Count = Width * Height;
    }

    public static unsafe class GridHelpers
    {
        public static int Index(int x, int y) => y * GridConstants.Width + x;

        public static bool InBounds(int x, int y)
            => x >= 0 && x < GridConstants.Width && y >= 0 && y < GridConstants.Height;

        public static bool IsWalkable(Frame f, int x, int y)
        {
            if (!InBounds(x, y)) return false;
            var grid = f.Unsafe.GetPointerSingleton<GridSingleton>();
            return grid->Tiles[Index(x, y)].Walkable != 0;
        }

        public static EntityRef GetOccupant(Frame f, int x, int y)
        {
            if (!InBounds(x, y)) return EntityRef.None;
            var grid = f.Unsafe.GetPointerSingleton<GridSingleton>();
            return grid->Tiles[Index(x, y)].Occupant;
        }

        public static void SetOccupant(Frame f, int x, int y, EntityRef occupant)
        {
            if (!InBounds(x, y)) return;
            var grid = f.Unsafe.GetPointerSingleton<GridSingleton>();
            grid->Tiles[Index(x, y)].Occupant = occupant;
        }

        public static void SetWalkable(Frame f, int x, int y, bool walkable)
        {
            if (!InBounds(x, y)) return;
            var grid = f.Unsafe.GetPointerSingleton<GridSingleton>();
            grid->Tiles[Index(x, y)].Walkable = (byte)(walkable ? 1 : 0);
        }
    }
}
