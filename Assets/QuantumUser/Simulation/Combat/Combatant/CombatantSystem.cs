namespace Quantum
{
    using Photon.Deterministic;

    /// <summary>
    /// Spawn les 2 combattants au demarrage du combat — via ISignalOnPlayerAdded pour
    /// les DEUX modes (IA et PvP).
    ///
    /// 2.2 — spawn hardcoded P0=Ghostra/P1=Soulrender en OnInit (test 1 seul client).
    /// 4.14.d — split OnInit (IA hardcoded) / OnPlayerAdded (PvP via RuntimePlayer).
    /// 5.4 (18 mai 2026) — unification : les 2 modes passent par OnPlayerAdded. Le bootstrap
    ///   IA (CombatBootstrapIA) AddPlayer les 2 slots localement avec RuntimePlayer.ClassId
    ///   resolu depuis DeckBridge (slot 0 = Lorenzo) + config inspector (slot 1 = bot).
    ///   Fix bug : avant, OnInit hardcodait P0=Ghostra meme si Lorenzo avait choisi une
    ///   autre classe dans le hub. Maintenant DeckBridge pilote la classe en IA comme en PvP.
    ///
    /// Positions hardcodees : slot 0 = (2,7) face SE, slot 1 = (7,2) face NW (cf POLISH-5e).
    /// </summary>
    public unsafe class CombatantSystem : SystemSignalsOnly, ISignalOnPlayerAdded
    {
        // POLISH-5e (17 mai) : grille 10x10, spawn sur la diagonale horizontale mediane du
        // losange iso (gx+gy=9). P0 a gauche-bas visuel = (2, 7), P1 a droite-haut visuel =
        // (7, 2). Distance Manhattan 10, distance world horizontal ~5 unites (face-a-face
        // pile dans l'arene). Cale sur les "angles gauche/droit" iso designe par Lorenzo
        // sur la map Map_Combat_1.
        private const int P1SpawnX = 2;
        private const int P1SpawnY = 7;
        private const int P2SpawnX = 7;
        private const int P2SpawnY = 2;

        // Tuto (1 juin) : mannequin placé vers le CENTRE-AVANT de l'arène, devant le joueur, pour un
        // tuto DIRECT et lisible. P0 reste (2,7) ; P1 = (3,4) -> Manhattan |3-2|+|4-7| = 4, écran =
        // droite + bas de P0 (vers le centre du losange iso), facing SE/NW conservé (axe -gy dominant).
        // Position calée sur le repère de Lorenzo (point rouge centre-avant).
        // Gate sur RuntimeConfig.TutorialPassiveBot (posé par CombatBootstrapIA en mode tuto).
        private const int TutorialP2SpawnX = 3;
        private const int TutorialP2SpawnY = 4;

        // 5.4 — Plus de spawn en OnInit. Les 2 modes (IA + PvP) attendent OnPlayerAdded.
        // Le bootstrap respectif (CombatBootstrapIA / CombatBootstrapCasual) gere l'AddPlayer.

        /// <summary>
        /// 5.4 — Spawn le Combatant pour ce player en utilisant le ClassId du RuntimePlayer.
        ///   Mode IA  : CombatBootstrapIA AddPlayer(0) et AddPlayer(1) localement
        ///              avec ClassId Lorenzo (DeckBridge) et ClassId Bot (configurable).
        ///   Mode PvP : CombatBootstrapCasual AddPlayer(localSlot) ; l'autre slot arrive
        ///              via Quantum sync reseau quand l'autre client AddPlayer aussi.
        /// firstTime=true uniquement au tout 1er AddPlayer du slot ; les reconnections
        /// (firstTime=false) ne re-spawnent pas (le Combatant existe deja).
        /// </summary>
        public void OnPlayerAdded(Frame f, PlayerRef player, bool firstTime)
        {
            // Reconnection : Combatant deja existant, ne pas re-spawn.
            if (!firstTime) return;

            int slot = player;
            // 1v1 cap : ignorer les slots > 1 (defensive, MaxPlayers Photon room = 2 deja).
            if (slot < 0 || slot > 1)
            {
                Log.Warn($"[CombatantSystem] OnPlayerAdded slot {slot} hors range 1v1 — ignore.");
                return;
            }

            var runtimePlayer = f.GetPlayerData(player);
            if (runtimePlayer == null)
            {
                Log.Warn($"[CombatantSystem] OnPlayerAdded slot {slot} : RuntimePlayer null — fallback Soulrender.");
            }

            var nymoraClass = runtimePlayer != null && runtimePlayer.ClassId != NymoraClass.None
                ? runtimePlayer.ClassId
                : NymoraClass.Soulrender; // fallback safe si bootstrap n'a pas set ClassId

            // 5.1 — EQUIPE : fournie par le bootstrap equipe (2v2/3v3) via RuntimePlayer.TeamId.
            //   Non fournie (sentinelle -1 = bootstraps 1v1 Casual/IA) -> fallback = slot.
            //   En 1v1 : team == slot == PlayerIndex -> les predicats TeamHelper restent
            //   strictement equivalents aux anciens checks PlayerIndex (non-regression).
            int teamId = (runtimePlayer != null && runtimePlayer.TeamId >= 0)
                ? runtimePlayer.TeamId
                : slot;

            // 5.2 — RANG intra-équipe (vote capitaine, brique 5.6). -1 = non fourni -> ordre par PlayerIndex.
            int teamOrder = runtimePlayer != null ? runtimePlayer.TeamOrder : -1;

            // Tuto : mannequin (slot 1) rapproché à 3 cases du joueur (slot 0 inchangé).
            bool tutorialClose = f.RuntimeConfig.TutorialPassiveBot && slot == 1;
            int x = slot == 0 ? P1SpawnX : (tutorialClose ? TutorialP2SpawnX : P2SpawnX);
            int y = slot == 0 ? P1SpawnY : (tutorialClose ? TutorialP2SpawnY : P2SpawnY);

            SpawnCombatant(f, playerIndex: slot, teamId: teamId, teamOrder: teamOrder, nymoraClass: nymoraClass, x: x, y: y);
            string modeTag = f.RuntimeConfig.IsBotMatch ? "IA" : "PvP";
            Log.Info($"[CombatantSystem] {modeTag} spawn slot {slot} class {nymoraClass} at ({x},{y})");
        }

        private static EntityRef SpawnCombatant(Frame f, int playerIndex, int teamId, int teamOrder, NymoraClass nymoraClass, int x, int y)
        {
            int maxHP = CombatantStats.GetMaxHP(nymoraClass);
            int maxPA = CombatantStats.GetMaxPA(nymoraClass);
            int maxPM = CombatantStats.GetMaxPM(nymoraClass);

            var combatantData = new Combatant
            {
                PlayerIndex = playerIndex,
                TeamId = teamId, // 5.1 — appartenance d'equipe (1v1 : == playerIndex)
                TeamOrder = teamOrder, // 5.2 — rang intra-équipe (vote capitaine ; -1 = ordre par PlayerIndex)
                Class = nymoraClass,
                MaxHP = maxHP,
                HP = maxHP,
                MaxPA = maxPA,
                PA = maxPA,
                MaxPM = maxPM,
                PM = maxPM,
                GridX = x,
                GridY = y,
                Resource = 0,
                LastResourceGainOnHitTurn = -1,
                OncePerMatchUsedFlags = 0,
                BonusPANextTurn = 0,
                LastAmeLaceeUsedOnTurn = -1000, // pas de cooldown au spawn (Bible : signature jouable des HG=5)
                LastCastOnTurn = -1,             // 2.12.bis : aucun cast au spawn (View detecte diff > 0)
                LastCastSpellId = SpellId.None,  // 2.12.bis
                LastTrapTriggeredOnTurn = -1000, // 2.15.c : aucun trap declenche au spawn (Seve Sauvage)
                LastTraquenardUsedOnTurn = -1000, // 2.16 : aucun cooldown au spawn (Bible V7.1)
                RepresaillesReflectsLeft = -1,   // 3.3.a.iii : -1 = pas de cap (defaut). Set explicitement par Represailles (4) / Riposte Carmin (-1).
                StoicismeExpiresOnTurn = -1,     // 3.3.c : inactif au spawn (Stoicisme set a currentTurn+2 au cast).
                HitsTakenThisRound = 0,          // 3.3.c : aucun hit au spawn.
                HitsTakenLastRound = 0,          // 3.3.c : aucun hit precedent au spawn.
                EffondrementAnnouncedOnTurn = -1, // 3.3.d : pas d'annonce active au spawn.
                LastEffondrementUsedOnTurn = -1000, // 3.3.d : signature jouable des FD=3 au spawn.
                EffondrementTargetEntity = EntityRef.None, // 3.3.d : pas de cible snapshot au spawn.
                LastVirusFatalUsedOnTurn = -1000, // 3.5.c.vi : signature jouable des PT=6 au spawn.
                LastPermutationOnTurn = -1000,    // 3.6 : permutation jouable des Angle 3 au spawn.
                LastPasDansLOmbreOnTurn = -1000,  // 3.7.b.ii : Pas dans l'Ombre jouable au spawn (cap 1x/tour).
                LastFacingForcedOnTurn = -1000,   // 3.7.a.iii : pas de direction forcee au spawn (lu par Frappe Fantome).
                LastDagueLanceeOnTurn = -1000,    // 3.7.b.iv : Dague Lancee jouable au spawn (cap 2x/tour).
                DagueLanceeCountThisTurn = 0,     // 3.7.b.iv : 0 cast au spawn.
                LastExecutionSpectraleUsedOnTurn = -1000, // 3.6 : signature Ghostra jouable au spawn (reserve pour 3.7.d).
                // POLISH-5e (17 mai) : nouveau facing post-spawn-diagonal.
                //   P0 spawn (2,7) regarde vers P1 (7,2) : world delta = ((7-2)*0.5, (2-7)*0.25)
                //     = (+2.5, -1.25) -> Sud-Est = IsoFacing.SE.
                //   P1 spawn (7,2) regarde vers P0 (2,7) : world delta = (-2.5, +1.25) -> Nord-Ouest = NW.
                Facing = (playerIndex == 0) ? IsoFacing.SE : IsoFacing.NW,
            };

            var entity = f.Create();
            f.Add(entity, combatantData);

            // Garantit un etat propre pour les 8 slots de statuses (2.10.a).
            // Important : default(Combatant) suffirait theoriquement, mais on est explicite
            // pour eviter une dependance silencieuse a l'init Quantum des fixed arrays.
            var combatant = f.Unsafe.GetPointer<Combatant>(entity);
            StatusHelper.ClearAll(combatant);

            GridHelpers.SetOccupant(f, x, y, entity);

            return entity;
        }
    }
}
