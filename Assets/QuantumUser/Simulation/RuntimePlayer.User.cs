namespace Quantum
{
    /// <summary>
    /// 4.14.d — Extension du RuntimePlayer Quantum pour porter le deck equipe d'un joueur PvP.
    /// Set par CombatBootstrapCasual cote Unity (depuis DeckBridge) avant Game.AddPlayer.
    /// Sync via Quantum AddPlayer -> chaque client recoit les RuntimePlayers des 2 slots
    /// au tick correspondant a son AddPlayer, et CombatantSystem.OnPlayerAdded spawn
    /// l'entity Combatant avec le ClassId.
    ///
    /// SpellIdValues : 6 ints = (int) Quantum.SpellId enum. Non utilise en sim Quantum
    /// pour MVP (les 16 sorts de la classe restent castables), mais accessible cote View
    /// via f.GetPlayerData(playerSlot) pour filtre castable cote CombatHUDController.
    /// Phase 5 polish : ajouter array<SpellId>[6] sur Combatant.qtn pour enforce la
    /// restriction "6 sorts only" cote sim.
    /// </summary>
    public partial class RuntimePlayer
    {
        public NymoraClass ClassId;
        public int[] SpellIdValues = new int[6];

        // 5.10 (A3) — Cosmétiques équipés du joueur, transmis à TOUS les clients via Quantum
        // AddPlayer (comme ClassId). VIEW-ONLY : jamais lus par la simulation → aucun impact
        // déterministe, pas de bump CombatRulesVersion. Le CombatantRenderer lit ces champs par
        // f.GetPlayerData(slot) pour appliquer le skin/familier de chaque combattant (local + adverse).
        //   SkinId : cosmeticId du skin de combat (ex "skin_soulrender_ashen_sovereign"), "" = aucun.
        //   PetId  : cosmeticId du familier équipé (B5), "" = aucun.
        public string SkinId = "";
        public string PetId = "";
    }
}