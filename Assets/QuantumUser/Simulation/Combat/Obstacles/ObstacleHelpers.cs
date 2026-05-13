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

            // 3.2 — Bible V7.1 Fondation : "+1 FD chaque fois que le Colossar pose un Pilier
            // ou un Mur". Branche ici (au lieu de dans chaque sort 3.3.b) pour DRY. Le helper
            // est no-op si owner != Colossar (defensif), donc safe a appeler aussi pour les
            // obstacles debug spawne par P0 si P0=Colossar (3.1.bis switch).
            if (owner != EntityRef.None && f.Unsafe.TryGetPointer<Combatant>(owner, out var ownerC))
            {
                ColossarPassif.GainFondation(ownerC, $"Spawn {kind}");
            }

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
            EntityRef ownerEntity = obs->Owner;

            // 1. Clear slot.
            if (GridHelpers.InBounds(x, y))
            {
                var sing = f.Unsafe.GetPointerSingleton<ObstacleSingleton>();
                sing->Tiles[GridHelpers.Index(x, y)].Obstacle = EntityRef.None;
            }

            // 2. Destroy entity.
            f.Destroy(entity);

            Log.Info($"[Obstacle] Destroy {kind} pos=({x},{y}) ownerP{ownerPlayerIndex}");

            // 3. 3.2 Bible V7.1 Densite Inerte : "+30 HP au Colossar quand un de ses Piliers
            // est detruit". Bible specifique "Pilier" (pas Mur). Le owner peut etre detruit
            // entre temps (kill match Soulrender), defensif TryGetPointer.
            if (kind == ObstacleKind.Pillar
                && ownerEntity != EntityRef.None
                && f.Unsafe.TryGetPointer<Combatant>(ownerEntity, out var ownerC)
                && ownerC->Class == NymoraClass.Colossar
                && ownerC->HP > 0)
            {
                int before = ownerC->HP;
                ownerC->HP = before + ColossarPassif.HpRestoredOnPillarDestroyed;
                if (ownerC->HP > ownerC->MaxHP) ownerC->HP = ownerC->MaxHP;
                Log.Info($"[Densite Inerte] Colossar P{ownerPlayerIndex} +{ownerC->HP - before} HP (Pilier detruit) : {before} -> {ownerC->HP}");
            }
        }

        // ====================================================================
        // 3.3.b.i — Line of Sight (Bresenham 2D deterministe int-only).
        //
        // Bible V7.1 : "Pilier/Mur bloque les lignes de vue/tir". Convention retenue :
        // les obstacles OWN du caster ne bloquent PAS sa LoS (sinon le Colossar
        // bloque ses propres sorts entre ses Murs — gameplay-killing). Les obstacles
        // adverses bloquent toujours. casterPlayerIndex = -1 -> tout obstacle bloque
        // (utile pour des checks neutres si jamais).
        //
        // Algo Bresenham strict : trace ligne (x0,y0)->(x1,y1), inspecte chaque case
        // INTERMEDIAIRE (skip endpoints). Retourne false des qu'un obstacle bloquant
        // est rencontre. Cas degenere x0==x1 && y0==y1 : retourne true.
        // ====================================================================

        public static bool HasLineOfSight(Frame f, int x0, int y0, int x1, int y1, int casterPlayerIndex = -1)
        {
            if (x0 == x1 && y0 == y1) return true;
            int dx = x1 > x0 ? x1 - x0 : x0 - x1;
            int dy = y1 > y0 ? y1 - y0 : y0 - y1;
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;
            int cx = x0;
            int cy = y0;
            // Safety : Manhattan + 2 garantit un nombre fini d'iterations meme en edge case.
            int safety = dx + dy + 2;
            while (safety-- > 0)
            {
                int e2 = err * 2;
                if (e2 > -dy) { err -= dy; cx += sx; }
                if (e2 < dx)  { err += dx; cy += sy; }
                if (cx == x1 && cy == y1) return true; // arrivee : la case cible elle-meme ne bloque pas
                // Case intermediaire : check obstacle bloquant.
                EntityRef obsE = GetObstacleAt(f, cx, cy);
                if (obsE == EntityRef.None) continue;
                if (casterPlayerIndex < 0) return false; // mode strict : tout obstacle bloque
                if (!f.Unsafe.TryGetPointer<Obstacle>(obsE, out var obsP)) return false;
                if (obsP->OwnerPlayerIndex != casterPlayerIndex) return false; // obstacle ennemi bloque
                // Sinon obstacle OWN : on traverse (Colossar voit a travers ses propres murs).
            }
            return true; // safety reached (ne devrait pas arriver)
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
