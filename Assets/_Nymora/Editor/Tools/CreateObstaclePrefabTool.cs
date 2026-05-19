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

        // 4 frames Pilier livrees par le designer (18 mai, re-livraison) : pilier qui se
        // degrade progressivement avec fissures + cristal dore plus visible a chaque frame.
        // Anime cote View pilote par le ratio HP/MaxHP (cf ObstacleView._hpFrames).
        private static readonly string[] ColossarPillarFramePaths =
        {
            "Assets/_Nymora/Art/Sprites/Colossar/Tiles/tiles_pilier_colossar_4frames1.png",
            "Assets/_Nymora/Art/Sprites/Colossar/Tiles/tiles_pilier_colossar_4frames2.png",
            "Assets/_Nymora/Art/Sprites/Colossar/Tiles/tiles_pilier_colossar_4frames3.png",
            "Assets/_Nymora/Art/Sprites/Colossar/Tiles/tiles_pilier_colossar_4frames4.png",
        };
        // PPU + pivot custom dedies aux 4 frames Pilier (tuning Lorenzo 18 mai en jeu).
        // Frames 128x128 px, contenu calibre par le designer mais shrink necessaire pour
        // bien caler le pilier sur la case iso (losange) :
        //   - PPU 76 (au lieu de 128) = sprite occupe ~1.68 unites world (vertical eleve)
        //   - Pivot Custom (0.5, 0.21) = base du pilier alignee sur le bas du losange tile
        // Determined visually par Lorenzo en Play Mode, validation OK.
        private const int PillarFramePPU = 76;
        private static readonly Vector2 PillarFramePivot = new Vector2(0.5f, 0.21f);

        // Sprite procedural fallback (priorite 2, genere si pas de tiles_fondation).
        private const string PlaceholderSpriteFolder = "Assets/_Nymora/Art/Sprites/Obstacles";
        private const string PlaceholderSpritePath = PlaceholderSpriteFolder + "/Placeholder_Pillar.png";

        // Prefabs cibles.
        private const string PrefabFolder = "Assets/_Nymora/Prefabs/Combat/Obstacles";
        private const string PillarPrefabPath = PrefabFolder + "/Obstacle_Pillar.prefab";
        private const string WallPrefabPath   = PrefabFolder + "/Obstacle_Wall.prefab";

        // Conventions PPU (1 case = 1 unite world Unity) :
        //   - tiles_fondation.png : 128x128 px -> PPU 180 (shrink, sprite occupe ~0.71 unite
        //     world). Sert au Mur uniquement (le Pilier a son propre asset anime depuis le
        //     re-livraison designer, cf tiles_pilier_colossar_4frames*.png).
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

            // 1. Resoud le sprite "base" (Wall + fallback) : tiles_fondation si livre, sinon placeholder.
            Sprite baseSprite = ResolveOrCreateObstacleSprite();
            if (baseSprite == null)
            {
                Debug.LogError("[CreateObstaclePrefabTool] Echec resolution sprite — aucun prefab cree.");
                return;
            }

            // 2. Resoud les 4 frames Pilier (re-livraison designer 18 mai). Fallback baseSprite
            // si les frames manquent (devrait pas arriver, mais on degrade proprement).
            Sprite[] pillarFrames = ResolvePillarFrames();
            Sprite pillarBaseSprite = (pillarFrames != null && pillarFrames.Length > 0 && pillarFrames[0] != null)
                ? pillarFrames[0]
                : baseSprite;

            // 3. Genere les 2 prefabs. Pillar : 4 frames pilotees par HP. Wall : sprite statique.
            CreateObstaclePrefab(PillarPrefabPath, "Obstacle_Pillar", pillarBaseSprite, pillarFrames, defaultHpLabel: "200/200");
            CreateObstaclePrefab(WallPrefabPath,   "Obstacle_Wall",   baseSprite,       hpFrames: null, defaultHpLabel: "150/150");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PillarPrefabPath);
            Debug.Log($"[CreateObstaclePrefabTool] DONE. Pilier : {PillarPrefabPath} ({(pillarFrames?.Length ?? 0)} frames) | Mur : {WallPrefabPath}");
        }

        // ====================================================================
        // Resolution frames pilier (re-livraison designer 18 mai).
        // ====================================================================

        private static Sprite[] ResolvePillarFrames()
        {
            var frames = new Sprite[ColossarPillarFramePaths.Length];
            int loaded = 0;
            for (int i = 0; i < ColossarPillarFramePaths.Length; i++)
            {
                string path = ColossarPillarFramePaths[i];
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"[CreateObstaclePrefabTool] Frame pilier manquante : {path}");
                    frames[i] = null;
                    continue;
                }
                ApplySpriteImportSettings(path, PillarFramePPU, PillarFramePivot);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    Debug.LogWarning($"[CreateObstaclePrefabTool] Frame pilier impossible a charger : {path}");
                }
                else
                {
                    loaded++;
                }
                frames[i] = sprite;
            }
            Debug.Log($"[CreateObstaclePrefabTool] Frames Pilier resolues : {loaded}/{ColossarPillarFramePaths.Length}");
            return loaded > 0 ? frames : null;
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
        /// Sprite type / PPU passe en arg / Point filter / alpha is transparency / no mipmap.
        /// Pivot : Center par defaut, ou Custom si <paramref name="customPivot"/> fourni
        /// (pivot precis = Vector2 en coordonnees normalisees 0..1).
        ///
        /// Tout passe par TextureImporterSettings (read/modify/write en 1 fois) — ne PAS
        /// melanger avec des modifs directes sur l'importer (importer.textureType = X), sinon
        /// SetTextureSettings ecrase les modifs directes faites juste avant.
        /// </summary>
        private static void ApplySpriteImportSettings(string spritePath, int ppu, Vector2? customPivot = null)
        {
            var importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
            if (importer == null) return;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            const int Center = (int)SpriteAlignment.Center;
            const int Custom = (int)SpriteAlignment.Custom;

            int targetAlignment = customPivot.HasValue ? Custom : Center;

            bool dirty = false;
            if (settings.textureType != TextureImporterType.Sprite) { settings.textureType = TextureImporterType.Sprite; dirty = true; }
            if (settings.spriteMode != (int)SpriteImportMode.Single) { settings.spriteMode = (int)SpriteImportMode.Single; dirty = true; }
            if (settings.spritePixelsPerUnit != ppu) { settings.spritePixelsPerUnit = ppu; dirty = true; }
            if (settings.filterMode != FilterMode.Point) { settings.filterMode = FilterMode.Point; dirty = true; }
            if (!settings.alphaIsTransparency) { settings.alphaIsTransparency = true; dirty = true; }
            if (settings.mipmapEnabled) { settings.mipmapEnabled = false; dirty = true; }
            if (settings.spriteAlignment != targetAlignment) { settings.spriteAlignment = targetAlignment; dirty = true; }
            if (customPivot.HasValue && settings.spritePivot != customPivot.Value) { settings.spritePivot = customPivot.Value; dirty = true; }

            if (dirty)
            {
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                string pivotLog = customPivot.HasValue ? $"Custom({customPivot.Value.x},{customPivot.Value.y})" : "Center";
                Debug.Log($"[CreateObstaclePrefabTool] Import settings applique : {spritePath} (PPU={ppu}, Point, transparent, pivot={pivotLog})");
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

        private static void CreateObstaclePrefab(string prefabPath, string rootName, Sprite sprite, Sprite[] hpFrames, string defaultHpLabel)
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
                // Position y=0.55f : sprite centre sur la case (pivot Center), label
                // juste au-dessus du sprite.
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

                // ObstacleView component sur le root (bind sprite + label + frames HP).
                var view = root.AddComponent<ObstacleView>();
                var so = new SerializedObject(view);
                SetObjectRef(so, "_sprite", sr);
                SetObjectRef(so, "_hpLabel", tmp);
                SetSpriteArray(so, "_hpFrames", hpFrames);
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                int frameCount = (hpFrames == null) ? 0 : hpFrames.Length;
                Debug.Log($"[CreateObstaclePrefabTool] Prefab cree : {prefabPath} (sprite={sprite.name}, hpFrames={frameCount})");
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

        private static void SetSpriteArray(SerializedObject so, string propertyName, Sprite[] sprites)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"[CreateObstaclePrefabTool] Champ '{propertyName}' introuvable.");
                return;
            }
            int count = sprites == null ? 0 : sprites.Length;
            prop.arraySize = count;
            for (int i = 0; i < count; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            }
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
