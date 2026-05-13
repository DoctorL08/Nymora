using System.Collections.Generic;
using System.IO;
using Nymora.Combat.View;
using Nymora.Combat.View.Animation;
using Quantum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Setup one-shot du systeme de terrains animes (2.13.e) :
    ///   1) Cree (ou charge) l'asset TerrainSpriteLibrary
    ///   2) Auto-populate les arrays Vapeur Carmin / Sang Coagule depuis les sub-sprites
    ///      des sheets PNG (Sprites/Soulrender/Terrains/*_Nframe.png).
    ///   3) Ajoute le composant TerrainView au GameObject "GridRoot" de la scene active
    ///      (ou cree un nouveau GameObject "TerrainOverlayRoot" si absent), et cable les references.
    ///
    /// Idempotent : re-run sans risque (ecrase l'asset et reconfigure le composant).
    /// Menu : Nymora > Setup > Setup Terrain View
    /// </summary>
    public static class SetupTerrainViewTool
    {
        private const string LibraryFolder = "Assets/_Nymora/ScriptableObjects/Combat";
        private const string LibraryAssetPath = LibraryFolder + "/TerrainSpriteLibrary.asset";
        private const string TerrainsFolder = "Assets/_Nymora/Art/Sprites/Soulrender/Terrains";

        // Fichiers de sheet a wirer. Le path est utilise pour LoadAllAssetRepresentationsAtPath
        // qui retourne tous les sub-sprites (frame_0, frame_1, ...).
        private const string VapeurCarminSheetPath = TerrainsFolder + "/tiles_vapeur_carmin_4frame.png";
        private const string SangCoaguleSheetPath  = TerrainsFolder + "/sang_coagule_4frame.png";

        [MenuItem("Nymora/Setup/Setup Terrain View")]
        public static void Run()
        {
            // 1) Asset library.
            var library = LoadOrCreateLibrary();
            if (library == null) return;

            Sprite[] vapeurFrames = LoadFramesFromSheet(VapeurCarminSheetPath);
            Sprite[] sangFrames   = LoadFramesFromSheet(SangCoaguleSheetPath);

            if (vapeurFrames == null || vapeurFrames.Length == 0)
            {
                Debug.LogWarning($"[SetupTerrainView] Aucune frame trouvee dans {VapeurCarminSheetPath}. " +
                                 "As-tu run 'Auto-slice Frame Sheets' avant ?");
            }
            if (sangFrames == null || sangFrames.Length == 0)
            {
                Debug.LogWarning($"[SetupTerrainView] Aucune frame trouvee dans {SangCoaguleSheetPath}. " +
                                 "As-tu run 'Auto-slice Frame Sheets' avant ?");
            }

            Undo.RecordObject(library, "Populate Terrain Sprite Library");
            var so = new SerializedObject(library);
            SetSpriteArrayRef(so, "_vapeurCarminFrames", vapeurFrames);
            SetSpriteArrayRef(so, "_sangCoaguleFrames", sangFrames);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            Debug.Log($"[SetupTerrainView] Library populee : Vapeur Carmin {SafeCount(vapeurFrames)} frames, " +
                      $"Sang Coagule {SafeCount(sangFrames)} frames -> {LibraryAssetPath}");

            // 2) Composant TerrainView dans la scene.
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogWarning("[SetupTerrainView] Aucune scene ouverte — composant non ajoute. " +
                                 "Re-run le tool une fois la scene combat ouverte.");
                return;
            }

            var gridRenderer = Object.FindFirstObjectByType<GridRenderer>();
            if (gridRenderer == null)
            {
                Debug.LogError("[SetupTerrainView] Aucun GridRenderer trouve dans la scene active. " +
                               "Ouvre la scene de combat (avec GridRoot) avant de lancer ce tool.");
                return;
            }

            var existingTerrainView = Object.FindFirstObjectByType<TerrainView>();
            TerrainView terrainView;
            if (existingTerrainView != null)
            {
                terrainView = existingTerrainView;
                Debug.Log($"[SetupTerrainView] TerrainView existant trouve sur {existingTerrainView.gameObject.name}, " +
                          "reconfiguration des references.");
            }
            else
            {
                terrainView = Undo.AddComponent<TerrainView>(gridRenderer.gameObject);
                Debug.Log($"[SetupTerrainView] TerrainView ajoute sur {gridRenderer.gameObject.name}.");
            }

            var tvSo = new SerializedObject(terrainView);
            SetObjectRef(tvSo, "_gridRenderer", gridRenderer);
            SetObjectRef(tvSo, "_library", library);
            tvSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(terrainView);
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("[SetupTerrainView] Setup termine. Lance Play pour voir les terrains s'animer.");
        }

        private static TerrainSpriteLibrary LoadOrCreateLibrary()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TerrainSpriteLibrary>(LibraryAssetPath);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder(LibraryFolder))
            {
                CreateFolderRecursive(LibraryFolder);
            }
            var created = ScriptableObject.CreateInstance<TerrainSpriteLibrary>();
            AssetDatabase.CreateAsset(created, LibraryAssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SetupTerrainView] Cree {LibraryAssetPath}");
            return created;
        }

        private static Sprite[] LoadFramesFromSheet(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[SetupTerrainView] Fichier sheet absent : {path}");
                return null;
            }
            // LoadAllAssetRepresentationsAtPath retourne les sub-sprites d'un sheet multiple.
            var reps = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
            var sprites = new List<Sprite>();
            foreach (var rep in reps)
            {
                if (rep is Sprite s) sprites.Add(s);
            }
            // Sort par nom pour ordonner _0, _1, _2, ...
            sprites.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return sprites.ToArray();
        }

        private static int SafeCount(Sprite[] arr) => arr == null ? 0 : arr.Length;

        private static void SetSpriteArrayRef(SerializedObject so, string propName, Sprite[] arr)
        {
            var prop = so.FindProperty(propName);
            if (prop == null)
            {
                Debug.LogError($"[SetupTerrainView] Propriete {propName} introuvable sur TerrainSpriteLibrary.");
                return;
            }
            prop.arraySize = arr != null ? arr.Length : 0;
            if (arr != null)
            {
                for (int i = 0; i < arr.Length; i++)
                {
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = arr[i];
                }
            }
        }

        private static void SetObjectRef(SerializedObject so, string propName, Object obj)
        {
            var prop = so.FindProperty(propName);
            if (prop == null) return;
            prop.objectReferenceValue = obj;
        }

        private static void CreateFolderRecursive(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                CreateFolderRecursive(parent);
            }
            string name = Path.GetFileName(folder);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
