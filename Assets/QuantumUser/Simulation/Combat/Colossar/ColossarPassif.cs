namespace Quantum
{
    using Photon.Deterministic;

    /// <summary>
    /// 3.2 — Helpers passif Colossar "Densite Inerte" (Bible V7.1).
    ///
    /// Effets continus du passif (etat lie au nb d'obstacles owner=Colossar actifs) :
    ///   1. -6% degats subis par obstacle actif (cap -18% = 3 obstacles) [refonte 29 mai].
    ///   2. +30 HP au Colossar quand un de ses Piliers est detruit.
    ///   3. +20 dmg sorts portee 1-2 si Colossar adjacent a un de ses obstacles
    ///      (sera branche en 3.3.a quand les sorts melee Colossar arrivent).
    ///
    /// Le passif n'a pas d'etat persistent en Combatant DSL — il se calcule a la volee
    /// depuis le Filter&lt;Obstacle&gt;. Justifie : le nb d'obstacles est petit (max ~6
    /// concurrents), le scan O(n) est negligeable. Pas de cache premature.
    /// </summary>
    public static unsafe class ColossarPassif
    {
        // Refonte 29 mai : nerf tankiness (compense 3 PM + signature imparable).
        public const int DamageReductionPercentPerObstacle = 6;  // etait 8
        public const int MaxDamageReductionPercent = 18; // cap = 3 obstacles (etait 24)
        public const int HpRestoredOnPillarDestroyed = 30;
        public const int AdjacentObstacleBonusDamage = 20;
        public const int AdjacentBonusMaxRange = 2; // sorts portee 1-2 (melee + courte distance)

        /// <summary>
        /// Compte le nombre d'obstacles vivants (HP > 0) appartenant a `ownerEntity`.
        /// Optionnel : filtre par kind (par ex. Pilier seulement).
        /// </summary>
        public static int CountObstaclesOwnedBy(Frame f, EntityRef ownerEntity, ObstacleKind? kindFilter = null)
        {
            if (ownerEntity == EntityRef.None) return 0;
            int count = 0;
            var filter = f.Filter<Obstacle>();
            while (filter.NextUnsafe(out EntityRef _, out Obstacle* obs))
            {
                if (obs->HP <= 0) continue;
                if (obs->Owner != ownerEntity) continue;
                if (kindFilter.HasValue && obs->Kind != kindFilter.Value) continue;
                count++;
            }
            return count;
        }

        /// <summary>
        /// Compte les obstacles owner=Colossar via le PlayerIndex stocke (resilient au
        /// destroy de l'entity Colossar entre temps). Utilise pour le passif damage reduction.
        /// </summary>
        public static int CountObstaclesOwnedByPlayer(Frame f, int ownerPlayerIndex)
        {
            int count = 0;
            var filter = f.Filter<Obstacle>();
            while (filter.NextUnsafe(out EntityRef _, out Obstacle* obs))
            {
                if (obs->HP <= 0) continue;
                if (obs->OwnerPlayerIndex != ownerPlayerIndex) continue;
                count++;
            }
            return count;
        }

        /// <summary>
        /// Densite Inerte — reduction % degats subis pour la cible si elle est Colossar.
        /// Retourne 0 si pas Colossar ou pas d'obstacle.
        ///
        /// Formule : 6% par obstacle actif owner=cible, cap 18% (3 obstacles) [refonte 29 mai].
        /// </summary>
        public static int GetDamageReductionPercent(Frame f, Combatant* target)
        {
            if (target->Class != NymoraClass.Colossar) return 0;
            int obstacles = CountObstaclesOwnedByPlayer(f, target->PlayerIndex);
            int pct = obstacles * DamageReductionPercentPerObstacle;
            return pct > MaxDamageReductionPercent ? MaxDamageReductionPercent : pct;
        }

        /// <summary>
        /// Applique la reduction Densite Inerte sur un montant de degats.
        /// Returns le dmg ajuste (toujours >= 0).
        /// </summary>
        public static int ApplyDamageReduction(Frame f, Combatant* target, int incomingDamage)
        {
            if (incomingDamage <= 0) return 0;
            int pct = GetCombinedDamageReductionPercent(f, target);
            if (pct == 0) return incomingDamage;
            int reduced = incomingDamage * (100 - pct) / 100;
            return reduced;
        }

        /// <summary>
        /// 3.3.c — Reduction de degats totale = Densite Inerte (passif Colossar) + Garde Protectrice
        /// (status DamageReductionPercent) en mode ADDITIF, clampe au cap Bible V7.1 -50%.
        ///
        /// Bible : "Ne se cumule pas avec le passif Densite Inerte au-dela du cap -50% total."
        /// Lecture additive choisie (vs multiplicative) : Densite Inerte 24% + Garde Prot 30% = 54%
        /// -> clamp 50%. Sans Garde Prot, Densite Inerte reste a 24% (pas affecte).
        ///
        /// Toute cible Colossar voit DensiteInerte. Toute cible (toute classe) qui porte
        /// DamageReductionPercent voit son magnitude ajoute.
        ///
        /// TODO Phase 3.4 : Bible "(sauf DoT venin Necram qui ignore les reductions)" -> bypass
        /// a brancher quand les marques venin arrivent.
        /// </summary>
        public static int GetCombinedDamageReductionPercent(Frame f, Combatant* target)
        {
            int densiteInerte = GetDamageReductionPercent(f, target);
            int gardeProtectrice = StatusHelper.GetMagnitude(target, StatusKind.DamageReductionPercent, 0);
            int total = densiteInerte + gardeProtectrice;
            if (total > SpellRegistry.MaxCombinedDamageReductionPct)
            {
                total = SpellRegistry.MaxCombinedDamageReductionPct;
            }
            return total;
        }

        /// <summary>
        /// True si le combattant est adjacent (4-connexite Manhattan, distance 1) a un de ses
        /// propres obstacles. Utilise pour le bonus +20 dmg sorts portee 1-2 (Bible) — sera
        /// branche en 3.3.a quand les sorts Colossar arrivent.
        /// </summary>
        public static bool IsAdjacentToOwnObstacle(Frame f, Combatant* combatant, int playerIndex)
        {
            int gx = combatant->GridX;
            int gy = combatant->GridY;
            // Check les 4 cases adjacentes.
            return HasOwnObstacleAt(f, gx + 1, gy, playerIndex)
                || HasOwnObstacleAt(f, gx - 1, gy, playerIndex)
                || HasOwnObstacleAt(f, gx, gy + 1, playerIndex)
                || HasOwnObstacleAt(f, gx, gy - 1, playerIndex);
        }

        private static bool HasOwnObstacleAt(Frame f, int x, int y, int playerIndex)
        {
            EntityRef obs = ObstacleHelpers.GetObstacleAt(f, x, y);
            if (obs == EntityRef.None) return false;
            if (!f.Unsafe.TryGetPointer<Obstacle>(obs, out var data)) return false;
            return data->OwnerPlayerIndex == playerIndex;
        }

        /// <summary>
        /// 3.3.a.i — Bible Frappe Lourde "epinglee" : la case juste DERRIERE la cible
        /// (cote oppose au caster sur l'axe dominant) est-elle un obstacle ou un bord ?
        ///
        /// Direction calculee axe-dominant (Manhattan), comme PushAndTrigger pour coherence.
        /// Si delta caster->target == (0,0) : pas epinglee (cas degenere).
        /// </summary>
        public static bool IsTargetPinnedFromCaster(Frame f, Combatant* caster, Combatant* targetC)
        {
            int dx = targetC->GridX - caster->GridX;
            int dy = targetC->GridY - caster->GridY;
            int absDx = dx < 0 ? -dx : dx;
            int absDy = dy < 0 ? -dy : dy;
            int stepX = 0, stepY = 0;
            if (absDx >= absDy)
            {
                stepX = dx > 0 ? 1 : (dx < 0 ? -1 : 0);
            }
            else
            {
                stepY = dy > 0 ? 1 : (dy < 0 ? -1 : 0);
            }
            if (stepX == 0 && stepY == 0) return false;
            int behindX = targetC->GridX + stepX;
            int behindY = targetC->GridY + stepY;
            // Bord de map = epingle.
            if (!GridHelpers.InBounds(behindX, behindY)) return true;
            // Obstacle (Pilier/Mur) = epingle. Bible ne specifie pas owner (Pilier ennemi
            // futur compte aussi, ce qui est coherent : c'est la geometrie qui compte).
            if (ObstacleHelpers.HasObstacleAt(f, behindX, behindY)) return true;
            return false;
        }

        // ====================================================================
        // Ressource Fondation (FD) — gain helper.
        // ====================================================================

        /// <summary>
        /// Gain +amount FD (defaut 1) sur le combattant si Class == Colossar. Cap a
        /// CombatantStats.GetMaxResource. No-op silencieux si pas Colossar ou amount <= 0
        /// (defensif). Log la raison pour debug. amount=2 utilise par Mur de Pierre (equilibrage juin).
        /// </summary>
        public static void GainFondation(Combatant* combatant, string reason, int amount = 1)
        {
            if (combatant->Class != NymoraClass.Colossar) return;
            if (amount <= 0) return;
            int max = CombatantStats.GetMaxResource(NymoraClass.Colossar);
            int before = combatant->Resource;
            if (before >= max) return; // deja au cap
            int after = before + amount;
            if (after > max) after = max;
            combatant->Resource = after;
            Log.Info($"[Fondation] +{after - before} FD sur P{combatant->PlayerIndex} ({reason}) : {before} -> {after}/{max}");
        }
    }
}
