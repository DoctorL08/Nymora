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
        // POLISH-5e (17 mai) : grille 10x10, spawn sur la diagonale horizontale mediane du
        // losange iso (gx+gy=9). P0 a gauche-bas visuel = (2, 7), P1 a droite-haut visuel =
        // (7, 2). Distance Manhattan 10, distance world horizontal ~5 unites (face-a-face
        // pile dans l'arene). Cale sur les "angles gauche/droit" iso designe par Lorenzo
        // sur la map Map_Combat_1.
        private const int P1SpawnX = 2;
        private const int P1SpawnY = 7;
        private const int P2SpawnX = 7;
        private const int P2SpawnY = 2;

        public override void OnInit(Frame f)
        {
            // 3.6 — P0 = Ghostra (test visuel anims + framework Angle Mort + Permutation).
            // Ghostra demarre avec 0 leurre = Angle 1 (passif neutre). Pour tester l'Angle :
            //   - Touche F12 : pose un leurre Standard sur la case clic (DebugSpawnDecoyCommand)
            //   - Touche P  : Permutation (requiert 3 leurres actifs = Angle 3)
            // SpellBar vide, frappe-au-corps via clic adjacent uniquement (les 16 sorts arrivent en 3.7.a-d).
            // Pour switch local : remplace NymoraClass.Ghostra ci-dessous par .Soulrender / .Nightseer / .Colossar / .Necram.
            SpawnCombatant(f, playerIndex: 0, nymoraClass: NymoraClass.Ghostra, x: P1SpawnX, y: P1SpawnY);
            SpawnCombatant(f, playerIndex: 1, nymoraClass: NymoraClass.Soulrender, x: P2SpawnX, y: P2SpawnY);
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
                RepresaillesReflectsLeft = -1,   // 3.3.a.iii : -1 = pas de cap (defaut). Set explicitement par Represailles (4) / Riposte Carmin (-1).
                StoicismeExpiresOnTurn = -1,     // 3.3.c : inactif au spawn (Stoicisme set a currentTurn+2 au cast).
                HitsTakenThisRound = 0,          // 3.3.c : aucun hit au spawn.
                HitsTakenLastRound = 0,          // 3.3.c : aucun hit precedent au spawn.
                EffondrementAnnouncedOnTurn = -1, // 3.3.d : pas d'annonce active au spawn.
                LastEffondrementUsedOnTurn = -1000, // 3.3.d : signature jouable des FD=3 au spawn.
                EffondrementTargetEntity = EntityRef.None, // 3.3.d : pas de cible snapshot au spawn.
                LastVirusFatalUsedOnTurn = -1000, // 3.5.c.vi : signature jouable des PT=6 au spawn.
                LastPermutationOnTurn = -1000,    // 3.6 : permutation jouable des Angle 3 au spawn.
                LastPasDansLOmbreOnTurn = -1000,  // 3.7.b.ii : Pas dans l'Ombre jouable au spawn (cap 1x/tour).
                LastFacingForcedOnTurn = -1000,   // 3.7.a.iii : pas de direction forcee au spawn (lu par Frappe Fantome).
                LastDagueLanceeOnTurn = -1000,    // 3.7.b.iv : Dague Lancee jouable au spawn (cap 2x/tour).
                DagueLanceeCountThisTurn = 0,     // 3.7.b.iv : 0 cast au spawn.
                LastExecutionSpectraleUsedOnTurn = -1000, // 3.6 : signature Ghostra jouable au spawn (reserve pour 3.7.d).
                // POLISH-5e (17 mai) : nouveau facing post-spawn-diagonal.
                //   P0 spawn (2,7) regarde vers P1 (7,2) : world delta = ((7-2)*0.5, (2-7)*0.25)
                //     = (+2.5, -1.25) -> Sud-Est = IsoFacing.SE.
                //   P1 spawn (7,2) regarde vers P0 (2,7) : world delta = (-2.5, +1.25) -> Nord-Ouest = NW.
                Facing = (playerIndex == 0) ? IsoFacing.SE : IsoFacing.NW,
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
