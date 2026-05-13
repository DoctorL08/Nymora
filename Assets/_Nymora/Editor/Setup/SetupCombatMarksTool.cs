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
    /// Setup one-shot du systeme de marques visuelles (2.13.e) :
    ///   1) Cree (ou charge) l'asset MarkSpriteLibrary
    ///   2) Auto-populate les frames Plaie Ouverte (AntiHealShield) + Marque de Carnage
    ///      depuis les sheets PNG slice.
    ///   3) Ajoute le composant CombatantMarksView au GameObject "GridRoot" et cable la library.
    ///
    /// Pre-requis : avoir lance "Nymora > Setup > Auto-slice Frame Sheets" avant.
    /// Idempotent. Menu : Nymora > Setup > Setup Combat Marks
    /// </summary>
    public static class SetupCombatMarksTool
    {
        private const string LibraryFolder = "Assets/_Nymora/ScriptableObjects/Combat";
        private const string LibraryAssetPath = LibraryFolder + "/MarkSpriteLibrary.asset";
        private const string PlaieOuverteSheetPath  = "Assets/_Nymora/Art/Sprites/Soulrender/Marks/plaie_ouverte_4frame.png";
        private const string MarqueCarnageSheetPath = "Assets/_Nymora/Art/Sprites/Soulrender/Marks/marque_de_carnage_4frame.png";

        [MenuItem("Nymora/Setup/Setup Combat Marks")]
        public static void Run()
        {
            var library = LoadOrCreateLibrary();
            if (library == null) return;

            Sprite[] plaieFrames   = LoadFramesFromSheet(PlaieOuverteSheetPath);
            Sprite[] carnageFrames = LoadFramesFromSheet(MarqueCarnageSheetPath);

            if (plaieFrames == null || plaieFrames.Length == 0)
            {
                Debug.LogWarning($"[SetupCombatMarks] Aucune frame dans {PlaieOuverteSheetPath}. " +
                                 "As-tu run 'Auto-slice Frame Sheets' avant ?");
            }
            if (carnageFrames == null || carnageFrames.Length == 0)
            {
                Debug.LogWarning($"[SetupCombatMarks] Aucune frame dans {MarqueCarnageSheetPath}. " +
                                 "As-tu run 'Auto-slice Frame Sheets' avant ?");
            }

            Undo.RecordObject(library, "Populate Mark Sprite Library");
            var so = new SerializedObject(library);
            SetSpriteArrayRef(so, "_antiHealShieldFrames", plaieFrames);
            SetSpriteArrayRef(so, "_markedByCarnageFrames", carnageFrames);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            Debug.Log($"[SetupCombatMarks] Library populee : Plaie Ouverte {SafeCount(plaieFrames)} frames, " +
                      $"Marque de Carnage {SafeCount(carnageFrames)} frames -> {LibraryAssetPath}");

            // Composant scene.
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogWarning("[SetupCombatMarks] Aucune scene ouverte — composant non ajoute.");
                return;
            }

            var gridRenderer = Object.FindFirstObjectByType<GridRenderer>();
            if (gridRenderer == null)
            {
                Debug.LogError("[SetupCombatMarks] Aucun GridRenderer trouve dans la scene active.");
                return;
            }

            var existing = Object.FindFirstObjectByType<CombatantMarksView>();
            CombatantMarksView view;
            if (existing != null)
            {
                view = existing;
                Debug.Log($"[SetupCombatMarks] CombatantMarksView existant sur {existing.gameObject.name}, reconfiguration.");
            }
            else
            {
                view = Undo.AddComponent<CombatantMarksView>(gridRenderer.gameObject);
                Debug.Log($"[SetupCombatMarks] CombatantMarksView ajoute sur {gridRenderer.gameObject.name}.");
            }

            var vSo = new SerializedObject(view);
            SetObjectRef(vSo, "_library", library);
            vSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("[SetupCombatMarks] Setup termine. Test : cast Marque de Carnage (touche 6) sur enemy, " +
                      "ou Ouvre-Plaie + 1 HG (Shift+1) sur enemy.");
        }

        private static MarkSpriteLibrary LoadOrCreateLibrary()
        {
            var existing = AssetDatabase.LoadAssetAtPath<MarkSpriteLibrary>(LibraryAssetPath);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder(LibraryFolder))
            {
                CreateFolderRecursive(LibraryFolder);
            }
            var created = ScriptableObject.CreateInstance<MarkSpriteLibrary>();
            AssetDatabase.CreateAsset(created, LibraryAssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SetupCombatMarks] Cree {LibraryAssetPath}");
            return created;
        }

        private static Sprite[] LoadFramesFromSheet(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[SetupCombatMarks] Fichier sheet absent : {path}");
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
