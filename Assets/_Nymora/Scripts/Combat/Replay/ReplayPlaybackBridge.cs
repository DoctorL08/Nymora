using UnityEngine;

namespace Nymora.Combat.Replay
{
    /// <summary>
    /// Brique 3.E.2 — Pont entre Replay Library (Editor) et le runtime de combat.
    /// Stocke le chemin du .nymrep a charger juste avant le scene load/reload.
    ///
    /// Single-scene mode (refactor B) : on rouvre la scene source du replay (lue
    /// dans metadata.SceneName, fallback 30_CombatIA). Le <see cref="ReplayPlaybackController"/>
    /// place sur la Main Camera detecte le path au demarrage et bascule en mode
    /// replay (disable LocalDebug + Recorder + InputController, lance le runner
    /// en GameMode.Replay). Si pas de path, il self-disable et la scene fonctionne
    /// normalement.
    ///
    /// Persistance via PlayerPrefs : indispensable car un static C# est reset par
    /// le domain reload que Unity declenche en entrant en Play Mode. Le path doit
    /// survivre entre "set cote Editor" et "consume cote runtime".
    /// </summary>
    public static class ReplayPlaybackBridge
    {
        /// <summary>Scene par defaut si metadata.SceneName est vide ou introuvable.</summary>
        public const string DefaultCombatSceneName = "30_CombatIA";

        private const string PrefsKey = "Nymora.Replay.PendingPath";

        /// <summary>Chemin absolu du .nymrep a charger. Null si aucun replay demande.</summary>
        public static string RequestedReplayPath
        {
            get
            {
                string s = PlayerPrefs.GetString(PrefsKey, "");
                return string.IsNullOrEmpty(s) ? null : s;
            }
            set
            {
                if (string.IsNullOrEmpty(value)) PlayerPrefs.DeleteKey(PrefsKey);
                else PlayerPrefs.SetString(PrefsKey, value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Retourne et clear le chemin demande (un replay = une lecture).</summary>
        public static string Consume()
        {
            string s = PlayerPrefs.GetString(PrefsKey, "");
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
            return string.IsNullOrEmpty(s) ? null : s;
        }
    }
}
