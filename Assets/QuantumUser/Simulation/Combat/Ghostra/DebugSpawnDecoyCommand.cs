namespace Quantum
{
    using Photon.Deterministic;

    /// <summary>
    /// 3.6 — Command DEBUG : spawn un leurre Standard a (TargetX, TargetY) pour le
    /// Ghostra actif. Sert a tester le framework Angle Mort + Permutation avant la
    /// livraison des sorts Ghostra (3.7.a Réplique Fantôme / Pas dans l'Ombre etc).
    ///
    /// Validation cote sim (GhostraSystem) :
    ///   - playerIndex sender == ActivePlayerIndex
    ///   - active combatant.Class == Ghostra
    ///   - (TargetX, TargetY) dans la grille
    ///   - case libre (pas occupee par Ghostra elle-meme, pas deja un autre leurre)
    ///   - cap 3 leurres pas atteint
    ///
    /// Sera RETIRE en 3.7.a/b quand les sorts feront ca proprement via SpellSystem.
    /// </summary>
    public class DebugSpawnDecoyCommand : DeterministicCommand
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
