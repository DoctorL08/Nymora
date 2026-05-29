namespace Quantum
{
    using Photon.Deterministic;

    /// <summary>
    /// Traite les MoveCommands envoyees par les joueurs.
    ///
    /// 2.5 : appelle A* pour calculer le chemin optimal vers la case cible.
    /// - Si distance Manhattan == 1 : application directe (skip A*, optim)
    /// - Sinon : A* deterministe (cf AStarPathfinder), application si path.length <= PM
    ///
    /// Application synchrone en 1 tick : combattant teleporte a la destination,
    /// PM decrement par la longueur du chemin. Le View lerp en ligne droite vers
    /// la nouvelle case (anim case-par-case = Phase 2.10+ si necessaire).
    /// </summary>
    public unsafe class MovementSystem : SystemMainThread
    {
        public override void Update(Frame f)
        {
            if (!f.TryGetSingleton<CombatState>(out var state)) return;
            if (state.CurrentPhase != CombatPhase.TurnActive) return;

            for (int playerIndex = 0; playerIndex < TurnConstants.PlayerCount; playerIndex++)
            {
                if (f.GetPlayerCommand(playerIndex) is MoveCommand cmd)
                {
                    TryMoveCombatant(f, playerIndex, cmd.TargetX, cmd.TargetY, state.ActivePlayerIndex);
                }
            }
        }

        private static void TryMoveCombatant(Frame f, int playerIndex, int targetX, int targetY, int activePlayerIndex)
        {
            if (playerIndex != activePlayerIndex)
            {
                Log.Warn($"[Movement] rejet : ce n'est pas le tour de P{playerIndex}");
                return;
            }

            EntityRef combatantEntity = EntityRef.None;
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef entity, out Combatant* c))
            {
                if (c->PlayerIndex == playerIndex)
                {
                    combatantEntity = entity;
                    break;
                }
            }

            if (combatantEntity == EntityRef.None)
            {
                Log.Warn($"[Movement] rejet : pas de Combatant pour P{playerIndex}");
                return;
            }

            var combatant = f.Unsafe.GetPointer<Combatant>(combatantEntity);

            if (combatant->PM <= 0)
            {
                Log.Warn("[Movement] rejet : PM=0");
                return;
            }

            // No-op silencieux si clic sur la propre case du combattant.
            if (targetX == combatant->GridX && targetY == combatant->GridY) return;

            if (!GridHelpers.InBounds(targetX, targetY))
            {
                Log.Warn($"[Movement] rejet : ({targetX},{targetY}) hors grille");
                return;
            }
            if (!GridHelpers.IsWalkable(f, targetX, targetY))
            {
                Log.Warn($"[Movement] rejet : ({targetX},{targetY}) non walkable");
                return;
            }
            if (GridHelpers.GetOccupant(f, targetX, targetY) != EntityRef.None)
            {
                Log.Warn($"[Movement] rejet : ({targetX},{targetY}) deja occupee");
                return;
            }
            // 3.1 — refus si obstacle Colossar (Pilier/Mur). Le combattant ne peut pas
            // traverser ses propres murs (seul Choc Sismique le peut, sort 3.3.b).
            if (ObstacleHelpers.HasObstacleAt(f, targetX, targetY))
            {
                Log.Warn($"[Movement] rejet : ({targetX},{targetY}) bloquee par un obstacle");
                return;
            }
            // 3.7.a.i.4 — TOUT leurre Ghostra bloque la destination (Bible-strict :
            // impossible de marcher sur une silhouette, allié ou ennemie). La Ghostra
            // doit utiliser la Permutation Angle 3 pour échanger avec un de ses leurres.
            if (DecoyHelpers.HasAnyDecoyAt(f, targetX, targetY))
            {
                Log.Warn($"[Movement] rejet : ({targetX},{targetY}) occupee par un leurre Ghostra");
                return;
            }

            // Heuristique rapide : si meme la distance Manhattan optimale depasse PM, inutile d'appeler A*.
            int dx = targetX - combatant->GridX;
            int dy = targetY - combatant->GridY;
            int absDx = dx < 0 ? -dx : dx;
            int absDy = dy < 0 ? -dy : dy;
            int manhattan = absDx + absDy;

            // 2.10.c : Vapeur Carmin sur la case d'arrivee coute +1 PM (simplification du Bible
            // "traversee" — verifie uniquement la destination, vraie traversee multi-case = Phase 7).
            int extraCostVapeur = 0;
            if (GridHelpers.GetTerrainKind(f, targetX, targetY) == TerrainKind.VapeurCarmin)
            {
                extraCostVapeur = 1;
            }

            if (manhattan + extraCostVapeur > combatant->PM)
            {
                Log.Warn($"[Movement] rejet : distance optimale {manhattan} (+{extraCostVapeur} Vapeur Carmin) > PM {combatant->PM}");
                return;
            }

            // 3.5.b.iii — Pas Spectral : si le combatant est un Necram porteur de
            // PasSpectralReady, A* peut traverser les ennemis ET on appliquera +1 marque venin
            // par ennemi traverse dans ApplyMove. Skip Manhattan==1 (pas de case intermediaire).
            bool pasSpectralActive = combatant->Class == NymoraClass.Necram
                                  && StatusHelper.Has(combatant, StatusKind.PasSpectralReady);

            // 3.7.c.v — Pas de l'Au-Dela : si la Ghostra porte le status PasAuDelaReady
            // (apply par cast Pas de l'Au-Dela ce tour, expire en fin de SON sub-turn via
            // TurnSystem.EnterTurnEnd), A* traverse ennemis ET leurres et chaque ennemi traverse
            // pendant les cases intermediaires encaisse PasAuDelaDorsalDamage HP loss direct
            // (frappe gratuite dorsale Bible). Status non consume au Move : tous les Moves du
            // tour Ghostra beneficient du bypass (coherent avec "+2 PM ce tour" Bible).
            bool pasAuDelaActive = combatant->Class == NymoraClass.Ghostra
                                && StatusHelper.Has(combatant, StatusKind.PasAuDelaReady);

            // OR combine : A* ignoreEnemyOccupants pour Pas Spectral OU Pas de l'Au-Dela.
            // (Les deux sont mutuellement exclusifs par classe en pratique 1v1 / 2v2 mono-classe.)
            bool ignoreUnitsForPath = pasSpectralActive || pasAuDelaActive;

            // Cas adjacent (1 case) : skip A* pour optimiser
            if (manhattan == 1)
            {
                ApplyMove(f, combatant, combatantEntity, targetX, targetY, 1 + extraCostVapeur, null, 0, false, false);
                return;
            }

            // Cas multi-cases : A* deterministe
            int* pathBuffer = stackalloc int[GridConstants.Count];
            if (!AStarPathfinder.TryFindPath(
                    f,
                    combatant->GridX, combatant->GridY,
                    targetX, targetY,
                    combatant->PM,
                    pathBuffer,
                    out int pathLength,
                    ignoreEnemyOccupants: ignoreUnitsForPath,
                    casterPlayerIndex: combatant->PlayerIndex))
            {
                Log.Warn($"[Movement] rejet : pas de chemin <= PM={combatant->PM} vers ({targetX},{targetY})");
                return;
            }

            int totalCost = pathLength + extraCostVapeur;
            if (totalCost > combatant->PM)
            {
                Log.Warn($"[Movement] rejet : path cost {pathLength} (+{extraCostVapeur} Vapeur Carmin) > PM {combatant->PM}");
                return;
            }

            // Refonte 29 mai — le Piège Bondissant se déclenche AU PASSAGE comme les autres pièges
            //   (crossing loop + destination dans ApplyMove -> FogHelpers.TryTriggerTrapOnEnter).
            //   Pas de troncature : l'éjection part de la case DU PIÈGE (gérée dans FogHelpers).
            ApplyMove(f, combatant, combatantEntity, targetX, targetY, totalCost, pathBuffer, pathLength, pasSpectralActive, pasAuDelaActive);
        }

        private static void ApplyMove(Frame f, Combatant* combatant, EntityRef entity, int targetX, int targetY, int cost,
                                      int* pathBuffer, int pathLength, bool applyPasSpectralCrossings, bool applyPasAuDelaCrossings)
        {
            // 3.7.a.i.0 — Update Facing depuis la direction du dernier deplacement (dx,dy)
            //   du from-cell vers la target. Lu par GhostraPassif.IsDorsalHit pour le bonus
            //   dorsal Ghostra Bible V7.1. Compute AVANT la mutation GridX/Y pour avoir le delta.
            int dxMove = targetX - combatant->GridX;
            int dyMove = targetY - combatant->GridY;
            if (dxMove != 0 || dyMove != 0)
            {
                combatant->Facing = FacingHelpers.FacingFromGridDelta(dxMove, dyMove);
            }

            GridHelpers.SetOccupant(f, combatant->GridX, combatant->GridY, EntityRef.None);
            combatant->GridX = targetX;
            combatant->GridY = targetY;
            combatant->PM -= cost;
            GridHelpers.SetOccupant(f, targetX, targetY, entity);

            Log.Info($"[Movement] P{combatant->PlayerIndex} -> ({targetX},{targetY}) cost={cost} PM restant={combatant->PM} facing={combatant->Facing}");

            int currentTurn = f.TryGetSingleton<CombatState>(out var st) ? st.TurnNumber : 0;

            // 3.5.b.iii — Pas Spectral : pose +1 marque venin sur chaque ennemi present sur les
            // cases INTERMEDIAIRES du path (skip destination, deja validee libre). Le pathBuffer
            // contient les indices grille des cases successives, start exclu et target inclus
            // au dernier index. On itere donc [0, pathLength-1) pour ne pas toucher la dest.
            if (applyPasSpectralCrossings && pathBuffer != null && pathLength > 1)
            {
                for (int i = 0; i < pathLength - 1; i++)
                {
                    int crossingIdx = pathBuffer[i];
                    int cx = crossingIdx % GridConstants.Width;
                    int cy = crossingIdx / GridConstants.Width;
                    EntityRef occ = GridHelpers.GetOccupant(f, cx, cy);
                    if (occ == EntityRef.None) continue;
                    if (!f.Unsafe.TryGetPointer<Combatant>(occ, out Combatant* crossed)) continue;
                    if (crossed->PlayerIndex == combatant->PlayerIndex) continue; // skip allie/self
                    if (crossed->HP <= 0) continue;
                    VeninHelpers.ApplyMark(f, crossed, SpellRegistry.PasSpectralMarksPerCrossing, currentTurn);
                    Log.Info($"[Pas Spectral] Necram P{combatant->PlayerIndex} traverse P{crossed->PlayerIndex} en ({cx},{cy}) : +{SpellRegistry.PasSpectralMarksPerCrossing} marque venin");
                }
            }

            // 3.7.c.v — Pas de l'Au-Dela : pour chaque ennemi present sur les cases intermediaires
            // du path -> PasAuDelaDorsalDamage HP loss direct (frappe dorsale gratuite Bible).
            // Pas de pipeline shield/reflect/redirect (decision Bible-stricte "frappe gratuite").
            // Multi-targets : TOUS les ennemis traverses prennent les dgts (decision Lorenzo).
            // Les leurres ennemis sur le path sont ignores (pas de dgts sur leurres : on les
            // traverse comme fantome physique sans interaction). DamageTakenThisRound increment
            // sur la victime pour preserver le passif Prescience Nightseer.
            if (applyPasAuDelaCrossings && pathBuffer != null && pathLength > 1)
            {
                for (int i = 0; i < pathLength - 1; i++)
                {
                    int crossingIdx = pathBuffer[i];
                    int cx = crossingIdx % GridConstants.Width;
                    int cy = crossingIdx / GridConstants.Width;
                    EntityRef occ = GridHelpers.GetOccupant(f, cx, cy);
                    if (occ == EntityRef.None) continue;
                    if (!f.Unsafe.TryGetPointer<Combatant>(occ, out Combatant* crossedEnemy)) continue;
                    if (crossedEnemy->PlayerIndex == combatant->PlayerIndex) continue; // skip allie/self
                    if (crossedEnemy->HP <= 0) continue;
                    int hpBeforePAD = crossedEnemy->HP;
                    crossedEnemy->HP -= SpellRegistry.PasAuDelaDorsalDamage;
                    if (crossedEnemy->HP < 0) crossedEnemy->HP = 0;
                    crossedEnemy->DamageTakenThisRound += SpellRegistry.PasAuDelaDorsalDamage;
                    Log.Info($"[Pas de l'Au-Dela] Ghostra P{combatant->PlayerIndex} traverse P{crossedEnemy->PlayerIndex} en ({cx},{cy}) : -{SpellRegistry.PasAuDelaDorsalDamage} HP dorsal gratuit (HP {hpBeforePAD} -> {crossedEnemy->HP})");
                }
            }


            // PATCH 22 mai (test designer) — Pieges declenches AU PASSAGE, pas seulement a
            // l'arret. Bible V7.1 : "Toute unite ennemie qui ENTRE" sur la case (Filet l.498,
            // Champ de Mines l.505). On itere les cases INTERMEDIAIRES du path dans l'ordre
            // (pathBuffer[0..pathLength-2] ; start exclu, destination au dernier index geree
            // juste apres) et on declenche tout piege rencontre. Si le combattant meurt en
            // cours de route (trap qui le tue), on stoppe la chaine.
            //
            // Note : Manhattan==1 -> pathBuffer null, boucle skip ; seule la destination est
            // une case entree, geree par l'appel ci-dessous.
            if (pathBuffer != null && pathLength > 1)
            {
                for (int i = 0; i < pathLength - 1; i++)
                {
                    if (combatant->HP <= 0) break;
                    int crossingIdx = pathBuffer[i];
                    int cx = crossingIdx % GridConstants.Width;
                    int cy = crossingIdx / GridConstants.Width;
                    FogHelpers.TryTriggerTrapOnEnter(f, entity, combatant, cx, cy, currentTurn);
                }
            }

            // 2.15.b — Trigger trap eventuel sur la case d'arrivee (Filet de Ronces, Mine).
            // L'helper gere damage + Empreinte + MovementMalus + Clear trap + +1 PR au owner.
            if (combatant->HP > 0)
            {
                FogHelpers.TryTriggerTrapOnEnter(f, entity, combatant, targetX, targetY, currentTurn);
            }

            // Refonte 29 mai — Brume Toxique entry SIMPLIFIÉE : plus de dégâts d'entrée. Entrer
            // sur une case BrumeToxique pose juste +1 marque venin. Skip Necram (immunisé).
            if (combatant->HP > 0
                && combatant->Class != NymoraClass.Necram
                && GridHelpers.GetTerrainKind(f, targetX, targetY) == TerrainKind.BrumeToxique)
            {
                VeninHelpers.ApplyMark(f, combatant, SpellRegistry.BrumeToxiqueMarksOnHit, currentTurn);
                Log.Info($"[Movement] Brume Toxique entry : +{SpellRegistry.BrumeToxiqueMarksOnHit} marque venin sur P{combatant->PlayerIndex} ({targetX},{targetY})");
            }
        }
    }
}
