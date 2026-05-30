using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Applique les réglages d'import demandés par Kyami (note polish) aux textures des maps :
    ///   - Filter Mode : Point (No Filter) -> conserve la netteté des pixels.
    ///   - Compression : None (Uncompressed) -> évite la dégradation de qualité.
    ///
    /// Cibles par défaut :
    ///   - MAP_HUB  : Art/Hub/map_hub.png
    ///   - MAP_ARENE: les 12 frames Art/UI/Maps/Arene1vs1_Anim/*.png (l'arène réellement
    ///     référencée par les 3 scènes combat ; Map_Combat_1.png est inutilisé).
    ///
    /// Option « Selection » : applique les mêmes réglages aux textures sélectionnées dans le
    /// Project (réutilisable pour les futurs imports de maps).
    ///
    /// 100% import-time (aucun code runtime, aucun bump CombatRulesVersion). Idempotent.
    ///
    /// Menu : Nymora &gt; Setup &gt; Apply Map Import Settings (Point + No Compression)
    /// </summary>
    public static class ApplyMapImportSettingsTool
    {
        private const string MapHubPath = "Assets/_Nymora/Art/Hub/map_hub.png";
        private const string AreneFolder = "Assets/_Nymora/Art/UI/Maps/Arene1vs1_Anim";

        [MenuItem("Nymora/Setup/Apply Map Import Settings (Point + No Compression)")]
        public static void ApplyToMaps()
        {
            var targets = new List<string>();
            if (File.Exists(MapHubPath)) targets.Add(MapHubPath);

            if (Directory.Exists(AreneFolder))
            {
                foreach (string file in Directory.GetFiles(AreneFolder, "*.png", SearchOption.TopDirectoryOnly))
                    targets.Add(file.Replace('\\', '/'));
            }

            int done = Apply(targets);
            Debug.Log($"[ApplyMapImportSettings] Maps : {done}/{targets.Count} texture(s) réglée(s) en Point + None.");
            EditorUtility.DisplayDialog("Map Import Settings",
                $"{done} texture(s) de map réglée(s) :\n- Filter Mode : Point\n- Compression : None\n\nMAP_HUB + {targets.Count - 1} frame(s) d'arène.", "OK");
        }

        [MenuItem("Nymora/Setup/Apply Map Import Settings — to Selection")]
        public static void ApplyToSelection()
        {
            var targets = new List<string>();
            foreach (Object obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path) && AssetImporter.GetAtPath(path) is TextureImporter)
                    targets.Add(path);
            }

            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("Map Import Settings",
                    "Aucune texture sélectionnée dans le Project.", "OK");
                return;
            }

            int done = Apply(targets);
            Debug.Log($"[ApplyMapImportSettings] Selection : {done}/{targets.Count} texture(s) réglée(s) en Point + None.");
            EditorUtility.DisplayDialog("Map Import Settings",
                $"{done} texture(s) sélectionnée(s) réglée(s) en Point + None.", "OK");
        }

        private static int Apply(List<string> assetPaths)
        {
            int done = 0;
            foreach (string path in assetPaths)
            {
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;

                bool changed = false;

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                if (settings.filterMode != FilterMode.Point)
                {
                    settings.filterMode = FilterMode.Point;
                    importer.SetTextureSettings(settings);
                    changed = true;
                }

                // Compression None sur la plateforme par défaut (héritée par Standalone).
                var def = importer.GetDefaultPlatformTextureSettings();
                if (def.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    def.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SetPlatformTextureSettings(def);
                    changed = true;
                }

                if (changed)
                {
                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();
                    done++;
                }
            }
            return done;
        }
    }
}
