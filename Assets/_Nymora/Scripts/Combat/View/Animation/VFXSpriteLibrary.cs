using Quantum;
using UnityEngine;

namespace Nymora.Combat.View.Animation
{
    /// <summary>
    /// Mapping SpellId -> frames de VFX one-shot. Utilise par CombatVFXView pour
    /// spawn un sprite anime one-shot quand un sort est cast.
    ///
    /// 2.13.e : seul Ame Laceree (signature Soulrender) a un VFX dispo (10 frames).
    /// Les autres sorts Soulrender peuvent etre ajoutes ici quand le designer livre,
    /// puis Nightseer/Colossar/etc en Phase 3+.
    ///
    /// Convention : VFX one-shot, joue 1 fois puis se detruit. Pour un VFX en boucle
    /// (genre une aura), c'est plutot un Status visuel (autre systeme, futur).
    /// </summary>
    [CreateAssetMenu(menuName = "Nymora/Combat/VFX Sprite Library", fileName = "VFXSpriteLibrary", order = 121)]
    public class VFXSpriteLibrary : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public SpellId Spell;
            public Sprite[] Frames;
        }

        [Header("Soulrender (2.13.e)")]
        [Tooltip("Ame Laceree — signature 5 HG, 320 dgts + heal 50%, cooldown 4 tours.")]
        [SerializeField] private Sprite[] _ameLaceeFrames;

        [Header("Colossar (3.3.d)")]
        [Tooltip("Effondrement — signature 3 FD, AoE rayon 2 + swap + Failles 2T + buff 2T.")]
        [SerializeField] private Sprite[] _effondrementFrames;

        [Header("Necram (3.5.c.vi)")]
        [Tooltip("Virus Fatal — signature 2 PA range 5, 6/6 PT consume, tick venin x3 sur cible + transfert marques si kill, cooldown 4 tours.")]
        [SerializeField] private Sprite[] _virusFatalFrames;

        [Header("Ghostra (3.7.d)")]
        [Tooltip("Execution Spectrale — signature 3 PA range 1 melee dorsal, 3/3 leurres consume, 350 dgts + plaie 50/3 + kill bonus +100 HP + 2 leurres respawn, cooldown 4 tours.")]
        [SerializeField] private Sprite[] _executionSpectraleFrames;

        // Phase ulterieure : autres sorts Soulrender (Charge Brutale, Detonation Sanglante, ...)
        // puis Nightseer/Necram/Ghostra non-signatures. On peut soit etendre avec des champs nommes
        // (comme ici), soit basculer sur un Entry[] generique si le nombre devient trop grand.

        public Sprite[] GetFrames(SpellId spell)
        {
            switch (spell)
            {
                case SpellId.SoulrenderAmeLaceree:        return _ameLaceeFrames;
                case SpellId.ColossarEffondrement:        return _effondrementFrames;
                case SpellId.NecramVirusFatal:            return _virusFatalFrames;
                case SpellId.GhostraExecutionSpectrale:   return _executionSpectraleFrames;
                default: return null;
            }
        }
    }
}
