namespace Quantum
{
    /// <summary>
    /// 2.14 — Systeme qui traite les commandes de DEBUG du brouillard de guerre.
    /// Aujourd'hui : DebugApplyVeilCommand (touche T cote View) qui pose un voile 2 tours
    /// sur la case visee, par le joueur actif.
    ///
    /// Sera retire / desactive en 2.15 quand les sorts Nightseer (Pas Furtif, Voile d'Ombre,
    /// Champ de Mines, etc.) prendront le relais via le SpellSystem.
    /// </summary>
    public unsafe class FogSystem : SystemMainThread
    {
        // Duree par defaut du voile pose en debug (Bible V7.1 : Pas Furtif voile 2 tours,
        // Champ de Mines indefiniment jusqu'a declenchement). On choisit 2 tours pour le
        // debug — assez long pour observer + decrementation visible.
        private const int DebugVeilTurns = 2;

        public override void OnInit(Frame f)
        {
            // Initialise le FogSingleton (255 tiles, tout a 0 / TrapKind.None).
            // Les fixed arrays Quantum sont deja zero-init mais on est explicite (pattern
            // miroir de GridSystem.OnInit).
            var fog = f.Unsafe.GetOrAddSingletonPointer<FogSingleton>(EntityRef.None);
            for (int i = 0; i < GridConstants.Count; i++)
            {
                fog->Tiles[i].VeiledByPlayer = 0;
                fog->Tiles[i].VeiledTurnsLeft = 0;
                fog->Tiles[i].VeiledAppliedOnTurn = 0;
                fog->Tiles[i].Trap = TrapKind.None;
                fog->Tiles[i].TrapOwner = 0;
                fog->Tiles[i].TrapAppliedOnTurn = 0;
            }
        }

        public override void Update(Frame f)
        {
            if (!f.TryGetSingleton<CombatState>(out var state)) return;
            if (state.CurrentPhase != CombatPhase.TurnActive) return;

            for (int playerIndex = 0; playerIndex < TurnConstants.PlayerCount; playerIndex++)
            {
                var cmd = f.GetPlayerCommand(playerIndex);
                if (cmd is DebugApplyVeilCommand veilCmd)
                {
                    HandleDebugApplyVeil(f, playerIndex, veilCmd, state.ActivePlayerIndex, state.TurnNumber);
                }
            }
        }

        private static void HandleDebugApplyVeil(Frame f, int playerIndex, DebugApplyVeilCommand cmd, int activePlayerIndex, int currentTurn)
        {
            // Securite minimale : seul le joueur actif peut poser un voile (en debug). Cela
            // reproduit la regle de cast classique (cf SpellSystem.TryCastSpell).
            if (playerIndex != activePlayerIndex)
            {
                Log.Warn($"[Fog] DEBUG ApplyVeil rejete : ce n'est pas le tour de P{playerIndex}");
                return;
            }
            if (!GridHelpers.InBounds(cmd.TargetX, cmd.TargetY))
            {
                Log.Warn($"[Fog] DEBUG ApplyVeil rejete : ({cmd.TargetX},{cmd.TargetY}) hors grille");
                return;
            }

            FogHelpers.ApplyVeil(f, cmd.TargetX, cmd.TargetY, playerIndex, DebugVeilTurns, currentTurn);
            Log.Info($"[Fog] DEBUG ApplyVeil P{playerIndex} -> case ({cmd.TargetX},{cmd.TargetY}) pour {DebugVeilTurns} tours (turn {currentTurn})");
        }
    }
}
