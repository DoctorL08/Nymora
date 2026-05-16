namespace Quantum
{
    /// <summary>
    /// 3.6 — Systeme Ghostra : parse les commands DEBUG/Permutation cote sim.
    ///
    /// Aujourd'hui :
    ///   - <see cref="DebugSpawnDecoyCommand"/> (cheat F12) : spawn leurre Standard
    ///     pour tester l'Angle Mort sans avoir Réplique Fantôme livre.
    ///   - <see cref="PermutationCommand"/> (touche P, Angle 3 only) : swap Ghostra<->leurre.
    ///
    /// 3.7+ — recevra les hooks specifiques Ghostra qui ne tiennent pas dans SpellSystem
    /// (e.g. heal owner a la destruction d'un leurre, redirection 40% dmg Réplique
    /// Protectrice, sort cible sur leurre = consume + passe a travers, etc).
    ///
    /// Pas de tick scheduled : le tick lifetime des leurres passe par GhostraPassif
    /// via TurnSystem.EnterTurnStart (calque NecramSystem qui delegue a NecramPassif).
    /// </summary>
    public unsafe class GhostraSystem : SystemMainThread
    {
        public override void Update(Frame f)
        {
            if (!f.TryGetSingleton<CombatState>(out var state)) return;
            if (state.CurrentPhase != CombatPhase.TurnActive) return;

            for (int playerIndex = 0; playerIndex < TurnConstants.PlayerCount; playerIndex++)
            {
                var cmd = f.GetPlayerCommand(playerIndex);
                if (cmd is DebugSpawnDecoyCommand spawnCmd)
                {
                    HandleDebugSpawnDecoy(f, playerIndex, spawnCmd, state.ActivePlayerIndex, state.TurnNumber);
                }
                else if (cmd is PermutationCommand permCmd)
                {
                    HandlePermutation(f, playerIndex, permCmd, state.ActivePlayerIndex, state.TurnNumber);
                }
            }
        }

        private static void HandleDebugSpawnDecoy(Frame f, int playerIndex, DebugSpawnDecoyCommand cmd, int activePlayer, int currentTurn)
        {
            if (playerIndex != activePlayer)
            {
                Log.Warn($"[Ghostra] DEBUG SpawnDecoy rejete : pas le tour de P{playerIndex}");
                return;
            }
            if (!GridHelpers.InBounds(cmd.TargetX, cmd.TargetY))
            {
                Log.Warn($"[Ghostra] DEBUG SpawnDecoy rejete : ({cmd.TargetX},{cmd.TargetY}) hors grille");
                return;
            }

            // Trouve le Ghostra actif (= caster).
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                if (c->HP <= 0) continue;
                if (c->PlayerIndex != playerIndex) continue;
                if (c->Class != NymoraClass.Ghostra)
                {
                    Log.Warn($"[Ghostra] DEBUG SpawnDecoy rejete : P{playerIndex} n'est pas Ghostra (classe = {c->Class})");
                    return;
                }
                DecoyHelpers.TrySpawn(f, c, cmd.TargetX, cmd.TargetY, DecoyKind.Standard, currentTurn);
                return;
            }
            Log.Warn($"[Ghostra] DEBUG SpawnDecoy : aucun Ghostra vivant pour P{playerIndex}");
        }

        private static void HandlePermutation(Frame f, int playerIndex, PermutationCommand cmd, int activePlayer, int currentTurn)
        {
            if (playerIndex != activePlayer)
            {
                Log.Warn($"[Ghostra] Permutation rejetee : pas le tour de P{playerIndex}");
                return;
            }

            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef e, out Combatant* c))
            {
                if (c->HP <= 0) continue;
                if (c->PlayerIndex != playerIndex) continue;
                if (c->Class != NymoraClass.Ghostra)
                {
                    Log.Warn($"[Ghostra] Permutation rejetee : P{playerIndex} n'est pas Ghostra");
                    return;
                }
                // 3.7.a.i.4 — Resolve slot index from TargetX/Y (case visée à la souris).
                // Bible-strict : le joueur doit cliquer précisément sur un de ses leurres.
                int slot = DecoyHelpers.FindSlotAtPosition(c, cmd.TargetX, cmd.TargetY);
                if (slot < 0)
                {
                    Log.Warn($"[Permutation] Rejet : case ({cmd.TargetX},{cmd.TargetY}) ne contient pas un de vos leurres. Cliquez sur un leurre pour le swap.");
                    return;
                }
                DecoyHelpers.TryPermute(f, e, c, slot, currentTurn);
                return;
            }
        }
    }
}
