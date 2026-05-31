using UnityEngine;

namespace Nymora.Core.Data
{
    /// <summary>
    /// 31 mai — Réplique CLIENT de la formule ELO du backend (backend/src/services/elo.service.ts),
    /// pour afficher le delta MMR exact dans le menu de fin de combat AVANT le settle serveur
    /// (qui n'a lieu qu'au retour hub via POST /ranked/report-result).
    ///
    /// IMPORTANT : doit rester STRICTEMENT alignée sur elo.service.ts. Toute modif de la formule
    /// serveur (K-factor, plancher, expected) doit être répercutée ici, sinon le preview diverge
    /// du settle réel. Le hub affiche ensuite la valeur autoritative (MMR_UPDATED websocket).
    ///
    /// VIEW-ONLY : aucun impact simulation Quantum, pas de bump CombatRulesVersion.
    /// </summary>
    public static class RankedEloPreview
    {
        public const int MmrFloor = 0;

        /// <summary>K-factor variable selon le nb de parties classées jouées (placements).</summary>
        public static int KFactor(int rankedGames)
        {
            if (rankedGames < 10) return 40;
            if (rankedGames < 30) return 25;
            return 15;
        }

        /// <summary>Score attendu E = 1 / (1 + 10^((Ropp - Rplayer)/400)).</summary>
        public static double ExpectedScore(int playerMmr, int opponentMmr)
        {
            return 1.0 / (1.0 + System.Math.Pow(10.0, (opponentMmr - playerMmr) / 400.0));
        }

        /// <summary>
        /// Delta MMR pour un résultat. score = 1 (victoire) / 0.5 (nul) / 0 (défaite).
        /// Même arrondi que le backend (Math.round).
        /// </summary>
        public static int ComputeDelta(int playerMmr, int opponentMmr, int rankedGames, float score)
        {
            double expected = ExpectedScore(playerMmr, opponentMmr);
            int k = KFactor(rankedGames);
            return Mathf.RoundToInt((float)(k * (score - expected)));
        }

        /// <summary>MMR après application du delta (planché à 0), comme computeNewMmr.</summary>
        public static int NewMmr(int playerMmr, int delta) => Mathf.Max(MmrFloor, playerMmr + delta);

        /// <summary>Convertit un MatchResult en score ELO (1 / 0.5 / 0).</summary>
        public static float ScoreFor(MatchResult result)
        {
            switch (result)
            {
                case MatchResult.Victory: return 1f;
                case MatchResult.Draw: return 0.5f;
                default: return 0f; // Defeat / None
            }
        }
    }
}
