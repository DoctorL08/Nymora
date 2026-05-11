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

        // ====================================================================
        // Terrains 2.10.c (Vapeur Carmin, Sang Coagule).
        // ====================================================================

        public static TerrainKind GetTerrainKind(Frame f, int x, int y)
        {
            if (!InBounds(x, y)) return TerrainKind.None;
            var grid = f.Unsafe.GetPointerSingleton<GridSingleton>();
            var tile = grid->Tiles[Index(x, y)];
            if (tile.TerrainTurnsLeft <= 0) return TerrainKind.None;
            return tile.Terrain;
        }

        public static int GetTerrainTurnsLeft(Frame f, int x, int y)
        {
            if (!InBounds(x, y)) return 0;
            var grid = f.Unsafe.GetPointerSingleton<GridSingleton>();
            return grid->Tiles[Index(x, y)].TerrainTurnsLeft;
        }

        /// <summary>
        /// Pose un terrain sur une case. Si un terrain est deja present, il est ecrase
        /// (refresh duree). AppliedOnTurn est utilise pour la regle skip-decrement
        /// (le tour ou le terrain a ete pose ne compte pas dans la decrementation).
        /// </summary>
        public static void SetTerrain(Frame f, int x, int y, TerrainKind kind, int turnsLeft, int currentTurn)
        {
            if (!InBounds(x, y)) return;
            if (kind == TerrainKind.None || turnsLeft <= 0)
            {
                ClearTerrain(f, x, y);
                return;
            }
            var grid = f.Unsafe.GetPointerSingleton<GridSingleton>();
            grid->Tiles[Index(x, y)].Terrain = kind;
            grid->Tiles[Index(x, y)].TerrainTurnsLeft = turnsLeft;
            grid->Tiles[Index(x, y)].TerrainAppliedOnTurn = currentTurn;
        }

        public static void ClearTerrain(Frame f, int x, int y)
        {
            if (!InBounds(x, y)) return;
            var grid = f.Unsafe.GetPointerSingleton<GridSingleton>();
            grid->Tiles[Index(x, y)].Terrain = TerrainKind.None;
            grid->Tiles[Index(x, y)].TerrainTurnsLeft = 0;
            grid->Tiles[Index(x, y)].TerrainAppliedOnTurn = 0;
        }

        /// <summary>
        /// Decrementation appelee a chaque TurnEnd. Pour chaque tile avec un terrain :
        ///   - skip si AppliedOnTurn == currentTurn (pose ce tour)
        ///   - sinon TurnsLeft -= 1, expire si <= 0
        /// </summary>
        public static void DecrementAllTerrainsOnTurnEnd(Frame f, int currentTurn)
        {
            var grid = f.Unsafe.GetPointerSingleton<GridSingleton>();
            for (int i = 0; i < GridConstants.Count; i++)
            {
                var t = grid->Tiles[i];
                if (t.Terrain == TerrainKind.None) continue;
                if (t.TerrainTurnsLeft <= 0) continue;
                if (t.TerrainAppliedOnTurn == currentTurn) continue;
                t.TerrainTurnsLeft -= 1;
                if (t.TerrainTurnsLeft <= 0)
                {
                    t.Terrain = TerrainKind.None;
                    t.TerrainTurnsLeft = 0;
                    t.TerrainAppliedOnTurn = 0;
                }
                grid->Tiles[i] = t;
            }
        }
    }
}
