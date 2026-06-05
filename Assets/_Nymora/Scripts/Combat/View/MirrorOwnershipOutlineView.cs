using UnityEngine;

namespace Nymora.Combat.View
{
    /// <summary>
    /// #23 (5 juin) — CONFIG de l'outline d'équipe (match miroir). Porte les 2 couleurs d'équipe
    /// (schéma fixe par PlayerIndex) + l'épaisseur du liseré, et les pousse à <see cref="MirrorOutlineHelper"/>.
    ///
    /// Le DESSIN du liseré (silhouette) est fait dans les vues d'objets (TrapView, DecoyView,
    /// ObstacleView, TerrainView) via MirrorOutlineHelper.Refresh ; le marquage n'apparaît qu'en
    /// match miroir (même classe), test fait dans les vues via Quantum.MatchViewHelpers.IsMirrorMatch.
    ///
    /// Poser ce composant UNE fois par scène combat (n'importe quel GameObject). Régler les couleurs
    /// dans l'Inspector. Pure View — aucun impact simulation.
    /// </summary>
    public class MirrorOwnershipOutlineView : MonoBehaviour
    {
        [Header("Couleurs d'équipe (schéma fixe par PlayerIndex)")]
        [Tooltip("Contour des objets du joueur 0 (host / Lorenzo en IA).")]
        [SerializeField] private Color _teamColorP0 = new Color(0.32f, 0.66f, 1f, 1f);   // bleu
        [Tooltip("Contour des objets du joueur 1 (invité / bot en IA).")]
        [SerializeField] private Color _teamColorP1 = new Color(1f, 0.55f, 0.16f, 1f);   // orange

        [Header("Rendu")]
        [Tooltip("Épaisseur du liseré en unités monde (0.02 fin → 0.08 épais).")]
        [SerializeField, Range(0.01f, 0.15f)] private float _outlineWidth = 0.05f;

        private void OnEnable() => Push();

        // Pousse chaque frame : robuste au tweak des couleurs/épaisseur en Play Mode.
        private void Update() => Push();

        private void Push()
        {
            MirrorOutlineHelper.SetPalette(_teamColorP0, _teamColorP1);
            MirrorOutlineHelper.SetWidth(_outlineWidth);
        }
    }
}
