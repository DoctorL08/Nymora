namespace Quantum
{
    /// <summary>
    /// 3.4 — Helpers marques venin Necram (Bible V7.1 passif "La Floraison").
    ///
    /// Convention :
    ///   - Une cible porte 0..4 marques venin (cap MaxStacksPerTarget).
    ///   - Les marques NE expirent PAS par duree (pas de TurnsLeft). Elles restent jusqu'a
    ///     consommation par Detonation Virulente (3.5.a), Virus Fatal (3.5.d signature) ou
    ///     kill+Morsure Putride (transfert sur ennemi le plus proche).
    ///   - Tick au DEBUT du sub-turn du PORTEUR : VeninStacks * GetTickDmgPerMark(density).
    ///     Le tick BYPASS ShieldActive, DamageReductionPercent et Densite Inerte (identite
    ///     Necram vs tanks Bible).
    ///
    /// Densite globale Floraison = somme(VeninStacks) sur TOUS les Combatants vivants
    /// (toutes cibles confondues). 3 paliers Bible :
    ///   - Densite 1-3 : 30 dmg/marque/tick
    ///   - Densite 4-6 : 40 dmg/marque/tick + regen Necram +10/marque + halo toxique rayon 3
    ///   - Densite 7+  : 50 dmg/marque/tick + Virus Fatal debloque
    ///
    /// Ressource Putrefaction gain rules (Bible) :
    ///   - +1 PT par tick venin global (TickAll appel ce gain via Necram lookup).
    ///   - +1 PT par marque appliquee (cap +2 PT par tour Necram via PutrefactionMarksGainedThisTurn).
    /// </summary>
    public static unsafe class VeninHelpers
    {
        // Bible V7.1 — verrouille (modif = bump CombatRulesVersion).
        public const int MaxStacksPerTarget = 4;

        public const int TickDmgPerMark_Tier1 = 30; // densite 1-3
        public const int TickDmgPerMark_Tier2 = 40; // densite 4-6
        public const int TickDmgPerMark_Tier3 = 50; // densite 7+

        public const int Tier2Threshold = 4; // densite >= 4 -> tier 2
        public const int Tier3Threshold = 7; // densite >= 7 -> tier 3

        public const int PutrefactionGainPerMarkApplied = 1;
        public const int PutrefactionGainPerTickGlobal = 1;
        public const int PutrefactionGainCapPerNecramTurn = 2; // cap +2 PT/tour via marques appliquees

        /// <summary>
        /// Somme des marques venin actives sur tous les combattants vivants. Calcule la
        /// densite globale Floraison (1 valeur partagee, pas une "densite par camp").
        /// </summary>
        public static int GetGlobalDensity(Frame f)
        {
            int density = 0;
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                if (c->HP <= 0) continue;
                density += c->VeninStacks;
            }
            return density;
        }

        /// <summary>
        /// Palier Floraison [0..2] : tier 0 (densite 1-3), tier 1 (4-6), tier 2 (7+).
        /// Retourne -1 si densite = 0 (pas de marque -> tick rien). Pratique pour les
        /// hooks regen / halo toxique.
        /// </summary>
        public static int GetFloraisonTier(int density)
        {
            if (density <= 0) return -1;
            if (density >= Tier3Threshold) return 2;
            if (density >= Tier2Threshold) return 1;
            return 0;
        }

        public static int GetTickDmgPerMark(int density)
        {
            int tier = GetFloraisonTier(density);
            switch (tier)
            {
                case 0:  return TickDmgPerMark_Tier1;
                case 1:  return TickDmgPerMark_Tier2;
                case 2:  return TickDmgPerMark_Tier3;
                default: return 0;
            }
        }

        /// <summary>
        /// Applique `amount` marques venin sur `target`, capped a MaxStacksPerTarget.
        /// Retourne le nombre EFFECTIVEMENT applique (peut etre < amount si proche du cap).
        ///
        /// Gain Putrefaction owner : si `applierIsNecram` true, on increment PT du Necram
        /// (lookup via Filter) ; cap +2 PT par tour Necram via PutrefactionMarksGainedThisTurn.
        /// </summary>
        public static int ApplyMark(Frame f, Combatant* target, int amount, int currentTurn)
        {
            if (target == null || target->HP <= 0 || amount <= 0) return 0;
            int before = target->VeninStacks;
            int after = before + amount;
            if (after > MaxStacksPerTarget) after = MaxStacksPerTarget;
            int applied = after - before;
            if (applied <= 0) return 0;
            target->VeninStacks = after;

            // Gain Putrefaction owner Necram (cap +2 PT/tour via marques appliquees).
            // En 1v1 simple : on cherche le seul Necram vivant. En multi (Phase 6+) on
            // tracera un VeninOwnerPlayerIndex sur le Combatant porteur si necessaire.
            GainPutrefactionFromMarkApply(f, applied, currentTurn);

            Log.Info($"[Venin] Apply +{applied} marque(s) sur P{target->PlayerIndex} ({before}->{after}). Density global={GetGlobalDensity(f)}");
            return applied;
        }

        public static void RemoveAllMarks(Combatant* target)
        {
            if (target == null) return;
            target->VeninStacks = 0;
        }

        /// <summary>
        /// 3.5.a.i — Morsure Putride : si la cible meurt avec des marques actives, on les
        /// transfere sur l'ennemi du Necram vivant le plus proche (Manhattan) — i.e. un
        /// autre combatant du camp adverse au Necram, allie du target mort. En 1v1 il n'y
        /// a personne d'autre -> marques perdues silencieusement.
        ///
        /// `casterPlayerIndex` = PlayerIndex du Necram qui a kill (necessaire pour identifier
        /// qui est "ennemi de qui"). On filtre les candidats par PlayerIndex == deadTarget.PlayerIndex
        /// (= meme camp que le mort = ennemi du Necram), vivant, et != deadTarget.
        ///
        /// Le transfert respecte le cap MaxStacksPerTarget : si le receveur a deja des marques,
        /// on add jusqu'au cap (et le reste est perdu).
        /// </summary>
        public static bool TryTransferVeninOnKill(Frame f, Combatant* deadTarget, int casterPlayerIndex, int currentTurn)
        {
            if (deadTarget == null || deadTarget->VeninStacks <= 0) return false;
            int amount = deadTarget->VeninStacks;
            int deadCamp = deadTarget->PlayerIndex;

            // Trouver l'autre ennemi du Necram (= allie du target mort) vivant le plus proche
            // en Manhattan.
            Combatant* bestReceiver = null;
            int bestDist = int.MaxValue;
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                if (c->HP <= 0) continue;
                if (c->PlayerIndex != deadCamp) continue; // doit etre ennemi du Necram
                // skip le target mort lui-meme (HP==0 deja filtre, mais on garde par securite si appel
                // intervient avant que HP soit set a 0).
                if (c->GridX == deadTarget->GridX && c->GridY == deadTarget->GridY && c->PlayerIndex == deadTarget->PlayerIndex)
                {
                    continue;
                }
                int dx = c->GridX - deadTarget->GridX; if (dx < 0) dx = -dx;
                int dy = c->GridY - deadTarget->GridY; if (dy < 0) dy = -dy;
                int dist = dx + dy;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestReceiver = c;
                }
            }

            if (bestReceiver == null)
            {
                // 1v1 case OU pas d'autre allie vivant -> marques perdues (vide les stacks du mort
                // par hygiene, meme s'il est mort et que le combatant ne tickera plus).
                Log.Info($"[Venin] Transfer skip (pas d'autre ennemi vivant pour Necram P{casterPlayerIndex}) — {amount} marques perdues.");
                deadTarget->VeninStacks = 0;
                return false;
            }

            int receiverBefore = bestReceiver->VeninStacks;
            int receiverAfter = receiverBefore + amount;
            if (receiverAfter > MaxStacksPerTarget) receiverAfter = MaxStacksPerTarget;
            int transferred = receiverAfter - receiverBefore;
            bestReceiver->VeninStacks = receiverAfter;
            deadTarget->VeninStacks = 0;

            Log.Info($"[Venin] Transfer Morsure Putride : {transferred}/{amount} marques transferees du mort P{deadTarget->PlayerIndex} -> P{bestReceiver->PlayerIndex} ({receiverBefore}->{receiverAfter}, Manhattan {bestDist})");
            return transferred > 0;
        }

        /// <summary>
        /// Tick venin sur `target` au debut de son sub-turn. Inflige
        /// `VeninStacks * GetTickDmgPerMark(density)` direct sur HP, ignore boucliers
        /// et reductions de degats. Idempotent dans le meme round (LastVeninTickOnTurn).
        ///
        /// Retourne true si un tick a ete effectivement applique.
        /// </summary>
        public static bool TryTick(Frame f, Combatant* target, int currentTurn)
        {
            if (target == null || target->HP <= 0) return false;
            if (target->VeninStacks <= 0) return false;
            if (target->LastVeninTickOnTurn == currentTurn) return false;

            int density = GetGlobalDensity(f);
            int dmgPerMark = GetTickDmgPerMark(density);
            if (dmgPerMark <= 0) return false;
            int totalDmg = target->VeninStacks * dmgPerMark;

            // 3.5.b.i — Marque Sacrificielle : bonus flat +20 dmg sur le tick (pas par marque)
            // pendant 3 rounds (Bible V7.1 "les marques sur la cible infligent +20 dgts par tick").
            // S'ajoute APRES le calcul `stacks * dmgPerMark` car c'est un bonus de tick, pas un
            // bonus par marque. Bypass shield/reduction comme le tick venin lui-meme.
            int marqueSacMagnitude = StatusHelper.GetMagnitude(target, StatusKind.MarqueSacrificielle, 0);
            int marqueSacBonus = 0;
            if (marqueSacMagnitude > 0)
            {
                marqueSacBonus = marqueSacMagnitude;
                totalDmg += marqueSacBonus;
            }

            int hpBefore = target->HP;
            target->HP -= totalDmg;
            if (target->HP < 0) target->HP = 0;
            target->LastVeninTickOnTurn = currentTurn;

            // Le tick venin compte AUSSI comme degats subis ce round (consistent avec
            // Prescience Nightseer + Ressac Vital tracking).
            target->DamageTakenThisRound += totalDmg;

            // Gain Putrefaction Necram : +1 PT par tick global (Bible "tour ou une unite
            // ennemie subit du DoT venin"). NON cape par PutrefactionMarksGainedThisTurn
            // (ce cap concerne uniquement les gains "par marque appliquee").
            GainPutrefactionFromTick(f);

            if (marqueSacBonus > 0)
            {
                Log.Info($"[Venin] Tick P{target->PlayerIndex} : {target->VeninStacks} marques * {dmgPerMark} + {marqueSacBonus} (Marque Sacrificielle) = -{totalDmg} HP (HP {hpBefore} -> {target->HP}, density {density})");
            }
            else
            {
                Log.Info($"[Venin] Tick P{target->PlayerIndex} : {target->VeninStacks} marques * {dmgPerMark} dmg = -{totalDmg} HP (HP {hpBefore} -> {target->HP}, density {density})");
            }

            // 3.5.b.ii — Symbiose Morbide : tout Necram vivant porteur du status est soigne
            // de min(stacks, 4) * Magnitude HP a chaque tick venin sur un ennemi (Bible V7.1
            // "cap 4 marques actives qui comptent pour le heal"). VeninStacks est deja cap 4
            // via ApplyMark mais on garde le min(,4) defense en profondeur.
            int stacksForHeal = target->VeninStacks > 4 ? 4 : target->VeninStacks;
            if (stacksForHeal > 0)
            {
                var symbioseFilter = f.Filter<Combatant>();
                while (symbioseFilter.NextUnsafe(out EntityRef _, out Combatant* necram))
                {
                    if (necram->Class != NymoraClass.Necram) continue;
                    if (necram->HP <= 0) continue;
                    int healPerMark = StatusHelper.GetMagnitude(necram, StatusKind.SymbioseMorbide, 0);
                    if (healPerMark <= 0) continue;
                    int healAmount = stacksForHeal * healPerMark;
                    int necramHpBefore = necram->HP;
                    necram->HP = necram->HP + healAmount > necram->MaxHP ? necram->MaxHP : necram->HP + healAmount;
                    int realHeal = necram->HP - necramHpBefore;
                    if (realHeal > 0)
                    {
                        Log.Info($"[Symbiose Morbide] Necram P{necram->PlayerIndex} heal +{realHeal} HP (tick sur P{target->PlayerIndex}, {stacksForHeal} marques * {healPerMark}) : {necramHpBefore}->{necram->HP}");
                    }
                }
            }

            return true;
        }

        // ===== Putrefaction gain hooks (1v1 — owner = unique Necram vivant) =====

        private static void GainPutrefactionFromMarkApply(Frame f, int marksApplied, int currentTurn)
        {
            if (marksApplied <= 0) return;
            int maxRes = CombatantStats.GetMaxResource(NymoraClass.Necram);
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                if (c->Class != NymoraClass.Necram) continue;
                if (c->HP <= 0) continue;
                int alreadyGained = c->PutrefactionMarksGainedThisTurn;
                int remainingCap = PutrefactionGainCapPerNecramTurn - alreadyGained;
                if (remainingCap <= 0) return;
                int gain = marksApplied * PutrefactionGainPerMarkApplied;
                if (gain > remainingCap) gain = remainingCap;
                int before = c->Resource;
                c->Resource = before + gain > maxRes ? maxRes : before + gain;
                int realGain = c->Resource - before;
                if (realGain > 0)
                {
                    c->PutrefactionMarksGainedThisTurn = alreadyGained + realGain;
                    Log.Info($"[Putrefaction] +{realGain} PT (marque appliquee, {c->PutrefactionMarksGainedThisTurn}/{PutrefactionGainCapPerNecramTurn} ce tour) sur P{c->PlayerIndex} : {before}->{c->Resource}/{maxRes}");
                }
                return;
            }
        }

        private static void GainPutrefactionFromTick(Frame f)
        {
            int maxRes = CombatantStats.GetMaxResource(NymoraClass.Necram);
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                if (c->Class != NymoraClass.Necram) continue;
                if (c->HP <= 0) continue;
                int before = c->Resource;
                c->Resource = before + PutrefactionGainPerTickGlobal > maxRes
                    ? maxRes
                    : before + PutrefactionGainPerTickGlobal;
                if (c->Resource != before)
                {
                    Log.Info($"[Putrefaction] +{c->Resource - before} PT (tick global) sur P{c->PlayerIndex} : {before}->{c->Resource}/{maxRes}");
                }
                return;
            }
        }
    }
}
