using Nymora.Core.Enums;
using Nymora.Core.ScriptableObjects;
using Nymora.Hub;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Brique 5.3.g.bis — Auto-bind les 5 NymoraClassDefinition.asset sur le prefab HubAvatar
    /// (champ _classDefinitions). Evite a Lorenzo de drag manuellement chaque ref.
    ///
    /// Menu : Nymora > Setup > Patch HubAvatar Prefab.
    /// </summary>
    public static class PatchHubAvatarPrefabTool
    {
        private const string PrefabPath = "Assets/_Nymora/Prefabs/Hub/HubAvatar.prefab";
        private const string ClassesFolder = "Assets/_Nymora/ScriptableObjects/Classes";

        [MenuItem("Nymora/Setup/Patch HubAvatar Prefab (Class Definitions)", priority = 39)]
        private static void Patch()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Patch HubAvatar", $"Prefab introuvable : {PrefabPath}", "OK");
                return;
            }

            var defs = new System.Collections.Generic.List<NymoraClassDefinition>();
            foreach (NymoraClass cls in System.Enum.GetValues(typeof(NymoraClass)))
            {
                if (cls == NymoraClass.None) continue;
                var def = AssetDatabase.LoadAssetAtPath<NymoraClassDefinition>($"{ClassesFolder}/{cls}.asset");
                if (def != null) defs.Add(def);
            }
            if (defs.Count != 5)
            {
                EditorUtility.DisplayDialog("Patch HubAvatar",
                    $"Attendu 5 ClassDefinition dans {ClassesFolder}, trouve {defs.Count}.", "OK");
                return;
            }

            // Edit le prefab via PrefabContents (modifie l'asset reel)
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var avatar = root.GetComponent<HubAvatar>();
                if (avatar == null)
                {
                    EditorUtility.DisplayDialog("Patch HubAvatar", "HubAvatar component manquant sur le prefab.", "OK");
                    return;
                }

                var so = new SerializedObject(avatar);
                var arr = so.FindProperty("_classDefinitions");
                arr.arraySize = defs.Count;
                for (int i = 0; i < defs.Count; i++)
                {
                    arr.GetArrayElementAtIndex(i).objectReferenceValue = defs[i];
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[Nymora.Setup] HubAvatar prefab patche : 5 ClassDefinition.asset bindes.");
                EditorUtility.DisplayDialog("Patch HubAvatar", "5 ClassDefinition.asset bindes sur le prefab HubAvatar.", "OK");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
