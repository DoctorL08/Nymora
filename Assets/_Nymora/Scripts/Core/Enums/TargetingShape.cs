namespace Nymora.Core.Enums
{
    /// <summary>
    /// Forme de la zone d'effet d'un sort (Bible V7.1).
    /// L'origine de la forme = la case visee.
    /// </summary>
    public enum TargetingShape
    {
        None = 0,

        SingleTile = 1,    // 1 case ciblee

        // Croix orthogonale (haut/bas/gauche/droite a partir du centre)
        CrossSmall = 10,   // centre + 4 cases adjacentes (5 total)
        CrossMedium = 11,  // centre + 4 directions sur 2 cases (9 total)
        CrossLarge = 12,   // centre + 4 directions sur 3 cases (13 total)

        // Carre centre sur la cible
        Square3x3 = 20,    // 9 cases
        Square5x5 = 21,    // 25 cases

        // Ligne droite (depuis le caster jusqu'a la cible)
        Line = 30,
        LineThrough = 31,  // ligne traversante (perce les unites)

        // Cone depuis le caster
        Cone = 40,

        // Cercle centre sur la cible (Manhattan)
        CircleSmall = 50,  // rayon 1 (5 cases)
        CircleMedium = 51, // rayon 2 (13 cases)
        CircleLarge = 52   // rayon 3 (25 cases)
    }
}
