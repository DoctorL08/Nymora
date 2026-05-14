using System;
using System.IO;
using Quantum;
using UnityEngine;

namespace Nymora.Combat.Replay
{
    /// <summary>
    /// Container fichier .nymrep — wrap les metadonnees Nymora et le replay Quantum natif.
    ///
    /// Format : JSON unique contenant un champ <see cref="Metadata"/> et un champ
    /// <see cref="QuantumReplayJson"/> qui est le JSON pre-serialise du
    /// <see cref="QuantumReplayFile"/> (string-in-string, escape natif JsonUtility).
    /// Ca evite d'avoir a customiser la serialisation des sous-types Photon
    /// (DeterministicSessionConfig, QuantumJsonFriendlyDataBlob, ChecksumFile...).
    /// </summary>
    [Serializable]
    public class NymoraReplayFile
    {
        public NymoraReplayMetadata Metadata = new NymoraReplayMetadata();

        /// <summary>JSON pre-serialise du QuantumReplayFile. Vide tant que non charge.</summary>
        public string QuantumReplayJson;

        /// <summary>Ecrit le fichier .nymrep sur disque. Cree le dossier au besoin.</summary>
        /// <returns>Le chemin complet du fichier ecrit.</returns>
        public string WriteToDisk(string fullPath)
        {
            string folder = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            string json = JsonUtility.ToJson(this, prettyPrint: true);
            File.WriteAllText(fullPath, json);
            return fullPath;
        }

        /// <summary>Lit un fichier .nymrep depuis disque. Retourne null si fichier introuvable.</summary>
        public static NymoraReplayFile ReadFromDisk(string fullPath)
        {
            if (!File.Exists(fullPath)) return null;
            string json = File.ReadAllText(fullPath);
            return JsonUtility.FromJson<NymoraReplayFile>(json);
        }

        /// <summary>
        /// Lit uniquement la portion metadata d'un fichier .nymrep. Utile pour la
        /// Replay Library qui n'a pas besoin de deserialiser le payload Quantum.
        /// </summary>
        public static NymoraReplayMetadata ReadMetadataOnly(string fullPath)
        {
            var file = ReadFromDisk(fullPath);
            return file != null ? file.Metadata : null;
        }

        /// <summary>
        /// Retourne le QuantumReplayFile deserialise. Null si pas de payload (fichier
        /// corrompu ou pas encore enregistre).
        /// </summary>
        public QuantumReplayFile ToQuantumReplay()
        {
            if (string.IsNullOrEmpty(QuantumReplayJson)) return null;
            return JsonUtility.FromJson<QuantumReplayFile>(QuantumReplayJson);
        }

        public void SetQuantumReplay(QuantumReplayFile replay)
        {
            QuantumReplayJson = replay != null ? JsonUtility.ToJson(replay) : string.Empty;
        }
    }
}
