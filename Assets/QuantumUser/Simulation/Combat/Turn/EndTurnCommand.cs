namespace Quantum
{
    using Photon.Deterministic;

    /// <summary>
    /// Command Quantum : le joueur actif demande de finir son tour manuellement.
    ///
    /// 2.13.a : le HUD a un bouton "End Turn" + clic icone signature avec cooldown actif
    /// envoie cette command. Avant ca, le tour ne s'arretait qu'au timer 0.
    ///
    /// Validee cote simu dans TurnSystem.Update : seul ActivePlayerIndex peut declencher
    /// la transition TurnActive -> TurnEnd. Toute autre situation = rejet silencieux.
    /// </summary>
    public class EndTurnCommand : DeterministicCommand
    {
        public override void Serialize(BitStream stream)
        {
            // Pas de payload, c'est juste un signal.
        }
    }
}
