using System.IO;
using Nymora.Combat.View.Obstacles;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Tools
{
    /// <summary>
    /// 3.1 — Editor tool : genere les prefabs placeholder Obstacle pour la View.
    /// Pour l'instant : Pilier (carre gris semi-transparent 64x64 + label HP TMP au-dessus).
    /// Wall et autres viennent en 3.3.b avec les sprites finaux du designer.
    ///
    /// Outputs :
    ///   Assets/_Nymora/Art/Sprites/Obstacles/Placeholder_Pillar.png (sprite procedural genere)
    ///   Assets/_Nymora/Prefabs/Combat/Obstacles/Obstacle_Pillar.prefab
    ///
    /// Usage : Menu Nymora > Setup > Create Obstacle Prefabs
    /// Idempotent : si les assets existent deja, ecrases.
    /// </summary>
    public static class CreateObstaclePrefabTool
    {
        private const string SpriteFolder = "Assets/_Nymora/Art/Sprites/Obstacles";
        private const string PrefabFolder = "Assets/_Nymora/Prefabs/Combat/Obstacles";

        private const string PillarSpritePath = SpriteFolder + "/Placeholder_Pillar.png";
        private const string PillarPrefabPath = PrefabFolder + "/Obstacle_Pillar.prefab";

        // Sprite procedural : 64x64 carre gris pierre semi-transparent avec bord noir.
        private const int SpriteSize = 64;
        private const int BorderPx = 4;

        [MenuItem("Nymora/Setup/Create Obstacle Prefabs")]
        public static void Run()
        {
            EnsureFolderRecursive(SpriteFolder);
            EnsureFolderRecursive(PrefabFolder);

            // 1. Genere le sprite Pilier (carre gris bord noir).
            CreatePillarSprite();

            // 2. Importe le sprite avec PPU 64 (1 case = 1 unite world).
            var importer = AssetImporter.GetAtPath(PillarSpritePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 64;
                importer.filterMode = FilterMode.Point;          // pixel art crisp
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PillarSpritePath);
            if (sprite == null)
            {
                Debug.LogError($"[CreateObstaclePrefabTool] Sprite introuvable apres import : {PillarSpritePath}");
                return;
            }

            // 3. Cree le prefab Pilier (root + sprite child + label TMP child).
            CreatePillarPrefab(sprite);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PillarPrefabPath);
            Debug.Log($"[CreateObstaclePrefabTool] DONE. Prefab : {PillarPrefabPath}");
        }

        private static void CreatePillarSprite()
        {
            var tex = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false);
            // Couleur Bible V7.1 Colossar : pierre #7A6B5C
            Color stone = new Color(0x7A / 255f, 0x6B / 255f, 0x5C / 255f, 0.85f);
            Color border = new Color(0.1f, 0.08f, 0.05f, 1f);

            for (int y = 0; y < SpriteSize; y++)
            {
                for (int x = 0; x < SpriteSize; x++)
                {
                    bool isBorder = x < BorderPx || x >= SpriteSize - BorderPx
                                 || y < BorderPx || y >= SpriteSize - BorderPx;
                    tex.SetPixel(x, y, isBorder ? border : stone);
                }
            }
            tex.Apply();

            byte[] png = tex.EncodeToPNG();
            File.WriteAllBytes(PillarSpritePath, png);
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(PillarSpritePath);
        }

        private static void CreatePillarPrefab(Sprite sprite)
        {
            // Cleanup existant (idempotent).
            if (File.Exists(PillarPrefabPath))
            {
                AssetDatabase.DeleteAsset(PillarPrefabPath);
            }

            var root = new GameObject("Obstacle_Pillar");
            try
            {
                // Sprite child (sortingOrder set par ObstacleView.UpdateData).
                var spriteGO = new GameObject("Sprite");
                spriteGO.transform.SetParent(root.transform, false);
                var sr = spriteGO.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;

                // HP label child (TMP world space, au-dessus du sprite).
                var labelGO = new GameObject("HPLabel");
                labelGO.transform.SetParent(root.transform, false);
                labelGO.transform.localPosition = new Vector3(0f, 0.55f, 0f); // au-dessus
                var tmp = labelGO.AddComponent<TextMeshPro>();
                tmp.text = "200/200";
                tmp.fontSize = 3;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = new Color(1f, 1f, 0.9f, 1f);
                tmp.sortingOrder = 1100; // au-dessus de tout (combattants ~700-990, sprites obstacles ~700-990)
                // Taille rect (sinon par defaut le RectTransform genere par TMP est trop grand)
                var rect = labelGO.GetComponent<RectTransform>();
                if (rect != null) rect.sizeDelta = new Vector2(2f, 0.5f);

                // ObstacleView component sur le root (bind sprite + label).
                var view = root.AddComponent<ObstacleView>();
                var so = new SerializedObject(view);
                SetObjectRef(so, "_sprite", sr);
                SetObjectRef(so, "_hpLabel", tmp);
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PillarPrefabPath);
                Debug.Log($"[CreateObstaclePrefabTool] Prefab cree : {PillarPrefabPath}");
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
