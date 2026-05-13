namespace Quantum
{
    using System.Collections.Generic;
    using Photon.Deterministic;

    public static partial class DeterministicCommandSetup
    {
        static partial void AddCommandFactoriesUser(ICollection<IDeterministicCommandFactory> factories, RuntimeConfig gameConfig, SimulationConfig simulationConfig)
        {
            // Phase 2 — Combat
            factories.Add(new MoveCommand());
            factories.Add(new CastSpellCommand());
            factories.Add(new EndTurnCommand());
            // 2.14 — debug fog (sera retire en 2.15+ quand sorts Nightseer livres).
            factories.Add(new DebugApplyVeilCommand());
        }
    }
}