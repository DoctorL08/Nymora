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

        // Phase ulterieure : autres sorts Soulrender (Charge Brutale, Detonation Sanglante, ...)
        // puis classes Phase 3+. On peut soit etendre avec des champs nommes (comme ici),
        // soit basculer sur un Entry[] generique si le nombre devient trop grand.

        public Sprite[] GetFrames(SpellId spell)
        {
            switch (spell)
            {
                case SpellId.SoulrenderAmeLaceree: return _ameLaceeFrames;
                default: return null;
            }
        }
    }
}
