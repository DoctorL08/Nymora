namespace Quantum
{
    using Photon.Deterministic;

    /// <summary>
    /// AI brain Nymora (Bloc E Phase 2).
    ///
    /// 2.16.a.i — squelette : EndTurn auto si bot actif (delai 30 ticks).
    /// 2.16.a.ii — ajoute TryGreedyMove : bot s'approche de l'ennemi (PM all-in).
    /// 2.16.a.iii — ajoute TryGreedyCast : boucle de casts offensifs. Le bot enumere
    ///              les sorts Soulrender (IsOffensive + Filter Enemy/AnyTile), filtre
    ///              PA/HG/range, et **pick au hasard via f.RNG** parmi les affordables
    ///              (vraie IA Easy : pas de strategie max-dmg). Skip signature Ame
    ///              Laceree pour eviter le burst 320 dgts qui one-turn le joueur en
    ///              fin de match. Recommence tant que PA permet. Pacte/Self/heals
    ///              ignores. AIEvaluator.EstimateSpellDamage reste dispo pour la
    ///              future IA Medium (2.16.b) qui repassera en greedy max-score.
    ///
    /// Pourquoi pas un MoveCommand simule ? Les DeterministicCommand viennent de
    /// l'input client. Une IA simu-side mute directement la primitive d'etat
    /// (GridX/Y, PM, SetOccupant) — meme effet que MovementSystem.ApplyMove. Le
    /// trigger trap (FogHelpers.TryTriggerTrapOnEnter) est appele en post-move
    /// pour conserver les semantiques marques/pieges Nightseer.
    ///
    /// Ordre des systems : AISystem AVANT TurnSystem dans SystemSetup pour que
    /// TurnTimerTicks=0 soit visible au meme tick par TickTurnActive.
    /// </summary>
    public unsafe class AISystem : SystemMainThread
    {
        public override void Update(Frame f)
        {
            var state = f.Unsafe.GetPointerSingleton<CombatState>();

            // L'IA n'agit que quand son joueur est actif ET le tour est en cours.
            if (state->CurrentPhase != CombatPhase.TurnActive) return;
            if (state->ActivePlayerIndex != AIConstants.BotPlayerIndex) return;

            // Recupere l'entity du bot (1 seul en 1v1). On extrait juste l'EntityRef
            // dans le filter pour eviter de garder un Combatant* vivant a travers
            // d'autres appels (pattern miroir de MovementSystem.TryMoveCombatant).
            EntityRef botEntity = EntityRef.None;
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef e, out Combatant* c))
            {
                if (c->PlayerIndex == AIConstants.BotPlayerIndex)
                {
                    botEntity = e;
                    break;
                }
            }
            if (botEntity == EntityRef.None) return;

            var bot = f.Unsafe.GetPointer<Combatant>(botEntity);
            if (bot->HP <= 0) return; // bot mort, TurnSystem gere MatchEnd

            // Actions (move + casts) declenchees UNIQUEMENT au 1er tick de TurnActive
            // (elapsed == 0). Sans ce gate, TryGreedyCast tournerait chaque tick et le
            // bot pourrait spammer 5-10 casts par tour, one-turn le joueur. AIConstants.
            // MaxCastsPerTurn capote en plus le nb de casts pour balance Easy.
            int totalDuration = TurnConstants.GetTurnDurationTicks(f);
            int elapsed = totalDuration - state->TurnTimerTicks;
            if (elapsed == 0)
            {
                TryGreedyMove(f, botEntity, bot);
                TryGreedyCast(f, botEntity, bot, state);
            }

            // Phase delay : end turn apres BotEndTurnDelayTicks ecoules. Laisse au
            // joueur humain le temps de voir le move + l'etat avant de passer au sien.
            if (elapsed < AIConstants.BotEndTurnDelayTicks) return;

            Log.Info($"[AI] Bot P{state->ActivePlayerIndex} termine son tour (PM restant {bot->PM})");
            state->TurnTimerTicks = 0;
        }

        /// <summary>
        /// Selectionne la meilleure case de destination dans la portee PM du bot et
        /// applique le move directement. No-op si PM=0 ou si aucune case ne fait
        /// strictement mieux que rester sur place.
        ///
        /// Algorithme :
        ///   1. Enumere toutes les cases (dx, dy) avec |dx| + |dy| <= PM
        ///   2. Filtre : InBounds + Walkable + non occupee
        ///   3. Verifie le chemin via A* (path length <= PM)
        ///   4. Ajoute le cout Vapeur Carmin (+1 PM sur case arrivee)
        ///   5. Score la destination via AIEvaluator.ScoreMoveDestination
        ///   6. Garde le meilleur (tie-break index grille croissant)
        ///   7. Applique le move + trigger trap si meilleur que stay-still
        /// </summary>
        private static void TryGreedyMove(Frame f, EntityRef botEntity, Combatant* bot)
        {
            if (bot->PM <= 0) return;

            int startX = bot->GridX;
            int startY = bot->GridY;
            int maxRange = bot->PM;

            int currentScore = AIEvaluator.ScoreMoveDestination(f, bot, startX, startY);

            int bestScore = currentScore;
            int bestX = startX;
            int bestY = startY;
            int bestCost = 0;
            int bestIdx = GridHelpers.Index(startX, startY);

            int* pathBuf = stackalloc int[GridConstants.Count];

            for (int dy = -maxRange; dy <= maxRange; dy++)
            {
                int absDy = dy < 0 ? -dy : dy;
                for (int dx = -maxRange; dx <= maxRange; dx++)
                {
                    int absDx = dx < 0 ? -dx : dx;
                    int manhattan = absDx + absDy;
                    if (manhattan == 0) continue;
                    if (manhattan > maxRange) continue;

                    int tx = startX + dx;
                    int ty = startY + dy;
                    if (!GridHelpers.InBounds(tx, ty)) continue;
                    if (!GridHelpers.IsWalkable(f, tx, ty)) continue;
                    if (GridHelpers.GetOccupant(f, tx, ty) != EntityRef.None) continue;

                    int extraCost = GridHelpers.GetTerrainKind(f, tx, ty) == TerrainKind.VapeurCarmin ? 1 : 0;
                    if (manhattan + extraCost > maxRange) continue;

                    if (!AStarPathfinder.TryFindPath(f, startX, startY, tx, ty, maxRange, pathBuf, out int pathLen))
                        continue;

                    int totalCost = pathLen + extraCost;
                    if (totalCost > maxRange) continue;

                    int score = AIEvaluator.ScoreMoveDestination(f, bot, tx, ty);
                    int idx = GridHelpers.Index(tx, ty);

                    if (score > bestScore || (score == bestScore && idx < bestIdx))
                    {
                        bestScore = score;
                        bestX = tx;
                        bestY = ty;
                        bestCost = totalCost;
                        bestIdx = idx;
                    }
                }
            }

            if (bestX == startX && bestY == startY) return;
            if (bestScore <= currentScore) return;

            Log.Info($"[AI] Bot P{bot->PlayerIndex} se deplace ({startX},{startY}) -> ({bestX},{bestY}) cost={bestCost} score={bestScore}");
            GridHelpers.SetOccupant(f, startX, startY, EntityRef.None);
            bot->GridX = bestX;
            bot->GridY = bestY;
            bot->PM -= bestCost;
            GridHelpers.SetOccupant(f, bestX, bestY, botEntity);

            int currentTurn = f.TryGetSingleton<CombatState>(out var st) ? st.TurnNumber : 0;
            FogHelpers.TryTriggerTrapOnEnter(f, botEntity, bot, bestX, bestY, currentTurn);
        }

        /// <summary>
        /// Boucle de casts greedy : tant que le bot peut affordable un sort offensif
        /// qui touche l'ennemi, cast le meilleur (= degats estimes les plus eleves)
        /// et recommence. Stop quand plus aucun sort ne passe ou ennemi mort.
        ///
        /// Sorts ignores pour l'IA Easy :
        ///   - Self filter (Pacte, Rugissement, Rage, Riposte, Cauter, Peau, Sève, Dernier)
        ///   - IsOffensive == 0 (Marque de Carnage, Empoignade) — pas de degats directs
        ///   - OncePerMatch (Pacte de Sang, Dernier Souffle) — preserve pour Phase 3
        ///
        /// Sorts utilises (Soulrender 2.16.a.iii) :
        ///   - Tranche-Ame  (10, 220 dgts, range 1, 3 PA)
        ///   - Ouvre-Plaie  (11, 110/230 dgts, range 1, 2 PA, +1 HG optional)
        ///   - Charge Brutale (12, 180 dgts, range 5 ligne, 4 PA — AnyTile)
        ///   - Detonation Sanglante (13, 60+40*HG, range 4 croix 3, 4 PA, 2 HG min — AnyTile)
        ///   - Curee        (14, 150 dgts, range 2, 2 PA, 2 HG mandatory)
        ///   - Ame Laceree  (25, 320 dgts, range 1, 2 PA, 5 HG mandatory, cooldown 4 tours)
        /// </summary>
        private static void TryGreedyCast(Frame f, EntityRef botEntity, Combatant* bot, CombatState* state)
        {
            // Find primary enemy (single in 1v1). Sera re-query apres chaque cast
            // au cas ou l'enemy aurait bouge (Empoignade pull) ou serait mort.
            int enemyX, enemyY;
            if (!TryFindEnemyPosition(f, bot, out enemyX, out enemyY)) return;

            // Buffer stack pour les sorts affordables ce tick (max 16 = nb sorts Soulrender).
            SpellId* affordable = stackalloc SpellId[16];

            for (int iter = 0; iter < AIConstants.MaxCastsPerTurn; iter++)
            {
                if (bot->PA <= 0) break;

                int affordableCount = 0;

                // Plage Soulrender = SpellId 10-25 (cf Spell.qtn).
                // SKIP signature Ame Laceree (25) — IA Easy : pas de burst 320 dgts.
                for (byte sb = 10; sb < 25; sb++)
                {
                    SpellId spellId = (SpellId)sb;
                    if (!SpellRegistry.TryGet(spellId, out var def)) continue;
                    if (def.IsOffensive == 0) continue;
                    if (def.Filter != TargetingFilter.Enemy && def.Filter != TargetingFilter.AnyTile) continue;

                    // Budget PA + HG mandatory.
                    if (bot->PA < def.PACost) continue;
                    if (bot->Resource < def.HGCostMandatory) continue;

                    // Skip OncePerMatch (Pacte, Dernier Souffle) — reserves IA Phase 3.
                    if (def.OncePerMatchBit != SpellRegistry.OncePerMatchBitNone) continue;

                    // Range Manhattan caster -> enemy cell. Pour AnyTile (Charge Brutale,
                    // Detonation Sanglante) on cible toujours la case ennemie ce qui maximise
                    // les degats (centre AoE / 1ere cible ligne).
                    int dist = AIEvaluator.Manhattan(bot->GridX, bot->GridY, enemyX, enemyY);
                    if (dist < def.RangeMin || dist > def.RangeMax) continue;

                    affordable[affordableCount++] = spellId;
                }

                if (affordableCount == 0) break; // plus aucun cast viable

                // 2.16.a.iii — Pick aleatoire via Frame.RNG (le seul RNG autorise dans
                // la simulation Quantum, deterministe par design). C'est ce qui rend l'IA
                // "Easy" : pas de strategie max-dmg, juste un sort au hasard parmi les
                // dispos. Effet attendu : parfois Curée a 2 HG quand pas la peine, parfois
                // Ouvre-Plaie au lieu de Tranche-Âme. Erreurs crédibles.
                int pickedIdx = f.RNG->Next(0, affordableCount);
                SpellId picked = affordable[pickedIdx];
                byte hgSpend = 0; // IA Easy ne depense JAMAIS HG optionnel (cf 2.16.a.iii).

                Log.Info($"[AI] Bot P{bot->PlayerIndex} cast {picked} sur ({enemyX},{enemyY}) (random {pickedIdx + 1}/{affordableCount} affordables)");

                // Reuse meme pipeline que les commands clients : construit un
                // CastSpellCommand a la volee. SpellSystem.TryCastSpell est public
                // depuis 2.16.a.iii pour ce use case.
                var cmd = new CastSpellCommand
                {
                    Spell = picked,
                    TargetX = enemyX,
                    TargetY = enemyY,
                    HGSpend = hgSpend,
                };
                SpellSystem.TryCastSpell(f, bot->PlayerIndex, cmd, state->ActivePlayerIndex);

                // Re-query position ennemi : kill check (sort plus tot du loop) ou
                // pull/push (Empoignade, Bourrasque) qui aurait deplace la cible.
                if (!TryFindEnemyPosition(f, bot, out enemyX, out enemyY))
                {
                    Log.Info($"[AI] Bot P{bot->PlayerIndex} : plus d'ennemi vivant, fin de cast loop");
                    return;
                }
            }
        }

        /// <summary>
        /// Retourne la position du 1er ennemi vivant trouve. False si aucun.
        /// En 1v1 il n'y en a qu'un, mais le code generalise pour 2v2/3v3 plus tard.
        /// </summary>
        private static bool TryFindEnemyPosition(Frame f, Combatant* bot, out int x, out int y)
        {
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                if (c->PlayerIndex == bot->PlayerIndex) continue;
                if (c->HP <= 0) continue;
                x = c->GridX;
                y = c->GridY;
                return true;
            }
            x = 0;
            y = 0;
            return false;
        }
    }
}
