using System;
using UnityEngine;

namespace Nymora.Combat.Replay
{
    /// <summary>
    /// Metadonnees d'un replay Nymora. Stockees en clair JSON pour pouvoir trier /
    /// filtrer dans la Replay Library sans deserialiser tout le payload Quantum.
    ///
    /// Le champ <see cref="FormatVersion"/> permet de gerer la compatibilite future
    /// si on change le schema. Toute modif breakante du format = bump du format.
    /// </summary>
    [Serializable]
    public class NymoraReplayMetadata
    {
        /// <summary>Version du schema du fichier .nymrep. Bumper si schema brise.</summary>
        public int FormatVersion = 1;

        /// <summary>Horodatage UTC ISO 8601 de l'enregistrement du replay.</summary>
        public string CreatedAtUtc;

        /// <summary>Duree du match en secondes (computed cote View).</summary>
        public int DurationSeconds;

        /// <summary>Nombre total de rounds (TurnNumber atteint en fin de match).</summary>
        public int TotalRounds;

        /// <summary>Version de la Bible (ex: V7.1). Sert a refuser un replay incompatible.</summary>
        public string BibleVersion;

        /// <summary>CombatRulesVersion au moment de l'enregistrement.</summary>
        public int CombatRulesVersion;

        /// <summary>Classe du joueur 0 (string pour serialisation JSON robuste).</summary>
        public string Player0Class;

        /// <summary>Classe du joueur 1.</summary>
        public string Player1Class;

        /// <summary>Index du joueur gagnant (-1 si match nul / double KO).</summary>
        public int WinnerPlayerIndex = -1;

        /// <summary>Nom de la scene dans laquelle le match a ete enregistre.</summary>
        public string SceneName;

        /// <summary>Note libre (optionnelle, future : tags joueur).</summary>
        public string Note;
    }
}
