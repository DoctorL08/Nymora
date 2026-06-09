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

        // Tuto T5 — Gel du timer. TRUE => TurnSystem ne décrémente PAS le timer pendant le tour du
        // JOUEUR (slot 0) : pas de fin de tour automatique, le joueur prend son temps et termine
        // manuellement. N'affecte pas le tour du bot (qui se rend seul). CombatRulesVersion bumpe (85).
        public bool TutorialFreezeTimer;

        // 5.2 (2v2/3v3) — Nombre TOTAL de joueurs du combat (2 / 4 / 6). Posé par le bootstrap de
        //   chaque scène (1v1 : non posé -> 0 -> TurnSystem.OnInit retombe sur 2). Le bootstrap équipe
        //   (scènes 41/42, brique 5.5) le met à 4 ou 6. Pilote la longueur du round + le build TurnOrder.
        public int PlayerCount;

        // 5.4b (2v2/3v3) — Map de combat (forme irrégulière + spawns). AssetRef = un GUID synchronisé ;
        //   chaque client charge le même AssetObject NymoraCombatMap localement (déterministe). Posé par
        //   le bootstrap équipe (scènes 41/42, brique 5.5) depuis le MapAsset authoré par l'éditeur (5.4c).
        //   NON posé en 1v1 (Id invalide) -> GridSystem retombe sur la zone rectangulaire LogicalDims et
        //   CombatantSystem sur les spawns hardcodés -> 1v1 inchangé.
        public AssetRef<NymoraCombatMap> CombatMap;
    }
}