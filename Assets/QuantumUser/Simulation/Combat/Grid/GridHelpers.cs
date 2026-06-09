namespace Quantum
{
    using Photon.Deterministic;

    // Constantes structurelles de la grille de combat (Bible V7.1).
    // Volontairement hardcodees ici car elles fixent la taille du fixed array
    // dans Grid.qtn / Fog.qtn / Obstacle.qtn (array<...>[Count]) — toute modification
    // implique regeneration du DSL Quantum + bump CombatRulesVersion.
    //
    // POLISH-5e (17 mai) : 15x17=255 -> 10x10=100 pour caler l'arene losange.
    // 5.4 (2v2/3v3) : Width/Height = dimension MAX = 15x15 (225) pour contenir la plus grande
    //   map (3v3). C'est AUSSI le STRIDE d'index (Index = y*Width + x) et la taille du fixed array.
    //   Les modes plus petits (1v1 10x10, 2v2 12x12) sont des SOUS-RÉGIONS logiques de ce tableau ;
    //   leur forme (rectangulaire par défaut, irrégulière via MapAsset en 5.4c) est portée par le
    //   masque Walkable. Les dimensions LOGIQUES par map vivent dans GridSingleton.Width/Height
    //   (la View se centre dessus -> le 1v1 reste rendu en 10x10, identique).
    public static class GridConstants
    {
        public const int Width = 15;  // MAX = stride d'index = taille fixe du tableau
        public const int Height = 15;
        public const int Count = Width * Height; // 225 (< 255, sous la limite Quantum)

        // 5.4 — Zone jouable LOGIQUE (rectangle de base) selon le nombre de joueurs du combat.
        //   1v1 -> 10x10 (inchangé), 2v2 -> 12x12, 3v3 -> 15x15. Posée dans GridSingleton.Width/Height
        //   par GridSystem.OnInit. Le MapAsset (5.4c) viendra carve la forme irrégulière par-dessus.
        public static void LogicalDims(int playerCount, out int width, out int height)
        {
            if (playerCount >= 6)      { width = 15; height = 15; } // 3v3
            else if (playerCount >= 4) { width = 12; height = 12; } // 2v2
            else                       { width = 10; height = 10; } // 1v1 (défaut)
        }
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
        public static void SetTerrain(Frame f, int x, int y, TerrainKind kind, int turnsLeft, int currentTurn, int ownerPlayerIndex = -1)
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
            // #23 — owner du terrain (affichage outline d'equipe en miroir), stocke dans FogSingleton.
            //   Pure data View ; la sim ne le lit jamais. -1 = aucun owner (debug / calls non migres).
            FogHelpers.SetTerrainOwner(f, x, y, ownerPlayerIndex);
        }

        public static void ClearTerrain(Frame f, int x, int y)
        {
            if (!InBounds(x, y)) return;
            var grid = f.Unsafe.GetPointerSingleton<GridSingleton>();
            grid->Tiles[Index(x, y)].Terrain = TerrainKind.None;
            grid->Tiles[Index(x, y)].TerrainTurnsLeft = 0;
            grid->Tiles[Index(x, y)].TerrainAppliedOnTurn = 0;
            FogHelpers.SetTerrainOwner(f, x, y, -1); // #23 — clear owner
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
