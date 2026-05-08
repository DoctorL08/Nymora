using Nymora.Core.Enums;
using UnityEngine;

namespace Nymora.Core.ScriptableObjects
{
    /// <summary>
    /// Definition d'une classe jouable de Nymora (Bible V7.1).
    /// Contient stats de base, ressource, passif, sort signature.
    /// 5 instances sont generees a la racine de Assets/_Nymora/ScriptableObjects/Classes/.
    ///
    /// IMPORTANT : modifier une valeur ici = incrementer GameVersion.CombatRulesVersion.
    /// </summary>
    [CreateAssetMenu(menuName = "Nymora/Class Definition", fileName = "NewClassDefinition", order = 100)]
    public class NymoraClassDefinition : ScriptableObject
    {
        [Header("Identity")]
        public NymoraClass ClassId = NymoraClass.None;

        [Tooltip("Nom affiche dans l'UI (capitalise — ex : Soulrender).")]
        public string DisplayName;

        [Tooltip("Couleur d'accent UI/HUD (Bible V7.1).")]
        public Color AccentColor = Color.white;

        [TextArea(2, 4)]
        [Tooltip("Description courte one-liner (fantasy de gameplay).")]
        public string Tagline;

        [Header("Base Stats (Bible V7.1)")]
        [Tooltip("HP de base — 1500 pour toutes les classes V7.1.")]
        public int BaseHP = 1500;

        [Tooltip("Points d'Action par tour — 8 pour toutes les classes V7.1.")]
        public int BaseActionPoints = 8;

        [Tooltip("Points de Mouvement par tour — 3 pour toutes les classes V7.1.")]
        public int BaseMovementPoints = 3;

        [Header("Resource (Bible V7.1)")]
        [Tooltip("Type de ressource de classe (HG/PR/FD/PT/RM).")]
        public ResourceType ResourceKind = ResourceType.None;

        [Tooltip("Cap maximum de la ressource (ex : 5 pour Hemoglyphe Soulrender).")]
        public int ResourceCap = 1;

        [TextArea(3, 6)]
        public string ResourceDescription;

        [Header("Passive")]
        public string PassiveName;

        [TextArea(3, 8)]
        public string PassiveDescription;

        [Header("Signature Spell")]
        public string SignatureName;

        [Tooltip("Cooldown du sort signature apres usage — 4 tours pour toutes les classes V7.1.")]
        public int SignatureCooldownTurns = 4;

        [TextArea(2, 5)]
        public string SignatureDescription;

        [Header("Visual Assets (a brancher plus tard)")]
        public Sprite IconSprite;
        public Sprite PortraitSprite;
    }
}
