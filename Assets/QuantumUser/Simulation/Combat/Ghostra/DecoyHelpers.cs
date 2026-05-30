namespace Quantum
{
    /// <summary>
    /// 3.6 — Helpers leurres Ghostra (Bible V7.1 ressource RÉMANENCE).
    ///
    /// Modelisation : inline array&lt;DecoySlot&gt;[3] sur Combatant Ghostra (cf Decoy.qtn).
    /// Cap 3 garanti par taille tableau. Slot libre = Kind == DecoyKind.None.
    ///
    /// Conventions :
    ///   - Lifetime 4 ROUNDS (AMENDEMENT 16 mai 2026 Lorenzo : Bible originale disait 2 tours,
    ///     mais le combo Ghostra (Réplique → setup Angle → combo dorsal) n'avait pas le temps
    ///     de se mettre en place. Étendu à 4 rounds pour permettre le gameplay strategique.
    ///     Cf [[project-decoy-lifetime-amended-4-rounds]] memory).
    ///   - Skip-decrement au tour de spawn (decoy.SpawnedOnTurn == currentTurn).
    ///   - Expiration = currentTurn &gt; SpawnedOnTurn + LifetimeRounds.
    ///   - Standard : HP=0, 1-hit destroy par sort cible/AoE.
    ///   - Protective : HP=200 (Bible Réplique Protectrice), absorbe avant destroy.
    ///
    /// Cote View : DecoyView lit Decoys[i] et spawn une "fausse Ghostra" sprite-identique
    /// a la vraie. Quand Kind=None ou expire -> despawn cote View.
    /// </summary>
    public static unsafe class DecoyHelpers
    {
        public const int MaxDecoys = 3;            // verrouille — taille array Decoys
        public const int LifetimeRounds = 4;       // AMENDEMENT 16 mai 2026 (Bible orig "2 tours" -> 4 pour permettre combos)
        public const int ProtectiveLifetimeRounds = 3; // AMENDEMENT 16 mai 2026 (cap Réplique Protectrice : 3 rounds au lieu de 4 pour balance)
        public const int ProtectiveDecoyMaxHP = 200; // Bible Réplique Protectrice

        // Heal Ghostra owner sur destruction d'un leurre (Bible V7.1 par sort) :
        //   - Réplique Fantôme  : +40 HP si detruit, +80 HP si survit la duree complete
        //   - Réplique Protectr : +60 HP si detruit
        // 3.7.b.i : ces heals sont appliques inline dans DecoyHelpers (TickLifetime + DestroyByEnemyAction)
        // pour discriminer Kind.RepliqueFantome vs Standard (pas de heal) vs Protective.
        public const int RepliqueFantomeHealOnDestroy   = 40;  // detruit prematurement par action adverse
        public const int RepliqueFantomeHealOnExpire    = 80;  // survit naturellement LifetimeRounds
        public const int RepliqueProtectriceHealOnDestroy = 80; // AMENDEMENT 16 mai 2026 (cap Bible orig 60 -> 80 pour compenser nerf 40%->30% + 3 PA -> 4 PA)

        /// <summary>
        /// Compte les leurres actifs (Kind != None) du Ghostra. Cache aussi le resultat
        /// dans le champ Combatant si besoin (pour l'overlay), mais on recalcule chaque
        /// fois pour rester source-of-truth.
        /// </summary>
        public static int CountActive(Combatant* ghostra)
        {
            if (ghostra == null) return 0;
            int count = 0;
            for (int i = 0; i < MaxDecoys; i++)
            {
                if (ghostra->Decoys[i].Kind != DecoyKind.None) count++;
            }
            return count;
        }

        /// <summary>
        /// Tente de poser un leurre sur (posX, posY). Retourne true si pose, false sinon
        /// (cap 3 atteint OU case occupee par Ghostra elle-meme OU hors grille).
        /// </summary>
        public static bool TrySpawn(Frame f, Combatant* ghostra, int posX, int posY, DecoyKind kind, int currentTurn)
        {
            if (ghostra == null || ghostra->HP <= 0) return false;
            if (kind == DecoyKind.None) return false;
            if (!GridHelpers.InBounds(posX, posY))
            {
                Log.Warn($"[Decoy] TrySpawn rejete : ({posX},{posY}) hors grille");
                return false;
            }
            // Refuse case occupee par la vraie Ghostra (Bible : un leurre sur la meme case
            // qu'elle ne fait pas sens — visuel duplique).
            if (ghostra->GridX == posX && ghostra->GridY == posY)
            {
                Log.Warn($"[Decoy] TrySpawn rejete : case ({posX},{posY}) occupee par Ghostra elle-meme");
                return false;
            }
            // Refuse case deja occupee par un autre leurre du meme Ghostra.
            for (int i = 0; i < MaxDecoys; i++)
            {
                if (ghostra->Decoys[i].Kind != DecoyKind.None
                    && ghostra->Decoys[i].PosX == posX
                    && ghostra->Decoys[i].PosY == posY)
                {
                    Log.Warn($"[Decoy] TrySpawn rejete : case ({posX},{posY}) deja occupee par un autre leurre");
                    return false;
                }
            }
            // Refuse case occupee par un combattant (allie ou ennemi). Bible-strict :
            // un leurre est un clone visuel sur une case ; il ne peut pas se superposer
            // a une unite. La grille tracks seulement les vrais combattants (pas les leurres).
            if (GridHelpers.GetOccupant(f, posX, posY) != EntityRef.None)
            {
                Log.Warn($"[Decoy] TrySpawn rejete : case ({posX},{posY}) occupee par un combattant");
                return false;
            }
            // Refuse case occupee par un obstacle (Pilier / Mur Colossar).
            if (ObstacleHelpers.HasObstacleAt(f, posX, posY))
            {
                Log.Warn($"[Decoy] TrySpawn rejete : case ({posX},{posY}) occupee par un obstacle");
                return false;
            }

            // Trouve un slot libre.
            int freeSlot = -1;
            for (int i = 0; i < MaxDecoys; i++)
            {
                if (ghostra->Decoys[i].Kind == DecoyKind.None) { freeSlot = i; break; }
            }
            if (freeSlot == -1)
            {
                Log.Info($"[Decoy] TrySpawn rejete : cap {MaxDecoys} leurres atteint pour P{ghostra->PlayerIndex}");
                return false;
            }

            int hp = (kind == DecoyKind.Protective) ? ProtectiveDecoyMaxHP : 0;
            ghostra->Decoys[freeSlot] = new DecoySlot
            {
                Kind = kind,
                PosX = posX,
                PosY = posY,
                SpawnedOnTurn = currentTurn,
                HP = hp,
            };

            // 3.6 RÉMANENCE sync : la ressource Ghostra Resource = compteur de leurres actifs
            // (Bible V7.1 "RÉMANENCE | 3 LEURRES MAXIMUM"). Lu par CombatDebugOverlay/HUD pour
            // afficher X/3. Sync ici (et a chaque mutation : DestroyAtSlot, DestroyByEnemyAction,
            // TickLifetimeAtSubTurnStart) pour rester coherent avec CountActive.
            ghostra->Resource = CountActive(ghostra);
            Log.Info($"[Decoy] Spawn P{ghostra->PlayerIndex} slot {freeSlot} kind={kind} pos=({posX},{posY}) hp={hp} turn={currentTurn} (actifs {ghostra->Resource}/{MaxDecoys}, Resource sync)");
            return true;
        }

        /// <summary>
        /// Destroy un slot specifique SANS heal. Utilise pour consommation INTERNE par la Ghostra
        /// elle-meme (Exécution Spectrale qui consomme 3/3 leurres, Permutation qui ne detruit pas
        /// mais swap, etc.). Pour destruction PAR ACTION ADVERSE (sort/AoE qui touche le leurre)
        /// utiliser <see cref="DestroyByEnemyAction"/> qui applique le heal Bible-conforme.
        /// </summary>
        public static void DestroyAtSlot(Combatant* ghostra, int slotIndex)
        {
            if (ghostra == null) return;
            if (slotIndex < 0 || slotIndex >= MaxDecoys) return;
            if (ghostra->Decoys[slotIndex].Kind == DecoyKind.None) return;
            Log.Info($"[Decoy] Destroy P{ghostra->PlayerIndex} slot {slotIndex} kind={ghostra->Decoys[slotIndex].Kind} pos=({ghostra->Decoys[slotIndex].PosX},{ghostra->Decoys[slotIndex].PosY})");
            ghostra->Decoys[slotIndex] = default;
            // 3.6 RÉMANENCE sync (cf TrySpawn).
            ghostra->Resource = CountActive(ghostra);
        }

        /// <summary>
        /// 3.7.b.i — Destroy un slot detruit par ACTION ADVERSE (sort ennemi qui touche le leurre,
        /// Charge Brutale qui traverse, AoE qui frappe la case, etc.). Applique le heal Bible-conforme
        /// AVANT clear :
        ///   - <see cref="DecoyKind.RepliqueFantome"/>   : +40 HP owner (Bible "Si le Leurre est detruit par un sort adverse")
        ///   - <see cref="DecoyKind.Protective"/>        : +60 HP owner (Bible Réplique Protectrice)
        ///   - <see cref="DecoyKind.Standard"/>          : pas de heal (F12/Pas dans l'Ombre/Dernier Pas — Bible : pas de heal explicite)
        /// </summary>
        public static void DestroyByEnemyAction(Combatant* ghostra, int slotIndex)
        {
            if (ghostra == null) return;
            if (slotIndex < 0 || slotIndex >= MaxDecoys) return;
            DecoyKind k = ghostra->Decoys[slotIndex].Kind;
            if (k == DecoyKind.None) return;

            int healAmount = 0;
            if (k == DecoyKind.RepliqueFantome) healAmount = RepliqueFantomeHealOnDestroy;
            else if (k == DecoyKind.Protective) healAmount = RepliqueProtectriceHealOnDestroy;

            int dx = ghostra->Decoys[slotIndex].PosX;
            int dy = ghostra->Decoys[slotIndex].PosY;
            ghostra->Decoys[slotIndex] = default;
            // 3.6 RÉMANENCE sync (cf TrySpawn).
            ghostra->Resource = CountActive(ghostra);

            if (healAmount > 0 && ghostra->HP > 0)
            {
                int hpBefore = ghostra->HP;
                int hpAfter = hpBefore + healAmount;
                if (hpAfter > ghostra->MaxHP) hpAfter = ghostra->MaxHP;
                ghostra->HP = hpAfter;
                int realHeal = hpAfter - hpBefore;
                Log.Info($"[Decoy] DestroyByEnemyAction P{ghostra->PlayerIndex} slot {slotIndex} kind={k} pos=({dx},{dy}) -> heal +{realHeal} HP (Bible {k}, {hpBefore}->{hpAfter})");
            }
            else
            {
                Log.Info($"[Decoy] DestroyByEnemyAction P{ghostra->PlayerIndex} slot {slotIndex} kind={k} pos=({dx},{dy}) (pas de heal pour ce Kind)");
            }
        }

        /// <summary>
        /// PATCH #6 — Un sort ENNEMI touche DIRECTEMENT la case d'un leurre (ciblage direct ou
        /// case d'effet AoE). Comportement selon Kind (decision Lorenzo) :
        ///   - Standard / RepliqueFantome (HP=0) : detruit en 1 coup (heal Bible selon Kind via
        ///     DestroyByEnemyAction : Standard 0, RepliqueFantome +40).
        ///   - Protective (HP=200) : encaisse `damage` sur ses HP ; detruit (+80 HP Ghostra) seulement
        ///     si HP tombe a 0. Un sort sans degats (damage &lt;= 0) ne le detruit PAS (absorbe 0).
        /// Retourne true si le leurre a ete DETRUIT, false s'il a juste encaisse/survecu.
        /// </summary>
        public static bool HitDecoyByEnemyAction(Combatant* ghostra, int slotIndex, int damage)
        {
            if (ghostra == null) return false;
            if (slotIndex < 0 || slotIndex >= MaxDecoys) return false;
            DecoyKind k = ghostra->Decoys[slotIndex].Kind;
            if (k == DecoyKind.None) return false;

            if (k == DecoyKind.Protective)
            {
                int hpBefore = ghostra->Decoys[slotIndex].HP;
                int absorbed = damage <= 0 ? 0 : (damage > hpBefore ? hpBefore : damage);
                int hpAfter = hpBefore - absorbed;
                var slot = ghostra->Decoys[slotIndex];
                slot.HP = hpAfter;
                ghostra->Decoys[slotIndex] = slot;
                Log.Info($"[Decoy] Hit direct PROTECTIVE P{ghostra->PlayerIndex} slot {slotIndex} : -{absorbed} HP ({hpBefore}->{hpAfter})");
                if (hpAfter <= 0)
                {
                    DestroyByEnemyAction(ghostra, slotIndex); // heal +80 Bible
                    return true;
                }
                return false;
            }

            // Standard / RepliqueFantome (HP=0) : 1-hit destroy (toute interaction de sort).
            DestroyByEnemyAction(ghostra, slotIndex);
            return true;
        }

        /// <summary>
        /// Cherche un slot decoy a la position (x,y) appartenant au Ghostra. Retourne
        /// l'index du slot ou -1.
        /// </summary>
        public static int FindSlotAtPosition(Combatant* ghostra, int x, int y)
        {
            if (ghostra == null) return -1;
            for (int i = 0; i < MaxDecoys; i++)
            {
                if (ghostra->Decoys[i].Kind == DecoyKind.None) continue;
                if (ghostra->Decoys[i].PosX == x && ghostra->Decoys[i].PosY == y) return i;
            }
            return -1;
        }

        /// <summary>
        /// 3.7.a — Nuée Spectrale : nombre de leurres du `ghostra` ADJACENTS (Manhattan 1) à la
        /// case (x,y) de la cible. Sert au scaling +30/leurre adjacent.
        /// </summary>
        public static int CountOwnDecoysAdjacent(Combatant* ghostra, int x, int y)
        {
            if (ghostra == null) return 0;
            int n = 0;
            for (int i = 0; i < MaxDecoys; i++)
            {
                if (ghostra->Decoys[i].Kind == DecoyKind.None) continue;
                int lx = ghostra->Decoys[i].PosX;
                int ly = ghostra->Decoys[i].PosY;
                int dist = (lx >= x ? lx - x : x - lx) + (ly >= y ? ly - y : y - ly);
                if (dist == 1) n++;
            }
            return n;
        }

        /// <summary>
        /// 3.7.c.ii — Voile Spectral (rework) : téléporte TOUS les leurres actifs du `ghostra` sur
        /// des cases libres AUTOUR de (cx,cy) (l'ennemi ciblé). Ordre déterministe : cardinales
        /// d'abord (Manhattan 1 -> enchaîne Éveil/dorsal), puis diagonales en secours. Une case est
        /// valide si en grille, walkable, sans obstacle, sans combattant, et pas déjà prise par un
        /// autre leurre. Retourne le nombre de leurres effectivement déplacés.
        /// </summary>
        public static int TeleportAllDecoysAround(Frame f, Combatant* ghostra, int cx, int cy)
        {
            if (ghostra == null) return 0;
            // Cardinales (E,O,N,S) puis diagonales (NE,NO,SE,SO). Déterministe.
            int* candX = stackalloc int[8] { cx + 1, cx - 1, cx,     cx,     cx + 1, cx - 1, cx + 1, cx - 1 };
            int* candY = stackalloc int[8] { cy,     cy,     cy + 1, cy - 1, cy + 1, cy + 1, cy - 1, cy - 1 };
            int moved = 0;
            for (int s = 0; s < MaxDecoys; s++)
            {
                if (ghostra->Decoys[s].Kind == DecoyKind.None) continue;
                for (int c = 0; c < 8; c++)
                {
                    int nx = candX[c], ny = candY[c];
                    if (!GridHelpers.InBounds(nx, ny)) continue;
                    if (!GridHelpers.IsWalkable(f, nx, ny)) continue;
                    if (ObstacleHelpers.HasObstacleAt(f, nx, ny)) continue;
                    if (GridHelpers.GetOccupant(f, nx, ny) != EntityRef.None) continue;
                    if (FindSlotAtPosition(ghostra, nx, ny) >= 0) continue; // case déjà prise par un leurre
                    ghostra->Decoys[s].PosX = nx;
                    ghostra->Decoys[s].PosY = ny;
                    moved++;
                    break;
                }
            }
            return moved;
        }

        /// <summary>
        /// 3.7.b — Éveil Spectral : cherche un leurre du `ghostra` ADJACENT (Manhattan 1) à la
        /// cible. Priorité au leurre qui frappe en DORSAL (derrière la cible) ; sinon le premier
        /// leurre adjacent trouvé. Retourne false si aucun leurre adjacent. `outDorsal` indique si
        /// le leurre choisi est dorsal (pour le bonus Angle Mort + Plaie côté SpellSystem).
        /// </summary>
        public static bool TryFindEveilLeurre(Frame f, Combatant* ghostra, Combatant* target,
            out int outX, out int outY, out bool outDorsal)
        {
            outX = -1; outY = -1; outDorsal = false;
            if (ghostra == null || target == null) return false;

            int fallbackX = -1, fallbackY = -1;
            bool found = false;
            for (int i = 0; i < MaxDecoys; i++)
            {
                if (ghostra->Decoys[i].Kind == DecoyKind.None) continue;
                int lx = ghostra->Decoys[i].PosX;
                int ly = ghostra->Decoys[i].PosY;
                int dist = (lx >= target->GridX ? lx - target->GridX : target->GridX - lx)
                         + (ly >= target->GridY ? ly - target->GridY : target->GridY - ly);
                if (dist != 1) continue; // adjacent Manhattan strict
                found = true;
                if (FacingHelpers.IsDorsalFromPosition(lx, ly, target))
                {
                    outX = lx; outY = ly; outDorsal = true; // dorsal prioritaire -> retour immediat
                    return true;
                }
                if (fallbackX < 0) { fallbackX = lx; fallbackY = ly; } // garde le 1er adjacent non-dorsal
            }
            if (!found) return false;
            outX = fallbackX; outY = fallbackY; outDorsal = false;
            return true;
        }

        /// <summary>
        /// Refonte 30 mai — Permutation deckable : true si le joueur `ownerPlayerIndex` possede un
        /// de ses leurres sur (x,y). Sert au filtre de ciblage TargetingFilter.TileWithLure
        /// (la Ghostra ne peut permuter qu'avec SES propres leurres).
        /// </summary>
        public static bool HasOwnDecoyAt(Frame f, int ownerPlayerIndex, int x, int y)
        {
            if (ownerPlayerIndex < 0) return false;
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                if (c->Class != NymoraClass.Ghostra) continue;
                if (c->HP <= 0) continue;
                if (c->PlayerIndex != ownerPlayerIndex) continue;
                if (FindSlotAtPosition(c, x, y) >= 0) return true;
            }
            return false;
        }

        /// <summary>
        /// 3.7.a.i.4 — Bible-strict : un leurre Ghostra ennemi BLOQUE la LoS et les
        /// sorts ciblés (pour préserver l'illusion "indiscernable cote adversaire").
        /// Retourne true si un leurre appartenant à une Ghostra du camp OPPOSE à
        /// `casterPlayerIndex` occupe (x,y).
        ///
        /// `casterPlayerIndex == -1` : retourne false (mode neutre — pas de notion d'ennemi).
        /// Pour les checks de MOUVEMENT, utiliser <see cref="HasAnyDecoyAt"/> à la place
        /// (tous les leurres bloquent — un combattant ne peut pas marcher sur une silhouette).
        /// </summary>
        public static bool HasEnemyDecoyAt(Frame f, int casterPlayerIndex, int x, int y)
        {
            if (casterPlayerIndex < 0) return false;
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                if (c->Class != NymoraClass.Ghostra) continue;
                if (c->HP <= 0) continue;
                if (c->PlayerIndex == casterPlayerIndex) continue; // skip Ghostra allié/self
                for (int i = 0; i < MaxDecoys; i++)
                {
                    if (c->Decoys[i].Kind == DecoyKind.None) continue;
                    if (c->Decoys[i].PosX == x && c->Decoys[i].PosY == y) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 3.7.a.i.4 — Bible-strict mouvement : TOUS les leurres bloquent la case
        /// (impossible de marcher sur une silhouette, qu'elle soit alliée ou ennemie).
        /// La Ghostra elle-même ne peut pas marcher sur ses propres leurres — pour
        /// échanger sa position avec un leurre, elle doit utiliser la Permutation Angle 3.
        ///
        /// Utilisé par MovementSystem / AStarPathfinder / MovementHelpers.MoveNonPM.
        /// Pour la LoS et les sorts ciblés, utiliser <see cref="HasEnemyDecoyAt"/>.
        /// </summary>
        public static bool HasAnyDecoyAt(Frame f, int x, int y)
        {
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                if (c->Class != NymoraClass.Ghostra) continue;
                if (c->HP <= 0) continue;
                for (int i = 0; i < MaxDecoys; i++)
                {
                    if (c->Decoys[i].Kind == DecoyKind.None) continue;
                    if (c->Decoys[i].PosX == x && c->Decoys[i].PosY == y) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 3.7.a.i.4 — Variante claire pour le caller : cherche un leurre Ghostra ENNEMI
        /// à `casterPlayerIndex` sur (x,y). Retourne (true + slot + ghostraPtr) si trouve.
        /// Equivalent à TryFindEnemyDecoyAt mais avec sémantique caster-centrée.
        /// Utilise par Charge Brutale pour consume le leurre traversé (Bible "AoE/sort
        /// qui touche un leurre le détruit").
        /// </summary>
        public static bool TryFindEnemyDecoyForCaster(Frame f, int casterPlayerIndex, int x, int y, out Combatant* outGhostra, out int outSlotIndex)
        {
            outGhostra = null;
            outSlotIndex = -1;
            if (casterPlayerIndex < 0) return false;
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                if (c->Class != NymoraClass.Ghostra) continue;
                if (c->HP <= 0) continue;
                if (c->PlayerIndex == casterPlayerIndex) continue; // skip Ghostra allié/self
                int slot = FindSlotAtPosition(c, x, y);
                if (slot >= 0)
                {
                    outGhostra = c;
                    outSlotIndex = slot;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 3.6 — Cherche un decoy d'un Ghostra ennemi sur la case (x,y) cote `targetCamp`.
        /// Retourne (true + slotIndex + ghostraPtr) si trouve, false sinon.
        /// Utilise par SpellSystem (3.7) pour intercepter les sorts cibles : si un sort
        /// adverse target une case qui est un leurre Ghostra -> consume + skip damage.
        /// </summary>
        public static bool TryFindEnemyDecoyAt(Frame f, int targetCamp, int x, int y, out Combatant* outGhostra, out int outSlotIndex)
        {
            outGhostra = null;
            outSlotIndex = -1;
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                if (c->Class != NymoraClass.Ghostra) continue;
                if (c->HP <= 0) continue;
                if (c->PlayerIndex != targetCamp) continue;
                int slot = FindSlotAtPosition(c, x, y);
                if (slot >= 0)
                {
                    outGhostra = c;
                    outSlotIndex = slot;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Tick lifetime des leurres du Ghostra au DEBUT de son sub-turn. Decremente
        /// la duree (skip-decrement si SpawnedOnTurn == currentTurn).
        ///
        /// 3.7.b.i — Heal owner sur EXPIRATION naturelle (Bible V7.1 "Si le Leurre survit la duree complete...") :
        ///   - <see cref="DecoyKind.RepliqueFantome"/> : +80 HP owner.
        ///   - Standard / Protective                   : pas de heal sur expire (Protective heal uniquement
        ///                                               sur destruction adverse, Bible).
        /// </summary>
        public static void TickLifetimeAtSubTurnStart(Combatant* ghostra, int currentTurn)
        {
            if (ghostra == null) return;
            bool anyExpiredThisTick = false;
            for (int i = 0; i < MaxDecoys; i++)
            {
                if (ghostra->Decoys[i].Kind == DecoyKind.None) continue;
                int spawnedOn = ghostra->Decoys[i].SpawnedOnTurn;
                if (spawnedOn == currentTurn) continue; // skip au round de spawn
                int age = currentTurn - spawnedOn;

                // AMENDEMENT 16 mai 2026 : lifetime varie par Kind. Default = LifetimeRounds (4),
                // Protective override = ProtectiveLifetimeRounds (3) pour cap balance Réplique Protectrice.
                DecoyKind kindHere = ghostra->Decoys[i].Kind;
                int lifetimeForKind = (kindHere == DecoyKind.Protective)
                    ? ProtectiveLifetimeRounds
                    : LifetimeRounds;
                if (age < lifetimeForKind) continue;

                DecoyKind expiredKind = kindHere;
                int dx = ghostra->Decoys[i].PosX;
                int dy = ghostra->Decoys[i].PosY;

                // Heal Bible "Si le Leurre survit la duree complete" (Réplique Fantôme uniquement, +80 HP).
                int healAmount = 0;
                if (expiredKind == DecoyKind.RepliqueFantome) healAmount = RepliqueFantomeHealOnExpire;

                ghostra->Decoys[i] = default;
                anyExpiredThisTick = true;
                Log.Info($"[Decoy] Expire P{ghostra->PlayerIndex} slot {i} kind={expiredKind} (age {age}>={lifetimeForKind}) pos=({dx},{dy})");

                if (healAmount > 0 && ghostra->HP > 0)
                {
                    int hpBefore = ghostra->HP;
                    int hpAfter = hpBefore + healAmount;
                    if (hpAfter > ghostra->MaxHP) hpAfter = ghostra->MaxHP;
                    ghostra->HP = hpAfter;
                    int realHeal = hpAfter - hpBefore;
                    Log.Info($"[Decoy] Réplique Fantôme survit -> heal P{ghostra->PlayerIndex} +{realHeal} HP ({hpBefore}->{hpAfter}, Bible V7.1)");
                }
            }
            // 3.6 RÉMANENCE sync (cf TrySpawn) : si au moins un leurre a expire ce tick, on
            // resynchronise Resource pour refleter le nouveau CountActive.
            if (anyExpiredThisTick)
            {
                ghostra->Resource = CountActive(ghostra);
            }
        }

        /// <summary>
        /// Permutation Angle 3 (Bible V7.1) : swap PosX/Y entre la Ghostra et un de ses
        /// leurres. INVISIBLE cote adversaire (les sprites sont identiques). 0 PA, 1x/tour.
        /// `slotIndex` = -1 -> auto-pick le premier slot non-None.
        ///
        /// Note grille : seule la VRAIE Ghostra occupe une case de la grid occupancy
        /// (les leurres ne sont PAS dans GridSingleton.Occupants — ils vivent uniquement
        /// dans Combatant.Decoys). Le swap met donc juste a jour l'occupant pour les
        /// 2 cases : ancienne case Ghostra vide, nouvelle case = ghostraEntity.
        /// </summary>
        public static bool TryPermute(Frame f, EntityRef ghostraEntity, Combatant* ghostra, int slotIndex, int currentTurn)
        {
            if (ghostra == null || ghostra->HP <= 0) return false;
            if (ghostra->Class != NymoraClass.Ghostra) return false;

            // Angle 3 required (3 leurres actifs Bible-strict).
            int active = CountActive(ghostra);
            if (active < MaxDecoys)
            {
                Log.Warn($"[Permutation] Rejet : Angle {GhostraPassif.ComputeAngleLevel(active)} (besoin Angle 3 = {MaxDecoys} leurres, actuel {active})");
                return false;
            }

            // 1x par tour cap.
            if (ghostra->LastPermutationOnTurn == currentTurn)
            {
                Log.Warn($"[Permutation] Rejet : deja utilisee ce tour (round {currentTurn})");
                return false;
            }

            // Auto-pick si slotIndex == -1.
            int slot = slotIndex;
            if (slot < 0)
            {
                for (int i = 0; i < MaxDecoys; i++)
                {
                    if (ghostra->Decoys[i].Kind != DecoyKind.None) { slot = i; break; }
                }
            }
            if (slot < 0 || slot >= MaxDecoys || ghostra->Decoys[slot].Kind == DecoyKind.None)
            {
                Log.Warn($"[Permutation] Rejet : slot {slot} invalide ou vide");
                return false;
            }

            // Swap positions logiques.
            int gx = ghostra->GridX;
            int gy = ghostra->GridY;
            int dx = ghostra->Decoys[slot].PosX;
            int dy = ghostra->Decoys[slot].PosY;

            ghostra->GridX = dx;
            ghostra->GridY = dy;
            ghostra->Decoys[slot].PosX = gx;
            ghostra->Decoys[slot].PosY = gy;
            ghostra->LastPermutationOnTurn = currentTurn;

            // Met a jour grid occupancy (seule la vraie Ghostra y est referencee).
            if (GridHelpers.InBounds(gx, gy))  GridHelpers.SetOccupant(f, gx, gy, EntityRef.None);
            if (GridHelpers.InBounds(dx, dy))  GridHelpers.SetOccupant(f, dx, dy, ghostraEntity);

            Log.Info($"[Permutation] P{ghostra->PlayerIndex} swap Ghostra({gx},{gy}) <-> Decoy slot {slot} ({dx},{dy}) — invisible cote adversaire");
            return true;
        }

        /// <summary>
        /// Refonte 30 mai — SWAP DECKABLE Ghostra<->leurre du slot donne, SANS gate (ni Angle 3 ni
        /// cap interne). La validation amont est faite dans SpellSystem :
        ///   - presence d'un leurre OWN a la case : filtre TargetingFilter.TileWithLure
        ///   - cap 2x/tour : moteur generique SpellLimitsHelper (SpellDef.MaxUsesPerTurn)
        /// Met a jour la grid occupancy (seule la vraie Ghostra y est referencee). Retourne false
        /// si slot invalide/vide. Coexiste avec TryPermute (ancienne voie gratuite Angle 3, dormante).
        /// </summary>
        public static bool PermuteToSlot(Frame f, EntityRef ghostraEntity, Combatant* ghostra, int slot)
        {
            if (ghostra == null || ghostra->HP <= 0) return false;
            if (ghostra->Class != NymoraClass.Ghostra) return false;
            if (slot < 0 || slot >= MaxDecoys || ghostra->Decoys[slot].Kind == DecoyKind.None)
            {
                Log.Warn($"[Permutation] (deckable) Rejet : slot {slot} invalide ou vide");
                return false;
            }

            int gx = ghostra->GridX;
            int gy = ghostra->GridY;
            int dx = ghostra->Decoys[slot].PosX;
            int dy = ghostra->Decoys[slot].PosY;

            ghostra->GridX = dx;
            ghostra->GridY = dy;
            ghostra->Decoys[slot].PosX = gx;
            ghostra->Decoys[slot].PosY = gy;

            if (GridHelpers.InBounds(gx, gy)) GridHelpers.SetOccupant(f, gx, gy, EntityRef.None);
            if (GridHelpers.InBounds(dx, dy)) GridHelpers.SetOccupant(f, dx, dy, ghostraEntity);

            Log.Info($"[Permutation] (deckable) P{ghostra->PlayerIndex} swap Ghostra({gx},{gy}) <-> leurre slot {slot} ({dx},{dy})");
            return true;
        }
    }
}
