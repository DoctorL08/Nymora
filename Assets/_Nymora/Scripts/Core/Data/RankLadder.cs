namespace Nymora.Core.Data
{
    /// <summary>
    /// Brique 6.4 — Echelle des 8 rangs ranked (Bronze -> Legende), derives du MMR.
    ///
    /// Meta/UI uniquement (PAS de la simulation combat -> pas de CombatRulesVersion).
    /// Seuils ajustables ici. Joueur par defaut : MMR 1000 = Argent.
    /// </summary>
    public readonly struct RankTier
    {
        public readonly int Index;       // 0 = Bronze ... 7 = Legende
        public readonly string Name;
        public readonly int MinMmr;      // seuil bas inclusif
        public readonly string HexColor; // couleur (rich text TMP + parsing)

        public RankTier(int index, string name, int minMmr, string hexColor)
        {
            Index = index;
            Name = name;
            MinMmr = minMmr;
            HexColor = hexColor;
        }
    }

    public static class RankLadder
    {
        // Ordonne par MMR croissant. 8 rangs.
        private static readonly RankTier[] Tiers =
        {
            new RankTier(0, "Bronze",        0,    "#cd7f32"),
            new RankTier(1, "Argent",        1000, "#c0c0c0"),
            new RankTier(2, "Or",            1200, "#ffd700"),
            new RankTier(3, "Platine",       1400, "#4de2c0"),
            new RankTier(4, "Diamant",       1600, "#6ec6ff"),
            new RankTier(5, "Maître",        1800, "#b06bff"),
            new RankTier(6, "Grand Maître",  2000, "#ff6b6b"),
            new RankTier(7, "Légende",       2200, "#ff9d2e"),
        };

        public static int TierCount => Tiers.Length;

        /// <summary>Rang correspondant a un MMR (le plus haut palier dont MinMmr &lt;= mmr).</summary>
        public static RankTier Resolve(int mmr)
        {
            for (int i = Tiers.Length - 1; i >= 0; i--)
            {
                if (mmr >= Tiers[i].MinMmr) return Tiers[i];
            }
            return Tiers[0];
        }

        /// <summary>Texte rich-text colore "Rang" pour TMP.</summary>
        public static string ColoredName(int mmr)
        {
            var t = Resolve(mmr);
            return $"<color={t.HexColor}>{t.Name}</color>";
        }
    }
}
