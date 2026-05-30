using System.Collections.Generic;
using UnityEngine;

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
        public readonly int Index;          // 0 = Bronze ... 7 = Legende
        public readonly string Name;
        public readonly int MinMmr;         // seuil bas inclusif
        public readonly string HexColor;    // couleur (rich text TMP + parsing)
        public readonly string IconResource; // chemin Resources de l'icône (Resources.Load<Sprite>)

        public RankTier(int index, string name, int minMmr, string hexColor, string iconResource)
        {
            Index = index;
            Name = name;
            MinMmr = minMmr;
            HexColor = hexColor;
            IconResource = iconResource;
        }
    }

    public static class RankLadder
    {
        // Ordonne par MMR croissant. 8 rangs.
        private static readonly RankTier[] Tiers =
        {
            new RankTier(0, "Bronze",        0,    "#cd7f32", "UI/Ranks/BRONZE"),
            new RankTier(1, "Argent",        1000, "#c0c0c0", "UI/Ranks/SILVER"),
            new RankTier(2, "Or",            1200, "#ffd700", "UI/Ranks/GOLD"),
            new RankTier(3, "Platine",       1400, "#4de2c0", "UI/Ranks/PLATINUM"),
            new RankTier(4, "Diamant",       1600, "#6ec6ff", "UI/Ranks/DIAMOND"),
            new RankTier(5, "Maître",        1800, "#b06bff", "UI/Ranks/MASTER"),
            new RankTier(6, "Grand Maître",  2000, "#ff6b6b", "UI/Ranks/GRANDMASTER"),
            new RankTier(7, "Légende",       2200, "#ff9d2e", "UI/Ranks/LEGEND"),
        };

        public static int TierCount => Tiers.Length;

        /// <summary>Palier par index (0 = Bronze ... TierCount-1 = Légende).</summary>
        public static RankTier ByIndex(int index) => Tiers[Mathf.Clamp(index, 0, Tiers.Length - 1)];

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

        // Cache des icônes de rang (chargées via Resources, une fois par palier).
        private static readonly Dictionary<int, Sprite> IconCache = new Dictionary<int, Sprite>();

        /// <summary>Icône du rang correspondant au MMR (depuis Resources/UI/Ranks/). Peut être null
        /// si l'asset est absent.</summary>
        public static Sprite ResolveIcon(int mmr)
        {
            var t = Resolve(mmr);
            if (IconCache.TryGetValue(t.Index, out var cached) && cached != null) return cached;
            var sprite = Resources.Load<Sprite>(t.IconResource);
            IconCache[t.Index] = sprite;
            return sprite;
        }
    }
}
