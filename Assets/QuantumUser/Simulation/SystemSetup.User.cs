namespace Quantum
{
    using System;
    using System.Collections.Generic;

    public static partial class DeterministicSystemSetup
    {
        static partial void AddSystemsUser(ICollection<SystemBase> systems, RuntimeConfig gameConfig, SimulationConfig simulationConfig, SystemsConfig systemsConfig)
        {
            // Phase 2 — Combat
            // Ordre : Grid -> Combatant (SetOccupant lit la grille) -> Turn (reset PA/PM)
            //         -> Movement -> Spell (les deux traitent les commands de player en lecture
            //         seule via GetPlayerCommand, donc l'ordre entre eux n'a pas d'impact).
            systems.Add(new GridSystem());
            systems.Add(new CombatantSystem());
            systems.Add(new TurnSystem());
            systems.Add(new MovementSystem());
            systems.Add(new SpellSystem());
            // 2.14 — process DebugApplyVeilCommand (sera retire en 2.15+ via sorts Nightseer).
            systems.Add(new FogSystem());
        }
    }
}