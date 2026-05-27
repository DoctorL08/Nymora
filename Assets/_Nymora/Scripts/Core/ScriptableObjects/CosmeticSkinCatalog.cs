using UnityEngine;

namespace Nymora.Core.ScriptableObjects
{
    /// <summary>
    /// Brique 5.10 (A2) — Catalogue des skins cosmétiques résolvables en COMBAT.
    ///
    /// Mappe un cosmeticId backend (ex : "skin_soulrender_ashen_sovereign") vers sa
    /// CosmeticSkinDefinition (qui porte les 6 AnimatorController de combat). Le CombatantRenderer
    /// le charge depuis Resources (pas de câblage de scène — marche dans les 3 scènes combat + build)
    /// et résout le skin équipé du joueur.
    ///
    /// Asset attendu : Assets/_Nymora/Resources/Cosmetics/CosmeticSkinCatalog.asset
    /// Peuplé par "Nymora > Setup > Build Cosmetic Skin Catalog" (scanne tous les CosmeticSkinDefinition).
    /// </summary>
    [CreateAssetMenu(menuName = "Nymora/Cosmetic Skin Catalog", fileName = "CosmeticSkinCatalog", order = 102)]
    public class CosmeticSkinCatalog : ScriptableObject
    {
        [Tooltip("Tous les skins du jeu. Rempli par le tool Build Cosmetic Skin Catalog.")]
        public CosmeticSkinDefinition[] Skins;

        /// <summary>Résout un cosmeticId vers sa définition, ou null si inconnu/vide.</summary>
        public CosmeticSkinDefinition Resolve(string cosmeticId)
        {
            if (string.IsNullOrEmpty(cosmeticId) || Skins == null) return null;
            for (int i = 0; i < Skins.Length; i++)
            {
                if (Skins[i] != null && Skins[i].CosmeticId == cosmeticId) return Skins[i];
            }
            return null;
        }
    }
}
