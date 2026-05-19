using Quantum;
using UnityEngine;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Bridge static View-only pour memoriser le dernier cast d'un sort signature. Permet au
    /// FloatingTextManager (via CombatantHPWatcher) de spawner un texte epique (gros + or +
    /// bounce) au lieu du texte standard quand un signature inflige des degats.
    ///
    /// Set par CombatInputController.SendSpellAt apres SendCommand. Lu par CombatantHPWatcher
    /// dans une fenetre temporelle courte (~1.5s) pour matcher le moment ou le delta HP arrive
    /// (delai entre cast command et application du dmg = quelques ticks Quantum).
    ///
    /// Fragile par design : si plusieurs casts en succession rapide, la fenetre peut chevaucher.
    /// Acceptable pour MVP — les signatures ont generalement un cooldown 4 tours donc pas de
    /// double cast realiste dans 1.5s. Si bug avere plus tard, migrer vers signal Quantum.
    /// </summary>
    public static class SignatureCastBridge
    {
        // -999 = never set, ne matchera jamais Time.unscaledTime - LastTime < window.
        private static float _lastSignatureCastTime = -999f;
        // Window dans laquelle un dmg delta est considere comme venant du signature recent.
        // 1.5s couvre largement le delai Quantum command -> apply damage (~10-30 ticks @ 60fps).
        private const float WindowSeconds = 1.5f;

        /// <summary>
        /// Hardcoded list des 5 SpellId signatures (un par classe).
        /// </summary>
        public static bool IsSignatureSpell(SpellId spell)
        {
            return spell == SpellId.SoulrenderAmeLaceree
                || spell == SpellId.NightseerTraquenard
                || spell == SpellId.ColossarEffondrement
                || spell == SpellId.NecramVirusFatal
                || spell == SpellId.GhostraExecutionSpectrale;
        }

        public static void NotifySpellCast(SpellId spell)
        {
            if (IsSignatureSpell(spell))
            {
                _lastSignatureCastTime = Time.unscaledTime;
            }
        }

        public static bool IsSignatureRecent()
        {
            return Time.unscaledTime - _lastSignatureCastTime < WindowSeconds;
        }

        public static void Reset()
        {
            _lastSignatureCastTime = -999f;
        }
    }
}
