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
            // 3.1 — debug obstacles (touches P/U). Sera retire en 3.3.b quand sorts
            // Pilier/Mur Colossar livres.
            factories.Add(new DebugSpawnObstacleCommand());
            factories.Add(new DebugDamageObstacleCommand());
            // 3.4 — debug marque venin (touche F11). Sera retire en 3.5.a quand
            // Crachat Acide / Inoculation livres.
            factories.Add(new DebugApplyVeninCommand());
        }
    }
}