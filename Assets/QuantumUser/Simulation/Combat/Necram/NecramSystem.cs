namespace Quantum
{
    /// <summary>
    /// 3.4 — Systeme Necram : aujourd'hui parse <see cref="DebugApplyVeninCommand"/>
    /// uniquement (cheat F11 cote View pour tester le framework marques venin avant
    /// la livraison des sorts).
    ///
    /// 3.5+ — recevra la gestion des hooks specifiques Necram qui ne tiennent pas dans
    /// SpellSystem (e.g. transfert de marques au kill via Morsure Putride, expiration
    /// du buff Carapace Visqueuse, tick Voile de Pestilence fin de tour, etc.).
    ///
    /// Pas de tick scheduled : tout passe par hooks d'autres systemes (NecramPassif
    /// via TurnSystem.EnterTurnStart, SpellSystem pour les sorts).
    /// </summary>
    public unsafe class NecramSystem : SystemMainThread
    {
        public override void Update(Frame f)
        {
            if (!f.TryGetSingleton<CombatState>(out var state)) return;
            if (state.CurrentPhase != CombatPhase.TurnActive) return;

            for (int playerIndex = 0; playerIndex < TurnConstants.PlayerCount; playerIndex++)
            {
                var cmd = f.GetPlayerCommand(playerIndex);
                if (cmd is DebugApplyVeninCommand veninCmd)
                {
                    HandleDebugApplyVenin(f, playerIndex, veninCmd, state.ActivePlayerIndex, state.TurnNumber);
                }
            }
        }

        private static void HandleDebugApplyVenin(Frame f, int playerIndex, DebugApplyVeninCommand cmd, int activePlayer, int currentTurn)
        {
            if (playerIndex != activePlayer)
            {
                Log.Warn($"[Necram] DEBUG ApplyVenin rejete : pas le tour de P{playerIndex}");
                return;
            }
            if (!GridHelpers.InBounds(cmd.TargetX, cmd.TargetY))
            {
                Log.Warn($"[Necram] DEBUG ApplyVenin rejete : ({cmd.TargetX},{cmd.TargetY}) hors grille");
                return;
            }

            // Cherche un combatant ennemi vivant sur la case.
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* target))
            {
                if (target->HP <= 0) continue;
                if (!TeamHelper.IsEnemyOfPlayer(f, playerIndex, target)) continue; // 5.1 : cible ennemie
                if (target->GridX != cmd.TargetX || target->GridY != cmd.TargetY) continue;
                int applied = VeninHelpers.ApplyMark(f, target, 1, currentTurn);
                if (applied == 0)
                {
                    Log.Info($"[Necram] DEBUG ApplyVenin : cible P{target->PlayerIndex} deja au cap ({VeninHelpers.MaxStacksPerTarget} marques)");
                }
                return;
            }
            Log.Warn($"[Necram] DEBUG ApplyVenin : aucun ennemi vivant sur ({cmd.TargetX},{cmd.TargetY})");
        }
    }
}
