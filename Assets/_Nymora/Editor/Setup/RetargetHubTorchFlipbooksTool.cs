using System.Linq;
using Nymora.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// POLISH KYAMI — Tâche 9 (fix) : les torches du hub sont animées par un composant
    /// <see cref="SpriteFlipbook"/> AJOUTÉ EN SCÈNE (pas par l'Animator du Torch.prefab), dont le
    /// tableau <c>_frames</c> pointe les ANCIENS sprites <c>torch_frame1..8</c>. Réécrire le clip
    /// Torch.anim n'a donc aucun effet visible.
    ///
    /// Ce tool ouvre <c>10_CommunityHub</c>, repère chaque <see cref="SpriteFlipbook"/> dont les
    /// frames courantes viennent de <c>Art/Hub/Torch/torch_frame*</c>, et remplace son <c>_frames</c>
    /// par les 8 nouvelles frames slicées de <c>torch_map_hub_animation_8frame.png</c> (importées via
    /// « Import Hub Torch Spritesheet + Normal »). On NE TOUCHE PAS <c>_fps</c> ni <c>_startOffset</c>
    /// → le déphasage par torche est préservé. Aucune Light2D / halo modifié.
    ///
    /// Prérequis : lancer d'abord « Import Hub Torch Spritesheet + Normal » (slice + normal).
    ///
    /// Menu : Nymora > Setup > Polish Kyami > Retarget Hub Torch Flipbooks (scene).
    /// </summary>
    public static class RetargetHubTorchFlipbooksTool
    {
        private const string HubScenePath = "Assets/_Nymora/Scenes/10_CommunityHub.unity";
        private const string NewSheetPath = "Assets/_Nymora/Art/Hub/Torch/torch_map_hub_animation_8frame.png";
        private const string OldFrameMarker = "/Hub/Torch/torch_frame"; // chemin des anciennes frames

        [MenuItem("Nymora/Setup/Polish Kyami/Retarget Hub Torch Flipbooks (scene)", priority = 63)]
        public static void Run()
        {
            // 1) Charge les nouvelles frames.
            var newFrames = AssetDatabase.LoadAllAssetsAtPath(NewSheetPath)
                .OfType<Sprite>()
                .OrderBy(s => TrailingInt(s.name))
                .ToArray();
            if (newFrames.Length == 0)
            {
                EditorUtility.DisplayDialog("Retarget Torches",
                    "Aucun sprite slicé trouvé dans :\n" + NewSheetPath +
                    "\n\nLance d'abord « Import Hub Torch Spritesheet + Normal ».", "OK");
                return;
            }

            // 2) S'assure que la scène hub est ouverte (propose de sauver l'actuelle si différente).
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != HubScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(HubScenePath, OpenSceneMode.Single);
            }

            // 3) Repère + repointe les SpriteFlipbook de torches.
            var flipbooks = Object.FindObjectsOfType<SpriteFlipbook>(true);
            int torchCount = 0, total = flipbooks.Length;
            foreach (var fb in flipbooks)
            {
                var so = new SerializedObject(fb);
                var framesProp = so.FindProperty("_frames");
                if (framesProp == null || !framesProp.isArray) continue;

                if (!IsTorchFlipbook(framesProp)) continue;

                framesProp.arraySize = newFrames.Length;
                for (int i = 0; i < newFrames.Length; i++)
                    framesProp.GetArrayElementAtIndex(i).objectReferenceValue = newFrames[i];

                so.ApplyModifiedPropertiesWithoutUndo(); // _fps / _startOffset intacts
                torchCount++;
            }

            if (torchCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log($"[RetargetHubTorch] {torchCount}/{total} SpriteFlipbook de torche repointés ({newFrames.Length} frames).");
            EditorUtility.DisplayDialog("Retarget Torches",
                $"{torchCount} torche(s) repointée(s) sur le nouveau sheet ({newFrames.Length} frames).\n" +
                $"({total} SpriteFlipbook au total dans la scène.)\n\n" +
                (torchCount > 0 ? "Scène sauvegardée. Lance le hub pour vérifier." : "Aucune torche détectée — vérifie que les frames actuelles viennent bien de torch_frame*."),
                "OK");
        }

        private static bool IsTorchFlipbook(SerializedProperty framesProp)
        {
            for (int i = 0; i < framesProp.arraySize; i++)
            {
                var sprite = framesProp.GetArrayElementAtIndex(i).objectReferenceValue as Sprite;
                if (sprite == null) continue;
                string path = AssetDatabase.GetAssetPath(sprite);
                if (!string.IsNullOrEmpty(path) && path.Contains(OldFrameMarker))
                    return true;
            }
            return false;
        }

        private static int TrailingInt(string name)
        {
            int i = name.Length - 1;
            while (i >= 0 && char.IsDigit(name[i])) i--;
            return int.TryParse(name.Substring(i + 1), out var v) ? v : 0;
        }
    }
}
