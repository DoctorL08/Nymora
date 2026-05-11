namespace Quantum
{
    using Photon.Deterministic;

    /// <summary>
    /// Spawn les 2 combattants initiaux au demarrage du combat (brique 2.2).
    /// Positions de spawn hardcodees en 2.2 (P1 a gauche, P2 a droite, symetrique).
    /// Sera remplace par un RuntimePlayer + SpawnConfig en Phase 6 (matchmaking).
    /// </summary>
    public unsafe class CombatantSystem : SystemSignalsOnly
    {
        // Spawn par defaut en 2.2 : 2 classes Phase 2 face a face sur la ligne centrale.
        private const int P1SpawnX = 3;
        private const int P1SpawnY = 8;
        private const int P2SpawnX = 11;
        private const int P2SpawnY = 8;

        public override void OnInit(Frame f)
        {
            SpawnCombatant(f, playerIndex: 0, nymoraClass: NymoraClass.Soulrender, x: P1SpawnX, y: P1SpawnY);
            SpawnCombatant(f, playerIndex: 1, nymoraClass: NymoraClass.Nightseer, x: P2SpawnX, y: P2SpawnY);
        }

        private static EntityRef SpawnCombatant(Frame f, int playerIndex, NymoraClass nymoraClass, int x, int y)
        {
            int maxHP = CombatantStats.GetMaxHP(nymoraClass);
            int maxPA = CombatantStats.GetMaxPA(nymoraClass);
            int maxPM = CombatantStats.GetMaxPM(nymoraClass);

            var combatantData = new Combatant
            {
                PlayerIndex = playerIndex,
                Class = nymoraClass,
                MaxHP = maxHP,
                HP = maxHP,
                MaxPA = maxPA,
                PA = maxPA,
                MaxPM = maxPM,
                PM = maxPM,
                GridX = x,
                GridY = y
            };

            var entity = f.Create();
            f.Add(entity, combatantData);

            GridHelpers.SetOccupant(f, x, y, entity);

            return entity;
        }
    }
}
