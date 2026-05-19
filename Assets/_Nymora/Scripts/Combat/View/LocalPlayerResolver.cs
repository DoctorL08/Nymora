using Nymora.Combat.Bootstrap;

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
    }
}
