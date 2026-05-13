using Quantum;
using UnityEngine;

namespace Nymora.Combat.View.Animation
{
    /// <summary>
    /// Mapping StatusKind -> frames de marque visuelle (anim loop sur le combatant porteur).
    /// Utilise par CombatantMarksView pour afficher un overlay au-dessus de chaque combatant
    /// porteur du status.
    ///
    /// 2.13.e : 2 marques visuellement disponibles via le pack designer Soulrender :
    ///   - StatusKind.AntiHealShield  (StatusKind = 1) -> sprite Plaie Ouverte (gueule).
    ///       Pose par : Ouvre-Plaie + 1 HG (Soulrender).
    ///   - StatusKind.MarkedByCarnage (StatusKind = 7) -> sprite Marque de Carnage (croix).
    ///       Pose par : Marque de Carnage (Soulrender).
    ///
    /// Phase 3+ : etendre avec les marques Nightseer (Traque/Voile/Empreinte), Necram (poison),
    /// Ghostra (rémanence), etc.
    /// </summary>
    [CreateAssetMenu(menuName = "Nymora/Combat/Mark Sprite Library", fileName = "MarkSpriteLibrary", order = 122)]
    public class MarkSpriteLibrary : ScriptableObject
    {
        [Header("Soulrender (2.13.e)")]
        [Tooltip("Marque visuelle posee par Ouvre-Plaie + 1 HG (anti-heal sur la cible).")]
        [SerializeField] private Sprite[] _antiHealShieldFrames;

        [Tooltip("Marque visuelle posee par Marque de Carnage (+1 HG bonus sur cast Soulrender).")]
        [SerializeField] private Sprite[] _markedByCarnageFrames;

        public Sprite[] GetFrames(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.AntiHealShield:  return _antiHealShieldFrames;
                case StatusKind.MarkedByCarnage: return _markedByCarnageFrames;
                default: return null;
            }
        }
    }
}
