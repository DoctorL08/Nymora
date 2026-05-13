using Nymora.Combat.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// 2.14 — Setup one-shot du systeme de brouillard de guerre cote View.
    /// Ajoute le composant FogOfWarView au GameObject du GridRenderer (= "GridRoot"
    /// typiquement) et cable la reference au GridRenderer.
    ///
    /// Idempotent. Menu : Nymora > Setup > Setup Fog of War
    /// </summary>
    public static class SetupFogOfWarTool
    {
        [MenuItem("Nymora/Setup/Setup Fog of War")]
        public static void Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("Setup Fog of War", "Aucune scene ouverte.", "OK");
                return;
            }

            var gridRenderer = Object.FindFirstObjectByType<GridRenderer>();
            if (gridRenderer == null)
            {
                Debug.LogError("[SetupFogOfWar] Aucun GridRenderer trouve dans la scene active.");
                return;
            }

            var existing = Object.FindFirstObjectByType<FogOfWarView>();
            FogOfWarView view;
            if (existing != null)
            {
                view = existing;
                Debug.Log($"[SetupFogOfWar] FogOfWarView existant sur {existing.gameObject.name}, reconfiguration.");
            }
            else
            {
                view = Undo.AddComponent<FogOfWarView>(gridRenderer.gameObject);
                Debug.Log($"[SetupFogOfWar] FogOfWarView ajoute sur {gridRenderer.gameObject.name}.");
            }

            var so = new SerializedObject(view);
            SetObjectRef(so, "_gridRenderer", gridRenderer);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("[SetupFogOfWar] Setup termine. Test : Play, appuie sur T pour poser un Voile, " +
                      "End Turn pour passer au POV adverse → la case devient masquee. F12 = mode GM (revele tout).");
        }

        private static void SetObjectRef(SerializedObject so, string propName, Object obj)
        {
            var prop = so.FindProperty(propName);
            if (prop == null) return;
            prop.objectReferenceValue = obj;
        }
    }
}
