using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Fix "Bug 2 halo" (23 mai) — les Torch/Magic Halo point lights du hub ciblaient
    /// Default + Personnages dans leur Target Sorting Layers. Comme le perso vit desormais
    /// sur le sorting layer "Personnages" (cf HubAvatar/IsoDepthSort), ces point lights le
    /// lavaient/teintaient en permanence : une Light2D 2D ignore la profondeur iso et eclaire
    /// uniformement tous les sprites de ses Target Sorting Layers, sans notion de devant/derriere.
    ///
    /// Ce tool remet TOUTES les point lights sur Default-only ({Default}). Elles eclairent
    /// donc le sol/murs (Default) mais plus le perso (Personnages). La GLOBAL Light n'est PAS
    /// touchee (filtre lightType == Point) : elle garde Default + Personnages pour continuer
    /// d'eclairer le perso en ambiance.
    ///
    /// Ne modifie QUE m_ApplyToSortingLayers. Intensite/couleur/radius/position/ombres/blend
    /// preservees (cf memoire feedback-dont-overwrite-light-values). Idempotent.
    ///
    /// Menu : Nymora > Setup > Fix Torch Light Target Layers.
    /// </summary>
    public static class HubLightTargetLayersTool
    {
        private const string ScenePath = "Assets/_Nymora/Scenes/10_CommunityHub.unity";
        private const string LightingRootName = "Scene Lighting 2D";

        [MenuItem("Nymora/Setup/Fix Torch Light Target Layers", priority = 65)]
        private static void Fix()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Fix Torch Light Target Layers", "Impossible pendant Play Mode.", "OK");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.path.EndsWith("10_CommunityHub.unity"))
            {
                if (!EditorUtility.DisplayDialog("Fix Torch Light Target Layers",
                        $"Scene active : {scene.path}\nAttendu : {ScenePath}\n\nOuvrir 10_CommunityHub ?",
                        "Ouvrir", "Annuler"))
                    return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var root = GameObject.Find(LightingRootName);
            if (root == null)
            {
                EditorUtility.DisplayDialog("Fix Torch Light Target Layers",
                    "Groupe '" + LightingRootName + "' introuvable.", "OK");
                return;
            }

            // uniqueID du sorting layer "Default" (toujours 0, mais on le resout proprement).
            int defaultLayerId = SortingLayer.NameToID("Default");

            int count = 0;
            foreach (var light in root.GetComponentsInChildren<Light2D>(true))
            {
                // La Global Light (lightType == Global) garde Default + Personnages : on ne touche
                // QUE les point lights (torches + magic halos).
                if (light.lightType != Light2D.LightType.Point) continue;

                var so = new SerializedObject(light);
                var prop = so.FindProperty("m_ApplyToSortingLayers");
                if (prop == null || !prop.isArray) continue;

                prop.arraySize = 1;
                prop.GetArrayElementAtIndex(0).intValue = defaultLayerId;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(light);
                count++;
            }

            EditorSceneManager.MarkSceneDirty(scene);

            string summary = count == 0
                ? "Aucune Point Light trouvee sous '" + LightingRootName + "'."
                : $"{count} point lights (torches + magic halos) remises sur Default-only.\n" +
                  "La Global Light reste sur Default + Personnages (perso toujours eclaire en ambiance).\n" +
                  "Le perso ne devrait plus etre lave par les halos de torche. Ctrl+S pour sauver.";
            EditorUtility.DisplayDialog("Fix Torch Light Target Layers", summary, "OK");
            Debug.Log("[HubLightTargetLayersTool] " + summary);
        }
    }
}
