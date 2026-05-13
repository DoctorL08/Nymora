namespace Quantum
{
    using Photon.Deterministic;

    /// <summary>
    /// 3.1 — Command DEBUG : inflige 50 dmg a l'obstacle sur la case (TargetX,TargetY).
    /// Touche U en View (CombatInputController). Process par ObstacleSystem.
    ///
    /// Permet de tester en Play Mode le destroy par HP=0 (4 hits sur Pilier 200 HP).
    /// Sera retiree en 3.3.b — les sorts feront ca via le SpellSystem (Choc Sismique
    /// peut traverser un Pilier, Brisure peut le briser, etc.).
    /// </summary>
    public class DebugDamageObstacleCommand : DeterministicCommand
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
