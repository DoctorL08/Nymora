namespace Quantum
{
    using System;
    using System.Collections.Generic;

    public static partial class DeterministicSystemSetup
    {
        static partial void AddSystemsUser(ICollection<SystemBase> systems, RuntimeConfig gameConfig, SimulationConfig simulationConfig, SystemsConfig systemsConfig)
        {
            // Phase 2 — Combat
            // Ordre : Grid -> Combatant (SetOccupant lit la grille) -> AI (set TurnTimerTicks=0
            //         pour le bot AVANT que TurnSystem le decremente) -> Turn (reset PA/PM)
            //         -> Movement -> Spell (les deux traitent les commands de player en lecture
            //         seule via GetPlayerCommand, donc l'ordre entre eux n'a pas d'impact).
            systems.Add(new GridSystem());
            systems.Add(new CombatantSystem());
            // 2.16.a.i — IA : doit tourner AVANT TurnSystem pour que les mutations IA
            // (TurnTimerTicks = 0 dans la brique squelette) soient prises en compte au
            // meme tick par TurnSystem.TickTurnActive.
            systems.Add(new AISystem());
            systems.Add(new TurnSystem());
            systems.Add(new MovementSystem());
            systems.Add(new SpellSystem());
            // 2.14 — process DebugApplyVeilCommand (sera retire en 2.15+ via sorts Nightseer).
            systems.Add(new FogSystem());
            // 3.1 — framework obstacles dynamiques (Pilier/Mur Colossar, plus tard Necram).
            // Init ObstacleSingleton + tick expirations + process des commandes DEBUG
            // (Spawn/Damage). Sera repris par les sorts Pilier/Mur en 3.3.b.
            systems.Add(new ObstacleSystem());
            // 3.4 — process DebugApplyVeninCommand (cheat F11) en attendant Crachat Acide /
            // Inoculation en 3.5.a qui feront ca via SpellSystem.
            systems.Add(new NecramSystem());
        }
    }
}