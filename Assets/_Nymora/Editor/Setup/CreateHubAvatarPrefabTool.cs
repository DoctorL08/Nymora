using System.IO;
using Fusion;
using Nymora.Hub;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Brique 4.3.c — Genere Assets/_Nymora/Prefabs/Hub/HubAvatar.prefab avec :
    ///   - SpriteRenderer (sprite Soulrender_Placeholder par defaut)
    ///   - NetworkObject (Fusion — auto-bake le GUID au save)
    ///   - NetworkTransform (Fusion — replique transform.position aux autres clients)
    ///   - HubAvatar (NetworkBehaviour)
    ///   - HubMovementController (local, pilote par State Authority)
    ///
    /// Idempotent : overwrite si existe.
    ///
    /// Menu : Nymora > Setup > Create Hub Avatar Prefab
    /// </summary>
    public static class CreateHubAvatarPrefabTool
    {
        private const string PrefabFolder = "Assets/_Nymora/Prefabs/Hub";
        private const string PrefabPath = PrefabFolder + "/HubAvatar.prefab";
        private const string AvatarSpritePath = "Assets/_Nymora/Art/Sprites/Combatants/Soulrender_Placeholder.png";

        [MenuItem("Nymora/Setup/Create Hub Avatar Prefab", priority = 34)]
        private static void CreatePrefab()
        {
            EnsureFolder(PrefabFolder);

            var avatarSprite = AssetDatabase.LoadAssetAtPath<Sprite>(AvatarSpritePath);
            if (avatarSprite == null)
            {
                EditorUtility.DisplayDialog("Hub Avatar Prefab",
                    $"Avatar sprite introuvable : {AvatarSpritePath}",
                    "OK");
                return;
            }

            if (File.Exists(PrefabPath))
            {
                if (!EditorUtility.DisplayDialog("Hub Avatar Prefab",
                    "HubAvatar.prefab existe deja. Le regenerer (ecrasement) ?",
                    "Regenerer", "Annuler"))
                {
                    return;
                }
            }

            // GameObject temporaire en scene
            // NOTE 4.3.c : NetworkTransform RETIRE — son reset de transform.position par tick
            // cassait le click-to-move. Sera reintroduit/reconfig en 4.4 (gate multi-instance)
            // soit via NetworkTransform en mode predicted, soit via [Networked] GridX/GridY + Render().
            var go = new GameObject("HubAvatar",
                typeof(SpriteRenderer),
                typeof(NetworkObject),
                typeof(HubAvatar),
                typeof(HubMovementController));

            // Sprite
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = avatarSprite;
            sr.sortingOrder = 100;

            // Cabler refs internes
            var avatar = go.GetComponent<HubAvatar>();
            var avatarSo = new SerializedObject(avatar);
            avatarSo.FindProperty("_avatarSprite").objectReferenceValue = avatarSprite;
            avatarSo.ApplyModifiedPropertiesWithoutUndo();

            var movement = go.GetComponent<HubMovementController>();
            var moveSo = new SerializedObject(movement);
            moveSo.FindProperty("_avatar").objectReferenceValue = avatar;
            moveSo.ApplyModifiedPropertiesWithoutUndo();

            // Save prefab (Fusion va bake NetworkObject.Guid automatiquement)
            bool success;
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath, out success);
            Object.DestroyImmediate(go);

            if (!success)
            {
                Debug.LogError($"[Nymora.Setup] Echec creation prefab {PrefabPath}");
                return;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[Nymora.Setup] Prefab genere : {PrefabPath}");

            EditorUtility.DisplayDialog("Hub Avatar Prefab",
                $"Prefab genere : {PrefabPath}\n\n" +
                "Etapes suivantes :\n" +
                "1. Le NetworkObjectGuid sera bake auto par Fusion (next compile)\n" +
                "2. Lance Nymora > Setup > Create Community Hub Scene\n" +
                "   pour cabler ce prefab sur HubBootstrap",
                "OK");
        }

        private static void EnsureFolder(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }
        }
    }
}
