using UnityEngine;

namespace Nymora.Combat.Spectate
{
    /// <summary>
    /// Brique S4 (mode spectateur) — Pont hub → scène combat pour le visionnage live.
    /// Le hub pose le matchId à spectater juste avant de charger la scène du mode ; le
    /// <see cref="LiveSpectateController"/> (sur la caméra de la scène) le consomme au démarrage
    /// et bascule en GameMode.Replay alimenté par le flux réseau (0 CCU Quantum).
    ///
    /// PlayerPrefs (comme ReplayPlaybackBridge) : un static C# est reset par le domain reload
    /// de l'entrée en Play Mode — il faut survivre entre le set (hub) et le consume (combat).
    /// </summary>
    public static class LiveSpectateBridge
    {
        private const string PrefsKey = "Nymora.Spectate.PendingMatchId";

        /// <summary>matchId à spectater. Null si aucun visionnage demandé.</summary>
        public static string RequestedMatchId
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

        /// <summary>Retourne et clear le matchId demandé (un visionnage = une lecture).</summary>
        public static string Consume()
        {
            string s = PlayerPrefs.GetString(PrefsKey, "");
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
            return string.IsNullOrEmpty(s) ? null : s;
        }
    }
}
