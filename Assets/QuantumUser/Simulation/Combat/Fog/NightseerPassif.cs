namespace Quantum
{
    /// <summary>
    /// Refonte 29 mai 2026 — Passif Nightseer "L'Œil qui n'est pas", PHASÉ sur la Prescience (PR, cap 5).
    ///
    /// Économie PR (remplace l'ancien "+1 si pas de dégâts / -1 si dégâts") :
    ///   +1 PR par PIÈGE POSÉ · +1 PR par PIÈGE DÉCLENCHÉ · +1 PR par MARQUE (Traqué) appliquée.
    ///   Cap +3 PR par tour du Nightseer (via Combatant.PrescienceGainedThisTurn, reset au TurnStart).
    ///
    /// Paliers (cumulatifs) :
    ///   PR 1-2 (P1) : +15% dégâts des pièges.
    ///   PR 3-4 (P2) : + (P1) + +30 dégâts flat sur les sorts ET +1 portée.
    ///   PR 5   (P3) : + (P1,P2) + ignore 50% des boucliers + pièges INVISIBLES + signature Traquenard prête.
    ///
    /// Pas d'état caché : la phase est dérivée de Resource (PR) à la volée.
    /// </summary>
    public static unsafe class NightseerPassif
    {
        public const int PrescienceGainCapPerTurn = 3;

        // Paliers (valeurs refonte 29 mai).
        public const int TrapDamageBonusPct = 15;  // P1+ : +15% dégâts pièges
        public const int FlatDamageBonus    = 30;  // P2+ : +30 dégâts flat sorts
        public const int RangeBonus         = 1;   // P2+ : +1 portée
        public const int ShieldIgnorePct    = 50;  // P3  : ignore 50% boucliers

        /// <summary>
        /// Phase du Nightseer dérivée de la Prescience : 0 (PR 0), 1 (PR 1-2), 2 (PR 3-4), 3 (PR 5+).
        /// </summary>
        public static int GetPhase(int prescience)
        {
            if (prescience >= 5) return 3;
            if (prescience >= 3) return 2;
            if (prescience >= 1) return 1;
            return 0;
        }

        /// <summary>
        /// True si le Nightseer de PlayerIndex `ownerPlayerIndex` rend ses pièges INVISIBLES
        /// (phase 3, PR 5). Évalué sur la Prescience ACTUELLE -> visibilité dynamique (tous les
        /// pièges du NS, même posés avant 5/5, basculent). Utilisé par TrapView (rendu).
        /// </summary>
        public static bool TrapsInvisibleForOwner(Frame f, int ownerPlayerIndex)
        {
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                if (c->PlayerIndex != ownerPlayerIndex) continue;
                if (c->Class != NymoraClass.Nightseer) return false;
                return TrapsInvisible(c->Resource);
            }
            return false;
        }

        public static bool TrapDamageBonusActive(int prescience)      => GetPhase(prescience) >= 1;
        public static bool FlatDamageRangeBonusActive(int prescience) => GetPhase(prescience) >= 2;
        public static bool ShieldIgnoreActive(int prescience)         => GetPhase(prescience) >= 3;
        public static bool TrapsInvisible(int prescience)             => GetPhase(prescience) >= 3;
        public static bool TraquenardUnlocked(int prescience)         => GetPhase(prescience) >= 3;

        /// <summary>
        /// +1 PR sur le Nightseer de PlayerIndex `ownerPlayerIndex` (piège posé / déclenché / marque).
        /// Cappé à +3 PR par tour du Nightseer (PrescienceGainedThisTurn) ET au cap de ressource (5).
        /// No-op si pas de Nightseer vivant pour ce playerIndex.
        /// </summary>
        public static void GainPrescienceForPlayer(Frame f, int ownerPlayerIndex, int currentTurn, string reason)
        {
            int maxRes = CombatantStats.GetMaxResource(NymoraClass.Nightseer);
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                if (c->Class != NymoraClass.Nightseer) continue;
                if (c->HP <= 0) continue;
                if (c->PlayerIndex != ownerPlayerIndex) continue;

                if (c->PrescienceGainedThisTurn >= PrescienceGainCapPerTurn) return; // cap tour atteint
                int before = c->Resource;
                if (before >= maxRes) return; // deja au cap ressource
                c->Resource = before + 1;
                c->PrescienceGainedThisTurn += 1;
                Log.Info($"[Prescience] +1 PR sur P{c->PlayerIndex} ({reason}, {c->PrescienceGainedThisTurn}/{PrescienceGainCapPerTurn} ce tour) : {before} -> {c->Resource}/{maxRes}");
                return;
            }
        }
    }
}
