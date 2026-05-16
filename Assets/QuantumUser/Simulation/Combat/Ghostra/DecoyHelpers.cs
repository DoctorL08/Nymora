namespace Quantum
{
    /// <summary>
    /// 3.6 — Helpers leurres Ghostra (Bible V7.1 ressource RÉMANENCE).
    ///
    /// Modelisation : inline array&lt;DecoySlot&gt;[3] sur Combatant Ghostra (cf Decoy.qtn).
    /// Cap 3 garanti par taille tableau. Slot libre = Kind == DecoyKind.None.
    ///
    /// Conventions :
    ///   - Lifetime 2 ROUNDS (Bible "2 tours" = 2 rounds depuis 2.14, cf project_turn_semantics).
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
        public const int LifetimeRounds = 2;       // Bible V7.1 "dure 2 tours"
        public const int ProtectiveDecoyMaxHP = 200; // Bible Réplique Protectrice

        // Heal Ghostra owner sur destruction d'un leurre (Bible V7.1 par sort) :
        //   - Réplique Fantôme  : +40 HP si detruit, +80 HP si survit 2 tours
        //   - Réplique Protectr : +60 HP si detruit
        // Le helper retourne juste les constantes ; les sorts (3.7) feront le heal explicite
        // selon leur regle (consume vs expire vs destroy).
        public const int ReplyiqueFantomeHealOnDestroy = 40;
        public const int ReplyiqueFantomeHealOnExpire  = 80;
        public const int ReplyiqueProtectriceHealOnDestroy = 60;

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

            Log.Info($"[Decoy] Spawn P{ghostra->PlayerIndex} slot {freeSlot} kind={kind} pos=({posX},{posY}) hp={hp} turn={currentTurn} (actifs {CountActive(ghostra)}/{MaxDecoys})");
            return true;
        }

        /// <summary>
        /// Destroy un slot specifique (mise a default). Pas de heal automatique — les sorts
        /// qui callent ce helper appliquent le heal selon leur regle Bible (cf constantes).
        /// </summary>
        public static void DestroyAtSlot(Combatant* ghostra, int slotIndex)
        {
            if (ghostra == null) return;
            if (slotIndex < 0 || slotIndex >= MaxDecoys) return;
            if (ghostra->Decoys[slotIndex].Kind == DecoyKind.None) return;
            Log.Info($"[Decoy] Destroy P{ghostra->PlayerIndex} slot {slotIndex} kind={ghostra->Decoys[slotIndex].Kind} pos=({ghostra->Decoys[slotIndex].PosX},{ghostra->Decoys[slotIndex].PosY})");
            ghostra->Decoys[slotIndex] = default;
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
        /// la duree (skip-decrement si SpawnedOnTurn == currentTurn). Les leurres
        /// expires sont detruits silencieusement (heal owner sera gere par 3.7.b
        /// Réplique Fantôme via Status auto-fire pendant que le slot est actif).
        /// </summary>
        public static void TickLifetimeAtSubTurnStart(Combatant* ghostra, int currentTurn)
        {
            if (ghostra == null) return;
            for (int i = 0; i < MaxDecoys; i++)
            {
                if (ghostra->Decoys[i].Kind == DecoyKind.None) continue;
                int spawnedOn = ghostra->Decoys[i].SpawnedOnTurn;
                if (spawnedOn == currentTurn) continue; // skip au round de spawn
                int age = currentTurn - spawnedOn;
                if (age >= LifetimeRounds)
                {
                    Log.Info($"[Decoy] Expire P{ghostra->PlayerIndex} slot {i} (age {age} rounds >= {LifetimeRounds}) pos=({ghostra->Decoys[i].PosX},{ghostra->Decoys[i].PosY})");
                    ghostra->Decoys[i] = default;
                }
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
    }
}
