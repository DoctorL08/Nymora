using UnityEngine;

namespace Nymora.Combat.View
{
    /// <summary>
    /// Projection iso 2:1 grille (int) -> monde (Vector3).
    /// Cote View uniquement — la simulation Quantum reste sur des coordonnees
    /// entieres rectangulaires, l'iso n'est qu'une representation visuelle.
    /// </summary>
    public static class IsoProjection
    {
        public static Vector3 GridToWorld(int gx, int gy, float tileWorldWidth, float tileWorldHeight)
        {
            float worldX = (gx - gy) * (tileWorldWidth * 0.5f);
            float worldY = (gx + gy) * (tileWorldHeight * 0.5f);
            return new Vector3(worldX, worldY, 0f);
        }

        // En iso, les tiles avec un (gx + gy) plus eleve sont visuellement plus haut
        // a l'ecran (donc plus loin). Elles doivent passer DERRIERE les tiles plus basses.
        // Sorting order decroissant avec (gx + gy).
        public static int SortingOrderFor(int gx, int gy, int baseOrder)
        {
            return baseOrder - (gx + gy);
        }

        /// <summary>
        /// Offset a appliquer pour recentrer une grille (width x height) autour de (0,0).
        /// Calcule la moyenne des 4 coins en iso puis inverse — le centre logique tombe a l'origine.
        /// </summary>
        public static Vector3 CenterOffset(int width, int height, float tileWorldWidth, float tileWorldHeight)
        {
            if (width <= 0 || height <= 0) return Vector3.zero;
            Vector3 bl = GridToWorld(0, 0, tileWorldWidth, tileWorldHeight);
            Vector3 br = GridToWorld(width - 1, 0, tileWorldWidth, tileWorldHeight);
            Vector3 tl = GridToWorld(0, height - 1, tileWorldWidth, tileWorldHeight);
            Vector3 tr = GridToWorld(width - 1, height - 1, tileWorldWidth, tileWorldHeight);
            Vector3 center = (bl + br + tl + tr) * 0.25f;
            return -center;
        }

        /// <summary>
        /// Inverse de GridToWorld : trouve la case (gx, gy) sous une position world (clic souris).
        ///
        /// Math : on resout le systeme
        ///   worldX = (gx - gy) * (tileW / 2)
        ///   worldY = (gx + gy) * (tileH / 2)
        /// Donc a = gx - gy = 2 * wx / tw  et  b = gx + gy = 2 * wy / th
        /// D'ou gx = (a + b) / 2 et gy = (b - a) / 2.
        /// </summary>
        public static (int gx, int gy) WorldToGrid(Vector3 worldPos, float tileWorldWidth, float tileWorldHeight, Vector3 centerOffset)
        {
            Vector3 local = worldPos - centerOffset;
            float a = 2f * local.x / tileWorldWidth;
            float b = 2f * local.y / tileWorldHeight;
            int gx = Mathf.RoundToInt((a + b) * 0.5f);
            int gy = Mathf.RoundToInt((b - a) * 0.5f);
            return (gx, gy);
        }
    }
}
