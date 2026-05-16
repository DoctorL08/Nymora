namespace Quantum
{
    using Photon.Deterministic;

    /// <summary>
    /// 3.6 — Command Permutation Ghostra (Bible V7.1 Angle 3) : swap PosX/Y entre
    /// la Ghostra et un de ses leurres. 0 PA, 1x par tour, requiert Angle 3 (3 leurres
    /// actifs). INVISIBLE cote adversaire (sprites identiques).
    ///
    /// SlotIndex :
    ///   - -1 : auto-pick le premier slot non-None (UX par defaut, touche P)
    ///   - 0..2 : slot specifique (futur UI selection)
    ///
    /// Validation cote sim (GhostraSystem) :
    ///   - playerIndex sender == ActivePlayerIndex
    ///   - active combatant.Class == Ghostra
    ///   - DecoyHelpers.CountActive(ghostra) == 3 (Angle 3 Bible-strict)
    ///   - LastPermutationOnTurn != currentTurn (cap 1x/tour)
    /// </summary>
    public class PermutationCommand : DeterministicCommand
    {
        public int SlotIndex;

        public override void Serialize(BitStream stream)
        {
            stream.Serialize(ref SlotIndex);
        }
    }
}
