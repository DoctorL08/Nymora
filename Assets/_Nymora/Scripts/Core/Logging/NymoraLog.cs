using System;
using UnityEngine;

namespace Nymora.Core.Logging
{
    /// <summary>
    /// Niveaux de log Nymora. Alignes avec Pino cote backend (info/warn/error/fatal=critical).
    /// </summary>
    public enum NymoraLogLevel
    {
        Info,
        Warn,
        Error,
        Critical,
    }

    /// <summary>
    /// Logger structure cote client Unity.
    ///
    /// Conventions :
    ///   - Toujours fournir une "category" courte qui identifie le sous-systeme
    ///     (ex: "Login", "Photon", "Combat", "Hub").
    ///   - Le message reste lisible humain ; les valeurs cles dans le message
    ///     sont nommees (ex: "JWT len=234" plutot que "234").
    ///   - Pour Critical : reserve aux erreurs qui empechent le jeu de continuer
    ///     (DB serveur down, version client incompatible, fichier de config manquant).
    ///
    /// Sous le hood : wrap Debug.Log/LogWarning/LogError pour rester visible dans la
    /// Console Unity ET le filtre NymoraConsoleWindow.
    ///
    /// Phase 7+ : l'event OnLogEmitted permettra de hook un sink HTTP vers Loki.
    /// </summary>
    public static class NymoraLog
    {
        /// <summary>
        /// Hook appele apres chaque log. Sera utilise en Phase 7+ pour pousser
        /// vers Loki via HTTP. Ne RIEN faire de bloquant dedans.
        /// </summary>
        public static event Action<NymoraLogLevel, string, string> OnLogEmitted;

        public static void Info(string category, string message)
            => Emit(NymoraLogLevel.Info, category, message);

        public static void Warn(string category, string message)
            => Emit(NymoraLogLevel.Warn, category, message);

        public static void Error(string category, string message)
            => Emit(NymoraLogLevel.Error, category, message);

        /// <summary>
        /// Niveau le plus haut : reserve aux erreurs qui empechent le jeu de fonctionner.
        /// Emis comme une LogError Unity (red) avec prefix [CRITICAL].
        /// </summary>
        public static void Critical(string category, string message)
            => Emit(NymoraLogLevel.Critical, category, message);

        private static void Emit(NymoraLogLevel level, string category, string message)
        {
            if (string.IsNullOrEmpty(category)) category = "Nymora";
            string formatted = $"[Nymora.{category}] {message}";

            switch (level)
            {
                case NymoraLogLevel.Info:
                    Debug.Log(formatted);
                    break;
                case NymoraLogLevel.Warn:
                    Debug.LogWarning(formatted);
                    break;
                case NymoraLogLevel.Error:
                    Debug.LogError(formatted);
                    break;
                case NymoraLogLevel.Critical:
                    Debug.LogError($"[CRITICAL] {formatted}");
                    break;
            }

            // Hook pour sink externe (Loki en Phase 7+). Ne JAMAIS throw.
            try
            {
                OnLogEmitted?.Invoke(level, category, message);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Nymora.Log] OnLogEmitted handler threw: {e.Message}");
            }
        }
    }
}
