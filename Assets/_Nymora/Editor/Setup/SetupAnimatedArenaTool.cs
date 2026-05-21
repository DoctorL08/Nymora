using System.Collections.Generic;
using Nymora.Combat.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Remplace la map statique des scenes combat (Map_Combat_1) par la map animee du
    /// designer : ajoute le composant MapSpriteAnimator sur Map_Combat_1 et cable les 12
    /// frames importees dans Art/UI/Maps/Arene1vs1_Anim. Conserve le SpriteRenderer existant
    /// (material, sortingOrder -1000, transform) — seul le sprite affiche devient anime.
    ///
    /// Idempotent : re-run reconfigure le composant sans le dupliquer. Sauvegarde chaque scene.
    /// Menu : Nymora > Setup > Setup Animated Arena Map
    /// </summary>
    public static class SetupAnimatedArenaTool
    {
        private const string FramesFolder = "Assets/_Nymora/Art/UI/Maps/Arene1vs1_Anim";
        private const string MapObjectName = "Map_Combat_1";
        private const float DefaultFps = 10f;

        private static readonly string[] CombatScenes =
        {
            "Assets/_Nymora/Scenes/30_CombatIA.unity",
            "Assets/_Nymora/Scenes/33_CombatCasual.unity",
        };

        [MenuItem("Nymora/Setup/Setup Animated Arena Map")]
        public static void Run()
        {
            Sprite[] frames = LoadFrames();
            if (frames == null || frames.Length == 0)
            {
                Debug.LogError($"[SetupAnimatedArena] Aucune frame trouvee dans {FramesFolder}. " +
                               "Verifie que les 12 PNG Arene1vs1_XX sont bien importes en mode Sprite.");
                return;
            }
            Debug.Log($"[SetupAnimatedArena] {frames.Length} frames chargees ({frames[0].name} -> {frames[frames.Length - 1].name}).");

            // Sauvegarde la scene courante si modifiee avant de basculer.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[SetupAnimatedArena] Annule : scene courante non sauvegardee.");
                return;
            }

            int patched = 0;
            foreach (var scenePath in CombatScenes)
            {
                if (PatchScene(scenePath, frames)) patched++;
            }

            Debug.Log($"[SetupAnimatedArena] Termine. {patched}/{CombatScenes.Length} scene(s) patchee(s). " +
                      "Lance Play (30_CombatIA ou 33_CombatCasual) pour voir la map s'animer.");
        }

        private static bool PatchScene(string scenePath, Sprite[] frames)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[SetupAnimatedArena] Impossible d'ouvrir {scenePath}.");
                return false;
            }

            GameObject mapGo = FindInScene(scene, MapObjectName);
            if (mapGo == null)
            {
                Debug.LogError($"[SetupAnimatedArena] '{MapObjectName}' introuvable dans {scenePath}.");
                return false;
            }

            var renderer = mapGo.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                Debug.LogError($"[SetupAnimatedArena] Pas de SpriteRenderer sur '{MapObjectName}' ({scenePath}).");
                return false;
            }

            var animator = mapGo.GetComponent<MapSpriteAnimator>();
            if (animator == null)
            {
                animator = mapGo.AddComponent<MapSpriteAnimator>();
            }

            var so = new SerializedObject(animator);
            var framesProp = so.FindProperty("_frames");
            framesProp.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++)
            {
                framesProp.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            }
            var fpsProp = so.FindProperty("_fps");
            if (Mathf.Approximately(fpsProp.floatValue, 0f)) fpsProp.floatValue = DefaultFps;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Affiche la 1ere frame en Edit Mode (preview).
            renderer.sprite = frames[0];

            EditorUtility.SetDirty(animator);
            EditorUtility.SetDirty(renderer);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SetupAnimatedArena] '{MapObjectName}' patche dans {scenePath} ({frames.Length} frames).");
            return true;
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
                var t = FindInChildren(root.transform, name);
                if (t != null) return t.gameObject;
            }
            return null;
        }

        private static Transform FindInChildren(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var found = FindInChildren(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static Sprite[] LoadFrames()
        {
            if (!AssetDatabase.IsValidFolder(FramesFolder)) return null;
            var guids = AssetDatabase.FindAssets("t:Sprite", new[] { FramesFolder });
            var sprites = new List<Sprite>();
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) sprites.Add(sprite);
            }
            sprites.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return sprites.ToArray();
        }
    }
}
