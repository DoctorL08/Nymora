using System.Threading.Tasks;

namespace Nymora.Combat.Bootstrap
{
    /// <summary>
    /// Brique 5.6 (2v2/3v3) — Bridge pré-combat « ordre de jeu ».
    ///
    /// Le rang intra-équipe (RuntimePlayer.TeamOrder) fige TurnOrder côté sim (cf
    /// TurnSystem.TryBuildTurnOrder). Il DOIT donc être connu AVANT les Game.AddPlayer.
    /// Ce bridge statique fait le lien Bootstrap (Combat) ⇄ panneau d'UI (View), sans casser
    /// l'asmdef (le Bootstrap ne référence jamais la View) : même pattern que DeckBridge /
    /// CombatCosmeticsContext.
    ///
    /// Flux hot-seat (5.6) :
    ///   1. CombatBootstrap2v2 publie le roster (4 joueurs : slot, équipe, libellé) via Request().
    ///   2. PreCombatOrderPanel (auto-instancié pour la scène 2v2/3v3) repère la requête, construit
    ///      l'UI, met Acknowledged = true, laisse Lorenzo réordonner chaque équipe.
    ///   3. Au clic « Lancer le combat », le panneau appelle Submit(orderByPlayerSlot).
    ///   4. Le bootstrap, qui await la Task, lit l'ordre et AddPlayer avec le TeamOrder voté.
    ///
    /// Sécurité anti-hang : si aucun panneau ne prend la main (Acknowledged reste false) après un
    /// court délai de grâce, le bootstrap part sur l'ordre PAR DÉFAUT (rang = ordre des slots) au
    /// lieu de bloquer indéfiniment. Submit(null) / Cancel() = « garde le défaut ».
    ///
    /// Pas de simulation ici → aucun bump CombatRulesVersion.
    /// </summary>
    public static class TeamOrderVote
    {
        /// <summary>Un combattant à ordonner. Label = texte affiché (nom de classe).</summary>
        public struct VoteSlot
        {
            public int PlayerSlot;  // slot Quantum (0..3 en 2v2)
            public int TeamId;      // 0 ou 1
            public string Label;    // ex "Soulrender"
        }

        /// <summary>Roster publié par le bootstrap (lu par le panneau pour construire l'UI).</summary>
        public static VoteSlot[] Roster { get; private set; }

        /// <summary>Mis à true par le panneau dès qu'il prend en charge la requête (anti-fallback).</summary>
        public static bool Acknowledged { get; set; }

        private static TaskCompletionSource<int[]> _pending;

        /// <summary>Une requête de vote est en cours (publiée, pas encore résolue).</summary>
        public static bool HasRequest => _pending != null && !_pending.Task.IsCompleted;

        /// <summary>Publie le roster et ouvre l'attente. La Task se résout quand le panneau Submit
        /// (ordre voté = int[] indexé par PlayerSlot, valeur = TeamOrder) ou null (= ordre défaut).</summary>
        public static Task<int[]> Request(VoteSlot[] roster)
        {
            Roster = roster;
            Acknowledged = false;
            _pending = new TaskCompletionSource<int[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            return _pending.Task;
        }

        /// <summary>Le panneau valide l'ordre choisi (indexé par PlayerSlot). null = ordre par défaut.</summary>
        public static void Submit(int[] teamOrderByPlayerSlot)
        {
            _pending?.TrySetResult(teamOrderByPlayerSlot);
        }

        /// <summary>Abandonne le vote en cours → ordre par défaut (panneau détruit / scène quittée).</summary>
        public static void Cancel()
        {
            _pending?.TrySetResult(null);
            _pending = null;
            Roster = null;
            Acknowledged = false;
        }
    }
}
