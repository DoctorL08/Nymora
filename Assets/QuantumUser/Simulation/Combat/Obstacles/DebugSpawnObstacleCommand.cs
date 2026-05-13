namespace Quantum
{
    using Photon.Deterministic;

    /// <summary>
    /// 3.1 — Command DEBUG : spawn un Pilier (200 HP, persistent) sur la case (TargetX,TargetY).
    /// Touche P en View (CombatInputController). Process par ObstacleSystem.
    ///
    /// Sera retiree en 3.3.b quand le sort PILIER Colossar prendra le relais via SpellSystem.
    /// </summary>
    public class DebugSpawnObstacleCommand : DeterministicCommand
    {
        public int TargetX;
        public int TargetY;

        public override void Serialize(BitStream stream)
        {
            stream.Serialize(ref TargetX);
            stream.Serialize(ref TargetY);
        }
    }
}
