using System.IO;
using Nymora.Combat.View.Obstacles;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// 3.1 / 3.1.bis — Editor tool : genere les prefabs Obstacle pour la View (Pilier + Mur).
    ///
    /// Strategie sprite (priorite descendante) :
    ///   1. Si <c>tiles_fondation.png</c> livre par le designer existe (3.1.bis Colossar) :
    ///      utilise comme sprite des 2 prefabs (Pilier + Mur) — Bible V7.1 dit "cube de pierre"
    ///      pour les deux. Le designer pourra livrer un sprite Mur dedie plus tard, on
    ///      branchera ici.
    ///   2. Sinon : genere un sprite procedural placeholder (carre pierre #7A6B5C 64x64) —
    ///      utile pour les premiers tests avant la livraison designer.
    ///
    /// Outputs systematiques :
    ///   Assets/_Nymora/Prefabs/Combat/Obstacles/Obstacle_Pillar.prefab
    ///   Assets/_Nymora/Prefabs/Combat/Obstacles/Obstacle_Wall.prefab
    ///   (Si placeholder genere : Assets/_Nymora/Art/Sprites/Obstacles/Placeholder_Pillar.png)
    ///
    /// Usage : Menu Nymora > Setup > Create Obstacle Prefabs
    /// Idempotent : si les assets existent deja, ecrases.
    /// </summary>
    public static class CreateObstaclePrefabTool
    {
        // ====================================================================
        // Chemins des assets (entree + sortie).
        // ====================================================================

        // Sprite designer livre en 3.1.bis (priorite 1).
        private const string ColossarTilesFondationPath = "Assets/_Nymora/Art/Sprites/Colossar/Tiles/tiles_fondation.png";

        // Sprite procedural fallback (priorite 2, genere si pas de tiles_fondation).
        private const string PlaceholderSpriteFolder = "Assets/_Nymora/Art/Sprites/Obstacles";
        private const string PlaceholderSpritePath = PlaceholderSpriteFolder + "/Placeholder_Pillar.png";

        // Prefabs cibles.
        private const string PrefabFolder = "Assets/_Nymora/Prefabs/Combat/Obstacles";
        private const string PillarPrefabPath = PrefabFolder + "/Obstacle_Pillar.prefab";
        private const string WallPrefabPath   = PrefabFolder + "/Obstacle_Wall.prefab";

        // Conventions PPU (1 case = 1 unite world Unity) :
        //   - tiles_fondation.png : 128x128 px -> PPU 180 (sprite occupe ~0.71 unite world,
        //     correspond au visuel d'une case iso pleine avec un debord vertical leger pour
        //     l'effet "cube qui pose dessus"). Tuning Lorenzo 3.1.bis :
        //       PPU 128 = 2x trop grand (le cube debordait sur cases voisines)
        //       PPU 256 = trop petit (le cube paraissait flotter au milieu de la case)
        //       PPU 180 = sweet spot (moyenne geometrique)
        //   - Placeholder procedural : 64x64 px -> PPU 64 (meme convention que tiles grille)
        private const int DesignerSpritePPU = 180;
        private const int PlaceholderSpritePPU = 64;

        // Sprite procedural placeholder.
        private const int PlaceholderSize = 64;
        private const int PlaceholderBorderPx = 4;

        // ====================================================================
        // Entree.
        // ====================================================================

        [MenuItem("Nymora/Setup/Create Obstacle Prefabs")]
        public static void Run()
        {
            EnsureFolderRecursive(PrefabFolder);

            // 1. Resoud le sprite a utiliser : designer-livre en priorite, sinon placeholder genere.
            Sprite sprite = ResolveOrCreateObstacleSprite();
            if (sprite == null)
            {
                Debug.LogError("[CreateObstaclePrefabTool] Echec resolution sprite — aucun prefab cree.");
                return;
            }

            // 2. Genere les 2 prefabs (Pilier + Mur). Pour l'instant meme sprite — le Mur
            // aura son propre sprite quand le designer le livrera.
            CreateObstaclePrefab(PillarPrefabPath, "Obstacle_Pillar", sprite, defaultHpLabel: "200/200");
            CreateObstaclePrefab(WallPrefabPath,   "Obstacle_Wall",   sprite, defaultHpLabel: "150/150");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PillarPrefabPath);
            Debug.Log($"[CreateObstaclePrefabTool] DONE. Pilier : {PillarPrefabPath} | Mur : {WallPrefabPath}");
        }

        // ====================================================================
        // Resolution sprite.
        // ====================================================================

        private static Sprite ResolveOrCreateObstacleSprite()
        {
            // Priorite 1 : sprite designer livre.
            if (File.Exists(ColossarTilesFondationPath))
            {
                Debug.Log($"[CreateObstaclePrefabTool] Sprite designer detecte : {ColossarTilesFondationPath}");
                ApplySpriteImportSettings(ColossarTilesFondationPath, DesignerSpritePPU);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ColossarTilesFondationPath);
                if (sprite != null) return sprite;
                Debug.LogWarning($"[CreateObstaclePrefabTool] {ColossarTilesFondationPath} existe mais le Sprite n'a pas pu etre charge — fallback placeholder.");
            }

            // Priorite 2 : genere un placeholder procedural.
            EnsureFolderRecursive(PlaceholderSpriteFolder);
            CreatePlaceholderSprite();
            ApplySpriteImportSettings(PlaceholderSpritePath, PlaceholderSpritePPU);
            return AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderSpritePath);
        }

        /// <summary>
        /// Force les import settings standard pour un sprite obstacle :
        /// Sprite type / PPU passe en arg / Point filter / alpha is transparency / no mipmap /
        /// pivot Center (3.1.bis Option A : sprite iso losange dessine centre dans le canvas
        /// 128x128 — pivot Center fait que le centre du losange tombe sur la case grille).
        ///
        /// Tout passe par TextureImporterSettings (read/modify/write en 1 fois) — ne PAS
        /// melanger avec des modifs directes sur l'importer (importer.textureType = X), sinon
        /// SetTextureSettings ecrase les modifs directes faites juste avant.
        /// </summary>
        private static void ApplySpriteImportSettings(string spritePath, int ppu)
        {
            var importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
            if (importer == null) return;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            const int Center = (int)SpriteAlignment.Center;

            bool dirty = false;
            if (settings.textureType != TextureImporterType.Sprite) { settings.textureType = TextureImporterType.Sprite; dirty = true; }
            if (settings.spriteMode != (int)SpriteImportMode.Single) { settings.spriteMode = (int)SpriteImportMode.Single; dirty = true; }
            if (settings.spritePixelsPerUnit != ppu) { settings.spritePixelsPerUnit = ppu; dirty = true; }
            if (settings.filterMode != FilterMode.Point) { settings.filterMode = FilterMode.Point; dirty = true; }
            if (!settings.alphaIsTransparency) { settings.alphaIsTransparency = true; dirty = true; }
            if (settings.mipmapEnabled) { settings.mipmapEnabled = false; dirty = true; }
            if (settings.spriteAlignment != Center) { settings.spriteAlignment = Center; dirty = true; }

            if (dirty)
            {
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                Debug.Log($"[CreateObstaclePrefabTool] Import settings applique : {spritePath} (PPU={ppu}, Point, transparent, pivot=Center)");
            }
        }

        private static void CreatePlaceholderSprite()
        {
            var tex = new Texture2D(PlaceholderSize, PlaceholderSize, TextureFormat.RGBA32, false);
            // Couleur Bible V7.1 Colossar : pierre #7A6B5C (semi-transparent + bord noir).
            Color stone = new Color(0x7A / 255f, 0x6B / 255f, 0x5C / 255f, 0.85f);
            Color border = new Color(0.1f, 0.08f, 0.05f, 1f);

            for (int y = 0; y < PlaceholderSize; y++)
            {
                for (int x = 0; x < PlaceholderSize; x++)
                {
                    bool isBorder = x < PlaceholderBorderPx || x >= PlaceholderSize - PlaceholderBorderPx
                                 || y < PlaceholderBorderPx || y >= PlaceholderSize - PlaceholderBorderPx;
                    tex.SetPixel(x, y, isBorder ? border : stone);
                }
            }
            tex.Apply();

            byte[] png = tex.EncodeToPNG();
            File.WriteAllBytes(PlaceholderSpritePath, png);
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(PlaceholderSpritePath);
        }

        // ====================================================================
        // Generation prefab (paramebrable Pilier / Mur / etc.).
        // ====================================================================

        private static void CreateObstaclePrefab(string prefabPath, string rootName, Sprite sprite, string defaultHpLabel)
        {
            // Cleanup existant (idempotent).
            if (File.Exists(prefabPath))
            {
                AssetDatabase.DeleteAsset(prefabPath);
            }

            var root = new GameObject(rootName);
            try
            {
                // Sprite child (sortingOrder set par ObstacleView.UpdateData).
                // 3.1.bis Option A : scale uniforme 1 (pas d'etirement, le pixel art ne
                // supporte pas le stretching non-uniforme). Combine au pivot BottomCenter,
                // la dalle pose proprement au sol de la case grille. Visuel placeholder
                // = "fondation" du pilier ; le vrai pilier vertical (3-4 strates animees)
                // viendra en 3.3.b avec le VFX_strates_qui_sempilent du designer.
                var spriteGO = new GameObject("Sprite");
                spriteGO.transform.SetParent(root.transform, false);
                var sr = spriteGO.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;

                // HP label child (TMP world space, au-dessus du sprite).
                // Position y=0.55f : sprite tiles_fondation 128px PPU 180 + pivot center
                // = sprite va de y=-0.355 a y=+0.355 unite. Label a 0.55 reste juste au-dessus.
                var labelGO = new GameObject("HPLabel");
                labelGO.transform.SetParent(root.transform, false);
                labelGO.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                var tmp = labelGO.AddComponent<TextMeshPro>();
                tmp.text = defaultHpLabel;
                tmp.fontSize = 3;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = new Color(1f, 1f, 0.9f, 1f);
                tmp.sortingOrder = 1100; // au-dessus de tout (combattants ~700-990)
                var rect = labelGO.GetComponent<RectTransform>();
                if (rect != null) rect.sizeDelta = new Vector2(2f, 0.5f);

                // ObstacleView component sur le root (bind sprite + label).
                var view = root.AddComponent<ObstacleView>();
                var so = new SerializedObject(view);
                SetObjectRef(so, "_sprite", sr);
                SetObjectRef(so, "_hpLabel", tmp);
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[CreateObstaclePrefabTool] Prefab cree : {prefabPath} (sprite={sprite.name})");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void SetObjectRef(SerializedObject so, string propertyName, Object value)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"[CreateObstaclePrefabTool] Champ '{propertyName}' introuvable.");
                return;
            }
            prop.objectReferenceValue = value;
        }

        private static void EnsureFolderRecursive(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) return;
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolderRecursive(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
