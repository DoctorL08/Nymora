using System;
using System.IO;
using UnityEngine;

namespace Nymora.Combat.Replay
{
    /// <summary>
    /// Constantes de chemins pour les replays Nymora. Centralise le dossier racine
    /// et l'extension pour que la View runtime, l'Editor Window et tout outil futur
    /// pointent au meme endroit.
    ///
    /// Dossier : <c>Application.persistentDataPath/Replays/</c> — soit sur Windows
    /// <c>C:\Users\&lt;user&gt;\AppData\LocalLow\&lt;Company&gt;\Nymora\Replays\</c>.
    /// </summary>
    public static class ReplayPaths
    {
        public const string ReplayExtension = ".nymrep";
        public const string FolderName = "Replays";

        public static string RootFolder
        {
            get { return Path.Combine(Application.persistentDataPath, FolderName); }
        }

        /// <summary>Cree le dossier s'il n'existe pas. Idempotent.</summary>
        public static void EnsureFolderExists()
        {
            string folder = RootFolder;
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        }

        /// <summary>
        /// Genere un nom de fichier unique pour un replay base sur la date courante.
        /// Format : <c>2026-05-14_23-45-12_Colossar-vs-Nightseer.nymrep</c>.
        /// </summary>
        public static string GenerateFilename(string player0Class, string player1Class, DateTime utcNow)
        {
            string p0 = string.IsNullOrEmpty(player0Class) ? "Unknown" : player0Class;
            string p1 = string.IsNullOrEmpty(player1Class) ? "Unknown" : player1Class;
            string stamp = utcNow.ToLocalTime().ToString("yyyy-MM-dd_HH-mm-ss");
            return string.Format("{0}_{1}-vs-{2}{3}", stamp, p0, p1, ReplayExtension);
        }

        public static string CombineWithRoot(string filename)
        {
            return Path.Combine(RootFolder, filename);
        }
    }
}
