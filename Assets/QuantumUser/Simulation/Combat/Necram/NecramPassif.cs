namespace Quantum
{
    /// <summary>
    /// 3.4 — Helpers passif Necram "La Floraison" (Bible V7.1).
    ///
    /// Effets continus du passif au debut de chaque sub-turn (calcules a la volee depuis
    /// la densite globale, pas d'etat cache) :
    ///   1. Tick venin sur le porteur (delegue a <see cref="VeninHelpers.TryTick"/>).
    ///   2. Si densite >= 4 (tier 2) : regen Necram +10 HP par marque GLOBALE active.
    ///   3. Si densite >= 4 (tier 2) : halo toxique rayon 3 — chaque ennemi qui commence
    ///      son sub-turn a Manhattan <= 3 du Necram prend 20 dmg.
    ///   4. Si densite >= 7 (tier 3) : Virus Fatal (signature 3.5.d) deviendrait
    ///      castable (gate handled cote SpellSystem quand 3.5.d sera livre).
    ///
    /// Pas de state Combatant DSL dedie au passif — densite et tier sont recalcules
    /// chaque hook depuis Filter&lt;Combatant&gt;. Cheap : 1v1 = 2 entities.
    /// </summary>
    public static unsafe class NecramPassif
    {
        public const int RegenPerMarkAtTier2 = 10; // +10 HP/marque globale au Necram (tier 2+)
        public const int HaloDamagePerTurnAtTier2 = 20; // -20 HP/tour aux ennemis adjacents (tier 2+)
        public const int HaloRangeManhattan = 3; // rayon halo toxique (Manhattan)

        /// <summary>
        /// Hook a appeler au DEBUT du sub-turn du combattant `active`. Gere :
        ///   - Tick venin sur le porteur (si VeninStacks > 0).
        ///   - Halo toxique si active est ENNEMI du Necram et a Manhattan &lt;= 3 (tier 2+).
        ///   - Regen + reset PutrefactionMarksGainedThisTurn si active est le Necram.
        /// </summary>
        public static void OnSubTurnStart(Frame f, Combatant* active, int currentTurn)
        {
            if (active == null || active->HP <= 0) return;

            // 1. Tick venin sur le porteur (toutes classes — meme un Necram qui se serait
            //    fait marquer par contagion future).
            VeninHelpers.TryTick(f, active, currentTurn);
            if (active->HP <= 0) return; // mort par tick

            int density = VeninHelpers.GetGlobalDensity(f);
            int tier = VeninHelpers.GetFloraisonTier(density);

            // 2. Si active = Necram : reset marks-gained-this-turn + regen tier 2.
            if (active->Class == NymoraClass.Necram)
            {
                active->PutrefactionMarksGainedThisTurn = 0;

                if (tier >= 1) // tier 2 (Bible : densite 4-6) ou tier 3 (7+)
                {
                    int regen = density * RegenPerMarkAtTier2;
                    int hpBefore = active->HP;
                    active->HP = active->HP + regen > active->MaxHP ? active->MaxHP : active->HP + regen;
                    if (active->HP != hpBefore)
                    {
                        Log.Info($"[Floraison] Regen Necram P{active->PlayerIndex} : +{active->HP - hpBefore} HP (density {density}, tier {tier + 1}) : {hpBefore}->{active->HP}");
                    }
                }
                return;
            }

            // 3. Si active = ennemi du Necram : halo toxique tier 2+ (rayon 3 Manhattan).
            if (tier >= 1)
            {
                TryApplyHaloOnEnemyTurnStart(f, active);
            }
        }

        private static void TryApplyHaloOnEnemyTurnStart(Frame f, Combatant* enemy)
        {
            // Cherche le Necram vivant (en 1v1 c'est trivial, en multi on appliquerait
            // par Necram = somme dmg de chaque halo... a re-clarifier en Phase 4 multi).
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* nec))
            {
                if (nec->Class != NymoraClass.Necram) continue;
                if (nec->HP <= 0) continue;
                if (nec->PlayerIndex == enemy->PlayerIndex) continue;
                int dx = nec->GridX - enemy->GridX; if (dx < 0) dx = -dx;
                int dy = nec->GridY - enemy->GridY; if (dy < 0) dy = -dy;
                int dist = dx + dy;
                if (dist > HaloRangeManhattan) continue;

                int hpBefore = enemy->HP;
                enemy->HP -= HaloDamagePerTurnAtTier2;
                if (enemy->HP < 0) enemy->HP = 0;
                enemy->DamageTakenThisRound += HaloDamagePerTurnAtTier2;
                Log.Info($"[Floraison] Halo toxique : -{HaloDamagePerTurnAtTier2} HP sur P{enemy->PlayerIndex} (Manhattan {dist} <= {HaloRangeManhattan}) : {hpBefore}->{enemy->HP}");
                break;
            }
        }
    }
}
