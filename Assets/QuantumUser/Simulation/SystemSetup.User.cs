namespace Quantum
{
    using System;
    using System.Collections.Generic;

    public static partial class DeterministicSystemSetup
    {
        static partial void AddSystemsUser(ICollection<SystemBase> systems, RuntimeConfig gameConfig, SimulationConfig simulationConfig, SystemsConfig systemsConfig)
        {
            // Phase 2 — Combat (ordre important : Grid avant Combatant car SetOccupant lit la grille)
            systems.Add(new GridSystem());
            systems.Add(new CombatantSystem());
        }
    }
}