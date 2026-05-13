using System.IO;
using UnityEditor;
using UnityEditor.U2D.Aseprite;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// Tool d'ajustement pivot + PPU pour les sprites .aseprite des combattants.
    ///
    /// IMPORTANT : Les sprites .aseprite ont leur PROPRE importer (AsepriteImporter, package
    /// com.unity.2d.aseprite). L'AssetPostprocessor NymoraSpriteImporterSettings ne s'applique
    /// QU'aux .png/.gif/.jpg (TextureImporter), pas aux .aseprite. Donc pour configurer le
    /// pivot des sprites animes du Soulrender (et futurs Nightseer/Colossar/etc.), il faut
    /// utiliser ce tool.
    ///
    /// Iteration visuelle :
    ///   1. Edite les const PivotX/PivotY/PixelsPerUnit ci-dessous.
    ///   2. Menu : Nymora > Setup > Tune Aseprite Character Sprites
    ///   3. Sauve, retour Play Mode, observe.
    ///   4. Repeter.
    ///
    /// Calcul de reference (Bible Nymora, iso 2:1, tile 1.0 unit, character avec pieds
    /// au pixel Y=6 du frame 128x128) :
    ///   PivotY = (0.25 unit + 6/PPU) / (128/PPU) = (0.25 + 6/PPU) * PPU/128
    ///   Pour PPU=96 : PivotY ~= 0.234 (pieds pile au bas du diamond iso).
    ///   Pour PPU=128 : PivotY ~= 0.297.
    /// </summary>
    public static class TuneAsepriteCharacterSpritesTool
    {
        // ====================================================================
        // EDIT THESE FOR FAST ITERATION :
        // ====================================================================
        private const float PivotX = 0.5f;
        private const float PivotY = 0.1f;
        private const float PixelsPerUnit = 96f;  // 96 = sprite 33% plus grand que tile (128 = exactement 1 tile)
        // ====================================================================

        private static readonly string[] CharacterAseFolders =
        {
            "Assets/_Nymora/Art/Sprites/Soulrender/Base/sources",
            "Assets/_Nymora/Art/Sprites/Nightseer/Base/sources",
            "Assets/_Nymora/Art/Sprites/Colossar/Base/sources",   // 3.1.bis
            // Phase 3 : Necram/Ghostra sources/
        };

        [MenuItem("Nymora/Setup/Tune Aseprite Character Sprites")]
        public static void Run()
        {
            int total = 0;
            foreach (var folder in CharacterAseFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Debug.Log($"[Nymora.TuneAseprite] Skip (folder absent) : {folder}");
                    continue;
                }
                // FindAssets("t:Texture2D") matche aussi les .aseprite (qui exposent une Texture2D).
                string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.EndsWith(".aseprite", System.StringComparison.OrdinalIgnoreCase)) continue;

                    var importer = AssetImporter.GetAtPath(path) as AsepriteImporter;
                    if (importer == null)
                    {
                        Debug.LogWarning($"[Nymora.TuneAseprite] Pas un AsepriteImporter : {path}");
                        continue;
                    }

                    importer.pivotAlignment = SpriteAlignment.Custom;
                    importer.customPivotPosition = new Vector2(PivotX, PivotY);
                    importer.spritePixelsPerUnit = PixelsPerUnit;
                    importer.SaveAndReimport();
                    total++;
                    Debug.Log($"[Nymora.TuneAseprite] {Path.GetFileName(path)} : pivot=({PivotX},{PivotY}) PPU={PixelsPerUnit}");
                }
            }
            AssetDatabase.Refresh();
            Debug.Log($"[Nymora.TuneAseprite] Termine : {total} .aseprite reimported.");
        }
    }
}
