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

        // #23 (5 juin) — Owner de l'obstacle sur la case (PlayerIndex), ou -1 si aucun. Read-only,
        //   pour l'affichage de l'outline d'equipe en match miroir (Mur / Pilier). Les Failles
        //   d'Effondrement ont owner=None -> OwnerPlayerIndex sentinelle ; la View ne colore que P0/P1.
        public static int GetObstacleOwnerAt(Frame f, int x, int y)
        {
            EntityRef e = GetObstacleAt(f, x, y);
            if (e == EntityRef.None) return -1;
            if (!f.Unsafe.TryGetPointer<Obstacle>(e, out var obs)) return -1;
            return obs->OwnerPlayerIndex;
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
            int expiresOnTurn,
            bool gainFondation = true)
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
            // Fix 2 juin — on ne pose pas d'obstacle (Pilier / Mur / Faille) sur une case qui porte
            // une EMBUCHE (piege Nightseer) ou un LEURRE Ghostra. Garde par-case : le Mur saute juste
            // le segment concerne, les Failles d'Effondrement sautent la case, le Pilier echoue (un
            // pre-check pre-PA evite de gaspiller le tour cote SpellSystem).
            if (FogHelpers.GetTrapOwner(f, x, y) != -1)
            {
                Log.Warn($"[Obstacle] Spawn rejete : case ({x},{y}) porte une embuche");
                return EntityRef.None;
            }
            if (DecoyHelpers.HasAnyDecoyAt(f, x, y))
            {
                Log.Warn($"[Obstacle] Spawn rejete : case ({x},{y}) porte un leurre");
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

            // 3.2 — Bible V7.1 Fondation : "+1 FD quand le Colossar pose un Pilier". Branche ici
            // (au lieu de dans chaque sort) pour DRY. No-op si owner != Colossar (defensif).
            // Equilibrage juin : gainFondation=false pour le Mur de Pierre -> ses segments ne
            // donnent plus +1 FD chacun (c'etait +3 a +5 FD/Mur) ; le handler Mur accorde +2 FD
            // FLAT apres la pose. Les Failles d'Effondrement passent owner=None -> aucun FD.
            if (gainFondation && owner != EntityRef.None && f.Unsafe.TryGetPointer<Combatant>(owner, out var ownerC))
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
        public static void DestroyObstacle(Frame f, EntityRef entity, bool triggerPassiveHeal = true)
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
            if (triggerPassiveHeal
                && kind == ObstacleKind.Pillar
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

        /// <summary>
        /// Patch 8 juin (#16) — cap les Piliers/Murs d'un Colossar à MaxObstaclesPerColossar (6) cases.
        /// Les Failles (Effondrement) sont EXCLUES. Tant que l'owner dépasse le cap, détruit son obstacle
        /// le PLUS ANCIEN (ExpiresOnTurn réutilisé comme tour de pose ; tie-break = ordre de Filter
        /// déterministe). Destruction SILENCIEUSE (pas de heal Densité Inerte : c'est un auto-cap, pas une
        /// destruction adverse). Appelé après chaque pose de Pilier / Mur.
        /// </summary>
        public static void EnforceObstacleCap(Frame f, int ownerPlayerIndex)
        {
            while (true)
            {
                int count = 0;
                EntityRef oldest = EntityRef.None;
                // Clé d'ancienneté IDENTIQUE à la numérotation View (ObstacleRenderer) : tour de pose
                //   (ExpiresOnTurn), puis position écran gx-gy (gauche->droite), puis gy. -> on détruit
                //   bien l'obstacle affiché "n°1".
                int oldestTurn = int.MaxValue, oldestScreenX = int.MaxValue, oldestGy = int.MaxValue;
                var filter = f.Filter<Obstacle>();
                while (filter.NextUnsafe(out EntityRef e, out Obstacle* o))
                {
                    if (o->OwnerPlayerIndex != ownerPlayerIndex) continue;
                    if (o->Kind != ObstacleKind.Pillar && o->Kind != ObstacleKind.Wall) continue;
                    count++;
                    int screenX = o->GridX - o->GridY;
                    bool isOlder = o->ExpiresOnTurn < oldestTurn
                        || (o->ExpiresOnTurn == oldestTurn && (screenX < oldestScreenX
                            || (screenX == oldestScreenX && o->GridY < oldestGy)));
                    if (isOlder)
                    {
                        oldestTurn = o->ExpiresOnTurn;
                        oldestScreenX = screenX;
                        oldestGy = o->GridY;
                        oldest = e;
                    }
                }
                if (count <= SpellRegistry.MaxObstaclesPerColossar) break;
                if (oldest == EntityRef.None) break; // garde-fou
                Log.Info($"[Obstacle] Cap {SpellRegistry.MaxObstaclesPerColossar} dépassé pour P{ownerPlayerIndex} ({count}) -> détruit le n°1 (posé tour {oldestTurn})");
                DestroyObstacle(f, oldest, triggerPassiveHeal: false);
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
            // 5.1 (2v2/3v3) — on raisonne en EQUIPE : les unités/obstacles ALLIÉS ne bloquent pas
            //   la LoS du caster, les ENNEMIS oui. Résolu une seule fois ici (pas par case).
            //   casterPlayerIndex < 0 = mode neutre strict (tout bloque) -> casterTeamId reste < 0.
            int casterTeamId = casterPlayerIndex < 0 ? -1 : TeamHelper.ResolveTeamId(f, casterPlayerIndex);
            if (casterPlayerIndex >= 0 && casterTeamId < 0) casterTeamId = casterPlayerIndex; // fallback 1v1
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
                // 3.7.a.i.4 — un leurre Ghostra ennemi bloque la LoS (Bible-strict :
                // "indiscernable cote adversaire" -> doit bloquer comme une vraie unité).
                if (DecoyHelpers.HasEnemyDecoyAt(f, casterPlayerIndex, cx, cy)) return false;
                // PATCH 22 mai (amendement Bible, decision Lorenzo) — un COMBATTANT ENNEMI vivant
                // sur une case intermediaire bloque aussi la ligne de vue/tir (la Bible V7.1 ne
                // citait que Pilier/Mur). Les allies ne bloquent PAS. casterPlayerIndex < 0
                // (mode strict neutre) -> toute unite bloque.
                EntityRef occE = GridHelpers.GetOccupant(f, cx, cy);
                if (occE != EntityRef.None
                    && f.Unsafe.TryGetPointer<Combatant>(occE, out var occC))
                {
                    // 5.3 — un CADAVRE (HP<=0, mode équipe) reste un obstacle NEUTRE : il bloque la
                    //   LoS pour tout le monde (alliés du mort inclus). En 1v1 la mort = MatchEnd
                    //   donc jamais rencontré (non-régression).
                    if (occC->HP <= 0) return false;
                    // 5.1 — unité VIVANTE : ennemie bloque, alliée non (mode neutre strict bloque tout).
                    if (casterTeamId < 0 || occC->TeamId != casterTeamId) return false;
                }
                // Case intermediaire : check obstacle bloquant.
                EntityRef obsE = GetObstacleAt(f, cx, cy);
                if (obsE == EntityRef.None) continue;
                if (casterTeamId < 0) return false; // mode strict : tout obstacle bloque
                if (!f.Unsafe.TryGetPointer<Obstacle>(obsE, out var obsP)) return false;
                // 5.1 — obstacle d'équipe ENNEMIE bloque ; obstacle de SON camp (ex: murs du Colossar
                //   allié) se traverse. Résout le team de l'owner (rare : peu de cases-obstacles).
                int obsTeam = TeamHelper.ResolveTeamId(f, obsP->OwnerPlayerIndex);
                if (obsTeam < 0) obsTeam = obsP->OwnerPlayerIndex; // fallback 1v1
                if (obsTeam != casterTeamId) return false; // obstacle ennemi bloque
                // Sinon obstacle du MEME camp : on traverse (Colossar voit a travers ses propres murs / ceux de son allié).
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

            // 3.3.d : Faille (Effondrement) est destructible par AoE adverse (Bible-balance 3.3.d).
            // 100 HP = ~1 cast AoE moyen suffit pour casser une Faille et creer un passage.
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
