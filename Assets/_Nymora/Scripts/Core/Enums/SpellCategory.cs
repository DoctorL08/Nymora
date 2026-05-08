namespace Nymora.Core.Enums
{
    /// <summary>
    /// Categories de sorts dans le deck d'une classe (Bible V7.1).
    /// 5 sorts par categorie x 3 categories = 15 sorts par classe.
    /// Le sort Signature est dans son propre slot (debloque a ressource max, cooldown 4 tours).
    /// </summary>
    public enum SpellCategory
    {
        None = 0,
        Offensive = 1,   // 5 sorts par classe — degats/burst
        Tactical = 2,    // 5 sorts par classe — controle/marques/positionnement
        Survival = 3,    // 5 sorts par classe — defense/heal/dispel
        Signature = 4    // 1 sort par classe — debloque a ressource max
    }
}
