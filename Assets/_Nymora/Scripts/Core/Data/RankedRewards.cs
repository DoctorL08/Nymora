namespace Nymora.Core.Data
{
    /// <summary>
    /// 31 mai — Réplique CLIENT de la table de récompenses ranked du backend
    /// (backend/src/routes/ranked.ts → const REWARDS), pour afficher Nymos + XP de classe
    /// gagnés dans le menu de fin de combat. Ce sont des CONSTANTES fixes par résultat
    /// (pas de calcul serveur) → l'affichage client est exact, aucune contrainte de timing.
    ///
    /// IMPORTANT : garder aligné sur REWARDS (ranked.ts). VIEW-ONLY, pas de bump CombatRulesVersion.
    /// </summary>
    public readonly struct RankedReward
    {
        public readonly int Nymos;
        public readonly int ClassXp;

        public RankedReward(int nymos, int classXp)
        {
            Nymos = nymos;
            ClassXp = classXp;
        }
    }

    public static class RankedRewards
    {
        // Aligné sur REWARDS : win {xp:200,nymos:100} / loss {xp:80,nymos:50} / draw {xp:120,nymos:75}.
        public static RankedReward For(MatchResult result)
        {
            switch (result)
            {
                case MatchResult.Victory: return new RankedReward(100, 200);
                case MatchResult.Draw: return new RankedReward(75, 120);
                case MatchResult.Defeat: return new RankedReward(50, 80);
                default: return new RankedReward(0, 0);
            }
        }
    }
}
