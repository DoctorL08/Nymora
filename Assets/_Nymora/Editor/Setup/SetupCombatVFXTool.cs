using System.Collections.Generic;
using System.IO;
using Nymora.Combat.View;
using Nymora.Combat.View.Animation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Setup one-shot du systeme de VFX combat (2.13.e) :
    ///   1) Cree (ou charge) l'asset VFXSpriteLibrary
    ///   2) Auto-populate les frames Ame Laceree depuis le sheet PNG slice.
    ///   3) Ajoute le composant CombatVFXView au GameObject "GridRoot" de la scene,
    ///      et cable les references (GridRenderer + library).
    ///
    /// Pre-requis : avoir lance "Nymora > Setup > Auto-slice Frame Sheets" avant pour
    /// que VFX_ame_laceree_10frame.png soit slice en 10 sub-sprites.
    ///
    /// Idempotent. Menu : Nymora > Setup > Setup Combat VFX
    /// </summary>
    public static class SetupCombatVFXTool
    {
        private const string LibraryFolder = "Assets/_Nymora/ScriptableObjects/Combat";
        private const string LibraryAssetPath = LibraryFolder + "/VFXSpriteLibrary.asset";
        private const string AmeLaceeSheetPath = "Assets/_Nymora/Art/VFX/Soulrender/VFX_ame_laceree_10frame.png";

        [MenuItem("Nymora/Setup/Setup Combat VFX")]
        public static void Run()
        {
            var library = LoadOrCreateLibrary();
            if (library == null) return;

            Sprite[] ameLaceeFrames = LoadFramesFromSheet(AmeLaceeSheetPath);
            if (ameLaceeFrames == null || ameLaceeFrames.Length == 0)
            {
                Debug.LogWarning($"[SetupCombatVFX] Aucune frame trouvee dans {AmeLaceeSheetPath}. " +
                                 "As-tu run 'Auto-slice Frame Sheets' avant ?");
            }

            Undo.RecordObject(library, "Populate VFX Sprite Library");
            var so = new SerializedObject(library);
            SetSpriteArrayRef(so, "_ameLaceeFrames", ameLaceeFrames);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            Debug.Log($"[SetupCombatVFX] Library populee : Ame Laceree {SafeCount(ameLaceeFrames)} frames -> {LibraryAssetPath}");

            // Composant scene.
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogWarning("[SetupCombatVFX] Aucune scene ouverte — composant non ajoute.");
                return;
            }

            var gridRenderer = Object.FindFirstObjectByType<GridRenderer>();
            if (gridRenderer == null)
            {
                Debug.LogError("[SetupCombatVFX] Aucun GridRenderer trouve dans la scene active.");
                return;
            }

            var existing = Object.FindFirstObjectByType<CombatVFXView>();
            CombatVFXView view;
            if (existing != null)
            {
                view = existing;
                Debug.Log($"[SetupCombatVFX] CombatVFXView existant sur {existing.gameObject.name}, reconfiguration.");
            }
            else
            {
                view = Undo.AddComponent<CombatVFXView>(gridRenderer.gameObject);
                Debug.Log($"[SetupCombatVFX] CombatVFXView ajoute sur {gridRenderer.gameObject.name}.");
            }

            var vSo = new SerializedObject(view);
            SetObjectRef(vSo, "_gridRenderer", gridRenderer);
            SetObjectRef(vSo, "_library", library);
            vSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("[SetupCombatVFX] Setup termine. Test : cast Ame Laceree (touche B) avec 5 HG.");
        }

        private static VFXSpriteLibrary LoadOrCreateLibrary()
        {
            var existing = AssetDatabase.LoadAssetAtPath<VFXSpriteLibrary>(LibraryAssetPath);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder(LibraryFolder))
            {
                CreateFolderRecursive(LibraryFolder);
            }
            var created = ScriptableObject.CreateInstance<VFXSpriteLibrary>();
            AssetDatabase.CreateAsset(created, LibraryAssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SetupCombatVFX] Cree {LibraryAssetPath}");
            return created;
        }

        private static Sprite[] LoadFramesFromSheet(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[SetupCombatVFX] Fichier sheet absent : {path}");
                return null;
            }
            var reps = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
            var sprites = new List<Sprite>();
            foreach (var rep in reps)
            {
                if (rep is Sprite s) sprites.Add(s);
            }
            sprites.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return sprites.ToArray();
        }

        private static int SafeCount(Sprite[] arr) => arr == null ? 0 : arr.Length;

        private static void SetSpriteArrayRef(SerializedObject so, string propName, Sprite[] arr)
        {
            var prop = so.FindProperty(propName);
            if (prop == null) return;
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
