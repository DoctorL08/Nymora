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
        /// Duree d'un tour en ticks Quantum, calculee depuis le tick rate de la session.
        /// Standard Quantum 3 = 60 ticks/sec, donc 15s = 900 ticks.
        /// </summary>
        public static int GetTurnDurationTicks(Frame f)
        {
            return TurnDurationSeconds * f.UpdateRate;
        }
    }
}
