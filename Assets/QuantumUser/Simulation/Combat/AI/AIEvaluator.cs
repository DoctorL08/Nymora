namespace Quantum
{
    /// <summary>
    /// Helpers de scoring deterministes pour l'IA (Bloc E Phase 2).
    ///
    /// Toutes les fonctions ici sont pures (lecture frame, pas de mutation) et
    /// strictement entieres. Elles produisent un score numerique compare ailleurs
    /// par l'AISystem pour selectionner la meilleure action (move + cast).
    ///
    /// 2.16.a.ii (cette brique) : score de deplacement Soulrender = -manhattan
    /// vers l'ennemi le plus proche. Plus la nouvelle case est pres d'un ennemi,
    /// meilleur le score. Tie-break par index grille croissant (cf AISystem).
    ///
    /// 2.16.a.iii (suivante) : ScoreSpell pour ranger les casts.
    /// 2.17 / Phase 3 : polyvalence par classe (Nightseer veut kite, Colossar
    /// veut tenir position, etc.). Pour l'instant la logique est Soulrender-only
    /// meme si bot.Class != Soulrender (juste suboptimal cote gameplay).
    /// </summary>
    public static unsafe class AIEvaluator
    {
        /// <summary>
        /// Score d'une case de destination pour le bot. Plus eleve = meilleur.
        /// Soulrender melee : -manhattan(dest, ennemi le plus proche).
        /// Si aucun ennemi : 0 (toutes cases equivalentes — n'arrive pas en 1v1).
        /// </summary>
        public static int ScoreMoveDestination(Frame f, Combatant* bot, int destX, int destY)
        {
            int nearest = NearestEnemyManhattan(f, bot, destX, destY);
            if (nearest == int.MaxValue) return 0;
            return -nearest;
        }

        /// <summary>
        /// Distance Manhattan vers l'ennemi le plus proche depuis (fromX, fromY).
        /// Iteration sur les Combatants, filtre par PlayerIndex != bot.PlayerIndex.
        /// Retourne int.MaxValue si aucun ennemi (sanity, ne devrait pas arriver).
        /// </summary>
        public static int NearestEnemyManhattan(Frame f, Combatant* bot, int fromX, int fromY)
        {
            int closest = int.MaxValue;
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                if (c->PlayerIndex == bot->PlayerIndex) continue;
                if (c->HP <= 0) continue; // ennemi mort = ne compte plus
                int d = Manhattan(fromX, fromY, c->GridX, c->GridY);
                if (d < closest) closest = d;
            }
            return closest;
        }

        /// <summary>Distance Manhattan entre deux cases grille (entier pur).</summary>
        public static int Manhattan(int x1, int y1, int x2, int y2)
        {
            int dx = x2 - x1; if (dx < 0) dx = -dx;
            int dy = y2 - y1; if (dy < 0) dy = -dy;
            return dx + dy;
        }

        /// <summary>
        /// Estime les degats d'un sort offensif Soulrender, ignorant buffs Pacte/Marque
        /// (approximation pour le scoring greedy 2.16.a.iii). Pour les sorts dont
        /// SpellDef.DamageAmount == 0 (formule dynamique), valeurs hardcodees ici.
        ///
        /// Returns 0 pour les sorts non-offensifs ou non-Soulrender. L'AI Easy n'utilise
        /// que ces 6 sorts ; le reste (tactiques, self-buffs, signature Nightseer) sera
        /// adresse en 2.17 / Phase 3 (IA polyvalente).
        /// </summary>
        public static int EstimateSpellDamage(SpellId spellId, byte hgSpend, byte hgMandatory)
        {
            switch (spellId)
            {
                // Bible V7.1 — values en dur, miroir des branches damage du SpellSystem.
                case SpellId.SoulrenderTrancheAme:          return 220;
                case SpellId.SoulrenderOuvrePlaie:          return hgSpend > 0 ? 230 : 110;
                case SpellId.SoulrenderChargeBrutale:       return 180;
                case SpellId.SoulrenderDetonationSanglante: return 60 + 40 * (hgMandatory + hgSpend);
                case SpellId.SoulrenderCuree:               return 150;
                case SpellId.SoulrenderAmeLaceree:          return 320;
                default:                                    return 0;
            }
        }
    }
}
