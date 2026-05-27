using UnityEngine;

namespace Nymora.Core.ScriptableObjects
{
    /// <summary>
    /// Brique 5.10 (B2) — Catalogue des familiers résolvables en jeu.
    ///
    /// Mappe un cosmeticId backend (ex "pet_cornecroc") vers sa PetDefinition. Chargé depuis
    /// Resources par le hub (B4) et le combat (B5) pour afficher le familier équipé de chaque joueur.
    ///
    /// Asset attendu : Assets/_Nymora/Resources/Cosmetics/PetCatalog.asset
    /// Peuplé par "Nymora > Setup > Build Pets".
    /// </summary>
    [CreateAssetMenu(menuName = "Nymora/Pet Catalog", fileName = "PetCatalog", order = 104)]
    public class PetCatalog : ScriptableObject
    {
        [Tooltip("Tous les familiers du jeu. Rempli par le tool Build Pets.")]
        public PetDefinition[] Pets;

        /// <summary>Résout un cosmeticId vers sa définition, ou null si inconnu/vide.</summary>
        public PetDefinition Resolve(string cosmeticId)
        {
            if (string.IsNullOrEmpty(cosmeticId) || Pets == null) return null;
            for (int i = 0; i < Pets.Length; i++)
            {
                if (Pets[i] != null && Pets[i].CosmeticId == cosmeticId) return Pets[i];
            }
            return null;
        }
    }
}
