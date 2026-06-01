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

        [TextArea(4, 10)]
        [Tooltip("Lore long affiche dans le Class Selector du Deck Builder (Phase 5.3.f). " +
                 "3-5 phrases de fantasy / motivation du personnage. Distinct du Tagline (one-liner).")]
        public string Lore;

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

        [Tooltip("AnimatorController du sprite Idle (Stage0_NE) pour le Class Selector Screen " +
                 "(Phase 5.3.f). Ex : Assets/_Nymora/Animations/Soulrender/SoulrenderStage0_NE.controller.")]
        public RuntimeAnimatorController IdleAnimator;

        [Tooltip("Frames Sprite extraites de IdleAnimator (Stage0_SE.controller, facing SUD-EST) " +
                 "pour animer l'UI Image dans le Class Selector + l'avatar hub direction SE.")]
        public Sprite[] IdleFrames;

        [Tooltip("AnimatorController Stage0_NE (facing NORD-EST). Reference seulement, " +
                 "le populator en extrait IdleFramesNE.")]
        public RuntimeAnimatorController IdleAnimatorNE;

        [Tooltip("Frames Sprite extraites de IdleAnimatorNE (Stage0_NE.controller, facing NORD-EST). " +
                 "Utilise par l'avatar hub direction NE. NW/SW = NE/SE mirror flipX.")]
        public Sprite[] IdleFramesNE;

        [Tooltip("FPS de l'animation Idle. 8 fps = pixel art smooth.")]
        public float IdleFps = 8f;

        [Tooltip("Frames Sprite extraites du clip Walk de l'AnimatorController Stage0_SE (facing SE). " +
                 "Utilise par l'avatar hub quand il bouge direction SE/SW (SW = flipX).")]
        public Sprite[] WalkFrames;

        [Tooltip("Frames Sprite extraites du clip Walk de l'AnimatorController Stage0_NE (facing NE). " +
                 "Utilise par l'avatar hub quand il bouge direction NE/NW (NW = flipX).")]
        public Sprite[] WalkFramesNE;

        [Tooltip("FPS de l'animation Walk. 12 fps = walk smooth pixel art (~33% plus rapide que Idle).")]
        public float WalkFps = 12f;

        [Header("Hub Visual Calibration (port depuis Combat prefab)")]
        [Tooltip("Scale uniforme applique au child 'Visual' du HubAvatar pour cette classe. " +
                 "Effectif (= scale combat racine x scale combat Visual child). 1.0 = pas de zoom.")]
        public float HubVisualScale = 1f;

        [Tooltip("Y offset local applique au child 'Visual' du HubAvatar pour cette classe " +
                 "(monde = root.y + scale.y * yOffset si root scale != 1). Negatif = sprite descend vis-a-vis du centre de tile.")]
        public float HubVisualYOffset = 0f;

        [Tooltip("X offset local applique au child 'Visual' du HubAvatar pour cette classe (recalage horizontal, reglable via F10 en hub).")]
        public float HubVisualXOffset = 0f;

        // ==================================================================
        // Calibration VISUELLE en COMBAT pour la classe de BASE (sans skin cosmetique).
        // VIEW-ONLY : ces champs ne touchent PAS la simulation Quantum -> aucun impact
        // deterministe, donc PAS de CombatRulesVersion a incrementer (meme categorie que
        // la calibration hub ci-dessus). Le CombatantRenderer les pousse au spawn quand
        // aucun skin n'est equipe ; reglables en LIVE via le tuner F10 (CombatSkinYTuner).
        // Un skin cosmetique equipe override ces valeurs avec les siennes (CosmeticSkinDefinition).
        // ==================================================================
        [Header("Combat Visual Calibration — base (VIEW-only, pas de CombatRulesVersion)")]
        [Tooltip("Echelle du sprite en COMBAT (multiplie l'echelle de base du child Visual). 1 = taille native du prefab.")]
        public float CombatVisualScale = 1f;
        [Tooltip("X offset combat par phase (child Visual). Reglable via F10.")]
        public float Stage0CombatXOffset = 0f;
        public float Stage1CombatXOffset = 0f;
        public float Stage2CombatXOffset = 0f;
        [Tooltip("Y offset combat par phase (child Visual). Reglable via F10.")]
        public float Stage0CombatYOffset = 0f;
        public float Stage1CombatYOffset = 0f;
        public float Stage2CombatYOffset = 0f;

        // ==================================================================
        // Calibration VISUELLE des LEURRES Ghostra pour la classe de BASE (sans skin).
        // VIEW-only (pas de CombatRulesVersion). Pendant des champs DecoyCombat* de
        // CosmeticSkinDefinition : quand aucun skin n'est equipe, DecoyView lit CES valeurs.
        // N'a de sens que pour la classe Ghostra (ignore pour les autres). Reglable via F10.
        // ==================================================================
        [Header("Combat Visual Calibration — leurres Ghostra base (VIEW-only)")]
        [Tooltip("Echelle des leurres de la Ghostra de base (multiplie l'echelle de base du leurre). 1 = base.")]
        public float DecoyCombatScale = 1f;
        [Tooltip("Decalage X ADDITIONNEL des leurres de la Ghostra de base (recalage horizontal). Reglable via F10.")]
        public float DecoyCombatXOffset = 0f;
        [Tooltip("Decalage Y ADDITIONNEL des leurres de la Ghostra de base (en plus de l'offset de base du DecoyView). Reglable via F10.")]
        public float DecoyCombatYOffset = 0f;
    }
}
