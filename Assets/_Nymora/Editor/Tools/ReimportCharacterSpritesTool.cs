using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// Force le reimport de tous les sprites de combattants pour appliquer les settings
    /// courants de NymoraSpriteImporterSettings (notamment le pivot custom).
    ///
    /// Useful quand on change la convention de pivot dans NymoraSpriteImporterSettings :
    /// les fichiers deja importes gardent leur ancien pivot tant qu'ils ne sont pas
    /// reimportes. Ce menu force le re-process.
    ///
    /// Menu : Nymora > Setup > Reimport Character Sprites
    /// </summary>
    public static class ReimportCharacterSpritesTool
    {
        private static readonly string[] CharacterFolders =
        {
            "Assets/_Nymora/Art/Sprites/Soulrender/Base",
            // Phase 3 : ajouter les dossiers Nightseer / Colossar / Necram / Ghostra ici.
        };

        [MenuItem("Nymora/Setup/Reimport Character Sprites")]
        public static void Run()
        {
            int total = 0;
            foreach (var folder in CharacterFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Debug.Log($"[Nymora.ReimportCharSprites] Skip (folder absent) : {folder}");
                    continue;
                }
                string[] guids = AssetDatabase.FindAssets("t:texture2D", new[] { folder });
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    total++;
                }
            }
            AssetDatabase.Refresh();
            Debug.Log($"[Nymora.ReimportCharSprites] Reimport force termine : {total} fichiers retraites.");
        }
    }
}
