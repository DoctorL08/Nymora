namespace Quantum
{
    using Photon.Deterministic;

    public unsafe class GridSystem : SystemSignalsOnly
    {
        public override void OnInit(Frame f)
        {
            var grid = f.Unsafe.GetOrAddSingletonPointer<GridSingleton>(EntityRef.None);

            // 5.4b — Si la config porte une MAP (forme irrégulière + dims), on l'applique ; sinon on
            //   retombe sur la zone rectangulaire LogicalDims (1v1 10x10 / 2v2 12x12 / 3v3 15x15).
            //   La map est un AssetObject déterministe (AssetRef = GUID synchronisé, chargé localement).
            NymoraCombatMap map = null;
            if (f.RuntimeConfig != null && f.RuntimeConfig.CombatMap.Id.IsValid)
            {
                map = f.FindAsset<NymoraCombatMap>(f.RuntimeConfig.CombatMap.Id);
            }
            bool hasMap = map != null
                && map.Width > 0 && map.Height > 0
                && map.Walkable != null && map.Walkable.Length == GridConstants.Count;

            int playerCount = f.RuntimeConfig != null ? f.RuntimeConfig.PlayerCount : 0;
            int logicalW, logicalH;
            if (hasMap) { logicalW = map.Width; logicalH = map.Height; }
            else GridConstants.LogicalDims(playerCount, out logicalW, out logicalH);

            grid->Width = logicalW;
            grid->Height = logicalH;

            // Tableau dimensionné au MAX (225) ; Walkable = masque irrégulier de la map si présente,
            //   sinon zone rectangulaire LogicalDims. En 1v1 (pas de map) -> 10x10 plein, identique.
            for (int y = 0; y < GridConstants.Height; y++)
            {
                for (int x = 0; x < GridConstants.Width; x++)
                {
                    int idx = GridHelpers.Index(x, y);
                    grid->Tiles[idx].Occupant = EntityRef.None;
                    grid->Tiles[idx].Walkable = hasMap
                        ? map.Walkable[idx]
                        : (byte)((x < logicalW && y < logicalH) ? 1 : 0);
                }
            }
        }
    }
}
