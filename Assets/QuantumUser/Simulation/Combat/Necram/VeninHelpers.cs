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
    /// (toutes cibles confondues). 3 paliers (refonte 29 mai) :
    ///   - Densite 1-2 : 40 dmg/marque/tick
    ///   - Densite 3-6 : 50 dmg/marque/tick + regen Necram +10/marque + halo toxique rayon 3
    ///   - Densite 7+  : 60 dmg/marque/tick + Virus Fatal debloque
    ///
    /// Ressource Putrefaction gain rules (Bible) :
    ///   - +1 PT par tick venin global (TickAll appel ce gain via Necram lookup).
    ///   - +1 PT par marque appliquee (cap +2 PT par tour Necram via PutrefactionMarksGainedThisTurn).
    /// </summary>
    public static unsafe class VeninHelpers
    {
        // Bible V7.1 — verrouille (modif = bump CombatRulesVersion).
        public const int MaxStacksPerTarget = 4;

        // Refonte 29 mai : clock renforce 40/50/60, palier 2 (tier 1) des densite 3.
        public const int TickDmgPerMark_Tier1 = 40; // densite 1-2
        public const int TickDmgPerMark_Tier2 = 50; // densite 3-6
        public const int TickDmgPerMark_Tier3 = 60; // densite 7+

        public const int Tier2Threshold = 3; // densite >= 3 -> tier 2 (refonte : etait 4)
        public const int Tier3Threshold = 7; // densite >= 7 -> tier 3

        public const int PutrefactionGainPerMarkApplied = 1;
        public const int PutrefactionGainPerTickGlobal = 1;
        public const int PutrefactionGainCapPerNecramTurn = 2; // cap +2 PT/tour via marques appliquees

        // Patch 5 juin — « les poisons durent 2 tours max ». Avant, les marques venin n'expiraient
        // jamais par duree (consommation only). Desormais chaque application (re)pose le minuteur
        // StatusKind.VeninDecay pour ce nombre de rounds ; a son expiration, ClearExpiredVenin vide
        // les VeninStacks du porteur (cf TurnSystem fin de round).
        public const int VeninDurationTurns = 2;

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
        /// FIX MIROIR (8 juin / v141) — densite PAR-NECRAM, vue "cote cible".
        /// Somme des marques venin SUBIES par l'equipe `teamPlayerIndex` (= venin pose par l'ennemi
        /// de cette equipe). Utilise pour le palier de TICK sur une cible : le tick scale avec le pool
        /// de venin que SON poisonneur a construit, pas avec le venin global de la map.
        /// En 1v1 non-miroir : identique a l'ancienne densite globale (l'ennemi non-Necram ne pose
        /// pas de venin). En miroir : chaque Necram a son propre pool, fini le pooling qui faussait
        /// les chiffres et la regen.
        /// </summary>
        public static int GetDensityOnTeam(Frame f, int teamPlayerIndex)
        {
            int density = 0;
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                if (c->HP <= 0) continue;
                if (c->PlayerIndex != teamPlayerIndex) continue;
                density += c->VeninStacks;
            }
            return density;
        }

        /// <summary>
        /// FIX MIROIR (8 juin / v141) — densite PAR-NECRAM, vue "cote Necram".
        /// Somme des marques venin APPLIQUEES par le Necram `necramPlayerIndex` (= venin sur tous ses
        /// ennemis). Utilise pour la REGEN et le HALO du Necram : il ne profite que de SON poison,
        /// jamais de celui d'un Necram adverse. En 1v1 non-miroir : identique a la densite globale.
        /// </summary>
        public static int GetDensityAppliedByNecram(Frame f, int necramPlayerIndex)
        {
            int density = 0;
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                if (c->HP <= 0) continue;
                if (c->PlayerIndex == necramPlayerIndex) continue;
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

            // 3.7.c.ii — Voile Spectral : si la cible porte DotImmune, skip l'apply de marque venin.
            // Bible "immunisee a toute nouvelle application de DoT" : VeninStacks est un DoT.
            // Log discret pour faciliter debug, pas de side-effect (pas de gain Putrefaction).
            if (StatusHelper.Has(target, StatusKind.DotImmune))
            {
                Log.Info($"[Venin] Apply +{amount} marque(s) SKIP sur P{target->PlayerIndex} (DotImmune Voile Spectral actif)");
                return 0;
            }

            int before = target->VeninStacks;
            int after = before + amount;
            if (after > MaxStacksPerTarget) after = MaxStacksPerTarget;
            int applied = after - before;
            target->VeninStacks = after;

            // Gain Putrefaction owner Necram : "PT sur l'intention" (decision Lorenzo 5 juin, B4).
            // Base sur AMOUNT (marques demandees) et NON `applied` : le Necram gagne ses PT pour avoir
            // travaille le poison MEME si la cible est saturee (applied=0, deja 4 marques qui ne
            // redescendent jamais). Cap +2 PT/tour conserve (PutrefactionMarksGainedThisTurn). DotImmune
            // (Voile Spectral) a deja court-circuite plus haut -> pas de PT (poison totalement nie).
            // Owner = le Necram dont l'EQUIPE != celle de la cible marquee (le venin est tjs pose sur
            // un ennemi). Robuste en miroir Necram vs Necram (chacun recolte SES PT) ; en 1v1 simple le
            // seul Necram est tjs l'ennemi de la cible. (Le cas Carapace Visqueuse marque l'attaquant :
            // pas de Necram en face -> 0 PT, ce qui est correct, c'est une riposte Ghostra.)
            // En multi (Phase 6+) il faudra un VeninOwnerPlayerIndex par porteur.
            GainPutrefactionFromMarkApply(f, amount, currentTurn, target->PlayerIndex);

            // Patch 5 juin — « les poisons durent 2 tours max » : (re)pose le minuteur d'expiration
            // a chaque application (refresh), MEME si la cible est saturee (applied=0) -> le Necram
            // entretient son poison et prolonge sa duree de 2 rounds. A l'expiration de VeninDecay,
            // ClearExpiredVenin (TurnSystem fin de round) vide les marques.
            StatusHelper.Apply(target, StatusKind.VeninDecay, magnitude: 0,
                turnsLeft: VeninDurationTurns, currentTurn);

            if (applied <= 0)
            {
                Log.Info($"[Venin] Apply +{amount} marque(s) SATURE sur P{target->PlayerIndex} (deja {before}/{MaxStacksPerTarget}) — PT gagnes sur l'intention");
                return 0;
            }

            Log.Info($"[Venin] Apply +{applied} marque(s) sur P{target->PlayerIndex} ({before}->{after}). Densite equipe={GetDensityOnTeam(f, target->PlayerIndex)}");
            return applied;
        }

        public static void RemoveAllMarks(Combatant* target)
        {
            if (target == null) return;
            target->VeninStacks = 0;
        }

        /// <summary>
        /// Patch 5 juin — « les poisons durent 2 tours max ». A appeler en fin de round (apres
        /// StatusHelper.DecrementAllOnTurnEnd, qui a deja expire les minuteurs VeninDecay echus).
        /// Pour chaque combattant qui porte encore des VeninStacks mais dont le minuteur VeninDecay
        /// a expire (plus actif), on vide les marques : le poison a vecu ses 2 tours sans etre
        /// rafraichi par une nouvelle application. Les marques consommees (Detonation/Virus Fatal)
        /// passent deja par VeninStacks=0 et sont ignorees ici (VeninStacks <= 0).
        /// </summary>
        public static void ClearExpiredVenin(Frame f)
        {
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                if (c->VeninStacks <= 0) continue;
                if (StatusHelper.Has(c, StatusKind.VeninDecay)) continue; // minuteur encore actif
                Log.Info($"[Venin] Marques expirees ({VeninDurationTurns} tours ecoules) sur P{c->PlayerIndex} : {c->VeninStacks} -> 0");
                c->VeninStacks = 0;
            }
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

            // FIX MIROIR v141 : palier de tick base sur le venin SUBI par l'equipe de la cible
            // (= le pool du Necram qui l'a empoisonnee), pas la densite globale poolee.
            int density = GetDensityOnTeam(f, target->PlayerIndex);
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

            // Refonte 29 mai — Brume Toxique : tick MAJORÉ si le porteur se tient dans la zone
            // (+BrumeToxiqueTickBonusPerMark par marque). Bypass shield/réduction comme le tick.
            // Patch 8 juin — owner-based : le tick n'est majoré que dans une Brume ADVERSE (pas la sienne).
            int brumeBonus = 0;
            if (GridHelpers.GetTerrainKind(f, target->GridX, target->GridY) == TerrainKind.BrumeToxique
                && FogHelpers.IsEnemyTerrainAt(f, target->GridX, target->GridY, target->PlayerIndex))
            {
                brumeBonus = target->VeninStacks * SpellRegistry.BrumeToxiqueTickBonusPerMark;
                totalDmg += brumeBonus;
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
            // (ce cap concerne uniquement les gains "par marque appliquee"). Owner = le Necram
            // dont l'EQUIPE != celle de la cible qui tick (cf ApplyMark) -> en miroir chaque
            // Necram gagne ses PT quand SON venin tick sur l'ennemi.
            GainPutrefactionFromTick(f, target->PlayerIndex);

            if (marqueSacBonus > 0)
            {
                Log.Info($"[Venin] Tick P{target->PlayerIndex} : {target->VeninStacks} marques * {dmgPerMark} + {marqueSacBonus} (Marque Sacrificielle) = -{totalDmg} HP (HP {hpBefore} -> {target->HP}, density {density})");
            }
            else
            {
                Log.Info($"[Venin] Tick P{target->PlayerIndex} : {target->VeninStacks} marques * {dmgPerMark} dmg = -{totalDmg} HP (HP {hpBefore} -> {target->HP}, density {density})");
            }

            // 3.5.b.ii — Symbiose Morbide (refonte 29 mai) : tout Necram vivant porteur du status
            // est soigne d'un montant FLAT (Magnitude = 15 HP) a CHAQUE tick venin sur un ennemi
            // (n'echelonne plus par nb de marques). Cap +60/tour Bible non traque ici (moot en 1v1,
            // a brancher avec un compteur si besoin en 2v2/3v3).
            {
                var symbioseFilter = f.Filter<Combatant>();
                while (symbioseFilter.NextUnsafe(out EntityRef _, out Combatant* necram))
                {
                    if (necram->Class != NymoraClass.Necram) continue;
                    if (necram->HP <= 0) continue;
                    // FIX MIROIR v141 : ne soigne QUE le Necram proprietaire du venin qui tick
                    // (= l'ennemi de la cible empoisonnee), jamais un Necram du camp de la cible.
                    if (necram->PlayerIndex == target->PlayerIndex) continue;
                    int healPerTick = StatusHelper.GetMagnitude(necram, StatusKind.SymbioseMorbide, 0);
                    if (healPerTick <= 0) continue;
                    int necramHpBefore = necram->HP;
                    necram->HP = necram->HP + healPerTick > necram->MaxHP ? necram->MaxHP : necram->HP + healPerTick;
                    int realHeal = necram->HP - necramHpBefore;
                    if (realHeal > 0)
                    {
                        Log.Info($"[Symbiose Morbide] Necram P{necram->PlayerIndex} heal +{realHeal} HP (tick sur P{target->PlayerIndex}, flat {healPerTick}) : {necramHpBefore}->{necram->HP}");
                    }
                }
            }

            return true;
        }

        // ===== Putrefaction gain hooks =====
        // Owner du venin = le Necram vivant dont l'EQUIPE (PlayerIndex) != celle de la cible
        // affectee. Le venin etant toujours pose sur un ENNEMI du Necram, ce Necram est forcement
        // l'auteur en 1v1 (mirror inclus). En 2v2/3v3 (Phase 6+) il faudra tracer un
        // VeninOwnerPlayerIndex par porteur pour lever l'ambiguite a plusieurs Necram par camp.

        private static void GainPutrefactionFromMarkApply(Frame f, int marksApplied, int currentTurn, int targetPlayerIndex)
        {
            if (marksApplied <= 0) return;
            int maxRes = CombatantStats.GetMaxResource(NymoraClass.Necram);
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                if (c->Class != NymoraClass.Necram) continue;
                if (c->HP <= 0) continue;
                if (c->PlayerIndex == targetPlayerIndex) continue; // owner = Necram d'en face (pas la cible/son camp)
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

        private static void GainPutrefactionFromTick(Frame f, int targetPlayerIndex)
        {
            int maxRes = CombatantStats.GetMaxResource(NymoraClass.Necram);
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                if (c->Class != NymoraClass.Necram) continue;
                if (c->HP <= 0) continue;
                if (c->PlayerIndex == targetPlayerIndex) continue; // owner = Necram d'en face (pas la cible/son camp)
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
