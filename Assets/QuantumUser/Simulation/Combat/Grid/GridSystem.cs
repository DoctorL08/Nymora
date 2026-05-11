namespace Quantum
{
    using Photon.Deterministic;

    public unsafe class GridSystem : SystemSignalsOnly
    {
        public override void OnInit(Frame f)
        {
            var grid = f.Unsafe.GetOrAddSingletonPointer<GridSingleton>(EntityRef.None);
            grid->Width = GridConstants.Width;
            grid->Height = GridConstants.Height;

            for (int i = 0; i < GridConstants.Count; i++)
            {
                grid->Tiles[i].Walkable = 1;
                grid->Tiles[i].Occupant = EntityRef.None;
            }
        }
    }
}
