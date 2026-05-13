namespace Quantum
{
    using Photon.Deterministic;

    /// <summary>
    /// 3.1 — Framework system pour les obstacles dynamiques (Pilier, Mur, et obstacles
    /// posables futurs).
    ///
    /// Responsabilites :
    ///   1. OnInit : initialise le ObstacleSingleton (clear les 255 slots).
    ///   2. Update : traite les commandes DEBUG de la brique (Spawn / Damage) — sera
    ///      remplace par les sorts (3.3.b Pilier, Mur de Pierre, Brisure).
    ///   3. EnterTurnEnd-style tick : Filter<Obstacle> et destroy ceux dont ExpiresOnTurn
    ///      == currentTurn (Mur 2 rounds, etc.). On utilise un signal "fin de tour" emis
    ///      par TurnSystem ; en attendant 3.2 ou la refacto signaux, on tick au TurnEnd via
    ///      un poll dans Update (compare au tour precedent observe).
    ///
    /// L'expiration est traitee a CHAQUE changement de TurnNumber (transition d'un round
    /// au suivant). Idempotent : meme si Update est appele plusieurs fois au meme TurnNumber,
    /// l'expiration n'est appliquee qu'une seule fois grace au tracking _lastSeenTurnNumber.
    /// </summary>
    public unsafe class ObstacleSystem : SystemMainThread
    {
        // Tracking du dernier TurnNumber observe (utilise un singleton minimal pour rester
        // deterministe — pas de field statique). Pour 3.1 simple, on stocke dans un slot
        // de l'ObstacleSingleton... mais le singleton n'a pas ce field. Alternative : on
        // tick a CHAQUE frame ou TurnNumber a change, en utilisant un signal dedie.
        //
        // Plus simple pour 3.1 : on tick juste en TurnEnd, en s'inscrivant via une convention
        // partagee avec TurnSystem (TurnSystem.EnterTurnEnd appelle ObstacleHelpers.TickExpirations).
        // Mais ca demande de modifier TurnSystem — preference : auto-detect TurnNumber change ici.
        //
        // 3.1 v1 : auto-detect via Filter sur Obstacle.ExpiresOnTurn (on rescan a chaque Update
        // tick mais c'est cheap : <= 10 obstacles concurrents typiques).
        //
        // L'idempotence vient du fait que DestroyObstacle clear le slot avant de Destroy(entity).
        // Apres destruction, l'entity est plus dans le Filter, donc pas de double-process.

        public override void OnInit(Frame f)
        {
            // Initialise le singleton (pattern miroir de GridSystem / FogSystem.OnInit).
            // Les fixed arrays Quantum sont deja zero-init, mais on est explicite.
            var sing = f.Unsafe.GetOrAddSingletonPointer<ObstacleSingleton>(EntityRef.None);
            for (int i = 0; i < GridConstants.Count; i++)
            {
                sing->Tiles[i].Obstacle = EntityRef.None;
            }
            Log.Info("[ObstacleSystem] OnInit : ObstacleSingleton initialise (255 slots).");
        }

        public override void Update(Frame f)
        {
            if (!f.TryGetSingleton<CombatState>(out var state)) return;

            // 1. Process des commandes DEBUG (sera retire en 3.3.b quand les sorts Pilier/Mur
            // arrivent).
            if (state.CurrentPhase == CombatPhase.TurnActive)
            {
                for (int playerIndex = 0; playerIndex < TurnConstants.PlayerCount; playerIndex++)
                {
                    var cmd = f.GetPlayerCommand(playerIndex);
                    if (cmd is DebugSpawnObstacleCommand spawnCmd)
                    {
                        HandleDebugSpawn(f, playerIndex, spawnCmd, state.ActivePlayerIndex);
                    }
                    else if (cmd is DebugDamageObstacleCommand dmgCmd)
                    {
                        HandleDebugDamage(f, playerIndex, dmgCmd, state.ActivePlayerIndex);
                    }
                }
            }

            // 2. Tick expirations en TurnEnd (decrement based on ExpiresOnTurn).
            // Convention : un Obstacle expire quand ExpiresOnTurn == TurnNumber ET phase ==
            // TurnEnd (juste avant de passer au round suivant). Pour eviter de double-process
            // (Update est appele a chaque tick simu), on tick uniquement quand on rentre dans
            // TurnEnd. La transition TurnActive -> TurnEnd est detectee via un Filter sans
            // state perso (idempotent par destruction de l'entity).
            //
            // Implementation simple : a chaque tick TurnEnd, scan Filter<Obstacle>, destroy
            // ceux qui expirent ce tour. Comme DestroyObstacle est idempotent, on peut
            // appeler N fois sans bug, mais on log spam. Donc on guard via une convention :
            // on tick UNIQUEMENT au tout premier sub-tick TurnEnd (TurnTimerTicks transitions).
            //
            // Pour 3.1 framework, le critere "ExpiresOnTurn <= currentTurn" suffit : une fois
            // detruit, l'entity sort du filter, donc pas de re-tick.
            if (state.CurrentPhase == CombatPhase.TurnEnd)
            {
                TickExpirations(f, state.TurnNumber);
            }
        }

        // ====================================================================
        // Tick des expirations.
        // ====================================================================

        // Capacity du buffer stackalloc des entities a destroy. Bible V7.1 Colossar
        // FD cap = 3 -> max 3 piliers + ~3 segments mur simultanes. 16 = marge x2.
        private const int MaxDestroyPerTick = 16;

        private static void TickExpirations(Frame f, int currentTurn)
        {
            // Collecte d'abord les entities a destroy (pas de destroy en cours de Filter,
            // sinon iterator invalidation). Stackalloc zero-heap (pattern AStarPathfinder).
            EntityRef* toDestroy = stackalloc EntityRef[MaxDestroyPerTick];
            int destroyCount = 0;

            var filter = f.Filter<Obstacle>();
            while (filter.NextUnsafe(out EntityRef entity, out Obstacle* obs))
            {
                // ExpiresOnTurn == 0 -> persistent (Pilier), jamais expire par timer.
                if (obs->ExpiresOnTurn == 0) continue;
                if (currentTurn >= obs->ExpiresOnTurn)
                {
                    if (destroyCount < MaxDestroyPerTick)
                    {
                        toDestroy[destroyCount++] = entity;
                    }
                    else
                    {
                        Log.Warn($"[ObstacleSystem] TickExpirations : > {MaxDestroyPerTick} obstacles a destroy ce tour, capacity buffer atteinte. Bump MaxDestroyPerTick si normal.");
                        break;
                    }
                }
            }

            for (int i = 0; i < destroyCount; i++)
            {
                ObstacleHelpers.DestroyObstacle(f, toDestroy[i]);
            }
        }

        // ====================================================================
        // Handlers commandes DEBUG (3.1 only — retires en 3.3.b).
        // ====================================================================

        private static void HandleDebugSpawn(Frame f, int playerIndex, DebugSpawnObstacleCommand cmd, int activePlayerIndex)
        {
            if (playerIndex != activePlayerIndex)
            {
                Log.Warn($"[ObstacleSystem] DEBUG Spawn rejete : ce n'est pas le tour de P{playerIndex}");
                return;
            }
            // Resolve l'entity owner = combattant du joueur actif (pour le passif Colossar
            // futur). Pour 3.1 on prend juste le combattant du player actif.
            EntityRef owner = EntityRef.None;
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef entity, out Combatant* c))
            {
                if (c->PlayerIndex == playerIndex)
                {
                    owner = entity;
                    break;
                }
            }

            ObstacleHelpers.SpawnObstacle(
                f,
                ObstacleKind.Pillar,
                hp: 200,                     // Bible V7.1 Pilier
                x: cmd.TargetX, y: cmd.TargetY,
                owner: owner,
                ownerPlayerIndex: playerIndex,
                expiresOnTurn: 0);           // 0 = persistent
        }

        private static void HandleDebugDamage(Frame f, int playerIndex, DebugDamageObstacleCommand cmd, int activePlayerIndex)
        {
            if (playerIndex != activePlayerIndex)
            {
                Log.Warn($"[ObstacleSystem] DEBUG Damage rejete : ce n'est pas le tour de P{playerIndex}");
                return;
            }
            const int DebugDmgAmount = 50;
            bool hit = ObstacleHelpers.DamageAt(f, cmd.TargetX, cmd.TargetY, DebugDmgAmount);
            if (!hit)
            {
                Log.Warn($"[ObstacleSystem] DEBUG Damage : pas d'obstacle sur ({cmd.TargetX},{cmd.TargetY})");
            }
        }
    }
}
