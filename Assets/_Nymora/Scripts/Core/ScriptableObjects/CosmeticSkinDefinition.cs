using System.Collections.Generic;
using Nymora.Core.Enums;
using UnityEngine;

namespace Nymora.Core.ScriptableObjects
{
    /// <summary>
    /// Brique 5.11 — Une animation de prévisu (frames SE d'un clip combat), pour le tooltip
    /// boutique. Extrait des AnimatorControllers par "Nymora > Setup > Extract Skin Combat Preview".
    /// </summary>
    [System.Serializable]
    public class SkinPreviewClip
    {
        public int Stage;            // 0 / 1 / 2
        public string Anim;          // "idle" | "walk" | "attack" | "cast" | "hurt" | "death"
        public Sprite[] Frames;
        public float Fps = 8f;
    }

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
        [Tooltip("Décalage X du sprite dans le HUB (recalage horizontal, réglable via F10 en hub).")]
        public float HubVisualXOffset = 0f;

        [Tooltip("5.12 — Échelle de la prévisu dans le MENU perso uniquement (indépendant du hub). " +
                 "1 = ajusté à la box comme les classes ; >1 agrandit, <1 réduit. Réglable PAR skin " +
                 "car chaque art a une marge native différente (ex Ashen ~1.4).")]
        public float MenuPortraitScale = 1f;

        // ==================================================================
        // Brique 5.10 — Skin en COMBAT.
        // Le combat anime un combattant via 6 AnimatorController (stage 0/1/2 × NE/SE)
        // exactement comme NymoraClassDefinition / le prefab Combatant_<Classe> (cf
        // CombatantView._stageXControllerYY). Quand ce skin est équipé, CombatantView
        // swappe ses 6 controllers de classe pour CEUX-CI.
        //
        // Peuplés par "Nymora > Setup > Build Ashen Sovereign Combat Animator" (lit les
        // .aseprite stage0/1/2 NE/SE et génère les controllers idle/walk/attack/cast/hurt/death).
        // Laissés null pour un skin sans variante combat (l'avatar hub reste skinné, le
        // combat garde le visuel de classe).
        // ==================================================================
        [Header("Combat — AnimatorControllers par stage (5.10)")]
        public RuntimeAnimatorController Stage0ControllerSE;
        public RuntimeAnimatorController Stage1ControllerSE;
        public RuntimeAnimatorController Stage2ControllerSE;
        public RuntimeAnimatorController Stage0ControllerNE;
        public RuntimeAnimatorController Stage1ControllerNE;
        public RuntimeAnimatorController Stage2ControllerNE;

        [Header("Combat — Y offset visuel par stage (pivot Aseprite non standardisé)")]
        public float Stage0CombatYOffset = 0f;
        public float Stage1CombatYOffset = 0f;
        public float Stage2CombatYOffset = 0f;

        [Header("Combat — X offset visuel par stage (recalage horizontal du sprite, réglable via F10)")]
        public float Stage0CombatXOffset = 0f;
        public float Stage1CombatXOffset = 0f;
        public float Stage2CombatXOffset = 0f;

        [Tooltip("Échelle du skin en COMBAT (multiplie l'échelle de base du child Visual de la classe). " +
                 "1 = taille de base de la classe ; <1 réduit, >1 agrandit. Réglable en live via F10.")]
        public float CombatVisualScale = 1f;

        [Header("Combat — Leurres (Ghostra) : calibration quand ce skin est porté")]
        [Tooltip("Échelle des leurres portant ce skin (multiplie l'échelle de base du leurre). 1 = base.")]
        public float DecoyCombatScale = 1f;
        [Tooltip("Décalage Y ADDITIONNEL appliqué aux leurres portant ce skin (en plus de l'offset de base du DecoyView).")]
        public float DecoyCombatYOffset = 0f;

        /// <summary>True si le skin embarque au moins le controller stage 0 SE (variante combat dispo).</summary>
        public bool HasCombatControllers => Stage0ControllerSE != null;

        // ==================================================================
        // Brique 5.11 — Frames de PRÉVISU (tooltip boutique). Extraites des clips des
        // AnimatorControllers SE (les controllers ne sont pas lisibles au runtime) par l'outil
        // "Extract Skin Combat Preview". Permet de rejouer stage 0/1/2 × idle/walk/attack/cast/
        // hurt/death dans une UI Image (UISpriteAnimator) sans Animator.
        // ==================================================================
        [Header("Prévisu boutique (frames extraites, 5.11)")]
        public List<SkinPreviewClip> CombatPreview = new List<SkinPreviewClip>();

        /// <summary>Frames d'un (stage, anim) de prévisu, ou null si absent.</summary>
        public SkinPreviewClip GetPreview(int stage, string anim)
        {
            if (CombatPreview == null) return null;
            for (int i = 0; i < CombatPreview.Count; i++)
            {
                var c = CombatPreview[i];
                if (c != null && c.Stage == stage && c.Anim == anim
                    && c.Frames != null && c.Frames.Length > 0)
                    return c;
            }
            return null;
        }

        /// <summary>True si au moins une prévisu combat a été extraite.</summary>
        public bool HasCombatPreview => CombatPreview != null && CombatPreview.Count > 0;
    }
}
