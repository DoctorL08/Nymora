namespace Quantum
{
    /// <summary>
    /// Phase 2 — MORT SUBITE / anti-antijeu. Empêche les parties qui s'éternisent.
    ///
    /// Entièrement dérivé de CombatState.TurnNumber (= ROUND, sémantique Dofus) -> AUCUN champ
    /// [Networked] ajouté (pas de régén prefab/scène). Paliers (décision Lorenzo 11 juin) :
    ///   - Round 23-24 : AVERTISSEMENT (la View affiche un bandeau, aucun effet gameplay).
    ///   - Round 25 (ENTRÉE, 1×) : purge TOUT le terrain (piliers/murs/failles, brume, pièges, voiles,
    ///     terrains Soulrender/Necram, leurres) en GARDANT les positions des combattants + ressources
    ///     de classe maxxées pour les deux équipes.
    ///   - Round >= 25 : POISON D'ARÈNE croissant (100, 200, 300… VRAIS dégâts = HP direct, ignore
    ///     bouclier/réduction) à tous les vivants au début du round ; et chaque joueur démarre son tour
    ///     à 12 PA / 4 PM + ressources de classe au max (ApplyBoost, appelé au reset du joueur actif).
    ///
    /// Hook : TurnSystem.EnterTurnStart. PUR SIM -> bump CombatRulesVersion.
    /// </summary>
    public static unsafe class SuddenDeath
    {
        public const int WarningRound = 23;   // début de l'avertissement
        public const int ActivateRound = 25;  // mort subite active
        private const int PoisonBase = 100;    // round 25 = 100 HP, +100 par round
        private const int BoostPA = 12;
        private const int BoostPM = 4;

        /// <summary>Avertissement affiché (rounds 23-24), pas encore d'effet.</summary>
        public static bool IsWarning(int round) => round >= WarningRound && round < ActivateRound;

        /// <summary>Mort subite active (round >= 25).</summary>
        public static bool IsActive(int round) => round >= ActivateRound;

        /// <summary>Dégâts de poison du round (>= ActivateRound) : 100, 200, 300...</summary>
        public static int PoisonForRound(int round) => (round - ActivateRound + 1) * PoisonBase;

        /// <summary>Entrée en mort subite (round 25, UNE fois) : purge le terrain + max ressources des deux.</summary>
        public static void OnActivate(Frame f)
        {
            PurgeObstacles(f);
            PurgeFog(f);
            PurgeGridTerrain(f);
            PurgeDecoys(f);

            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                if (c->HP <= 0) continue;
                c->Resource = CombatantStats.GetMaxResource(c->Class);
            }
            Log.Info("[SuddenDeath] MORT SUBITE activée (round 25) : terrain purgé, ressources maxxées.");
        }

        /// <summary>Poison d'arène : VRAIS dégâts (HP direct, ignore bouclier/réduction) à tous les vivants.</summary>
        public static void ApplyPoison(Frame f, int round)
        {
            int dmg = PoisonForRound(round);
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                if (c->HP <= 0) continue;
                int before = c->HP;
                c->HP -= dmg;
                if (c->HP < 0) c->HP = 0;
                Log.Info($"[SuddenDeath] Poison d'arène round {round} : -{dmg} HP sur P{c->PlayerIndex} ({before} -> {c->HP})");
            }
        }

        /// <summary>Boost du joueur actif pendant la mort subite : 12 PA / 4 PM + ressources de classe max.</summary>
        public static void ApplyBoost(Combatant* c)
        {
            c->PA = BoostPA;
            c->PM = BoostPM;
            c->Resource = CombatantStats.GetMaxResource(c->Class);
        }

        // ===== Purge terrain (les combattants GARDENT leur position) =====

        private static void PurgeObstacles(Frame f)
        {
            // 1 obstacle = 1 entity référencée dans ObstacleSingleton.Tiles[index]. DestroyObstacle
            //   détruit l'entity + clear le slot (triggerPassiveHeal=false : pas de heal Colossar à la purge).
            var sing = f.Unsafe.GetPointerSingleton<ObstacleSingleton>();
            for (int i = 0; i < sing->Tiles.Length; i++)
            {
                var e = sing->Tiles[i].Obstacle;
                if (e != EntityRef.None && f.Exists(e))
                    ObstacleHelpers.DestroyObstacle(f, e, triggerPassiveHeal: false);
            }
        }

        private static void PurgeFog(Frame f)
        {
            // Voiles + pièges (mines/filet/piège bondissant) + owner de terrain : reset complet par case.
            var fog = f.Unsafe.GetPointerSingleton<FogSingleton>();
            for (int i = 0; i < fog->Tiles.Length; i++)
                fog->Tiles[i] = default(FogTile);
        }

        private static void PurgeGridTerrain(Frame f)
        {
            // Terrains posés (Vapeur Carmin / Sang Coagulé / Brume Toxique) : reset, GARDE Walkable + occupant.
            var grid = f.Unsafe.GetPointerSingleton<GridSingleton>();
            for (int i = 0; i < grid->Tiles.Length; i++)
            {
                grid->Tiles[i].Terrain = TerrainKind.None;
                grid->Tiles[i].TerrainTurnsLeft = 0;
                grid->Tiles[i].TerrainAppliedOnTurn = 0;
            }
        }

        private static void PurgeDecoys(Frame f)
        {
            // Leurres Ghostra : stockés dans Combatant.Decoys[] (n'occupent pas la grille) -> clear Kind.
            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* c))
                for (int i = 0; i < 3; i++)
                    c->Decoys[i].Kind = DecoyKind.None;
        }
    }
}
