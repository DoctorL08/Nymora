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
    }
}