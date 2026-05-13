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
                GridY = y,
                Resource = 0,
                LastResourceGainOnHitTurn = -1,
                OncePerMatchUsedFlags = 0,
                BonusPANextTurn = 0,
                LastAmeLaceeUsedOnTurn = -1000, // pas de cooldown au spawn (Bible : signature jouable des HG=5)
                LastCastOnTurn = -1,             // 2.12.bis : aucun cast au spawn (View detecte diff > 0)
                LastCastSpellId = SpellId.None,  // 2.12.bis
                LastTrapTriggeredOnTurn = -1000, // 2.15.c : aucun trap declenche au spawn (Seve Sauvage)
                LastTraquenardUsedOnTurn = -1000, // 2.16 : aucun cooldown au spawn (Bible V7.1)
            };

            var entity = f.Create();
            f.Add(entity, combatantData);

            // Garantit un etat propre pour les 8 slots de statuses (2.10.a).
            // Important : default(Combatant) suffirait theoriquement, mais on est explicite
            // pour eviter une dependance silencieuse a l'init Quantum des fixed arrays.
            var combatant = f.Unsafe.GetPointer<Combatant>(entity);
            StatusHelper.ClearAll(combatant);

            GridHelpers.SetOccupant(f, x, y, entity);

            return entity;
        }
    }
}
