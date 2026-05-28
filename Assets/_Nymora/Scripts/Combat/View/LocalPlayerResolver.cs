using Nymora.Combat.Bootstrap;
using Quantum;

namespace Nymora.Combat.View
{
    /// <summary>
    /// Resout le PlayerIndex local du client courant (qui suis-je sur ce client ?).
    /// Factorise le pattern duplique dans CombatInputController / CombatHUDController /
    /// TargetingPreviewView depuis la livraison 4.14.f (PvP cross-internet).
    ///
    /// Resolution :
    ///   1. Si CombatBootstrapCasual.Instance != null et LocalPlayerSlot >= 0 (PvP) :
    ///      retourne LocalPlayerSlot (0 = host MasterClient, 1 = guest).
    ///   2. Sinon (mode IA / pas de bootstrap PvP detecte) :
    ///      retourne 0 (Lorenzo toujours slot 0 en IA contre bot slot 1).
    ///
    /// **Important** : c'est different de state.ActivePlayerIndex (joueur dont c'est
    /// le tour). Pour les checks de visibilite cote View (brouillard, pieges, etc.),
    /// utiliser CE helper, pas l'ActivePlayer — sinon en PvP, l'adversaire voit tes
    /// pieges pendant ton tour car le check confond "joueur du tour" et "joueur local".
    /// </summary>
    public static class LocalPlayerResolver
    {
        public static int Resolve()
        {
            var casual = CombatBootstrapCasual.Instance;
            if (casual != null && casual.LocalPlayerSlot >= 0) return casual.LocalPlayerSlot;
            return 0;
        }

        /// <summary>
        /// True si le client LOCAL possede le joueur `playerIndex`. Source de verite robuste
        /// (cf piege Quantum PlayerRef != slot suppose) :
        ///   - ONLINE (CombatBootstrapCasual present) : lit les PlayerRef locaux directement
        ///     dans la frame via `game.GetLocalPlayers()` (1 seul en PvP). Evite le fallback
        ///     trompeur de Resolve() (slot 0) quand LocalPlayerSlot n'est pas encore resolu —
        ///     c'est ce fallback qui faisait voir au guest les leurres ennemis teintes "caster".
        ///   - IA / local (pas de bootstrap Casual) : les 2 joueurs sont locaux, donc on NE PEUT
        ///     PAS utiliser GetLocalPlayers (renverrait les 2). L'humain est slot 0 (AddPlayer
        ///     deterministe), donc owner == (playerIndex == 0).
        /// </summary>
        public static bool LocalOwns(QuantumGame game, int playerIndex)
        {
            if (CombatBootstrapCasual.Instance != null)
            {
                if (game != null)
                {
                    var locals = game.GetLocalPlayers();
                    if (locals != null)
                    {
                        for (int i = 0; i < locals.Count; i++)
                        {
                            int pr = locals[i];
                            if (pr == playerIndex) return true;
                        }
                    }
                }
                return false; // online : si on ne se reconnait pas proprietaire -> NON (indiscernable)
            }
            // IA / local : humain = slot 0.
            return playerIndex == 0;
        }
    }
}
