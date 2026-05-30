namespace Quantum
{
    /// <summary>
    /// Moteur GENERIQUE des LIMITES PAR TOUR (caps "Nx/tour") et des RELANCES (cooldowns
    /// "N tours") introduits par la refonte d'equilibrage du 29 mai 2026.
    ///
    /// DECLARATION : par sort, dans SpellRegistry.SpellDef (MaxUsesPerTurn / CooldownTurns).
    /// APPLICATION : 100% generique dans SpellSystem (gate pre-PA + commit apres consommation
    ///               des PA), AUCUN code par sort. Remplace les champs dedies au cas par cas
    ///               (DagueLanceeCountThisTurn, LastXxxUsedOnTurn) pour les sorts de la refonte.
    ///
    /// COEXISTENCE : les signatures deja livrees (Ame Laceree, Traquenard, Effondrement, Virus
    ///               Fatal, Execution Spectrale) gardent provisoirement leurs champs dedies +
    ///               leurs gates dedies tant qu'elles ne sont pas migrees. Pas de double
    ///               comptage : leur SpellDef laisse MaxUsesPerTurn=0 / CooldownTurns=0, donc
    ///               le moteur generique les ignore (no-op).
    ///
    /// STOCKAGE [Networked] (Combatant.qtn) : journal des casts du round (CastsThisTurnLog/
    ///               CastsThisTurnCount/CastsThisTurnLoggedTurn) + dernier round de cast indexe
    ///               par SpellId (SpellCooldownLastTurn). Reset PARESSEUX par comparaison au
    ///               TurnNumber courant -> aucun hook dans TurnSystem.
    ///
    /// "tour" = ROUND = CombatState.TurnNumber (semantique Dofus depuis 2.14).
    /// </summary>
    public static unsafe class SpellLimitsHelper
    {
        // Doivent matcher les tailles des array<> declarees dans Combatant.qtn.
        public const int LogSlots      = 16;
        public const int CooldownSlots = 110;

        /// <summary>
        /// Nombre de fois que `id` a deja ete caste par `c` pendant le round `currentTurn`.
        /// Renvoie 0 si le journal date d'un round anterieur (reset paresseux).
        /// </summary>
        public static int UsesThisTurn(Combatant* c, SpellId id, int currentTurn)
        {
            if (c->CastsThisTurnLoggedTurn != currentTurn) return 0;
            int count = c->CastsThisTurnCount;
            if (count > LogSlots) count = LogSlots;
            int n = 0;
            for (int i = 0; i < count; i++)
            {
                if (c->CastsThisTurnLog[i] == id) n++;
            }
            return n;
        }

        /// <summary>
        /// Nombre TOTAL de sorts deja castes par `c` ce round (tous sorts confondus).
        /// 0 si le journal date d'un round anterieur (reset paresseux). Sert au passif
        /// Appel du Sang : -1 PA uniquement sur le 1er sort du tour (== 0 avant le cast).
        /// </summary>
        public static int TotalCastsThisTurn(Combatant* c, int currentTurn)
        {
            if (c->CastsThisTurnLoggedTurn != currentTurn) return 0;
            return c->CastsThisTurnCount;
        }

        /// <summary>
        /// True si le cap `maxUsesPerTurn` (>0) est deja atteint ce round. 0 = illimite.
        /// </summary>
        public static bool CapReached(Combatant* c, SpellId id, int maxUsesPerTurn, int currentTurn)
        {
            if (maxUsesPerTurn <= 0) return false;
            return UsesThisTurn(c, id, currentTurn) >= maxUsesPerTurn;
        }

        /// <summary>
        /// Journalise un cast reussi. A appeler UNE fois apres consommation des PA.
        /// </summary>
        public static void RecordCast(Combatant* c, SpellId id, int currentTurn)
        {
            if (c->CastsThisTurnLoggedTurn != currentTurn)
            {
                c->CastsThisTurnLoggedTurn = currentTurn;
                c->CastsThisTurnCount = 0;
            }
            if (c->CastsThisTurnCount < LogSlots)
            {
                c->CastsThisTurnLog[c->CastsThisTurnCount] = id;
                c->CastsThisTurnCount += 1;
            }
        }

        /// <summary>
        /// True si `id` est encore en relance. `remaining` = tours restants (0 sinon).
        /// `cooldownTurns` 0 = pas de relance.
        /// </summary>
        public static bool OnCooldown(Combatant* c, SpellId id, int cooldownTurns, int currentTurn, out int remaining)
        {
            remaining = 0;
            if (cooldownTurns <= 0) return false;
            int idx = (int)id;
            if (idx < 0 || idx >= CooldownSlots) return false; // garde-fou hors borne
            int last = c->SpellCooldownLastTurn[idx];
            if (last <= 0) return false; // jamais caste (sentinelle 0)
            int delta = currentTurn - last;
            if (delta < cooldownTurns)
            {
                remaining = cooldownTurns - delta;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Arme la relance (memorise le round de cast). A appeler UNE fois apres consommation des PA.
        /// </summary>
        public static void SetCooldown(Combatant* c, SpellId id, int currentTurn)
        {
            int idx = (int)id;
            if (idx < 0 || idx >= CooldownSlots) return;
            c->SpellCooldownLastTurn[idx] = currentTurn;
        }

        // ----- Variantes "par valeur" pour la VIEW (HUD) qui lit une COPIE verifiee du frame -----

        /// <summary>Comme <see cref="UsesThisTurn(Combatant*,SpellId,int)"/> mais sur une copie (HUD).</summary>
        public static int UsesThisTurn(Combatant c, SpellId id, int currentTurn)
        {
            if (c.CastsThisTurnLoggedTurn != currentTurn) return 0;
            int count = c.CastsThisTurnCount;
            if (count > LogSlots) count = LogSlots;
            int n = 0;
            for (int i = 0; i < count; i++)
            {
                if (c.CastsThisTurnLog[i] == id) n++;
            }
            return n;
        }

        /// <summary>Comme <see cref="TotalCastsThisTurn(Combatant*,int)"/> mais sur une copie (HUD).
        /// Sert au coût PA effectif affiché par la barre de sorts (bonus -1 PA du 1er sort).</summary>
        public static int TotalCastsThisTurn(Combatant c, int currentTurn)
        {
            if (c.CastsThisTurnLoggedTurn != currentTurn) return 0;
            return c.CastsThisTurnCount;
        }

        /// <summary>
        /// Tours de relance RESTANTS pour `id` (0 si dispo), sur une copie (HUD). `cooldownTurns`
        /// 0 = pas de relance. Sert au grisage + label "Nt" de la barre de sorts.
        /// </summary>
        public static int CooldownRemaining(Combatant c, SpellId id, int cooldownTurns, int currentTurn)
        {
            if (cooldownTurns <= 0) return 0;
            int idx = (int)id;
            if (idx < 0 || idx >= CooldownSlots) return 0;
            int last = c.SpellCooldownLastTurn[idx];
            if (last <= 0) return 0;
            int rem = cooldownTurns - (currentTurn - last);
            return rem > 0 ? rem : 0;
        }
    }
}
