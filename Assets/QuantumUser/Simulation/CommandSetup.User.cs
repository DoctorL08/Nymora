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
        }
    }
}