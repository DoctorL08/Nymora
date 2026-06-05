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
        // trapDir (refonte 29 mai) : direction d'éjection du Piège Bondissant (0 = aucune pour les
        //   pièges normaux ; 1=+X 2=-X 3=+Y 4=-Y). Optionnel -> calls existants inchangés.
        public static void PlaceTrap(Frame f, int x, int y, TrapKind kind, int ownerPlayer, int currentTurn, byte trapDir = 0)
        {
            if (!GridHelpers.InBounds(x, y)) return;
            if (kind == TrapKind.None)
            {
                ClearTrap(f, x, y);
                return;
            }
            // Fix 2 juin — pas d'embuche sur une case occupee par un COMBATTANT (joueur), un OBSTACLE
            // (Pilier/Mur/Faille) ou un LEURRE Ghostra. Garde par-case : couvre les poses directes
            // (Filet de Ronces, Piege Bondissant), les clusters (Champ de Mines saute la case) et les
            // poses secondaires (Filet derriere la cible). Symetrique du garde SpawnObstacle.
            if (GridHelpers.GetOccupant(f, x, y) != EntityRef.None) { Log.Warn($"[Trap] pose rejetee : case ({x},{y}) occupee par un combattant"); return; }
            if (ObstacleHelpers.HasObstacleAt(f, x, y)) { Log.Warn($"[Trap] pose rejetee : case ({x},{y}) porte un obstacle"); return; }
            if (DecoyHelpers.HasAnyDecoyAt(f, x, y)) { Log.Warn($"[Trap] pose rejetee : case ({x},{y}) porte un leurre"); return; }
            // #12 (5 juin) — INTERDIT de poser sur une case qui porte DÉJÀ un piège (own ou adverse) :
            //   plus d'écrasement (decision Lorenzo). Une case = un seul piège. Le Champ de Mines saute
            //   simplement les cases déjà piégées (pose partielle, comme pour occupant/obstacle/leurre).
            if (GetTrapKind(f, x, y) != TrapKind.None) { Log.Warn($"[Trap] pose rejetee : case ({x},{y}) porte deja un piege"); return; }
            var fog = f.Unsafe.GetPointerSingleton<FogSingleton>();
            int idx = GridHelpers.Index(x, y);
            fog->Tiles[idx].Trap = kind;
            fog->Tiles[idx].TrapOwner = ownerPlayer;
            fog->Tiles[idx].TrapAppliedOnTurn = currentTurn;
            fog->Tiles[idx].TrapDir = trapDir;

            // Refonte 29 mai — les pièges N'UTILISENT PLUS le voile. Leur visibilité est purement
            //   une décision de RENDU (TrapView) évaluée en continu sur la phase ACTUELLE du Nightseer :
            //   visibles par défaut, invisibles (rien du tout, pas de brouillard) tant que le NS est
            //   en phase 3 (PR 5) — y compris les pièges posés avant d'atteindre 5/5.
            // + économie PR : +1 PR au poseur Nightseer (cap +3/tour).
            NightseerPassif.GainPrescienceForPlayer(f, ownerPlayer, currentTurn, "piège posé");
        }

        public static void ClearTrap(Frame f, int x, int y)
        {
            if (!GridHelpers.InBounds(x, y)) return;
            var fog = f.Unsafe.GetPointerSingleton<FogSingleton>();
            int idx = GridHelpers.Index(x, y);
            fog->Tiles[idx].Trap = TrapKind.None;
            fog->Tiles[idx].TrapOwner = 0;
            fog->Tiles[idx].TrapAppliedOnTurn = 0;
            fog->Tiles[idx].TrapDir = 0;
        }

        public static byte GetTrapDir(Frame f, int x, int y)
        {
            if (!GridHelpers.InBounds(x, y)) return 0;
            var fog = f.Unsafe.GetPointerSingleton<FogSingleton>();
            return fog->Tiles[GridHelpers.Index(x, y)].TrapDir;
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

        // #23 (5 juin) — Owner du TERRAIN (Vapeur Carmin / Sang Coagulé / Brume Toxique) pour
        //   l'affichage (outline d'équipe en match miroir). Convention PlayerIndex+1 (0 = aucun),
        //   comme VeiledByPlayer. Écrit par GridHelpers.SetTerrain ; la sim ne lit jamais ce champ.
        public static void SetTerrainOwner(Frame f, int x, int y, int ownerPlayerIndex)
        {
            if (!GridHelpers.InBounds(x, y)) return;
            var fog = f.Unsafe.GetPointerSingleton<FogSingleton>();
            fog->Tiles[GridHelpers.Index(x, y)].TerrainOwner =
                ownerPlayerIndex < 0 ? (byte)0 : (byte)(ownerPlayerIndex + 1);
        }

        /// <summary>Owner du terrain sur la case (PlayerIndex), ou -1 si aucun owner enregistré.
        /// Ne teste PAS la présence d'un terrain : l'appelant (View) vérifie d'abord GetTerrainKind.</summary>
        public static int GetTerrainOwner(Frame f, int x, int y)
        {
            if (!GridHelpers.InBounds(x, y)) return -1;
            var fog = f.Unsafe.GetPointerSingleton<FogSingleton>();
            byte raw = fog->Tiles[GridHelpers.Index(x, y)].TerrainOwner;
            return raw == 0 ? -1 : raw - 1;
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

        // `depth` (fix 2 juin) : profondeur de récursion du chaînage de catapultes (Piège Bondissant).
        //   Les appels externes (MovementSystem/SpellSystem/AISystem) passent 0 ; seule la ré-entrance
        //   interne incrémente. Au-delà du cap on stoppe net pour interdire toute boucle infinie
        //   (ex. deux Bondissants qui se renvoient la cible) -> plus de StackOverflow / desync.
        public static bool TryTriggerTrapOnEnter(Frame f, EntityRef enterer, Combatant* entererC,
            int x, int y, int currentTurn, int depth = 0)
        {
            if (depth > Quantum.SpellRegistry.PiegeBondissantMaxChainDepth)
            {
                Log.Warn($"[Trap] Chaîne de catapultes coupée à la profondeur {depth} (cap {Quantum.SpellRegistry.PiegeBondissantMaxChainDepth}) -> anti-boucle.");
                return false;
            }
            TrapKind trap = GetTrapKind(f, x, y);
            if (trap == TrapKind.None) return false;
            int trapOwner = GetTrapOwner(f, x, y);
            if (trapOwner < 0) return false;
            if (trapOwner == entererC->PlayerIndex) return false; // pas son propre trap

            // Refonte 29 mai — PIÈGE BONDISSANT : éjecte l'enterer de 3 cases dans la direction stockée
            //   (catapulte), pas de dégâts. Applique Traqué + clear + PR. L'éjection part de la position
            //   COURANTE de l'enterer (move déjà appliqué -> pas de ré-entrance).
            if (trap == TrapKind.Bondissant)
            {
                byte dir = GetTrapDir(f, x, y);
                int edx = 0, edy = 0;
                switch (dir)
                {
                    case 1: edx = 1; break;
                    case 2: edx = -1; break;
                    case 3: edy = 1; break;
                    case 4: edy = -1; break;
                }
                // FALLBACK (refonte 29 mai) : si aucune direction valide n'a été stockée (2e clic
                //   ambigu / non capturé -> TrapDir=0), éjecte l'enterer LOIN du propriétaire du
                //   piège. Garantit que la catapulte pousse TOUJOURS (fix "parfois ça pousse pas").
                if (edx == 0 && edy == 0)
                {
                    var ownerLookup = f.Filter<Combatant>();
                    while (ownerLookup.NextUnsafe(out EntityRef _, out Combatant* ow))
                    {
                        if (ow->PlayerIndex != trapOwner) continue;
                        int ddx = entererC->GridX - ow->GridX;
                        int ddy = entererC->GridY - ow->GridY;
                        int aDx = ddx < 0 ? -ddx : ddx;
                        int aDy = ddy < 0 ? -ddy : ddy;
                        if (aDx >= aDy) edx = ddx >= 0 ? 1 : -1;
                        else            edy = ddy >= 0 ? 1 : -1;
                        break;
                    }
                    if (edx == 0 && edy == 0) edx = 1; // ultime défaut (owner sur même case, improbable)
                    Log.Info($"[Trap] Piège Bondissant : pas de direction stockée -> fallback éjection loin du propriétaire (edx={edx}, edy={edy})");
                }
                {
                    // Lancement DEPUIS la case du piège (la catapulte), pas depuis l'arrivée du move.
                    //   On libère d'abord la case courante de l'enterer (il a pu passer/finir ailleurs)
                    //   pour ne pas bloquer le calcul sur lui-même, puis on projette depuis (x,y).
                    GridHelpers.SetOccupant(f, entererC->GridX, entererC->GridY, EntityRef.None);
                    int curX = x, curY = y;
                    int landed = 0;
                    for (int s = 0; s < Quantum.SpellRegistry.PiegeBondissantEjectDist; s++)
                    {
                        int nx = curX + edx, ny = curY + edy;
                        if (!GridHelpers.InBounds(nx, ny)) break;
                        if (!GridHelpers.IsWalkable(f, nx, ny)) break;
                        if (ObstacleHelpers.HasObstacleAt(f, nx, ny)) break;
                        if (GridHelpers.GetOccupant(f, nx, ny) != EntityRef.None) break;
                        curX = nx; curY = ny; landed++;
                    }
                    entererC->GridX = curX; entererC->GridY = curY;
                    entererC->Facing = FacingHelpers.FacingFromGridDelta(edx, edy);
                    GridHelpers.SetOccupant(f, curX, curY, enterer);
                    // Signal View : rendre l'éjection comme un LANCEMENT (dash), pas un walk.
                    entererC->LastEjectedSequence += 1;
                    Log.Info($"[Trap] Piège Bondissant ({x},{y}) éjecte P{entererC->PlayerIndex} de {landed} cases (dir {dir}) -> ({curX},{curY})");

                    // FIX 2 juin — consommer CE piège AVANT de rejouer la trajectoire. Sans ça, deux
                    //   Pièges Bondissants face à face se renvoyaient la cible à l'infini (la récursion
                    //   ci-dessous re-rentrait sur ce piège encore présent) -> StackOverflow + halt sim.
                    //   Un piège consommé ne peut plus se re-déclencher pendant la même résolution.
                    ClearTrap(f, x, y);
                    ClearVeil(f, x, y);

                    // FIX 30 mai — pièges déclenchés AU PASSAGE sur la trajectoire de CATAPULTE.
                    //   La projection ci-dessus survole jusqu'à `landed` cases sans rien déclencher :
                    //   un autre piège (Filet/Mine/Bondissant) sur la trajectoire d'éjection était
                    //   ignoré (« la poussée passe dessus et rien ne se passe »). On rejoue les cases
                    //   survolées depuis le piège (x,y exclu, déjà en cours de traitement) jusqu'à
                    //   l'atterrissage inclus, et on déclenche chaque piège rencontré.
                    //   `depth + 1` : cap anti-boucle (cf garde en tête de méthode).
                    int fxc = x, fyc = y;
                    for (int s = 1; s <= landed; s++)
                    {
                        if (entererC->HP <= 0) break;
                        fxc += edx; fyc += edy;
                        TryTriggerTrapOnEnter(f, enterer, entererC, fxc, fyc, currentTurn, depth + 1);
                    }
                }
                MarkHelpers.ApplyMark(entererC, MarkKind.Traque,
                    Quantum.SpellRegistry.ChampDeMinesEmpreinteTurns, trapOwner, currentTurn);
                var bondFilter = f.Filter<Combatant>();
                while (bondFilter.NextUnsafe(out EntityRef _, out Combatant* bc))
                {
                    if (bc->PlayerIndex == trapOwner) { bc->LastTrapTriggeredOnTurn = currentTurn; break; }
                }
                NightseerPassif.GainPrescienceForPlayer(f, trapOwner, currentTurn, "piège bondissant déclenché");
                return true;
            }

            int dmg = trap == TrapKind.FiletRonces
                ? Quantum.SpellRegistry.FiletDeRoncesDmg
                : Quantum.SpellRegistry.ChampDeMinesDmg;

            // Refonte 29 mai — Passif phasé P1+ : +15% dégâts des pièges si le Nightseer proprietaire
            //   est en phase >= 1 (PR >= 1). Lookup du owner pour lire sa Prescience.
            {
                var ownerFilter = f.Filter<Combatant>();
                while (ownerFilter.NextUnsafe(out EntityRef _, out Combatant* owner))
                {
                    if (owner->PlayerIndex != trapOwner) continue;
                    if (owner->Class == NymoraClass.Nightseer
                        && NightseerPassif.TrapDamageBonusActive(owner->Resource))
                    {
                        dmg += dmg * NightseerPassif.TrapDamageBonusPct / 100;
                    }
                    break;
                }
            }

            int hpBefore = entererC->HP;
            entererC->HP -= dmg;
            if (entererC->HP < 0) entererC->HP = 0;
            entererC->DamageTakenThisRound += dmg;
            Log.Info($"[Trap] {trap} declenche sur P{entererC->PlayerIndex} ({x},{y}) : -{dmg} HP ({hpBefore} -> {entererC->HP})");

            // Refonte 29 mai — marque unique TRAQUÉ (Empreinté supprimé). Le piège applique Traqué.
            int markTurns = trap == TrapKind.FiletRonces
                ? Quantum.SpellRegistry.FiletDeRoncesEmpreinteTurns
                : Quantum.SpellRegistry.ChampDeMinesEmpreinteTurns;
            MarkHelpers.ApplyMark(entererC, MarkKind.Traque, markTurns, trapOwner, currentTurn);

            // -2 PM si Filet de Ronces (MovementMalus 1 tour).
            if (trap == TrapKind.FiletRonces)
            {
                StatusHelper.Apply(entererC, StatusKind.MovementMalus,
                    magnitude: Quantum.SpellRegistry.FiletDeRoncesPMReduce, turnsLeft: 1, currentTurn);
            }

            // Consume trap + voile lie.
            ClearTrap(f, x, y);
            ClearVeil(f, x, y);

            // Refonte 29 mai — Champ de Mines CHAÎNE : déclencher une Mine détonne les mines proches
            //   du même owner (cluster, Manhattan <= 2) sur l'enterer : +40 chacune, cap 2 chaînées.
            //   Total typique 70 + 40 + 40 = 150.
            if (trap == TrapKind.Mine)
            {
                int chained = 0;
                int r = Quantum.SpellRegistry.ChampDeMinesChainRadius;
                for (int oy = y - r; oy <= y + r && chained < Quantum.SpellRegistry.ChampDeMinesChainMax; oy++)
                {
                    for (int ox = x - r; ox <= x + r && chained < Quantum.SpellRegistry.ChampDeMinesChainMax; ox++)
                    {
                        if (ox == x && oy == y) continue;
                        if (GetTrapKind(f, ox, oy) != TrapKind.Mine) continue;
                        if (GetTrapOwner(f, ox, oy) != trapOwner) continue;

                        int chainDmg = Quantum.SpellRegistry.ChampDeMinesChainDmg;
                        int hbChain = entererC->HP;
                        entererC->HP -= chainDmg;
                        if (entererC->HP < 0) entererC->HP = 0;
                        entererC->DamageTakenThisRound += chainDmg;
                        MarkHelpers.ApplyMark(entererC, MarkKind.Traque,
                            Quantum.SpellRegistry.ChampDeMinesEmpreinteTurns, trapOwner, currentTurn);
                        ClearTrap(f, ox, oy);
                        ClearVeil(f, ox, oy);
                        NightseerPassif.GainPrescienceForPlayer(f, trapOwner, currentTurn, "mine chaînée");
                        Log.Info($"[Trap] Mine CHAÎNÉE ({ox},{oy}) sur P{entererC->PlayerIndex} : -{chainDmg} HP ({hbChain}->{entererC->HP})");
                        chained++;
                    }
                }
            }

            // Tracking LastTrapTriggeredOnTurn (Seve Sauvage bonus heal) — tous casters.
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                if (c->PlayerIndex != trapOwner) continue;
                c->LastTrapTriggeredOnTurn = currentTurn;
                break;
            }

            // Refonte 29 mai — économie PR : +1 PR au Nightseer (piège déclenché), cappé +3/tour.
            NightseerPassif.GainPrescienceForPlayer(f, trapOwner, currentTurn, "piège déclenché");
            return true;
        }
    }
}
