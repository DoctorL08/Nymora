using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Nymora.Core.Audio;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Brique A6 — Auto-assigne les AudioClips d'un dossier aux entrées du SoundBank, par
    /// correspondance NOM DE FICHIER -> SoundId. Plus besoin de glisser chaque clip à la main.
    ///
    /// Convention de nommage (dans Assets/_Nymora/Audio/Clips/, sous-dossiers OK) :
    ///   UiClick.wav                 -> SoundId.UiClick
    ///   UiClick_2.wav / UiClick (2) -> ajouté à SoundId.UiClick (variation aléatoire)
    ///   MusicHub.ogg                -> SoundId.MusicHub
    ///   AmbienceCombat.mp3          -> SoundId.AmbienceCombat
    /// Le suffixe de variation (espace/underscore + nombre, ou "(n)") est ignoré pour le match.
    ///
    /// Idempotent : ré-exécutable. Ne touche QUE le champ Clips des entrées qui ont des fichiers
    /// correspondants ; préserve Volume / Bus / PitchRange réglés à la main. Les entrées sans
    /// fichier ne sont pas vidées.
    ///
    /// Menu : Nymora > Audio > Auto-Bind Clips from Folder
    /// </summary>
    public static class AudioClipAutoBinderTool
    {
        private const string ClipsDir = "Assets/_Nymora/Audio/Clips";
        private const string BankAssetPath = "Assets/_Nymora/Resources/Audio/MainSoundBank.asset";

        [MenuItem("Nymora/Audio/Auto-Bind Clips from Folder")]
        public static void AutoBind()
        {
            var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankAssetPath);
            if (bank == null)
            {
                Debug.LogError($"[Audio] SoundBank introuvable à {BankAssetPath}. Lance d'abord « Nymora > Setup > Setup Audio System ».");
                return;
            }

            if (!AssetDatabase.IsValidFolder(ClipsDir))
            {
                EnsureFolder(ClipsDir);
                Debug.LogWarning($"[Audio] Dossier {ClipsDir} créé (vide). Dépose tes clips nommés comme les SoundId, puis relance.");
                return;
            }

            // Index : SoundId -> clips trouvés (triés par nom pour un ordre de variation stable).
            var byId = new Dictionary<SoundId, List<(string name, AudioClip clip)>>();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { ClipsDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;

                string baseName = StripVariationSuffix(Path.GetFileNameWithoutExtension(path));
                if (!TryParseSoundId(baseName, out SoundId id)) continue;

                if (!byId.TryGetValue(id, out var list)) { list = new List<(string, AudioClip)>(); byId[id] = list; }
                list.Add((Path.GetFileNameWithoutExtension(path), clip));
            }

            // Applique sur les entrées correspondantes.
            int boundIds = 0, boundClips = 0;
            var stillEmpty = new List<string>();
            foreach (var entry in bank.Entries)
            {
                if (entry == null || entry.Id == SoundId.None) continue;
                if (byId.TryGetValue(entry.Id, out var list) && list.Count > 0)
                {
                    entry.Clips = list.OrderBy(x => x.name, StringComparer.OrdinalIgnoreCase)
                                      .Select(x => x.clip).ToArray();
                    boundIds++;
                    boundClips += entry.Clips.Length;
                }
                else if (entry.Clips == null || entry.Clips.Length == 0)
                {
                    stillEmpty.Add(entry.Id.ToString());
                }
            }

            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Audio] Auto-bind terminé : {boundClips} clips assignés sur {boundIds} SoundId. " +
                      (stillEmpty.Count == 0
                          ? "Toutes les entrées ont au moins un clip. 🎉"
                          : $"Encore vides ({stillEmpty.Count}) : {string.Join(", ", stillEmpty)}"));
            Selection.activeObject = bank;
            EditorGUIUtility.PingObject(bank);
        }

        private static string StripVariationSuffix(string name)
            => Regex.Replace(name, @"[ _]?\(?\d+\)?$", "").Trim();

        private static bool TryParseSoundId(string baseName, out SoundId id)
        {
            id = SoundId.None;
            if (string.IsNullOrEmpty(baseName)) return false;
            // Enum.TryParse insensible à la casse ; rejette les valeurs purement numériques.
            return Enum.TryParse(baseName, ignoreCase: true, out id)
                   && id != SoundId.None
                   && !int.TryParse(baseName, out _);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            string leaf = path.Substring(slash + 1);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
