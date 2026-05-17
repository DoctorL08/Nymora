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

        [Tooltip("POLISH-5b (17 mai) : marque visuelle Untargetable (Voile d'Ombre Nightseer). " +
                 "Bien que le sprite source soit nomme 'voile_brume' (Bible : voile = brume protectrice), " +
                 "il s'applique sur le combatant porteur du StatusKind.Untargetable, pas sur la case.")]
        [SerializeField] private Sprite[] _untargetableFrames;

        [Header("Necram (3.4 — Floraison)")]
        [Tooltip("Marque visuelle Venin (cumulable 0-4) — posee par Crachat Acide / Inoculation / Brume Toxique / Voile Pestilence / Carapace Visqueuse. Cumulable cap 4/cible (Bible V7.1 Floraison).")]
        [SerializeField] private Sprite[] _veninStacksFrames;

        [Header("Ghostra (StatusKind 3.7.a — Angle Mort)")]
        [Tooltip("Marque visuelle Plaie Ouverte (DoT 40/tour x 2t) — posee par bonus dorsal Angle 2+ ou Frappe Fantome conditional Volte-Face.")]
        [SerializeField] private Sprite[] _plaieOuverteFrames;

        [Tooltip("Marque visuelle Marque de l'Ombre (+20 dmg sorts Ghostra + PlaieOuverte auto sur dorsal) — posee par Marque de l'Ombre.")]
        [SerializeField] private Sprite[] _marqueDeLOmbreFrames;

        public Sprite[] GetFrames(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.AntiHealShield:  return _antiHealShieldFrames;
                case StatusKind.MarkedByCarnage: return _markedByCarnageFrames;
                case StatusKind.PlaieOuverte:    return _plaieOuverteFrames;
                case StatusKind.MarqueDeLOmbre:  return _marqueDeLOmbreFrames;
                case StatusKind.Untargetable:    return _untargetableFrames;
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

        /// <summary>
        /// 3.4 — retourne les frames Venin Necram (cumulable 0-4 marques). Le rendu cote
        /// View peut soit afficher 1 sprite + count num, soit empiler N sprites — choix de
        /// VeninMarkOverlay (TODO). Ici on retourne juste les frames anim de base.
        /// </summary>
        public Sprite[] GetVeninFrames()
        {
            return _veninStacksFrames;
        }
    }
}
