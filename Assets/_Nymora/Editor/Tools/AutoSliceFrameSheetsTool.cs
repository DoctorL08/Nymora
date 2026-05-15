using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// Auto-slicer pour les sprite sheets horizontaux des animations Nymora.
    ///
    /// Pattern detecte : nom de fichier "*_Nframe.png" -> sheet horizontal de N cellules
    /// de meme largeur. Configure le TextureImporter en spriteMode = Multiple et genere
    /// N SpriteRect (1 par frame) via l'API moderne SpriteDataProvider.
    ///
    /// API : on utilise SpriteDataProviderFactories (Unity 2020+) au lieu du legacy
    /// TextureImporter.spritesheet qui est IGNORE silencieusement par Unity 2022+ —
    /// d'ou les sheets en spriteMode Multiple mais sprites: [] (bug rencontre 12 mai 2026
    /// sur la brique 2.13.e).
    ///
    /// Dossiers scannes :
    ///   - Assets/_Nymora/Art/Sprites/Soulrender/Marks/
    ///   - Assets/_Nymora/Art/Sprites/Soulrender/Terrains/
    ///   - Assets/_Nymora/Art/VFX/Soulrender/
    ///
    /// Reutilisable : ajouter d'autres dossiers dans SheetFolders pour Nightseer/Colossar/etc.
    /// Idempotent : re-run sans risque (regenere les SpriteRects).
    /// </summary>
    public static class AutoSliceFrameSheetsTool
    {
        private static readonly string[] SheetFolders =
        {
            "Assets/_Nymora/Art/Sprites/Soulrender/Marks",
            "Assets/_Nymora/Art/Sprites/Soulrender/Terrains",
            "Assets/_Nymora/Art/VFX/Soulrender",
            "Assets/_Nymora/Art/Sprites/Nightseer/Tiles",
            // 3.3.d polish : ajout des dossiers Nightseer Marks + Colossar VFX/Marques (assets .gif).
            "Assets/_Nymora/Art/Sprites/Nightseer/Marks",
            "Assets/_Nymora/Art/Sprites/Colossar/VFX",
            "Assets/_Nymora/Art/Sprites/Colossar/Marques",
            // 3.5.a.iii : dossiers Necram livres par designer (marque venin / tile zone putride / VFX signature).
            "Assets/_Nymora/Art/Sprites/Necram/Marques",
            "Assets/_Nymora/Art/Sprites/Necram/Tiles",
            "Assets/_Nymora/Art/Sprites/Necram/VFX",
        };

        // Match .png ET .gif (designer Nightseer/Colossar livre des GIFs aplats horizontaux).
        private static readonly Regex FramePattern = new Regex(@"_(\d+)frame\.(png|gif)$", RegexOptions.IgnoreCase);

        [MenuItem("Nymora/Setup/Auto-slice Frame Sheets")]
        public static void Run()
        {
            int processed = 0;
            int skipped = 0;

            foreach (var folder in SheetFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Debug.Log($"[Nymora.AutoSlice] Skip (folder absent) : {folder}");
                    continue;
                }

                string[] guids = AssetDatabase.FindAssets("t:texture2D", new[] { folder });
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var match = FramePattern.Match(Path.GetFileName(path));
                    if (!match.Success)
                    {
                        skipped++;
                        continue;
                    }

                    int frameCount = int.Parse(match.Groups[1].Value);
                    if (SliceOne(path, frameCount))
                    {
                        processed++;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Nymora.AutoSlice] Termine : {processed} sheets slicees, {skipped} fichiers ignores (pattern non match).");
        }

        private static bool SliceOne(string path, int frameCount)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[Nymora.AutoSlice] Pas un TextureImporter : {path}");
                return false;
            }

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogWarning($"[Nymora.AutoSlice] Impossible de charger la texture : {path}");
                return false;
            }

            int sheetWidth = tex.width;
            int sheetHeight = tex.height;

            if (sheetWidth % frameCount != 0)
            {
                Debug.LogWarning($"[Nymora.AutoSlice] {path} : largeur {sheetWidth} non divisible par {frameCount} frames -> skip");
                return false;
            }

            int frameWidth = sheetWidth / frameCount;
            int frameHeight = sheetHeight;
            string baseName = Path.GetFileNameWithoutExtension(path);

            // Force Multiple si pas deja fait (le postprocessor le fait deja mais belt-and-suspenders).
            if (importer.spriteImportMode != SpriteImportMode.Multiple)
            {
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.SaveAndReimport();
            }

            // API moderne : SpriteDataProviderFactories. L'ancien chemin TextureImporter.spritesheet
            // est deprecate dans Unity 2022+ et n'ecrit plus rien en pratique (les serialized
            // sprites restent vides).
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
            if (dataProvider == null)
            {
                Debug.LogError($"[Nymora.AutoSlice] SpriteDataProvider null pour {path} — verifie que le package com.unity.2d.sprite est present.");
                return false;
            }
            dataProvider.InitSpriteEditorDataProvider();

            var rects = new SpriteRect[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                rects[i] = new SpriteRect
                {
                    name = $"{baseName}_{i}",
                    rect = new Rect(i * frameWidth, 0, frameWidth, frameHeight),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    border = Vector4.zero,
                    spriteID = GUID.Generate(),
                };
            }
            dataProvider.SetSpriteRects(rects);

            // ISpriteNameFileIdDataProvider : table name -> fileId requise depuis Unity 2021.2
            // pour conserver les references inter-asset stables. Sans ca, les Sprite[] reference
            // par TerrainSpriteLibrary risquent de se decabler au prochain import.
            var nameFileIdProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameFileIdProvider != null)
            {
                var pairs = new List<SpriteNameFileIdPair>(frameCount);
                for (int i = 0; i < frameCount; i++)
                {
                    pairs.Add(new SpriteNameFileIdPair(rects[i].name, rects[i].spriteID));
                }
                nameFileIdProvider.SetNameFileIdPairs(pairs);
            }

            dataProvider.Apply();
            importer.SaveAndReimport();

            Debug.Log($"[Nymora.AutoSlice] {baseName} : {frameCount} frames ({frameWidth}x{frameHeight}) sliced");
            return true;
        }
    }
}
