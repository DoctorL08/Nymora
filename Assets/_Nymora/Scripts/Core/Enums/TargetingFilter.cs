namespace Nymora.Core.Enums
{
    /// <summary>
    /// Filtre de cible accepte par un sort.
    /// Le systeme de combat valide qu'au moins une cible matchant le filtre est dans la zone.
    /// </summary>
    public enum TargetingFilter
    {
        None = 0,

        Self = 1,             // uniquement le caster
        Ally = 2,             // uniquement allies (hors self)
        AllyIncludingSelf = 3,
        Enemy = 4,            // uniquement ennemis
        AnyUnit = 5,          // toute unite (allie ou ennemi)
        EmptyTile = 6,        // case vide (pour spawn obstacles/leurres)
        TileWithObstacle = 7, // case occupee par un obstacle
        TileWithLure = 8,     // case occupee par un leurre Ghostra
        AnyTile = 9           // n'importe quelle case (rare)
    }
}
