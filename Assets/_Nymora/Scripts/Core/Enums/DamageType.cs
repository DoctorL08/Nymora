namespace Nymora.Core.Enums
{
    /// <summary>
    /// Types de degats dans le combat Nymora (Bible V7.1).
    /// Note : la roadmap V2 parlait d'un enum "Element" mais le design ne comporte
    /// pas d'elements magiques (feu/glace) — uniquement des types de degats.
    /// </summary>
    public enum DamageType
    {
        None = 0,
        Physical = 1,    // Degats directs standards
        Bleed = 2,       // Saignement (Soulrender, Ghostra)
        Poison = 3,      // Venin (Necram) — ignore reductions
        Mental = 4,      // Effets Prescience (Nightseer)
        True = 5         // Degats vrais — ignore tout (signature/burst)
    }
}
