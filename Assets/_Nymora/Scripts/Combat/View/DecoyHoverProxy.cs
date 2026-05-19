using Quantum;
using UnityEngine;

namespace Nymora.Combat.View
{
    /// <summary>
    /// Marqueur attache aux GameObjects leurre Ghostra par DecoyView. Permet a
    /// TileHoverView de detecter le survol d'un leurre et d'afficher le MEME tooltip
    /// que pour le vrai Ghostra parent (Bible V7.1 "indiscernable cote adversaire" :
    /// l'adversaire qui survole un leurre voit les HP du vrai Ghostra = mensonge
    /// volontaire ; le caster Ghostra voit aussi le tooltip Ghostra car il sait
    /// deja que c'est un leurre via le tint visuel applique par DecoyView).
    /// </summary>
    public sealed class DecoyHoverProxy : MonoBehaviour
    {
        /// <summary>Entity du Ghostra parent dont les HP/MaxHP servent au tooltip.</summary>
        public EntityRef GhostraParentEntity { get; set; }
    }
}
