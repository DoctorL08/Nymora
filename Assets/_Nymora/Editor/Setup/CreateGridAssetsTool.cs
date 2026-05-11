using System.IO;
using Nymora.Combat.Grid;
using Nymora.Combat.View;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Outil one-shot Phase 2.1 : genere les assets necessaires a la grille iso.
    /// - Sprite losange placeholder (64x32 px, PPU 64)
    /// - Prefab TileView avec SpriteRenderer pre-cable
    /// - SO GridSettings (cree uniquement s'il n'existe pas)
    ///
    /// Menu : Nymora > Setup > Create Grid Assets
    /// Idempotent : peut etre relance, ecrase la texture placeholder mais
    /// preserve le GridSettings.asset existant pour ne pas perdre les tunings.
    /// </summary>
    public static class CreateGridAssetsTool
    {
        private const string ArtFolder = "Assets/_Nymora/Art/Sprites";
        private const string PrefabFolder = "Assets/_Nymora/Prefabs/Combat";
        private const string SettingsFolder = "Assets/_Nymora/Settings";

        private const string SpritePath = ArtFolder + "/TilePlaceholder.png";
        private const string PrefabPath = PrefabFolder + "/TileView.prefab";
        private const string SettingsPath = SettingsFolder + "/GridSettings.asset";

        private const int SpriteWidth = 64;
        private const int SpriteHeight = 32;

        [MenuItem("Nymora/Setup/Create Grid Assets")]
        public static void Run()
        {
            EnsureFolder(ArtFolder);
            EnsureFolder(PrefabFolder);
            EnsureFolder(SettingsFolder);

            GenerateDiamondSprite();
            var settings = EnsureSettings();
            CreatePrefab(settings);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Nymora.CreateGridAssets] OK\n  - {SpritePath}\n  - {PrefabPath}\n  - {SettingsPath}");
            EditorUtility.DisplayDialog(
                "Create Grid Assets",
                $"Assets generes/mis a jour :\n\n" +
                $"- {SpritePath}\n" +
                $"- {PrefabPath}\n" +
                $"- {SettingsPath}\n\n" +
                "Tu peux maintenant ouvrir QuantumGameScene et ajouter un GameObject avec GridRenderer.",
                "OK");
        }

        private static void EnsureFolder(string assetsRelativePath)
        {
            if (AssetDatabase.IsValidFolder(assetsRelativePath)) return;
            var parent = Path.GetDirectoryName(assetsRelativePath)?.Replace('\\', '/');
            var leaf = Path.GetFileName(assetsRelativePath);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void GenerateDiamondSprite()
        {
            var tex = new Texture2D(SpriteWidth, SpriteHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            // Couleurs : losange clair avec bordure plus sombre
            Color clear = new Color(0f, 0f, 0f, 0f);
            Color fill = new Color(1f, 1f, 1f, 1f); // blanc — le TileView appliquera la couleur via SpriteRenderer.color
            Color border = new Color(0.4f, 0.4f, 0.4f, 1f);

            // Initialise transparent
            var pixels = new Color[SpriteWidth * SpriteHeight];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

            // Trace le losange : pour chaque ligne y, calcule la largeur du losange
            // Losange centre, demi-largeur = (h/2 - |y - h/2|) * (w/h)
            float halfW = SpriteWidth * 0.5f;
            float halfH = SpriteHeight * 0.5f;

            for (int y = 0; y < SpriteHeight; y++)
            {
                // Distance verticale depuis le centre (0 au milieu, halfH au bord)
                float dy = Mathf.Abs(y + 0.5f - halfH);
                // Demi-largeur du losange a cette hauteur (forme triangle iso)
                float rowHalfW = (halfH - dy) * (halfW / halfH);
                int xMin = Mathf.RoundToInt(halfW - rowHalfW);
                int xMax = Mathf.RoundToInt(halfW + rowHalfW) - 1;
                if (xMax < xMin) continue;

                for (int x = xMin; x <= xMax; x++)
                {
                    if (x < 0 || x >= SpriteWidth) continue;
                    bool isBorder = (x == xMin || x == xMax);
                    pixels[y * SpriteWidth + x] = isBorder ? border : fill;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            // Sauvegarde PNG
            byte[] png = tex.EncodeToPNG();
            File.WriteAllBytes(SpritePath, png);
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceUpdate);

            // Configure l'import : Sprite single, PPU 64, pivot center, no compression
            var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 64f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;

            var ps = importer.spritesheet; // pas necessaire en single mais on s'assure
            importer.SaveAndReimport();
        }

        private static GridSettings EnsureSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GridSettings>(SettingsPath);
            if (existing != null) return existing;

            var settings = ScriptableObject.CreateInstance<GridSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        private static void CreatePrefab(GridSettings settings)
        {
            // Charge le sprite genere
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            if (sprite == null)
            {
                Debug.LogError($"[Nymora.CreateGridAssets] Sprite introuvable a {SpritePath} apres generation.");
                return;
            }

            // Detruit l'ancien prefab si present pour eviter les drifts de structure
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(PrefabPath);
            }

            var root = new GameObject("TileView");
            var sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = Color.white;
            sr.sortingLayerName = string.IsNullOrEmpty(settings.SortingLayer) ? "Default" : settings.SortingLayer;
            sr.sortingOrder = 0;

            var view = root.AddComponent<TileView>();
            // Cable la ref SpriteRenderer via SerializedObject pour eviter de la perdre
            var so = new SerializedObject(view);
            var spriteProp = so.FindProperty("_sprite");
            if (spriteProp != null)
            {
                spriteProp.objectReferenceValue = sr;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
        }
    }
}
