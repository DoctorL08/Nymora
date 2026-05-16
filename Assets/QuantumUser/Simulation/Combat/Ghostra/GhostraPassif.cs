namespace Quantum
{
    /// <summary>
    /// 3.6 — Helpers passif Ghostra "L'ANGLE MORT" (Bible V7.1).
    ///
    /// Systeme de DESYNCHRONISATION qui module les sorts dorsaux selon la DENSITE
    /// de leurres actifs :
    ///   - ANGLE 1 (0 leurre)   : aucun bonus dorsal
    ///   - ANGLE 2 (1-2 leurres): +50 dmg dorsal + applique PLAIE OUVERTE (40/tour x 2t)
    ///   - ANGLE 3 (3 leurres)  : +80 dmg dorsal + debloque PERMUTATION 0 PA 1x/tour
    ///
    /// Le passif est NON-LINEAIRE : recompense la densite de leurres plutot que les
    /// degats infliges. Bible ligne 1027 "Le passif recompense la DENSITE de leurres.
    /// La Ghostra ne devient pas plus puissante en infligeant des degats — elle devient
    /// plus illisible."
    ///
    /// Pas de state Combatant DSL dedie : Angle est recalcule a la volee via
    /// DecoyHelpers.CountActive. Cheap.
    /// </summary>
    public static unsafe class GhostraPassif
    {
        // Bible V7.1 — verrouille (modif = bump CombatRulesVersion).
        public const int Angle2DorsalBonusDmg = 50; // +50 dmg dorsal a 1-2 leurres
        public const int Angle3DorsalBonusDmg = 80; // +80 dmg dorsal a 3 leurres

        // PLAIE OUVERTE (Bible Angle 2+ auto sur dorsal) — DoT 40/tour pendant 2 rounds.
        // StatusKind.PlaieOuverte sera ajoute en 3.7.a (Frappe Fantôme / Marque de l'Ombre
        // l'appliquent aussi). En 3.6 framework : le helper expose les constantes Bible
        // mais l'application effective ApplyPlaieOuverte() est un stub log-only en
        // attendant le StatusKind dedie.
        public const int PlaieOuverteDmgPerTurn = 40;
        public const int PlaieOuverteDurationRounds = 2;

        /// <summary>
        /// Retourne l'Angle (1 / 2 / 3) en fonction du nombre de leurres actifs.
        /// Bible V7.1 :
        ///   - 0 leurre  -> Angle 1 (passif neutre)
        ///   - 1-2 leurres -> Angle 2
        ///   - 3 leurres -> Angle 3
        /// </summary>
        public static int ComputeAngleLevel(int activeDecoys)
        {
            if (activeDecoys <= 0) return 1;
            if (activeDecoys >= 3) return 3;
            return 2;
        }

        /// <summary>
        /// Bonus de degats dorsal selon l'Angle :
        ///   - Angle 1 : 0
        ///   - Angle 2 : +50
        ///   - Angle 3 : +80
        /// </summary>
        public static int GetDorsalBonusDmg(int angleLevel)
        {
            switch (angleLevel)
            {
                case 2: return Angle2DorsalBonusDmg;
                case 3: return Angle3DorsalBonusDmg;
                default: return 0;
            }
        }

        /// <summary>
        /// Helper combine : bonus dorsal pour un Ghostra donne, calcule en lisant
        /// directement ses leurres actifs. Utilise par les sorts 3.7.a/b/c qui appliquent
        /// le bonus passif si dorsal au moment du dmg.
        /// </summary>
        public static int GetDorsalBonusForGhostra(Combatant* ghostra)
        {
            if (ghostra == null || ghostra->HP <= 0) return 0;
            if (ghostra->Class != NymoraClass.Ghostra) return 0;
            int active = DecoyHelpers.CountActive(ghostra);
            int angle = ComputeAngleLevel(active);
            return GetDorsalBonusDmg(angle);
        }

        /// <summary>
        /// Bible Angle 2+ : si la Ghostra touche une cible en DORSAL, applique
        /// automatiquement PLAIE OUVERTE (40 dmg/tour x 2 rounds). Stub log-only en 3.6
        /// (le StatusKind.PlaieOuverte sera ajoute en 3.7.a avec Frappe Fantôme).
        ///
        /// Appel par les sorts 3.7.a/b/c apres le dmg loop si la touche etait dorsale.
        /// Si angle == 1 (0 leurre) : no-op (Bible : passif neutre Angle 1).
        /// </summary>
        public static void ApplyPlaieOuverteIfAngle2Plus(Frame f, Combatant* caster, Combatant* target, int currentTurn)
        {
            if (caster == null || target == null) return;
            if (target->HP <= 0) return;
            if (caster->Class != NymoraClass.Ghostra) return;
            int active = DecoyHelpers.CountActive(caster);
            int angle = ComputeAngleLevel(active);
            if (angle < 2) return;

            // STUB 3.6 : StatusKind.PlaieOuverte sera ajoute en 3.7.a. Pour l'instant
            // on log l'intention pour pouvoir valider l'enchainement passif Angle 2+ -> dorsal.
            Log.Info($"[Angle Mort] Angle {angle} dorsal sur P{target->PlayerIndex} -> PLAIE OUVERTE ({PlaieOuverteDmgPerTurn}/tour x {PlaieOuverteDurationRounds}t) — stub 3.6, branche reelle en 3.7.a");
        }

        /// <summary>
        /// Hook a appeler au DEBUT du sub-turn du combattant `active`. Pour la Ghostra
        /// uniquement : tick lifetime de ses leurres (expiration apres 2 rounds).
        /// Pattern parallele a NecramPassif.OnSubTurnStart.
        ///
        /// Pas d'effet de pression continu type halo Necram : la Ghostra n'a pas
        /// d'aura passive — son passif est entierement conditionnel (sur dorsal hit).
        /// </summary>
        public static void OnSubTurnStart(Frame f, Combatant* active, int currentTurn)
        {
            if (active == null || active->HP <= 0) return;
            if (active->Class != NymoraClass.Ghostra) return;
            DecoyHelpers.TickLifetimeAtSubTurnStart(active, currentTurn);
        }
    }
}
