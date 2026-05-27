namespace Nymora.Combat.View
{
    /// <summary>
    /// Brique J8 (juice combat) — easing partage pour les animations "sort du sol" (obstacles,
    /// pieges, tiles). Back-ease-out : part de 0, depasse legerement 1 (~1.1) puis se cale a 1,
    /// donnant un petit rebond d'emergence. Sans dependance Unity (pur math), accessible aux
    /// vues du namespace Nymora.Combat.View et de ses sous-namespaces.
    /// </summary>
    internal static class GroundEmergeEase
    {
        /// <summary>t (0..1) -> facteur d'echelle (0 -> ~1.1 -> 1) avec leger overshoot.</summary>
        public static float BackOut(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            const float c1 = 2.0f;
            const float c3 = c1 + 1f;
            float tm = t - 1f;
            return 1f + c3 * tm * tm * tm + c1 * tm * tm;
        }
    }
}
