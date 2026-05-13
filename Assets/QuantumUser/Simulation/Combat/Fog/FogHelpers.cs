namespace Quantum
{
    /// <summary>
    /// Helpers brouillard de guerre (Voile sur cases) + pieges (Filet de Ronces, Mine).
    /// Stockage : FogSingleton.Tiles[idx] avec idx = GridHelpers.Index(x, y).
    /// Pattern miroir de GridHelpers (terrains) et StatusHelper (statuses).
    ///
    /// Convention :
    ///   - VeiledByPlayer == 0 : pas de voile.
    ///   - VeiledByPlayer == playerIndex + 1 (donc 1 ou 2 en 1v1).
    ///   - Le voile est INVISIBLE pour les autres joueurs (gameplay), VISIBLE pour le poseur.
    ///   - Decrementation centralisee dans TurnSystem.EnterTurnEnd (skip si applique ce tour).
    ///
    /// Pieges : pas de timer (Bible V7.1 — ils restent jusqu'au declenchement). Si plusieurs
    /// pieges veulent etre poses sur la meme case, on ecrase le precedent (semantique simple
    /// pour 2.14 ; le designer pourra raffiner au cas par cas en 2.15).
    /// </summary>
    public static unsafe class FogHelpers
    {
        // ====================================================================
        // VOILE (fog of war sur case, MARQUE VOILÉ Bible V7.1).
        // ====================================================================

        /// <summary>
        /// Applique un voile sur une case par playerIndex pour `turns` tours.
        /// Si un voile existe deja (meme owner ou autre), il est ecrase.
        /// </summary>
        public static void ApplyVeil(Frame f, int x, int y, int playerIndex, int turns, int currentTurn)
        {
            if (!GridHelpers.InBounds(x, y)) return;
            if (turns <= 0)
            {
                ClearVeil(f, x, y);
                return;
            }
            var fog = f.Unsafe.GetPointerSingleton<FogSingleton>();
            int idx = GridHelpers.Index(x, y);
            fog->Tiles[idx].VeiledByPlayer = (byte)(playerIndex + 1);
            fog->Tiles[idx].VeiledTurnsLeft = turns;
            fog->Tiles[idx].VeiledAppliedOnTurn = currentTurn;
        }

        public static void ClearVeil(Frame f, int x, int y)
        {
            if (!GridHelpers.InBounds(x, y)) return;
            var fog = f.Unsafe.GetPointerSingleton<FogSingleton>();
            int idx = GridHelpers.Index(x, y);
            fog->Tiles[idx].VeiledByPlayer = 0;
            fog->Tiles[idx].VeiledTurnsLeft = 0;
            fog->Tiles[idx].VeiledAppliedOnTurn = 0;
        }

        /// <summary>
        /// PlayerIndex (0..) du joueur qui voit cette case voilee (= proprietaire), ou -1 si
        /// pas de voile actif. Utilise par la View pour decider quel joueur masque la case.
        /// </summary>
        public static int GetVeilOwner(Frame f, int x, int y)
        {
            if (!GridHelpers.InBounds(x, y)) return -1;
            var fog = f.Unsafe.GetPointerSingleton<FogSingleton>();
            var tile = fog->Tiles[GridHelpers.Index(x, y)];
            if (tile.VeiledTurnsLeft <= 0 || tile.VeiledByPlayer == 0) return -1;
            return tile.VeiledByPlayer - 1;
        }

        /// <summary>
        /// True si la case est voilee ET masquee POUR le viewerPlayer (= viewerPlayer != owner).
        /// </summary>
        public static bool IsVeiledFor(Frame f, int x, int y, int viewerPlayer)
        {
            int owner = GetVeilOwner(f, x, y);
            if (owner < 0) return false;
            return owner != viewerPlayer;
        }

        /// <summary>
        /// Appelee a chaque TurnEnd. Pour chaque case voilee :
        ///   - skip si VeiledAppliedOnTurn == currentTurn (posee ce tour)
        ///   - sinon TurnsLeft -= 1, expire si <= 0
        /// </summary>
        public static void DecrementAllVeilsOnTurnEnd(Frame f, int currentTurn)
        {
            var fog = f.Unsafe.GetPointerSingleton<FogSingleton>();
            for (int i = 0; i < GridConstants.Count; i++)
            {
                var t = fog->Tiles[i];
                if (t.VeiledByPlayer == 0) continue;
                if (t.VeiledTurnsLeft <= 0) continue;
                if (t.VeiledAppliedOnTurn == currentTurn) continue;
                t.VeiledTurnsLeft -= 1;
                if (t.VeiledTurnsLeft <= 0)
                {
                    t.VeiledByPlayer = 0;
                    t.VeiledTurnsLeft = 0;
                    t.VeiledAppliedOnTurn = 0;
                }
                fog->Tiles[i] = t;
            }
        }

        // ====================================================================
        // PIEGES (Filet de Ronces, Mine — Nightseer).
        // ====================================================================

        /// <summary>
        /// Pose un piege sur une case. Ecrase tout piege existant (semantique simple 2.14).
        /// </summary>
        public static void PlaceTrap(Frame f, int x, int y, TrapKind kind, int ownerPlayer, int currentTurn)
        {
            if (!GridHelpers.InBounds(x, y)) return;
            if (kind == TrapKind.None)
            {
                ClearTrap(f, x, y);
                return;
            }
            var fog = f.Unsafe.GetPointerSingleton<FogSingleton>();
            int idx = GridHelpers.Index(x, y);
            fog->Tiles[idx].Trap = kind;
            fog->Tiles[idx].TrapOwner = ownerPlayer;
            fog->Tiles[idx].TrapAppliedOnTurn = currentTurn;
        }

        public static void ClearTrap(Frame f, int x, int y)
        {
            if (!GridHelpers.InBounds(x, y)) return;
            var fog = f.Unsafe.GetPointerSingleton<FogSingleton>();
            int idx = GridHelpers.Index(x, y);
            fog->Tiles[idx].Trap = TrapKind.None;
            fog->Tiles[idx].TrapOwner = 0;
            fog->Tiles[idx].TrapAppliedOnTurn = 0;
        }

        public static TrapKind GetTrapKind(Frame f, int x, int y)
        {
            if (!GridHelpers.InBounds(x, y)) return TrapKind.None;
            var fog = f.Unsafe.GetPointerSingleton<FogSingleton>();
            return fog->Tiles[GridHelpers.Index(x, y)].Trap;
        }

        public static int GetTrapOwner(Frame f, int x, int y)
        {
            if (!GridHelpers.InBounds(x, y)) return -1;
            var fog = f.Unsafe.GetPointerSingleton<FogSingleton>();
            var tile = fog->Tiles[GridHelpers.Index(x, y)];
            return tile.Trap == TrapKind.None ? -1 : tile.TrapOwner;
        }

        // ====================================================================
        // 2.15.b — Trigger trap quand un combattant entre sur la case.
        // Appele depuis MovementSystem (mouvement normal) et SpellSystem.PushAndTrigger
        // (Bourrasque, Souffle Glacial). Bible V7.1 :
        //   - FiletRonces : 100 dgts + -2 PM + Empreinte 2 tours
        //   - Mine        : 70 dgts + Empreinte 2 tours
        //   - Trap consomme apres declenchement (Clear)
        //   - Voile lie a la case aussi clear (le piege n'est plus secret)
        //   - Gain +1 PR au owner du trap (passif L'Œil qui n'est pas : declenchement de marque)
        // ====================================================================

        public static bool TryTriggerTrapOnEnter(Frame f, EntityRef enterer, Combatant* entererC,
            int x, int y, int currentTurn)
        {
            TrapKind trap = GetTrapKind(f, x, y);
            if (trap == TrapKind.None) return false;
            int trapOwner = GetTrapOwner(f, x, y);
            if (trapOwner < 0) return false;
            if (trapOwner == entererC->PlayerIndex) return false; // pas son propre trap

            int dmg = trap == TrapKind.FiletRonces
                ? Quantum.SpellRegistry.FiletDeRoncesDmg
                : Quantum.SpellRegistry.ChampDeMinesDmg;

            int hpBefore = entererC->HP;
            entererC->HP -= dmg;
            if (entererC->HP < 0) entererC->HP = 0;
            entererC->DamageTakenThisRound += dmg;
            Log.Info($"[Trap] {trap} declenche sur P{entererC->PlayerIndex} ({x},{y}) : -{dmg} HP ({hpBefore} -> {entererC->HP})");

            // Empreinte 2 tours.
            int empreinteTurns = trap == TrapKind.FiletRonces
                ? Quantum.SpellRegistry.FiletDeRoncesEmpreinteTurns
                : Quantum.SpellRegistry.ChampDeMinesEmpreinteTurns;
            MarkHelpers.ApplyMark(entererC, MarkKind.Empreinte, empreinteTurns, trapOwner, currentTurn);

            // -2 PM si Filet de Ronces (MovementMalus 1 tour).
            if (trap == TrapKind.FiletRonces)
            {
                StatusHelper.Apply(entererC, StatusKind.MovementMalus,
                    magnitude: Quantum.SpellRegistry.FiletDeRoncesPMReduce, turnsLeft: 1, currentTurn);
            }

            // Consume trap + voile lie.
            ClearTrap(f, x, y);
            ClearVeil(f, x, y);

            // Gain +1 PR au owner si Nightseer (Bible : declenchement de marque) +
            // tracking LastTrapTriggeredOnTurn (Seve Sauvage bonus heal).
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                if (c->PlayerIndex != trapOwner) continue;
                c->LastTrapTriggeredOnTurn = currentTurn; // 2.15.c — tous casters (pas que Nightseer)
                if (c->Class != NymoraClass.Nightseer)
                {
                    break;
                }
                int maxRes = Quantum.CombatantStats.GetMaxResource(c->Class);
                int beforeRes = c->Resource;
                c->Resource = beforeRes + 1 > maxRes ? maxRes : beforeRes + 1;
                if (c->Resource != beforeRes)
                {
                    Log.Info($"[Prescience] +1 PR sur P{c->PlayerIndex} (Trap {trap} declenche) : {beforeRes} -> {c->Resource}");
                }
                break;
            }
            return true;
        }
    }
}
