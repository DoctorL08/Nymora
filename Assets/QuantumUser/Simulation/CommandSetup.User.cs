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
            // 3.6 — debug spawn leurre Ghostra (touche F12) + Permutation (touche P,
            // Angle 3 only). Le DebugSpawnDecoyCommand sera retire en 3.7.a quand
            // Réplique Fantôme / Pas dans l'Ombre livres ; la PermutationCommand reste
            // (mecanique Bible V7.1 permanente).
            factories.Add(new DebugSpawnDecoyCommand());
            factories.Add(new PermutationCommand());
            // POLISH-4.5 — debug F1-F10 : applique Status / pose Terrain / pose Trap
            // arbitrairement pour tester visuellement marques/tiles/VFX. A retirer
            // en alpha/beta clean-up.
            factories.Add(new DebugApplyStatusCommand());
            factories.Add(new DebugSpawnTerrainCommand());
            factories.Add(new DebugSpawnTrapCommand());
            // POLISH-4.5b — debug Shift+F2/F3 : applique MarkKind Nightseer (Traque /
            // Empreinte) qui vit dans Combatant.CurrentMark (distinct de StatusKind).
            factories.Add(new DebugApplyMarkCommand());
        }
    }
}