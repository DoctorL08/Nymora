using Quantum;
using UnityEngine;

namespace Nymora.Combat.View.Animation
{
    /// <summary>
    /// ScriptableObject de mapping TerrainKind -> frames sprite. Branche par TerrainView
    /// au moment de l'application visuelle des terrains poses par les sorts.
    ///
    /// 2.13.e : Vapeur Carmin + Sang Coagule (Soulrender). Ajouter les terrains
    /// Nightseer/Colossar/etc. au fur et a mesure que les sorts qui les posent existent.
    ///
    /// La cadence FPS est globale (champ dans TerrainView), pas dans le SO : 2 raisons
    ///   1) tous les terrains pixel-art Nymora tournent au meme rythme (look coherent)
    ///   2) eviter de fragmenter les params d'animation dans plusieurs assets
    /// Si un terrain venait a vraiment necessiter sa propre cadence, on ajouterait
    /// un champ Fps par entree ici.
    /// </summary>
    [CreateAssetMenu(menuName = "Nymora/Combat/Terrain Sprite Library", fileName = "TerrainSpriteLibrary", order = 120)]
    public class TerrainSpriteLibrary : ScriptableObject
    {
        [Header("Soulrender (2.13.e)")]
        [Tooltip("Frames de Vapeur Carmin (terrain pose par Charge Brutale).")]
        [SerializeField] private Sprite[] _vapeurCarminFrames;

        [Tooltip("Frames de Sang Coagule (terrain pose par Detonation Sanglante / Ame Laceree).")]
        [SerializeField] private Sprite[] _sangCoaguleFrames;

        [Header("Necram (3.5.a.iii)")]
        [Tooltip("Frames de Brume Toxique (zone DoT 3x3 posee par Brume Toxique).")]
        [SerializeField] private Sprite[] _brumeToxiqueFrames;

        // Phase 3+ : ajouter les terrains des autres classes (Filet de Ronces Nightseer, mines, etc).

        public Sprite[] GetFrames(TerrainKind kind)
        {
            switch (kind)
            {
                case TerrainKind.VapeurCarmin: return _vapeurCarminFrames;
                case TerrainKind.SangCoagule:  return _sangCoaguleFrames;
                case TerrainKind.BrumeToxique: return _brumeToxiqueFrames;
                default: return null;
            }
        }
    }
}
