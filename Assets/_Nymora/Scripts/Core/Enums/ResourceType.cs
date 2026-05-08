namespace Nymora.Core.Enums
{
    /// <summary>
    /// Les 5 ressources de classe (Bible V7.1).
    /// Chaque classe a sa ressource avec son cap propre :
    /// Hemoglyphe 5, Prescience 4, Fondation 3, Putrefaction 6, Remanence (3 leurres max).
    /// </summary>
    public enum ResourceType
    {
        None = 0,
        Hemoglyphe = 1,    // Soulrender — cap 5
        Prescience = 2,    // Nightseer — cap 4
        Fondation = 3,     // Colossar — cap 3
        Putrefaction = 4,  // Necram — cap 6
        Remanence = 5      // Ghostra — 3 leurres max
    }
}
