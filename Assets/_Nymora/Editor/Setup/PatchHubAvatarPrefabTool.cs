using Nymora.Core.Enums;
using Nymora.Core.ScriptableObjects;
using Nymora.Hub;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Brique 5.3.g.bis + 5.5.f — Auto-bind sur le prefab HubAvatar : les 5 NymoraClassDefinition
    /// (_classDefinitions), les CosmeticSkinDefinition (_skinDefinitions) et NymoraBackendSettings
    /// (_backendSettings). Evite a Lorenzo de drag manuellement, ET re-cable tout apres une regen
    /// Fusion (cf feedback-networked-field-regen-protocol : ajout de NetSkinId 5.5.f).
    ///
    /// Menu : Nymora > Setup > Patch HubAvatar Prefab.
    /// </summary>
    public static class PatchHubAvatarPrefabTool
    {
        private const string PrefabPath = "Assets/_Nymora/Prefabs/Hub/HubAvatar.prefab";
        private const string ClassesFolder = "Assets/_Nymora/ScriptableObjects/Classes";
        private const string BackendSettingsPath = "Assets/_Nymora/Settings/NymoraBackendSettings.asset";

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

                // 5.5.f — Skins cosmetiques (tous les CosmeticSkinDefinition du projet).
                int skinCount = 0;
                var skinsProp = so.FindProperty("_skinDefinitions");
                if (skinsProp != null)
                {
                    var skinGuids = AssetDatabase.FindAssets("t:CosmeticSkinDefinition");
                    skinsProp.arraySize = skinGuids.Length;
                    for (int i = 0; i < skinGuids.Length; i++)
                    {
                        var skin = AssetDatabase.LoadAssetAtPath<CosmeticSkinDefinition>(
                            AssetDatabase.GUIDToAssetPath(skinGuids[i]));
                        skinsProp.GetArrayElementAtIndex(i).objectReferenceValue = skin;
                    }
                    skinCount = skinGuids.Length;
                }

                // 5.5.f — Backend settings (pour fetch l'inventaire / skin equipe).
                var settingsProp = so.FindProperty("_backendSettings");
                if (settingsProp != null && settingsProp.objectReferenceValue == null)
                {
                    var settings = AssetDatabase.LoadMainAssetAtPath(BackendSettingsPath);
                    if (settings != null) settingsProp.objectReferenceValue = settings;
                }

                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[Nymora.Setup] HubAvatar prefab patche : {defs.Count} ClassDefinition + {skinCount} skin(s) + backend settings.");
                EditorUtility.DisplayDialog("Patch HubAvatar",
                    $"Câblé sur HubAvatar :\n- {defs.Count} ClassDefinition\n- {skinCount} CosmeticSkinDefinition\n- backend settings", "OK");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
