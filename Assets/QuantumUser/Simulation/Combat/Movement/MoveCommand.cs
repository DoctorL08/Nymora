namespace Quantum
{
    using Photon.Deterministic;

    /// <summary>
    /// Command Quantum : demande de deplacement du combattant du joueur vers (TargetX, TargetY).
    /// La validation (adjacence, walkable, free, PM) est faite cote simu dans MovementSystem.
    /// </summary>
    public class MoveCommand : DeterministicCommand
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
