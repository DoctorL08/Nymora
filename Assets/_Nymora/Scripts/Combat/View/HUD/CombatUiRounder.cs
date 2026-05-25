using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Petit décorateur runtime : applique un fond à coins arrondis (CombatUiKit) sur l'Image
    /// du même GameObject au démarrage. Posé par 'Restyle Combat HUD' sur les Images de scène
    /// qui n'ont pas de script dédié (ex : bouton Fin de tour) — le sprite arrondi étant généré
    /// en code, il ne peut pas être sérialisé dans la scène, d'où ce composant. 100% View.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class CombatUiRounder : MonoBehaviour
    {
        [Tooltip("Rayon des coins en px (def = CombatUiKit.CornerRadius).")]
        [SerializeField] private float _radius = CombatUiKit.CornerRadius;

        private void Awake()
        {
            CombatUiKit.ApplyRounded(GetComponent<Image>(), _radius);
        }
    }
}
