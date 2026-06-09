namespace Quantum
{
    /// <summary>
    /// Constantes du systeme de tour (Bible V7.1).
    /// Verrouillees — ne PAS modifier sans incrementer CombatRulesVersion.
    /// </summary>
    public static class TurnConstants
    {
        public const int TurnDurationSeconds = 15;

        // 1v1 = 2 joueurs. CONSERVÉ comme défaut (RuntimeConfig.PlayerCount non fourni -> 2).
        // ⚠️ NE PLUS l'utiliser pour la LONGUEUR DU ROUND ni la rotation : depuis 5.2 c'est
        //    CombatState.PlayerCount (dynamique par mode) qui pilote ça, via CombatState.TurnOrder.
        public const int PlayerCount = 2;

        // 5.2 (2v2/3v3) — borne MAX de joueurs d'un combat (3v3 = 6). Sert UNIQUEMENT de borne
        //   de scan « pour chaque slot » dans les systèmes de commandes (Movement/Spell/Fog/...) :
        //   un slot vide -> GetPlayerCommand null / aucun Combatant -> no-op, donc balayer 0..5
        //   est sûr et déterministe même en 1v1. La vraie longueur de round = CombatState.PlayerCount.
        public const int MaxPlayers = 6;

        /// <summary>
        /// PATCH 22 mai (test designer) — Delai d'intro "pile ou face" en combat CASUAL.
        /// Pendant ce delai la FSM reste en PreMatch (timer de tour GELE) le temps que la View
        /// joue l'animation de revelation du premier joueur. Ainsi le 1er joueur ne perd pas de
        /// temps sur son tour : le timer 15s ne demarre qu'au TurnStart, apres l'intro.
        /// La View (CoinFlipIntroView) cale la duree de son animation sur cette valeur.
        /// </summary>
        public const int IntroDelaySeconds = 3;

        /// <summary>
        /// Duree d'un tour en ticks Quantum, calculee depuis le tick rate de la session.
        /// Standard Quantum 3 = 60 ticks/sec, donc 15s = 900 ticks.
        /// </summary>
        public static int GetTurnDurationTicks(Frame f)
        {
            return TurnDurationSeconds * f.UpdateRate;
        }

        /// <summary>Duree de l'intro pile ou face en ticks (casual uniquement).</summary>
        public static int GetIntroDelayTicks(Frame f)
        {
            return IntroDelaySeconds * f.UpdateRate;
        }
    }
}
