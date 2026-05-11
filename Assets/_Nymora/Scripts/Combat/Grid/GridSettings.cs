using UnityEngine;

namespace Nymora.Combat.Grid
{
    /// <summary>
    /// Settings de visualisation de la grille de combat (cote View uniquement).
    /// Les dimensions logiques (15x17) sont verrouillees dans GridConstants cote
    /// Quantum, ne PAS les redefinir ici. Ce SO ne controle que le rendu iso.
    /// </summary>
    [CreateAssetMenu(menuName = "Nymora/Combat/Grid Settings", fileName = "GridSettings")]
    public class GridSettings : ScriptableObject
    {
        [Header("Projection isometrique (ratio 2:1)")]
        [Tooltip("Largeur d'une case en unites monde Unity. 1.0 = 64 px a PPU 64.")]
        public float TileWorldWidth = 1.0f;

        [Tooltip("Hauteur visuelle d'une case (ratio 2:1 = 0.5 pour iso classique).")]
        public float TileWorldHeight = 0.5f;

        [Header("Ordre de rendu")]
        [Tooltip("Sorting layer des tiles de sol. Doit etre derriere les entities.")]
        public string SortingLayer = "Default";

        [Tooltip("Sorting order de base pour la grille (decremente par (x+y) en iso).")]
        public int BaseSortingOrder = 0;

        [Header("Cadrage")]
        [Tooltip("Si vrai, recentre la grille autour de (0,0) localement. Ainsi un GridRoot a la position (0,0,0) tombe juste sous la camera par defaut.")]
        public bool CenterGrid = true;
    }
}
