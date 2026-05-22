namespace Quantum
{
    /// <summary>
    /// Constantes du systeme de tour (Bible V7.1).
    /// Verrouillees — ne PAS modifier sans incrementer CombatRulesVersion.
    /// </summary>
    public static class TurnConstants
    {
        public const int TurnDurationSeconds = 15;
        public const int PlayerCount = 2;

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
