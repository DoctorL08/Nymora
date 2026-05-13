namespace Quantum
{
    /// <summary>
    /// Constantes deterministes pour l'IA Nymora (Bloc E Phase 2).
    ///
    /// Toutes les valeurs ici sont entieres (int) et compilent en dur dans la sim. Si
    /// on a besoin d'un parametre runtime (difficulte selectionnable, etc.), il devra
    /// passer par RuntimeConfig + SimulationConfig — pas par cette classe.
    /// </summary>
    public static class AIConstants
    {
        // Phase 2 : P1 est le bot, Lorenzo joue P0. Hardcoded jusqu'a Phase 5/6 ou un
        // RuntimePlayer/RoomConfig permettra de configurer humain vs bot par slot.
        public const int BotPlayerIndex = 1;

        // Delai en ticks Quantum (60Hz par defaut) avant que le bot termine son tour
        // en l'absence d'action. Permet au joueur humain de voir que c'est le tour
        // adverse. 30 ticks = 0.5s a 60Hz.
        //
        // 2.16.a.i : le bot ne fait QUE finir son tour (squelette).
        // 2.16.a.ii+ : ce delai sera remplace par le temps d'execution reel des actions
        //              (move + casts), avec une eventuelle pause finale courte.
        public const int BotEndTurnDelayTicks = 30;
    }
}
