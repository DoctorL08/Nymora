namespace Quantum
{
    using Photon.Deterministic;

    /// <summary>
    /// 3.6 — Command Permutation Ghostra (Bible V7.1 Angle 3) : swap PosX/Y entre
    /// la Ghostra et un de ses leurres. 0 PA, 1x par tour, requiert Angle 3 (3 leurres
    /// actifs). INVISIBLE cote adversaire (sprites identiques).
    ///
    /// 3.7.a.i.4 — TargetX/Y : la case visée à la souris au moment de l'appui touche P.
    /// Le sim cherche un leurre de la Ghostra à cette case et l'utilise pour le swap.
    /// Si pas de leurre à la case visée -> rejet avec log explicite (le joueur doit
    /// recliquer sur un leurre précis).
    ///
    /// Validation cote sim (GhostraSystem) :
    ///   - playerIndex sender == ActivePlayerIndex
    ///   - active combatant.Class == Ghostra
    ///   - DecoyHelpers.CountActive(ghostra) == 3 (Angle 3 Bible-strict)
    ///   - LastPermutationOnTurn != currentTurn (cap 1x/tour)
    ///   - Decoys[i] à (TargetX, TargetY) trouvé
    /// </summary>
    public class PermutationCommand : DeterministicCommand
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
