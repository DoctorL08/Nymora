using Nymora.Core.Data;
using UnityEngine;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Patch UI combat 8 juin (3d) — Pousse dans le chat de combat la RAISON pour laquelle un sort
    /// n'a pas pu être lancé (PA insuffisants, relance, ressource manquante, hors cible…), pour que
    /// le joueur comprenne. Throttle anti-spam : une même raison répétée (clics multiples) n'est
    /// affichée qu'une fois par fenêtre courte. 100% View -> aucun bump CombatRulesVersion.
    /// </summary>
    public static class CombatCastFeedback
    {
        private const string WarnColor = "#d9a441"; // ambre discret (avertissement)
        private const float ThrottleSeconds = 1.2f;

        private static string _lastReason;
        private static float _lastTime = -100f;

        public static void Notify(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return;
            float now = Time.unscaledTime;
            if (reason == _lastReason && now - _lastTime < ThrottleSeconds) return;
            _lastReason = reason;
            _lastTime = now;
            // × (U+00D7, multiplication) = glyphe croix présent dans Ari/Liberation, contrairement
            // à ✗ (U+2717) qui rendait un carré placeholder. Pour la vraie croix SVG, il faudrait un
            // TMP Sprite Asset + tag <sprite> (chantier à part, le projet n'en utilise aucun).
            CombatLogRelay.Push($"<color={WarnColor}>× {reason}</color>");
        }
    }
}
