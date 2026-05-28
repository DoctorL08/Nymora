namespace Nymora.Core.Data
{
    /// <summary>
    /// Brique T1 (Tutoriel) — Pont hub/dev → combat pour le mode TUTORIEL, sur le modèle de
    /// <see cref="CombatCosmeticsContext"/>.
    ///
    /// Le combat (asmdef Nymora.Combat) ne référence ni le réseau ni l'éditeur. Le déclencheur
    /// (lancement DEV en éditeur via TutorialDevLauncher, ou plus tard le routing post-login d'un
    /// nouveau compte en T4) écrit <see cref="Active"/> = true AVANT de charger la scène combat
    /// 30_CombatIA. Le <c>CombatBootstrapIA</c> le lit pour imposer la classe Soulrender + un deck
    /// fixe (au lieu du DeckBridge), et — à partir de T2 — pour rendre le mannequin passif.
    ///
    /// Statique volontairement : survit au changement de scène (hub → combat), comme
    /// <see cref="CombatCosmeticsContext"/>. ⚠️ Reset au domain-reload Unity (entrée Play Mode) :
    /// en éditeur on le repose donc via TutorialDevLauncher après le reload ; en prod le flow
    /// runtime (hub → combat dans la même session) n'est pas concerné.
    /// </summary>
    public static class TutorialContext
    {
        /// <summary>True = le combat 30_CombatIA doit démarrer en mode tutoriel scripté
        /// (classe Soulrender imposée, deck fixe). La passivité du mannequin arrive en T2.</summary>
        public static bool Active;

        /// <summary>
        /// Deck Soulrender du tutoriel (6 SpellId snake_case du SpellCatalog). Imposé au slot 0
        /// quand <see cref="Active"/> est true, ce qui rend le scénario 100% prévisible.
        ///
        /// ⚠️ PROVISOIRE T1 : sélection pédagogique à confirmer en T4 (Bible V7.1) une fois les
        /// étapes du tuto rédigées (attaque de base → sort de charge/mobilité → finisher, etc.).
        /// </summary>
        public static readonly string[] SoulrenderTutorialDeck =
        {
            "soulrender_tranche_ame",        // attaque de contact de base (apprendre à lancer un sort)
            "soulrender_charge_brutale",     // charge/mobilité (apprendre un sort de déplacement)
            "soulrender_ouvre_plaie",        // dégâts
            "soulrender_detonation_sanglante", // dégâts
            "soulrender_curee",              // finisher
            "soulrender_peau_de_fer",        // défensif
        };

        public static void Reset() => Active = false;
    }
}
