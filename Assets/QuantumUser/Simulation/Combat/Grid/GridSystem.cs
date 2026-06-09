namespace Quantum
{
    using Photon.Deterministic;

    public unsafe class GridSystem : SystemSignalsOnly
    {
        public override void OnInit(Frame f)
        {
            var grid = f.Unsafe.GetOrAddSingletonPointer<GridSingleton>(EntityRef.None);

            // 5.4 — Dimensions LOGIQUES de la map selon le mode (1v1 10x10 / 2v2 12x12 / 3v3 15x15).
            //   Le tableau Tiles est dimensionné au MAX (225) ; on n'active Walkable que dans la
            //   zone jouable (sous-région rectangulaire). En 1v1 : zone 10x10 -> état identique à
            //   avant. La forme irrégulière fine (carve via masque) viendra du MapAsset en 5.4c.
            int playerCount = f.RuntimeConfig != null ? f.RuntimeConfig.PlayerCount : 0;
            GridConstants.LogicalDims(playerCount, out int logicalW, out int logicalH);
            grid->Width = logicalW;
            grid->Height = logicalH;

            for (int y = 0; y < GridConstants.Height; y++)
            {
                for (int x = 0; x < GridConstants.Width; x++)
                {
                    int idx = GridHelpers.Index(x, y);
                    grid->Tiles[idx].Occupant = EntityRef.None;
                    grid->Tiles[idx].Walkable = (byte)((x < logicalW && y < logicalH) ? 1 : 0);
                }
            }
        }
    }
}
