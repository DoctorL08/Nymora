namespace Quantum
{
    using Photon.Deterministic;

    /// <summary>
    /// 3.1 — Helpers pour le framework obstacles dynamiques.
    ///
    /// Stockage hybride :
    ///   - ObstacleSingleton.Tiles[idx].Obstacle : EntityRef -> lookup O(1) par case.
    ///   - component Obstacle sur l'entity : HP / Owner / Kind / Pos.
    ///
    /// Operations principales :
    ///   - Spawn : creer entity + ecrire slot singleton.
    ///   - Damage : decrement HP via le pointer (le SpellSystem appellera ca en 3.3.b).
    ///   - Destroy : enleve l'entity + clear slot.
    ///   - HasObstacleAt / GetObstacleAt : lookup par coords.
    ///
    /// Pattern miroir des FogHelpers (Voile/Trap) mais entity-based plutot que data-only.
    /// Justification : les obstacles peuvent etre cibles par des sorts (HP, destruction
    /// par dmg), donc le lifecycle entity est plus naturel que data-only.
    /// </summary>
    public static unsafe class ObstacleHelpers
    {
        // ====================================================================
        // Lookups par case (utilise par MovementSystem + AStarPathfinder).
        // ====================================================================

        public static bool HasObstacleAt(Frame f, int x, int y)
        {
            if (!GridHelpers.InBounds(x, y)) return false;
            var sing = f.Unsafe.GetPointerSingleton<ObstacleSingleton>();
            return sing->Tiles[GridHelpers.Index(x, y)].Obstacle != EntityRef.None;
        }

        public static EntityRef GetObstacleAt(Frame f, int x, int y)
        {
            if (!GridHelpers.InBounds(x, y)) return EntityRef.None;
            var sing = f.Unsafe.GetPointerSingleton<ObstacleSingleton>();
            return sing->Tiles[GridHelpers.Index(x, y)].Obstacle;
        }

        // ====================================================================
        // Spawn / Destroy lifecycle.
        // ====================================================================

        /// <summary>
        /// Cree un Obstacle entity + ecrit le slot singleton pour la case (x,y).
        ///
        /// Refus si :
        ///   - (x,y) hors grille
        ///   - case deja occupee par un Combatant ou un autre Obstacle
        ///
        /// Retourne EntityRef.None en cas d'echec, sinon l'entity nouvellement creee.
        ///
        /// expiresOnTurn = 0 -> persistent (Pilier). Sinon = TurnNumber a partir duquel
        /// l'obstacle expire au TurnEnd (Mur 2 rounds = currentTurn + 2).
        /// </summary>
        public static EntityRef SpawnObstacle(
            Frame f,
            ObstacleKind kind, int hp,
            int x, int y,
            EntityRef owner, int ownerPlayerIndex,
            int expiresOnTurn)
        {
            if (!GridHelpers.InBounds(x, y))
            {
                Log.Warn($"[Obstacle] Spawn rejete : ({x},{y}) hors grille");
                return EntityRef.None;
            }
            if (GridHelpers.GetOccupant(f, x, y) != EntityRef.None)
            {
                Log.Warn($"[Obstacle] Spawn rejete : case ({x},{y}) occupee par un combattant");
                return EntityRef.None;
            }
            if (HasObstacleAt(f, x, y))
            {
                Log.Warn($"[Obstacle] Spawn rejete : case ({x},{y}) deja un obstacle");
                return EntityRef.None;
            }
            if (kind == ObstacleKind.None || hp <= 0)
            {
                Log.Warn($"[Obstacle] Spawn rejete : kind={kind} hp={hp} invalides");
                return EntityRef.None;
            }

            EntityRef entity = f.Create();
            f.Add<Obstacle>(entity, new Obstacle
            {
                Owner = owner,
                OwnerPlayerIndex = ownerPlayerIndex,
                Kind = kind,
                HP = hp,
                MaxHP = hp,
                GridX = x,
                GridY = y,
                ExpiresOnTurn = expiresOnTurn,
            });

            var sing = f.Unsafe.GetPointerSingleton<ObstacleSingleton>();
            sing->Tiles[GridHelpers.Index(x, y)].Obstacle = entity;

            Log.Info($"[Obstacle] Spawn {kind} entity={entity} pos=({x},{y}) HP={hp} owner=P{ownerPlayerIndex} expires={expiresOnTurn}");
            return entity;
        }

        /// <summary>
        /// Destroy une entity Obstacle proprement :
        ///   1. Clear le slot singleton de la case.
        ///   2. Frame.Destroy(entity).
        ///   3. Log + retour signal pour les passifs (Colossar Densite Inerte +30 HP/Pilier detruit
        ///      sera branche en 3.2 : il scan le log ou abonne un signal — TBD).
        ///
        /// Idempotent : si l'entity n'est plus valide, no-op silencieux.
        /// </summary>
        public static void DestroyObstacle(Frame f, EntityRef entity)
        {
            if (entity == EntityRef.None) return;
            if (!f.Unsafe.TryGetPointer<Obstacle>(entity, out var obs))
            {
                // Deja detruit ou pas un Obstacle.
                return;
            }

            int x = obs->GridX;
            int y = obs->GridY;
            ObstacleKind kind = obs->Kind;
            int ownerPlayerIndex = obs->OwnerPlayerIndex;

            // 1. Clear slot.
            if (GridHelpers.InBounds(x, y))
            {
                var sing = f.Unsafe.GetPointerSingleton<ObstacleSingleton>();
                sing->Tiles[GridHelpers.Index(x, y)].Obstacle = EntityRef.None;
            }

            // 2. Destroy entity.
            f.Destroy(entity);

            Log.Info($"[Obstacle] Destroy {kind} pos=({x},{y}) ownerP{ownerPlayerIndex}");

            // 3. TODO (3.2) : signal OnObstacleDestroyed pour passif Colossar Densite Inerte
            // (+30 HP au owner si Pilier detruit). Pour 3.1 framework only, on log juste.
        }

        // ====================================================================
        // Damage (utilise par DebugDamageObstacleCommand en 3.1, par SpellSystem en 3.3.b).
        // ====================================================================

        /// <summary>
        /// Inflige `dmg` HP a l'obstacle de la case (x,y). Si HP tombe a 0, l'obstacle est
        /// detruit IMMEDIATEMENT (pas en queue). Retourne true si dgts appliques, false si
        /// pas d'obstacle sur la case.
        /// </summary>
        public static bool DamageAt(Frame f, int x, int y, int dmg)
        {
            EntityRef obsEntity = GetObstacleAt(f, x, y);
            if (obsEntity == EntityRef.None) return false;
            if (!f.Unsafe.TryGetPointer<Obstacle>(obsEntity, out var obs)) return false;

            int hpBefore = obs->HP;
            obs->HP -= dmg;
            if (obs->HP < 0) obs->HP = 0;
            Log.Info($"[Obstacle] Damage {obs->Kind} ({x},{y}) : -{dmg} HP ({hpBefore} -> {obs->HP})");

            if (obs->HP <= 0)
            {
                DestroyObstacle(f, obsEntity);
            }
            return true;
        }
    }
}
