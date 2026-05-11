namespace Quantum
{
    using System;
    using System.Collections.Generic;

    public static partial class DeterministicSystemSetup
    {
        static partial void AddSystemsUser(ICollection<SystemBase> systems, RuntimeConfig gameConfig, SimulationConfig simulationConfig, SystemsConfig systemsConfig)
        {
            // Phase 2 — Combat (ordre important : Grid avant Combatant car SetOccupant lit la grille,
            // TurnSystem apres pour avoir les Combatants deja crees quand on reset leur PA/PM,
            // MovementSystem en dernier pour traiter les commands apres la transition de phase).
            systems.Add(new GridSystem());
            systems.Add(new CombatantSystem());
            systems.Add(new TurnSystem());
            systems.Add(new MovementSystem());
        }
    }
}