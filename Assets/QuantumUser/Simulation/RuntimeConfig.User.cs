namespace Quantum
{
    public partial class RuntimeConfig
    {
        // 4.14.b — Flag mode IA (vs bot) vs PvP (vs humain online).
        //   TRUE  -> 30_CombatIA (asset RuntimeConfigCombatIA.asset, IsBotMatch coche)
        //   FALSE -> 33_CombatCasual + futurs ranked (default safe pour PvP)
        // Lu par TurnSystem.OnInit et copie dans CombatState.IsBotMatch (sim-side).
        // AISystem.Update early-out si state->IsBotMatch == false.
        public bool IsBotMatch;

        // Tuto T2 — Mannequin passif. TRUE => AISystem ne fait QUE rendre le tour du bot
        // (aucun move/cast) : le combattant adverse devient une cible inerte pour le tutoriel.
        // Pose au runtime par CombatBootstrapIA quand TutorialContext.Active. Lu directement via
        // f.RuntimeConfig (pas un field [Networked] => aucun regen prefab/scene requis), mais
        // c'est une modif de logique sim -> CombatRulesVersion bumpe (84).
        public bool TutorialPassiveBot;
    }
}