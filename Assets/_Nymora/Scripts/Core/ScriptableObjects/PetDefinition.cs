using UnityEngine;

namespace Nymora.Core.ScriptableObjects
{
    /// <summary>
    /// Brique 5.10 (B2) — Définition visuelle d'un familier (cosmétique type 'pet').
    ///
    /// Un familier équipé suit l'avatar dans le hub et accompagne le combattant en combat
    /// (sur la même case). Mêmes conventions de frames que NymoraClassDefinition /
    /// CosmeticSkinDefinition : idle/walk × SE/NE (NW/SW = mirror flipX).
    ///
    /// `CosmeticId` doit matcher l'id catalogue backend (src/shop/catalog.ts), ex "pet_cornecroc".
    /// Peuplé par "Nymora > Setup > Build Pets".
    /// </summary>
    [CreateAssetMenu(menuName = "Nymora/Pet Definition", fileName = "NewPet", order = 103)]
    public class PetDefinition : ScriptableObject
    {
        [Tooltip("Id catalogue backend (ShopItemDef.id), ex : pet_cornecroc.")]
        public string CosmeticId;

        [Tooltip("Nom affiché (optionnel).")]
        public string DisplayName;

        [Header("Frames (SE + NE ; NW/SW = mirror flipX)")]
        public Sprite[] IdleFrames;      // SE
        public Sprite[] IdleFramesNE;    // NE
        public float IdleFps = 6f;
        public Sprite[] WalkFrames;      // SE
        public Sprite[] WalkFramesNE;    // NE
        public float WalkFps = 8f;

        [Header("Calibration visuelle (hub + combat)")]
        [Tooltip("Échelle du familier (1 = taille native PPU). Le familier est plus petit que le joueur.")]
        public float VisualScale = 1f;
        [Tooltip("Décalage Y du familier (pivot pieds non standardisé).")]
        public float VisualYOffset = 0f;
    }
}
