using Quantum;
using UnityEngine;

namespace Nymora.Combat.View.Animation
{
    /// <summary>
    /// Mapping (StatusKind | MarkKind) -> frames de marque visuelle (anim loop sur le combatant
    /// porteur). Utilise par CombatantMarksView pour afficher un overlay au-dessus de chaque
    /// combatant porteur du status/marque.
    ///
    /// 2.13.e : 2 marques StatusKind visuellement disponibles via le pack designer Soulrender :
    ///   - StatusKind.AntiHealShield  -> sprite Plaie Ouverte (Ouvre-Plaie + 1 HG).
    ///   - StatusKind.MarkedByCarnage -> sprite Marque de Carnage (Marque de Carnage).
    ///
    /// 3.3.d polish : 2 marques MarkKind Nightseer (sur unite) :
    ///   - MarkKind.Traque    -> sprite Traque (oeil pulsant, pose par Marque du Chasseur).
    ///   - MarkKind.Empreinte -> sprite Empreinte (sillage, pose par Frappe de l'Ombre / Filet / Mines).
    ///   (Voile = sur case, gere par FogOfWarView, pas ici.)
    /// </summary>
    [CreateAssetMenu(menuName = "Nymora/Combat/Mark Sprite Library", fileName = "MarkSpriteLibrary", order = 122)]
    public class MarkSpriteLibrary : ScriptableObject
    {
        [Header("Soulrender (StatusKind 2.13.e)")]
        [Tooltip("Marque visuelle posee par Ouvre-Plaie + 1 HG (anti-heal sur la cible).")]
        [SerializeField] private Sprite[] _antiHealShieldFrames;

        [Tooltip("Marque visuelle posee par Marque de Carnage (+1 HG bonus sur cast Soulrender).")]
        [SerializeField] private Sprite[] _markedByCarnageFrames;

        [Header("Nightseer (MarkKind 3.3.d)")]
        [Tooltip("Marque visuelle Traque (oeil pulsant) — pose par Marque du Chasseur Nightseer.")]
        [SerializeField] private Sprite[] _traqueFrames;

        [Tooltip("Marque visuelle Empreinte (sillage) — pose par Frappe de l'Ombre / Filet / Champ de Mines.")]
        [SerializeField] private Sprite[] _empreinteFrames;

        public Sprite[] GetFrames(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.AntiHealShield:  return _antiHealShieldFrames;
                case StatusKind.MarkedByCarnage: return _markedByCarnageFrames;
                default: return null;
            }
        }

        /// <summary>
        /// 3.3.d polish : retourne les frames pour une MarkKind (Nightseer "L'Œil qui n'est pas").
        /// Voile (MarkKind=Voile aurait pu exister) n'est PAS dans cet enum cote sim : voile est
        /// sur case via FogOfWar, pas sur unite. Donc seuls Traque/Empreinte sont gerés ici.
        /// </summary>
        public Sprite[] GetMarkFrames(MarkKind kind)
        {
            switch (kind)
            {
                case MarkKind.Traque:    return _traqueFrames;
                case MarkKind.Empreinte: return _empreinteFrames;
                default: return null;
            }
        }
    }
}
