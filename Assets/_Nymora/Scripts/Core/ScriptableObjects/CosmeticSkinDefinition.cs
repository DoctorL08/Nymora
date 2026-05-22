using Nymora.Core.Enums;
using UnityEngine;

namespace Nymora.Core.ScriptableObjects
{
    /// <summary>
    /// Brique 5.5.e — Définition visuelle d'un skin cosmétique (alternative aux frames de la
    /// classe). Quand ce skin est équipé pour sa classe, l'avatar hub joue CES frames au lieu
    /// de celles du NymoraClassDefinition. Mêmes champs visuels que la classe (Idle/Walk × SE/NE
    /// + fps + calibration scale/yOffset).
    ///
    /// `CosmeticId` doit matcher l'id catalogue backend (src/shop/catalog.ts), ex :
    /// "skin_soulrender_ashen_sovereign". Peuplé via "Nymora > Setup > Patch Ashen Sovereign Skin".
    /// </summary>
    [CreateAssetMenu(menuName = "Nymora/Cosmetic Skin Definition", fileName = "NewCosmeticSkin", order = 101)]
    public class CosmeticSkinDefinition : ScriptableObject
    {
        [Tooltip("Id catalogue backend (ShopItemDef.id), ex : skin_soulrender_ashen_sovereign.")]
        public string CosmeticId;

        [Tooltip("Classe à laquelle ce skin s'applique.")]
        public NymoraClass ClassId = NymoraClass.None;

        [Tooltip("Nom affiché (optionnel).")]
        public string DisplayName;

        [Header("Frames (mêmes conventions que NymoraClassDefinition)")]
        public Sprite[] IdleFrames;      // SE
        public Sprite[] IdleFramesNE;    // NE (NW/SW = mirror flipX)
        public float IdleFps = 8f;
        public Sprite[] WalkFrames;      // SE
        public Sprite[] WalkFramesNE;    // NE
        public float WalkFps = 12f;

        [Header("Hub Visual Calibration")]
        public float HubVisualScale = 1f;
        public float HubVisualYOffset = 0f;
    }
}
