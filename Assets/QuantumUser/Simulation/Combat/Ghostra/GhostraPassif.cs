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
        /// automatiquement PLAIE OUVERTE (40 dmg/tour x 2 rounds). Branche en 3.7.a.i.1.
        ///
        /// Appel par les sorts 3.7.a/b/c apres le dmg loop si la touche etait dorsale.
        /// Si angle == 1 (0 leurre) : no-op (Bible : passif neutre Angle 1).
        /// Si dorsal == false : no-op (effet conditionnel Bible explicite).
        /// </summary>
        public static void ApplyPlaieOuverteIfAngle2Plus(Frame f, Combatant* caster, Combatant* target, int currentTurn)
        {
            if (caster == null || target == null) return;
            if (target->HP <= 0) return;
            if (caster->Class != NymoraClass.Ghostra) return;
            if (!FacingHelpers.IsDorsalHit(caster, target)) return;

            // 3.7.c.ii — Voile Spectral : si la cible porte DotImmune, skip l'apply de PlaieOuverte.
            // Bible "immunisee a toute nouvelle application de DoT" : PlaieOuverte est un DoT.
            if (StatusHelper.Has(target, StatusKind.DotImmune))
            {
                Log.Info($"[Angle Mort] SKIP PlaieOuverte sur P{target->PlayerIndex} (DotImmune Voile Spectral actif)");
                return;
            }

            // 3.7.b.v — Marque de l'Ombre bypass requirement Angle 2+ : si target marquee ET
            //   dorsal Ghostra -> applique PlaieOuverte meme a Angle 1 (sans leurre). Bible
            //   "Si la cible est touchee en dorsal pendant ces 2 tours : applique automatiquement
            //   PLAIE OUVERTE" -> anti-tank par contournement Bible explicite.
            bool hasMarque = StatusHelper.Has(target, StatusKind.MarqueDeLOmbre);

            int active = DecoyHelpers.CountActive(caster);
            int angle = ComputeAngleLevel(active);
            if (angle < 2 && !hasMarque) return;

            // StatusHelper.Apply ecrase (refresh) si meme Kind deja present (turnsLeft + magnitude
            // reset). Pattern standard Bible Status. Recast meme tour -> reset a 2 rounds.
            StatusHelper.Apply(target, StatusKind.PlaieOuverte,
                magnitude: PlaieOuverteDmgPerTurn,
                turnsLeft: PlaieOuverteDurationRounds,
                currentTurn: currentTurn);
            string source = (angle >= 2) ? $"Angle {angle}" : "MARQUE";
            Log.Info($"[Angle Mort] {source} DORSAL sur P{target->PlayerIndex} -> PLAIE OUVERTE applique ({PlaieOuverteDmgPerTurn}/tour x {PlaieOuverteDurationRounds}t)");
        }

        /// <summary>
        /// 3.7.b — Éveil Spectral : variante de <see cref="ApplyPlaieOuverteIfAngle2Plus"/> où le
        /// dorsal est évalué depuis la CASE du leurre (ax,ay) et non depuis la Ghostra. Applique
        /// PLAIE OUVERTE si le leurre frappe en dorsal ET (Angle 2+ OU cible Marquée). Mêmes gardes
        /// (DotImmune skip, cible vivante).
        /// </summary>
        public static void ApplyPlaieOuverteFromPosition(Frame f, Combatant* caster, Combatant* target, int ax, int ay, int currentTurn)
        {
            if (caster == null || target == null) return;
            if (target->HP <= 0) return;
            if (caster->Class != NymoraClass.Ghostra) return;
            if (!FacingHelpers.IsDorsalFromPosition(ax, ay, target)) return;

            if (StatusHelper.Has(target, StatusKind.DotImmune))
            {
                Log.Info($"[Éveil Spectral] SKIP PlaieOuverte sur P{target->PlayerIndex} (DotImmune Voile Spectral actif)");
                return;
            }

            bool hasMarque = StatusHelper.Has(target, StatusKind.MarqueDeLOmbre);
            int active = DecoyHelpers.CountActive(caster);
            int angle = ComputeAngleLevel(active);
            if (angle < 2 && !hasMarque) return;

            StatusHelper.Apply(target, StatusKind.PlaieOuverte,
                magnitude: PlaieOuverteDmgPerTurn,
                turnsLeft: PlaieOuverteDurationRounds,
                currentTurn: currentTurn);
            string source = (angle >= 2) ? $"Angle {angle}" : "MARQUE";
            Log.Info($"[Éveil Spectral] {source} DORSAL (leurre {ax},{ay}) sur P{target->PlayerIndex} -> PLAIE OUVERTE ({PlaieOuverteDmgPerTurn}/tour x {PlaieOuverteDurationRounds}t)");
        }

        /// <summary>
        /// 3.7.a — Bonus dorsal Ghostra applique aux sorts offensifs (Lame Spectrale,
        /// Frappe Fantome, Lame Vorace, Saigne-Âme, Danse des Lames).
        /// Compute : check dorsal + lookup Angle Mort -> retourne 0/+50/+80.
        ///
        /// A appeler depuis SpellSystem dans le damage loop AVANT compute du dmg final.
        /// Si caster pas Ghostra ou hit non dorsal -> retourne 0 (no-op).
        /// </summary>
        public static int GetDorsalBonusIfApplicable(Combatant* caster, Combatant* target)
        {
            if (caster == null || target == null) return 0;
            if (caster->Class != NymoraClass.Ghostra) return 0;
            if (!FacingHelpers.IsDorsalHit(caster, target)) return 0;
            return GetDorsalBonusForGhostra(caster);
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
