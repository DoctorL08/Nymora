namespace Quantum
{
    /// <summary>
    /// Phase 2 — MORT SUBITE / anti-antijeu. Empêche les parties qui s'éternisent.
    ///
    /// Entièrement dérivé de CombatState.TurnNumber (= ROUND, sémantique Dofus) -> AUCUN champ
    /// [Networked] ajouté (pas de régén prefab/scène). Paliers (décision Lorenzo 11 juin) :
    ///   - Round 23-24 : AVERTISSEMENT (la View affiche un bandeau, aucun effet gameplay).
    ///   - Round 25 (ENTRÉE, 1×) : purge TOUT le terrain (piliers/murs/failles, brume, pièges, voiles,
    ///     terrains Soulrender/Necram) en GARDANT les positions des combattants + ressources de classe
    ///     maxxées pour les deux équipes. EXCEPTION Ghostra : ses leurres ne sont PAS purgés (= sa
    ///     ressource, requise pour l'ulti) mais TÉLÉPORTÉS dans un coin ; et sa ressource n'est pas
    ///     "maxxée" (elle reflète le nombre de leurres, auto-synchronisé).
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
            RelocateGhostraDecoysToCorner(f); // PAS de purge : les leurres = ressource Ghostra (ulti) -> exil en coin.

            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                if (c->HP <= 0) continue;
                // Ghostra exclu : sa "ressource" = le NOMBRE de leurres actifs (auto-synchronisé via
                //   DecoyHelpers.CountActive). Forcer GetMaxResource ici la désynchroniserait (compteur
                //   à 3 sans 3 leurres réels) et son ulti Exécution Spectrale — qui exige 3 leurres
                //   ACTIFS — resterait injouable. On laisse ses leurres préservés gouverner sa ressource.
                if (c->Class == NymoraClass.Ghostra) continue;
                c->Resource = CombatantStats.GetMaxResource(c->Class);
            }
            Log.Info("[SuddenDeath] MORT SUBITE activée (round 25) : terrain purgé, leurres Ghostra exilés en coin, ressources maxxées.");
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
            // Ghostra exclu (cf OnActivate) : sa ressource = nombre de leurres (auto-sync), pas un
            //   compteur à maxer. On ne lui applique donc que le boost PA/PM.
            if (c->Class != NymoraClass.Ghostra)
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

        private static void RelocateGhostraDecoysToCorner(Frame f)
        {
            // Mort subite : on NE détruit PAS les leurres Ghostra (décision Lorenzo 11 juin). Ce sont sa
            //   RESSOURCE : l'ulti Exécution Spectrale exige 3 leurres ACTIFS, donc les purger la privait
            //   injustement de sa signature. À la place on les TÉLÉPORTE dans un coin de la map pour
            //   retirer l'emprise de plateau (un leurre bloque la vue ennemie + le mouvement sur sa case),
            //   comme les autres classes perdent murs/pièges/terrains. Kind/HP/SpawnedOnTurn préservés :
            //   seules les positions changent. Les obstacles sont déjà purgés -> seuls les combattants et
            //   les leurres déjà replacés bloquent une case de coin.
            var grid = f.Unsafe.GetPointerSingleton<GridSingleton>();
            int w = grid->Width;
            int h = grid->Height;

            var filter = f.Filter<Combatant>();
            while (filter.NextUnsafe(out EntityRef _, out Combatant* c))
            {
                if (c->Class != NymoraClass.Ghostra) continue;
                if (c->HP <= 0) continue; // un Ghostra mort : ses leurres ne bloquent déjà rien (HasAnyDecoyAt skip les morts).
                for (int i = 0; i < 3; i++)
                {
                    if (c->Decoys[i].Kind == DecoyKind.None) continue;
                    if (TryFindCornerTile(f, w, h, out int cx, out int cy))
                    {
                        c->Decoys[i].PosX = cx;
                        c->Decoys[i].PosY = cy;
                        Log.Info($"[SuddenDeath] leurre P{c->PlayerIndex} slot {i} exilé en coin ({cx},{cy})");
                    }
                    // Aucune case libre (map saturée, quasi impossible) : le leurre reste où il est.
                }
            }
        }

        /// <summary>
        /// Première case marchable ET libre (ni combattant ni leurre déjà replacé) en partant du coin
        /// (0,0), balayage ligne par ligne. Robuste aux maps irrégulières (cases carvées = non walkable,
        /// sautées). Déterministe -> sûr en Quantum.
        /// </summary>
        private static bool TryFindCornerTile(Frame f, int w, int h, out int outX, out int outY)
        {
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!GridHelpers.IsWalkable(f, x, y)) continue;
                if (GridHelpers.GetOccupant(f, x, y) != EntityRef.None) continue;
                if (DecoyHelpers.HasAnyDecoyAt(f, x, y)) continue;
                outX = x; outY = y;
                return true;
            }
            outX = -1; outY = -1;
            return false;
        }
    }
}
