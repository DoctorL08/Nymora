namespace Quantum
{
    using Photon.Deterministic;

    /// <summary>
    /// AI brain Nymora (Bloc E Phase 2).
    ///
    /// 2.16.a.i — squelette : detecte quand le joueur actif est un bot (P1 hardcoded)
    /// et termine son tour automatiquement apres un court delai. Aucune logique de
    /// move/cast pour cette brique : le bot passe son tour. Permet de valider que
    /// l'integration sim+view tient debout avant d'ajouter la logique de decision.
    ///
    /// 2.16.a.ii — ajoutera EvaluateMove + deplacement greedy.
    /// 2.16.a.iii — ajoutera EvaluateSpell + boucle de cast greedy.
    ///
    /// Pourquoi pas un EndTurnCommand simule ? Les DeterministicCommand viennent de
    /// l'input client. Une IA simu-side ne peut pas (et ne doit pas) injecter une
    /// command : elle mute directement l'etat via la meme primitive que le handler
    /// EndTurnCommand utilise (state->TurnTimerTicks = 0). Ca evite un aller-retour
    /// inutile et garde l'IA strictement deterministe simu-side.
    ///
    /// Ordre des systems : AISystem doit tourner AVANT TurnSystem dans le tick. Sinon
    /// le set TurnTimerTicks = 0 ne sera vu par TickTurnActive qu'au tick suivant
    /// (1 frame de latence, sans impact gameplay mais inutile).
    /// </summary>
    public unsafe class AISystem : SystemMainThread
    {
        public override void Update(Frame f)
        {
            var state = f.Unsafe.GetPointerSingleton<CombatState>();

            // L'IA n'agit que quand son joueur est actif ET le tour est en cours.
            if (state->CurrentPhase != CombatPhase.TurnActive) return;
            if (state->ActivePlayerIndex != AIConstants.BotPlayerIndex) return;

            // Delai avant fin de tour : calcule depuis le timer restant pour eviter
            // d'ajouter un champ d'etat dedie (CombatState reste minimal).
            int totalDuration = TurnConstants.GetTurnDurationTicks(f);
            int elapsed = totalDuration - state->TurnTimerTicks;
            if (elapsed < AIConstants.BotEndTurnDelayTicks) return;

            // Termine le tour : meme effet que la branche EndTurnCommand dans
            // TurnSystem.TickTurnActive. TurnSystem fera la transition TurnActive
            // -> TurnEnd ce tick (puisque AISystem tourne avant) ou au tick suivant.
            Log.Info($"[AI] Bot P{state->ActivePlayerIndex} termine son tour (squelette 2.16.a.i, pas d'action)");
            state->TurnTimerTicks = 0;
        }
    }
}
