namespace Quantum
{
    using Photon.Deterministic;

    /// <summary>
    /// PATCH 22 mai (test designer, B8) — Command Quantum : le joueur ABANDONNE le combat
    /// (forfait volontaire). Traitee dans TurnSystem.Update : le slot qui l'envoie perd
    /// immediatement, l'autre joueur gagne (1v1). Fonctionne en IA (local) comme en casual (PvP).
    ///
    /// Pas de payload : l'identite du forfaiteur est le slot qui a envoye la command
    /// (f.GetPlayerCommand(slot)).
    /// </summary>
    public class ForfeitCommand : DeterministicCommand
    {
        public override void Serialize(BitStream stream)
        {
            // Pas de payload, c'est juste un signal.
        }
    }
}
