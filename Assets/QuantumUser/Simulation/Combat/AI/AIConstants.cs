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

        // 2.16.a.iii — cap dur des casts par tour pour l'IA Easy.
        //
        // Constate empiriquement : meme avec random pick + skip signature, un bot
        // Soulrender qui exploite le passif Appel du Sang (-1 PA cost <70% HP)
        // peut chainer 3-4 Tranche-Ame = 660-880 dgts/tour et tuer le joueur en
        // 2-3 tours. Pour une vraie "Easy" on cap a 2 casts/tour max -> ~440 dgts
        // top, le joueur a 4-5 tours pour reagir.
        //
        // HG optionnel desactive en 2.16.a.iii (cf TryGreedyCast HGSpend = 0).
        //
        // IA Medium en 2.16.b reviendra a 8 (PA-limited natural) + greedy max-score
        // + signature autorisee + HG optionnel. IA Hard ulterieurement aura planif
        // multi-tour.
        public const int MaxCastsPerTurn = 2;
    }
}
