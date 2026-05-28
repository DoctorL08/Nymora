namespace Quantum
{
    using Photon.Deterministic;

    /// <summary>
    /// AI brain Nymora (Bloc E Phase 2).
    ///
    /// 2.16.a.i — squelette : EndTurn auto si bot actif (delai 30 ticks).
    /// 2.16.a.ii — ajoute TryGreedyMove : bot s'approche de l'ennemi (PM all-in).
    /// 2.16.a.iii — ajoute TryGreedyCast : boucle de casts offensifs. Le bot enumere
    ///              les sorts Soulrender, filtre PA/HG/range. **Easy** : pick aleatoire
    ///              via f.RNG, skip signature, HGSpend=0, cap 2 casts. **Medium** :
    ///              greedy max-score via AIEvaluator.EstimateSpellDamage, signature
    ///              autorisee (cooldown 4t), HG optionnel maximise, cap PA naturel.
    /// 2.16.b — IA Medium activable via AIConstants.CurrentDifficulty.
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

            // 4.14.b — Early-out si mode PvP (33_CombatCasual / ranked). En PvP les
            // 2 slots sont des humains, AISystem ne doit jamais agir sinon il prend
            // le controle du slot P1 en plein match. Int32 0/1 cote sim (Quantum .qtn
            // ne supporte pas Bool primitive).
            if (state->IsBotMatch == 0) return;

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

            // 2.16.c.v — Pacing des actions du bot pour visibilite "vrai joueur".
            //   tick 0          : TryGreedyMove
            //   tick N*interval : 1 cast par interval (interval = ActionIntervalTicks)
            //   apres N casts   : delai puis EndTurn
            // Si un cast renvoie "rien a caster", on saute directement au EndTurn delay.
            int totalDuration = TurnConstants.GetTurnDurationTicks(f);
            int elapsed = totalDuration - state->TurnTimerTicks;
            int interval = AIConstants.ActionIntervalTicks;

            bool isMedium = AIConstants.CurrentDifficulty == AIDifficulty.Medium;
            int maxCasts = isMedium ? AIConstants.MaxCastsPerTurnMedium : AIConstants.MaxCastsPerTurnEasy;

            // Phase MOVE : petit délai (BotFirstMoveDelayTicks) au lieu de tick 0, pour laisser le
            // CombatantRenderer poser la vue du bot sur sa case de SPAWN avant qu'il se déplace
            // (sinon la vue, créée paresseusement en IA, apparaît directement sur la case d'arrivée
            // ≈ milieu — cf AIConstants.BotFirstMoveDelayTicks).
            if (elapsed == AIConstants.BotFirstMoveDelayTicks)
            {
                TryGreedyMove(f, botEntity, bot);
                return; // attend le prochain interval pour le 1er cast
            }

            // Phase CAST : 1 cast par intervalle, jusqu'a maxCasts. castSlot 1..maxCasts.
            // Detection precise du tick d'intervalle (elapsed == 1*interval, 2*interval, ...).
            if (elapsed % interval == 0)
            {
                int castSlot = elapsed / interval; // 1, 2, 3, ...
                if (castSlot >= 1 && castSlot <= maxCasts)
                {
                    bool casted = TryGreedyCastSingle(f, botEntity, bot, state);
                    if (!casted)
                    {
                        // Plus aucun sort affordable -> fin de tour anticipee apres delai.
                        Log.Info($"[AI] Bot P{state->ActivePlayerIndex} pas de cast au slot {castSlot}, fin de tour");
                        EndBotTurn(f, state, bot);
                        return;
                    }
                }
            }

            // Phase END TURN : apres le dernier slot de cast + delai final.
            int lastActionTick = (1 + maxCasts) * interval; // move + maxCasts casts
            if (elapsed >= lastActionTick + AIConstants.BotEndTurnDelayTicks)
            {
                EndBotTurn(f, state, bot);
            }
        }

        private static void EndBotTurn(Frame f, CombatState* state, Combatant* bot)
        {
            Log.Info($"[AI] Bot P{state->ActivePlayerIndex} termine son tour (PA restant {bot->PA}, PM restant {bot->PM})");
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
            // 3.7.a.i.0 — Update Facing du bot apres son deplacement IA (sinon target.Facing
            // du bot reste sur sa valeur initiale, et le check dorsal Ghostra est faussé).
            bot->Facing = FacingHelpers.FacingFromGridDelta(bestX - startX, bestY - startY);
            GridHelpers.SetOccupant(f, bestX, bestY, botEntity);

            int currentTurn = f.TryGetSingleton<CombatState>(out var st) ? st.TurnNumber : 0;
            FogHelpers.TryTriggerTrapOnEnter(f, botEntity, bot, bestX, bestY, currentTurn);
        }

        /// <summary>
        /// 2.16.c.v — Tente UN SEUL cast offensif. Retourne true si un sort a ete
        /// lance, false sinon (pas de spell affordable / pas d'ennemi vivant).
        ///
        /// Appele par AISystem.Update a intervalles reguliers (ActionIntervalTicks)
        /// pour espacer visuellement les casts du bot. La boucle multi-cast historique
        /// (2.16.a.iii) est remontee dans AISystem.Update via la sequence elapsed-based.
        ///
        /// Decks par difficulte :
        ///   Easy   : SoulrenderEasyDeck (OuvrePlaie, Curee + utility non offensifs)
        ///   Medium : SoulrenderMediumDeck (OuvrePlaie, ChargeBrutale, Curee + utility)
        ///
        /// Strategy :
        ///   Easy   : random pick via f.RNG (erreurs credibles)
        ///   Medium : greedy max-score via EstimateSpellDamage
        /// HGSpend toujours 0 (HG optionnel exclu pour Easy + Medium, cf brique 2.16.b).
        /// </summary>
        private static bool TryGreedyCastSingle(Frame f, EntityRef botEntity, Combatant* bot, CombatState* state)
        {
            if (bot->PA <= 0) return false;

            // Find primary enemy (single in 1v1).
            int enemyX, enemyY;
            if (!TryFindEnemyPosition(f, bot, out enemyX, out enemyY))
            {
                Log.Info($"[AI] Bot P{bot->PlayerIndex} : plus d'ennemi vivant, fin de cast");
                return false;
            }

            // Buffer stack pour les sorts affordables ce tick (max 16 = nb sorts Soulrender).
            SpellId* affordable = stackalloc SpellId[16];
            int* affordableScore = stackalloc int[16];

            bool isMedium = AIConstants.CurrentDifficulty == AIDifficulty.Medium;
            SpellId[] deck = isMedium ? AIConstants.SoulrenderMediumDeck : AIConstants.SoulrenderEasyDeck;

            int affordableCount = 0;
            for (int di = 0; di < deck.Length; di++)
            {
                SpellId spellId = deck[di];
                if (!SpellRegistry.TryGet(spellId, out var def)) continue;
                if (def.IsOffensive == 0) continue;
                if (def.Filter != TargetingFilter.Enemy && def.Filter != TargetingFilter.AnyTile) continue;

                // Budget PA + HG mandatory.
                if (bot->PA < def.PACost) continue;
                if (bot->Resource < def.HGCostMandatory) continue;

                // Skip OncePerMatch.
                if (def.OncePerMatchBit != SpellRegistry.OncePerMatchBitNone) continue;

                // Cooldown Ame Laceree.
                if (spellId == SpellId.SoulrenderAmeLaceree)
                {
                    int turnsSince = state->TurnNumber - bot->LastAmeLaceeUsedOnTurn;
                    if (turnsSince < SpellRegistry.AmeLaceeCooldownTurns) continue;
                }

                // Range Manhattan caster -> enemy.
                int dist = AIEvaluator.Manhattan(bot->GridX, bot->GridY, enemyX, enemyY);
                if (dist < def.RangeMin || dist > def.RangeMax) continue;

                affordable[affordableCount] = spellId;
                affordableScore[affordableCount] = AIEvaluator.EstimateSpellDamage(spellId, hgSpend: 0, def.HGCostMandatory);
                affordableCount++;
            }

            if (affordableCount == 0) return false;

            // Selection strategy.
            int pickedIdx;
            if (isMedium)
            {
                pickedIdx = 0;
                int bestScore = affordableScore[0];
                for (int i = 1; i < affordableCount; i++)
                {
                    if (affordableScore[i] > bestScore)
                    {
                        bestScore = affordableScore[i];
                        pickedIdx = i;
                    }
                }
            }
            else
            {
                pickedIdx = f.RNG->Next(0, affordableCount);
            }

            SpellId picked = affordable[pickedIdx];
            int pickedScore = affordableScore[pickedIdx];

            Log.Info($"[AI] Bot P{bot->PlayerIndex} ({AIConstants.CurrentDifficulty}) cast {picked} sur ({enemyX},{enemyY}) estimDmg={pickedScore} (choisi {pickedIdx + 1}/{affordableCount})");

            // Reuse meme pipeline que les commands clients.
            var cmd = new CastSpellCommand
            {
                Spell = picked,
                TargetX = enemyX,
                TargetY = enemyY,
                HGSpend = 0,
            };
            SpellSystem.TryCastSpell(f, bot->PlayerIndex, cmd, state->ActivePlayerIndex);

            return true;
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
