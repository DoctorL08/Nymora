namespace Quantum
{
    /// <summary>
    /// POLISH-4.5 — Systeme DEBUG dedie aux F-keys de test marques/tiles/VFX.
    ///
    /// Parse les 3 commands :
    ///   - DebugApplyStatusCommand : applique un Status arbitraire (PlaieOuverte, BleedDoT,
    ///     ShieldActive, Untargetable, MarqueDeLOmbre) avec magnitude/duree raisonnables Bible.
    ///   - DebugSpawnTerrainCommand : pose un TerrainKind (SangCoagule/VapeurCarmin/BrumeToxique)
    ///     avec une duree par defaut sur la case souris.
    ///   - DebugSpawnTrapCommand : pose un TrapKind (FiletRonces/Mine) au nom du sender via
    ///     FogHelpers.PlaceTrap.
    ///
    /// Validation commune : sender == ActivePlayerIndex (regle classique cast Quantum), case
    /// dans grille. Le sender devient l'owner pour les pieges/poses qui en ont besoin.
    ///
    /// Tous ces shortcuts seront RETIRES en clean-up alpha/beta. Pour l'instant ils servent
    /// d'outil designer/QA pour valider visuellement le rendu des marques/tiles/VFX livres.
    /// </summary>
    public unsafe class DebugSystem : SystemMainThread
    {
        public override void Update(Frame f)
        {
            if (!f.TryGetSingleton<CombatState>(out var state)) return;
            if (state.CurrentPhase != CombatPhase.TurnActive) return;

            for (int playerIndex = 0; playerIndex < TurnConstants.PlayerCount; playerIndex++)
            {
                var cmd = f.GetPlayerCommand(playerIndex);
                switch (cmd)
                {
                    case DebugApplyStatusCommand statusCmd:
                        HandleApplyStatus(f, playerIndex, statusCmd, state.ActivePlayerIndex, state.TurnNumber);
                        break;
                    case DebugSpawnTerrainCommand terrainCmd:
                        HandleSpawnTerrain(f, playerIndex, terrainCmd, state.ActivePlayerIndex, state.TurnNumber);
                        break;
                    case DebugSpawnTrapCommand trapCmd:
                        HandleSpawnTrap(f, playerIndex, trapCmd, state.ActivePlayerIndex, state.TurnNumber);
                        break;
                    case DebugApplyMarkCommand markCmd:
                        HandleApplyMark(f, playerIndex, markCmd, state.ActivePlayerIndex, state.TurnNumber);
                        break;
                }
            }
        }

        // -------------------------------------------------------------------
        // STATUS APPLY (F1 / F2 / F8 / F9 / F10)
        // -------------------------------------------------------------------

        private static void HandleApplyStatus(Frame f, int playerIndex, DebugApplyStatusCommand cmd, int activePlayer, int currentTurn)
        {
            if (playerIndex != activePlayer)
            {
                Log.Warn($"[Debug] ApplyStatus rejete : pas le tour de P{playerIndex}");
                return;
            }
            if (!GridHelpers.InBounds(cmd.TargetX, cmd.TargetY))
            {
                Log.Warn($"[Debug] ApplyStatus rejete : ({cmd.TargetX},{cmd.TargetY}) hors grille");
                return;
            }

            StatusKind kind = (StatusKind)cmd.StatusKindByte;
            if (kind == StatusKind.None)
            {
                Log.Warn($"[Debug] ApplyStatus rejete : StatusKind.None");
                return;
            }

            // Defaults Bible-friendly par Status (magnitude + turnsLeft).
            int magnitude;
            int turnsLeft;
            switch (kind)
            {
                case StatusKind.PlaieOuverte:     magnitude = 40;  turnsLeft = 2; break; // Ghostra DoT
                case StatusKind.BleedDoT:         magnitude = 40;  turnsLeft = 2; break; // generic DoT
                case StatusKind.ShieldActive:     magnitude = 200; turnsLeft = 2; break; // Peau de Fer Bible
                case StatusKind.Untargetable:     magnitude = 0;   turnsLeft = 1; break; // Voile d'Ombre 1 round
                case StatusKind.MarqueDeLOmbre:   magnitude = 20;  turnsLeft = 2; break; // Ghostra +20 dmg
                case StatusKind.MarkedByCarnage:  magnitude = 0;   turnsLeft = 3; break; // Soulrender Marque de Carnage Bible 3 tours
                default:                          magnitude = 0;   turnsLeft = 2; break; // fallback
            }

            // Self-target : on prend le caster (sender). Sinon : ennemi vivant sur la case.
            Combatant* target = null;
            int targetPlayerForLog = -1;
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                if (c->HP <= 0) continue;
                if (cmd.SelfTarget != 0)
                {
                    if (c->PlayerIndex == playerIndex) { target = c; targetPlayerForLog = c->PlayerIndex; break; }
                }
                else
                {
                    if (c->PlayerIndex == playerIndex) continue; // skip caster pour cible-ennemi
                    if (c->GridX == cmd.TargetX && c->GridY == cmd.TargetY)
                    {
                        target = c; targetPlayerForLog = c->PlayerIndex; break;
                    }
                }
            }
            if (target == null)
            {
                string mode = cmd.SelfTarget != 0 ? "caster" : "ennemi sous souris";
                Log.Warn($"[Debug] ApplyStatus {kind} : aucun {mode} vivant trouve (target=({cmd.TargetX},{cmd.TargetY}))");
                return;
            }

            StatusHelper.Apply(target, kind, magnitude, turnsLeft, currentTurn);
            Log.Info($"[Debug] ApplyStatus {kind} sur P{targetPlayerForLog} ({target->GridX},{target->GridY}) : magnitude={magnitude} turnsLeft={turnsLeft}");
        }

        // -------------------------------------------------------------------
        // TERRAIN SPAWN (F3 / F4 / F5)
        // -------------------------------------------------------------------

        private static void HandleSpawnTerrain(Frame f, int playerIndex, DebugSpawnTerrainCommand cmd, int activePlayer, int currentTurn)
        {
            if (playerIndex != activePlayer)
            {
                Log.Warn($"[Debug] SpawnTerrain rejete : pas le tour de P{playerIndex}");
                return;
            }
            if (!GridHelpers.InBounds(cmd.TargetX, cmd.TargetY))
            {
                Log.Warn($"[Debug] SpawnTerrain rejete : ({cmd.TargetX},{cmd.TargetY}) hors grille");
                return;
            }

            TerrainKind kind = (TerrainKind)cmd.TerrainKindByte;
            if (kind == TerrainKind.None)
            {
                Log.Warn($"[Debug] SpawnTerrain rejete : TerrainKind.None");
                return;
            }

            // Durees Bible-friendly par TerrainKind.
            int turnsLeft;
            switch (kind)
            {
                case TerrainKind.SangCoagule:  turnsLeft = 2; break; // Bible Detonation Sanglante / Ame Laceree
                case TerrainKind.VapeurCarmin: turnsLeft = 1; break; // Bible Charge Brutale (-1 PM 1 tour)
                case TerrainKind.BrumeToxique: turnsLeft = 2; break; // Bible Brume Toxique zone 2 rounds
                default:                       turnsLeft = 2; break;
            }

            GridHelpers.SetTerrain(f, cmd.TargetX, cmd.TargetY, kind, turnsLeft, currentTurn);
            Log.Info($"[Debug] SpawnTerrain {kind} sur ({cmd.TargetX},{cmd.TargetY}) : turnsLeft={turnsLeft}");
        }

        // -------------------------------------------------------------------
        // TRAP SPAWN (F6 / F7)
        // -------------------------------------------------------------------

        private static void HandleSpawnTrap(Frame f, int playerIndex, DebugSpawnTrapCommand cmd, int activePlayer, int currentTurn)
        {
            if (playerIndex != activePlayer)
            {
                Log.Warn($"[Debug] SpawnTrap rejete : pas le tour de P{playerIndex}");
                return;
            }
            if (!GridHelpers.InBounds(cmd.TargetX, cmd.TargetY))
            {
                Log.Warn($"[Debug] SpawnTrap rejete : ({cmd.TargetX},{cmd.TargetY}) hors grille");
                return;
            }

            TrapKind kind = (TrapKind)cmd.TrapKindByte;
            if (kind == TrapKind.None)
            {
                Log.Warn($"[Debug] SpawnTrap rejete : TrapKind.None");
                return;
            }

            FogHelpers.PlaceTrap(f, cmd.TargetX, cmd.TargetY, kind, playerIndex, currentTurn);
            Log.Info($"[Debug] SpawnTrap {kind} sur ({cmd.TargetX},{cmd.TargetY}) par P{playerIndex}");
        }

        // -------------------------------------------------------------------
        // MARK APPLY (Shift+F2 / Shift+F3)
        // -------------------------------------------------------------------

        private static void HandleApplyMark(Frame f, int playerIndex, DebugApplyMarkCommand cmd, int activePlayer, int currentTurn)
        {
            if (playerIndex != activePlayer)
            {
                Log.Warn($"[Debug] ApplyMark rejete : pas le tour de P{playerIndex}");
                return;
            }
            if (!GridHelpers.InBounds(cmd.TargetX, cmd.TargetY))
            {
                Log.Warn($"[Debug] ApplyMark rejete : ({cmd.TargetX},{cmd.TargetY}) hors grille");
                return;
            }

            MarkKind kind = (MarkKind)cmd.MarkKindByte;
            if (kind == MarkKind.None)
            {
                Log.Warn($"[Debug] ApplyMark rejete : MarkKind.None");
                return;
            }

            // Default turnsLeft Bible-friendly.
            int turnsLeft;
            switch (kind)
            {
                case MarkKind.Traque:    turnsLeft = 3; break; // Marque du Chasseur Bible 3 tours
                case MarkKind.Empreinte: turnsLeft = 2; break; // Empreinte Bible 2 tours
                default:                 turnsLeft = 2; break;
            }

            // Cherche un combatant ennemi vivant sur la case.
            Combatant* target = null;
            int targetPlayerForLog = -1;
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                if (c->HP <= 0) continue;
                if (c->PlayerIndex == playerIndex) continue;
                if (c->GridX != cmd.TargetX || c->GridY != cmd.TargetY) continue;
                target = c;
                targetPlayerForLog = c->PlayerIndex;
                break;
            }
            if (target == null)
            {
                Log.Warn($"[Debug] ApplyMark {kind} : aucun ennemi vivant sur ({cmd.TargetX},{cmd.TargetY})");
                return;
            }

            MarkHelpers.ApplyMark(target, kind, turnsLeft, playerIndex, currentTurn);
            Log.Info($"[Debug] ApplyMark {kind} sur P{targetPlayerForLog} ({target->GridX},{target->GridY}) : turnsLeft={turnsLeft} owner=P{playerIndex}");
        }
    }
}
