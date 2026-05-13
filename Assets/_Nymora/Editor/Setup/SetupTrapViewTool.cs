using System.Collections.Generic;
using System.IO;
using Nymora.Combat.View;
using Nymora.Editor.Tools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Setup one-shot de la View Trap Nightseer (2.15.b refactor 13 mai 2026) :
    ///   1) Garantit que tiles_piege_runique_4frame.png est slice (4 frames horizontales) via
    ///      AutoSliceFrameSheetsTool (idempotent).
    ///   2) Charge les 4 sub-sprites slices.
    ///   3) Trouve le composant TrapView dans la scene active (ou l'ajoute sur le GameObject
    ///      portant GridRenderer si manquant).
    ///   4) Cable _gridRenderer + _trapFrames.
    ///
    /// Remplace la manip Unity manuelle (Sprite Editor -> Slice 4x1 -> drag dans Inspector).
    /// Idempotent : re-run safe. Menu : Nymora > Setup > Setup Trap View
    /// </summary>
    public static class SetupTrapViewTool
    {
        private const string SheetPath = "Assets/_Nymora/Art/Sprites/Nightseer/Tiles/tiles_piege_runique_4frame.png";

        [MenuItem("Nymora/Setup/Setup Trap View")]
        public static void Run()
        {
            // 1) Garantir le slicing du PNG (idempotent — re-slice systematiquement).
            AutoSliceFrameSheetsTool.Run();

            // 2) Charger les 4 sub-sprites.
            Sprite[] frames = LoadFramesFromSheet(SheetPath);
            if (frames == null || frames.Length == 0)
            {
                Debug.LogError($"[SetupTrapView] Aucune frame trouvee dans {SheetPath}. " +
                               "Verifie que le PNG existe et que AutoSliceFrameSheetsTool a tourne.");
                return;
            }
            if (frames.Length != 4)
            {
                Debug.LogWarning($"[SetupTrapView] {frames.Length} frames trouvees (attendu 4). " +
                                 "Le slicing a peut-etre echoue — verifie l'asset.");
            }

            // 3) Composant scene.
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogWarning("[SetupTrapView] Aucune scene ouverte — composant non cable.");
                return;
            }

            var gridRenderer = Object.FindFirstObjectByType<GridRenderer>();
            if (gridRenderer == null)
            {
                Debug.LogError("[SetupTrapView] Aucun GridRenderer trouve dans la scene active. " +
                               "Ouvre la scene de combat (ex: 30_CombatIA) avant de lancer.");
                return;
            }

            var existing = Object.FindFirstObjectByType<TrapView>();
            TrapView view;
            if (existing != null)
            {
                view = existing;
                Debug.Log($"[SetupTrapView] TrapView existant sur {existing.gameObject.name}, reconfiguration.");
            }
            else
            {
                view = Undo.AddComponent<TrapView>(gridRenderer.gameObject);
                Debug.Log($"[SetupTrapView] TrapView ajoute sur {gridRenderer.gameObject.name}.");
            }

            var so = new SerializedObject(view);
            SetObjectRef(so, "_gridRenderer", gridRenderer);
            SetSpriteArrayRef(so, "_trapFrames", frames);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[SetupTrapView] Setup termine. {frames.Length} frames cablees -> TrapView " +
                      $"sur {gridRenderer.gameObject.name}. " +
                      "Test : pose Filet de Ronces (teinte verte) ou Champ de Mines (teinte rouge) " +
                      "et verifie l'animation runique cote Nightseer uniquement.");
        }

        private static Sprite[] LoadFramesFromSheet(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[SetupTrapView] Fichier sheet absent : {path}");
                return null;
            }
            var reps = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
            var sprites = new List<Sprite>();
            foreach (var rep in reps)
            {
                if (rep is Sprite s) sprites.Add(s);
            }
            // Trie par nom (suffix _0, _1, _2, _3) pour ordre deterministe.
            sprites.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return sprites.ToArray();
        }

        private static void SetSpriteArrayRef(SerializedObject so, string propName, Sprite[] arr)
        {
            var prop = so.FindProperty(propName);
            if (prop == null)
            {
                Debug.LogWarning($"[SetupTrapView] Field {propName} introuvable sur TrapView.");
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
            if (prop == null)
            {
                Debug.LogWarning($"[SetupTrapView] Field {propName} introuvable sur TrapView.");
                return;
            }
            prop.objectReferenceValue = obj;
        }
    }
}
