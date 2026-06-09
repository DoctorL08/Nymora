namespace Quantum
{
    /// <summary>
    /// 5.1 (2v2/3v3) — Predicats allie/ennemi CENTRALISES.
    ///
    /// AVANT (1v1) : "ennemi" = PlayerIndex different. Cette regle casse en equipe (un allie
    /// devient une cible des auras/zones/DoT). Desormais l'appartenance se lit sur Combatant.TeamId
    /// et TOUT check allie/ennemi de la simulation passe par ce helper.
    ///
    /// INVARIANT 1v1 : au spawn, TeamId == PlayerIndex en 1v1 (cf CombatantSystem). Donc
    /// AreEnemies/AreAllies produisent EXACTEMENT le meme resultat que les anciens checks
    /// PlayerIndex -> la conversion des call-sites est NON-REGRESSIVE en 1v1.
    ///
    /// Regle "PAS de tir allie" (decision 9 juin 2026) : tout effet offensif (degats directs,
    /// auras, DoT, zones d'effet) filtre les cibles via AreEnemies ; tout effet de soutien
    /// (heal/bouclier/buff, self-only en v1) via SameTeam / AreAllies.
    /// </summary>
    public static unsafe class TeamHelper
    {
        /// <summary>Deux combattants sont-ils ennemis (equipes differentes) ?</summary>
        public static bool AreEnemies(Combatant* a, Combatant* b)
            => a->TeamId != b->TeamId;

        /// <summary>Variante quand on a deja le TeamId du caster (int) et un Combatant* cible.
        /// Evite un scan O(n) dans les boucles serrees (preview/AoE par case).</summary>
        public static bool IsEnemyOf(int casterTeamId, Combatant* other)
            => casterTeamId != other->TeamId;

        /// <summary>Deux combattants sont-ils allies (meme equipe, entites DISTINCTES) ?
        /// Un combattant n'est PAS son propre allie (compare par pointeur).</summary>
        public static bool AreAllies(Combatant* a, Combatant* b)
            => a != b && a->TeamId == b->TeamId;

        /// <summary>Meme equipe, SOI-MEME INCLUS. Utile pour "epargner mon camp" quand on itere
        /// (skip allies ET self d'un coup). C'est le remplacant direct de l'ancien
        /// `other->PlayerIndex == caster->PlayerIndex` dans les boucles de degats.</summary>
        public static bool SameTeam(Combatant* a, Combatant* b)
            => a->TeamId == b->TeamId;

        /// <summary>Variante par TeamId int (caster) + Combatant* — "meme camp que le caster".</summary>
        public static bool IsSameTeamAs(int casterTeamId, Combatant* other)
            => casterTeamId == other->TeamId;

        /// <summary>Resout le TeamId d'un playerIndex (scan O(n) des Combatants). -1 si introuvable.
        /// A utiliser AVEC PARCIMONIE (une fois avant une boucle, pas par case) — preferer threader
        /// le TeamId du caster directement quand c'est possible.</summary>
        public static int ResolveTeamId(Frame f, int playerIndex)
        {
            var filter = f.Filter<Combatant>();
            while (filter.Next(out _, out Combatant c))
            {
                if (c.PlayerIndex == playerIndex) return c.TeamId;
            }
            return -1;
        }

        /// <summary>Deux playerIndex appartiennent-ils a des equipes ennemies ? Pour les helpers
        /// qui ne manipulent que des int (owner de piege/leurre vs caster). Resout les deux TeamId.
        /// Fallback 1v1-safe : si un TeamId est introuvable, on retombe sur "indices differents".</summary>
        public static bool AreEnemiesByPlayerIndex(Frame f, int playerA, int playerB)
        {
            if (playerA == playerB) return false;
            int ta = ResolveTeamId(f, playerA);
            int tb = ResolveTeamId(f, playerB);
            if (ta < 0 || tb < 0) return playerA != playerB; // fallback 1v1
            return ta != tb;
        }

        /// <summary>"other est-il un ennemi du joueur casterPlayerIndex ?" — quand on a un
        /// Combatant* cible mais seulement un int pour le caster. Resout le TeamId du caster
        /// (O(n) une fois) ; preferer la variante IsEnemyOf(int casterTeamId, ...) si le TeamId
        /// est deja connu. Fallback 1v1-safe.</summary>
        public static bool IsEnemyOfPlayer(Frame f, int casterPlayerIndex, Combatant* other)
        {
            if (other->PlayerIndex == casterPlayerIndex) return false; // soi-meme
            int casterTeam = ResolveTeamId(f, casterPlayerIndex);
            if (casterTeam < 0) return other->PlayerIndex != casterPlayerIndex; // fallback 1v1
            return other->TeamId != casterTeam;
        }
    }
}
