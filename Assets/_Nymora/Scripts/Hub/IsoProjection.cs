using UnityEngine;

namespace Nymora.Hub
{
    /// <summary>
    /// Projection iso 2:1 grille (int) -> monde (Vector3).
    /// Dup pure de Nymora.Combat.View.IsoProjection (Brique 4.3.a) — l'isolation
    /// d'asmdef Nymora.Hub vs Nymora.Combat interdit la reference croisee.
    /// Math statique uniquement, aucune dependance Quantum.
    /// </summary>
    public static class IsoProjection
    {
        public static Vector3 GridToWorld(int gx, int gy, float tileWorldWidth, float tileWorldHeight)
        {
            float worldX = (gx - gy) * (tileWorldWidth * 0.5f);
            float worldY = (gx + gy) * (tileWorldHeight * 0.5f);
            return new Vector3(worldX, worldY, 0f);
        }

        public static int SortingOrderFor(int gx, int gy, int baseOrder)
        {
            return baseOrder - (gx + gy);
        }

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
