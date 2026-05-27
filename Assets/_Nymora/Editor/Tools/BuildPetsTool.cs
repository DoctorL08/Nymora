using System.Collections.Generic;
using System.IO;
using Nymora.Core.ScriptableObjects;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// Brique 5.10 (B2) — Importe + slice les sprite sheets des familiers, crée les PetDefinition
    /// et peuple le PetCatalog (Resources).
    ///
    /// Inputs (livrés par Kyami, copiés dans le projet) :
    ///   Assets/_Nymora/Art/Cosmetics/Pets/&lt;nom&gt;/&lt;nom&gt;_{idle,walk}_{NE,SE}.png
    ///   Sheets horizontaux de cellules 64×64 (idle 6 frames = 384px, walk 8 frames = 512px).
    ///
    /// Étapes par PNG :
    ///   1. TextureImporter en Sprite/Multiple, PPU 96 (échelle monde, comme les persos),
    ///      Point filter, non compressé, alphaIsTransparency.
    ///   2. Slice en (largeur/64) cellules, pivot BAS-CENTRE (le familier pose ses pieds sur la
    ///      case), via SpriteDataProviderFactories (l'API legacy spritesheet est ignorée en 2022+).
    ///   3. Charge les sprites dans l'ordre et peuple la PetDefinition (idle/walk SE+NE).
    ///   4. Crée/peuple le PetCatalog dans Resources.
    ///
    /// Idempotent. Menu : Nymora > Setup > Build Pets.
    /// </summary>
    public static class BuildPetsTool
    {
        private const int FrameSize = 64;
        private const float Ppu = 96f;
        private const string PetsArtDir = "Assets/_Nymora/Art/Cosmetics/Pets";
        private const string PetDefDir = "Assets/_Nymora/ScriptableObjects/Cosmetics/Pets";
        private const string CatalogDir = "Assets/_Nymora/Resources/Cosmetics";
        private const string CatalogPath = CatalogDir + "/PetCatalog.asset";

        // folder (lowercase) -> (cosmeticId, displayName). cosmeticId == id catalogue backend.
        private static readonly (string folder, string cosmeticId, string display)[] Pets =
        {
            ("cornecroc", "pet_cornecroc", "Cornecroc"),
            ("grumon",    "pet_grumon",    "Grumon"),
            ("lentille",  "pet_lentille",  "Lentille"),
            ("ossivore",  "pet_ossivore",  "Ossivore"),
            ("voilard",   "pet_voilard",   "Voilard"),
        };

        [MenuItem("Nymora/Setup/Build Pets", priority = 48)]
        public static void Run()
        {
            var actions = new List<string>();
            EnsureFolder("Assets/_Nymora/ScriptableObjects/Cosmetics");
            EnsureFolder(PetDefDir);
            EnsureFolder("Assets/_Nymora/Resources");
            EnsureFolder(CatalogDir);

            var defs = new List<PetDefinition>();
            foreach (var (folder, cosmeticId, display) in Pets)
            {
                string dir = $"{PetsArtDir}/{folder}";
                if (!AssetDatabase.IsValidFolder(dir))
                {
                    actions.Add($"⚠ dossier absent : {dir} — familier {display} ignoré");
                    continue;
                }

                var idleSE = SliceAndLoad($"{dir}/{folder}_idle_SE.png", actions);
                var idleNE = SliceAndLoad($"{dir}/{folder}_idle_NE.png", actions);
                var walkSE = SliceAndLoad($"{dir}/{folder}_walk_SE.png", actions);
                var walkNE = SliceAndLoad($"{dir}/{folder}_walk_NE.png", actions);

                string defPath = $"{PetDefDir}/{cosmeticId}.asset";
                var def = AssetDatabase.LoadAssetAtPath<PetDefinition>(defPath);
                bool created = def == null;
                if (created)
                {
                    def = ScriptableObject.CreateInstance<PetDefinition>();
                    AssetDatabase.CreateAsset(def, defPath);
                }
                def.CosmeticId = cosmeticId;
                def.DisplayName = display;
                def.IdleFrames = idleSE;
                def.IdleFramesNE = idleNE;
                def.WalkFrames = walkSE;
                def.WalkFramesNE = walkNE;
                EditorUtility.SetDirty(def);
                defs.Add(def);
                actions.Add($"{display} : idle {idleSE.Length}/{idleNE.Length} · walk {walkSE.Length}/{walkNE.Length} ({(created ? "créé" : "maj")})");
            }

            // Catalogue.
            var catalog = AssetDatabase.LoadAssetAtPath<PetCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<PetCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.Pets = defs.ToArray();
            EditorUtility.SetDirty(catalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[BuildPets] " + string.Join("\n", actions));
            Selection.activeObject = catalog;
            EditorUtility.DisplayDialog("Build Pets",
                string.Join("\n", actions) + $"\n\nPetCatalog : {defs.Count} familier(s).", "OK");
        }

        /// <summary>
        /// Configure l'import en Sprite/Multiple + slice en cellules 64px (pivot bas-centre),
        /// puis charge les sprites triés par index. Retourne [] si le fichier est absent.
        /// </summary>
        private static Sprite[] SliceAndLoad(string path, List<string> actions)
        {
            if (!File.Exists(path)) { actions.Add($"⚠ PNG absent : {path}"); return new Sprite[0]; }

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { actions.Add($"⚠ pas un TextureImporter : {path}"); return new Sprite[0]; }

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) { actions.Add($"⚠ texture illisible : {path}"); return new Sprite[0]; }

            int frameCount = tex.width / FrameSize;
            if (frameCount <= 0 || tex.width % FrameSize != 0)
            {
                actions.Add($"⚠ {Path.GetFileName(path)} : largeur {tex.width} non multiple de {FrameSize} — skip");
                return new Sprite[0];
            }

            // 1. Réglages d'import (pixel-art, échelle monde).
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = Ppu;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            // 2. Slice via SpriteDataProvider (API moderne ; legacy spritesheet ignoré en 2022+).
            int frameWidth = FrameSize;
            int frameHeight = tex.height;
            string baseName = Path.GetFileNameWithoutExtension(path);

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
            if (dataProvider == null)
            {
                actions.Add($"⚠ SpriteDataProvider null : {path}");
                return new Sprite[0];
            }
            dataProvider.InitSpriteEditorDataProvider();

            var rects = new SpriteRect[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                rects[i] = new SpriteRect
                {
                    name = $"{baseName}_{i}",
                    rect = new Rect(i * frameWidth, 0, frameWidth, frameHeight),
                    alignment = SpriteAlignment.BottomCenter, // pieds sur la case
                    pivot = new Vector2(0.5f, 0f),
                    border = Vector4.zero,
                    spriteID = GUID.Generate(),
                };
            }
            dataProvider.SetSpriteRects(rects);

            var nameFileIdProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameFileIdProvider != null)
            {
                var pairs = new List<SpriteNameFileIdPair>(frameCount);
                for (int i = 0; i < frameCount; i++)
                    pairs.Add(new SpriteNameFileIdPair(rects[i].name, rects[i].spriteID));
                nameFileIdProvider.SetNameFileIdPairs(pairs);
            }

            dataProvider.Apply();
            importer.SaveAndReimport();

            // 3. Charge les sprites triés par index de frame.
            var sprites = new List<Sprite>();
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
                if (a is Sprite sp) sprites.Add(sp);
            sprites.Sort((x, y) => FrameIndex(x.name).CompareTo(FrameIndex(y.name)));
            return sprites.ToArray();
        }

        private static int FrameIndex(string spriteName)
        {
            int us = spriteName.LastIndexOf('_');
            if (us >= 0 && int.TryParse(spriteName.Substring(us + 1), out int idx)) return idx;
            return 0;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            string parent = folder.Substring(0, slash);
            string name = folder.Substring(slash + 1);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
