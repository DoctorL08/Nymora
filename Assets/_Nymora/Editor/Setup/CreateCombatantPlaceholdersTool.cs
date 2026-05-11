using System.IO;
using Nymora.Combat.View;
using Quantum;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Genere les 5 sprites placeholder + 5 prefabs CombatantView (1 par classe Bible V7.1).
    /// Sprites = 128x128 (PPU 64, ratio compatible avec le format final pixel art).
    /// Pivot center pour la 2.2 — sera ajuste a bottom-center sur le .meta quand
    /// les vrais sprites arriveront (sans toucher aux prefabs ni a la scene).
    ///
    /// Menu : Nymora > Setup > Create Combatant Placeholders
    /// Idempotent : peut etre relance, ecrase les sprites et prefabs placeholder.
    /// </summary>
    public static class CreateCombatantPlaceholdersTool
    {
        private const string ArtFolder = "Assets/_Nymora/Art/Sprites/Combatants";
        private const string PrefabFolder = "Assets/_Nymora/Prefabs/Combat/Combatants";

        // Convention Phase 2 :
        //   - Tiles  : 64x64  PPU 64  -> 1x1 unite world = 1 case
        //   - Persos : 128x128 PPU 128 -> 1x1 unite world = 1 case
        // Chaque categorie de sprite a son PPU adapte a sa resolution. Quand le vrai
        // sprite final arrivera (128x128 perso debout), aucun changement sur les prefabs/scene.
        private const int SpriteSize = 128;
        private const float PixelsPerUnit = 128f;

        // Couleurs accent Bible V7.1 (cf. STATUT_ACTUEL : decision 8 mai 2026).
        private static readonly (NymoraClass cls, string hex)[] Palette = new[]
        {
            (NymoraClass.Soulrender, "#B22222"),
            (NymoraClass.Nightseer,  "#6A4FB6"),
            (NymoraClass.Colossar,   "#7A6B5C"),
            (NymoraClass.Necram,     "#5A8B3E"),
            (NymoraClass.Ghostra,    "#6F8FA8"),
        };

        [MenuItem("Nymora/Setup/Create Combatant Placeholders")]
        public static void Run()
        {
            EnsureFolder(ArtFolder);
            EnsureFolder(PrefabFolder);

            foreach (var (cls, hex) in Palette)
            {
                Color accent = ParseHex(hex);
                string spritePath = $"{ArtFolder}/{cls}_Placeholder.png";
                string prefabPath = $"{PrefabFolder}/Combatant_{cls}.prefab";

                GeneratePawnSprite(spritePath, accent);
                CreatePrefab(prefabPath, spritePath, cls);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Nymora.CreateCombatantPlaceholders] OK — 5 sprites + 5 prefabs sous {ArtFolder} et {PrefabFolder}.");
            EditorUtility.DisplayDialog(
                "Create Combatant Placeholders",
                $"5 sprites + 5 prefabs generes :\n\n{ArtFolder}/<Class>_Placeholder.png\n{PrefabFolder}/Combatant_<Class>.prefab\n\n" +
                "Ouvre QuantumGameScene, ajoute un GameObject avec CombatantRenderer et drag les 5 prefabs dans les slots.",
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

        private static Color ParseHex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out var c)) return c;
            return Color.magenta;
        }

        private static void GeneratePawnSprite(string path, Color accent)
        {
            var tex = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color border = new Color(accent.r * 0.4f, accent.g * 0.4f, accent.b * 0.4f, 1f);

            float centerX = SpriteSize * 0.5f;
            float centerY = SpriteSize * 0.5f;
            // Rond placeholder qui occupe presque tout le sprite (~115 px de diametre
            // dans un sprite 128x128). Avec PPU 128, ca donne ~1 unite world = 1 case,
            // bonne taille pour un perso sur la grille iso 1x0.5. Le vrai sprite final
            // utilisera aussi tout le 128x128 — aucune modif des prefabs ne sera necessaire.
            float radius = SpriteSize * 0.45f;
            float borderThickness = 3f;

            var pixels = new Color[SpriteSize * SpriteSize];
            for (int y = 0; y < SpriteSize; y++)
            {
                for (int x = 0; x < SpriteSize; x++)
                {
                    float dx = x + 0.5f - centerX;
                    float dy = y + 0.5f - centerY;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    int idx = y * SpriteSize + x;
                    if (dist > radius)
                    {
                        pixels[idx] = clear;
                    }
                    else if (dist > radius - borderThickness)
                    {
                        pixels[idx] = border;
                    }
                    else
                    {
                        pixels[idx] = accent;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            // Pivot center pour la 2.2 (placeholder rond centre).
            // Quand le vrai sprite arrivera (perso debout), changer en (0.5, 0.25) ou (0.5, 0)
            // via le .meta pour ancrer les pieds sur la case sans toucher au prefab.
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.SaveAndReimport();
        }

        private static void CreatePrefab(string prefabPath, string spritePath, NymoraClass cls)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null)
            {
                Debug.LogError($"[Nymora.CreateCombatantPlaceholders] Sprite introuvable a {spritePath}");
                return;
            }

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing != null) AssetDatabase.DeleteAsset(prefabPath);

            var root = new GameObject($"Combatant_{cls}");
            var sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = Color.white;
            sr.sortingLayerName = "Default";
            sr.sortingOrder = 100;

            var view = root.AddComponent<CombatantView>();

            // Cable la ref SpriteRenderer dans CombatantView via SerializedObject.
            var so = new SerializedObject(view);
            var spriteProp = so.FindProperty("_sprite");
            if (spriteProp != null)
            {
                spriteProp.objectReferenceValue = sr;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
        }
    }
}
