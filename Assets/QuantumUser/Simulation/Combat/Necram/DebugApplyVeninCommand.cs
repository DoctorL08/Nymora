namespace Quantum
{
    using Photon.Deterministic;

    /// <summary>
    /// 3.4 — Command DEBUG : applique +1 marque venin sur le combatant qui occupe
    /// (TargetX, TargetY). Sert a tester le framework Floraison avant la livraison
    /// des sorts Necram (3.5.a Crachat Acide, Inoculation, Brume Toxique, etc.).
    ///
    /// Validation cote sim (NecramSystem) :
    ///   - playerIndex sender == ActivePlayerIndex (regle classique cast)
    ///   - (TargetX, TargetY) dans la grille
    ///   - un combatant ENNEMI vivant occupe la case (sinon rejet silencieux)
    ///
    /// Sera RETIRE en 3.5.a quand Crachat Acide / Inoculation feront ca proprement
    /// via SpellSystem.
    /// </summary>
    public class DebugApplyVeninCommand : DeterministicCommand
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
