using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Nymora.Hub;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Outil opt-in — ajoute le composant TorchLightFlicker a toutes les Point Light 2D du
    /// groupe "Scene Lighting 2D" pour qu'elles vacillent facon flamme (View only).
    ///
    /// Ne change AUCUNE valeur de light : le flicker capture l'intensite courante comme base.
    /// Idempotent (skip les lights deja flicker). Lance par Lorenzo a la demande.
    ///
    /// Menu : Nymora > Setup > Add Torch Flicker to Hub Lights.
    /// </summary>
    public static class HubTorchFlickerTool
    {
        private const string ScenePath = "Assets/_Nymora/Scenes/10_CommunityHub.unity";
        private const string LightingRootName = "Scene Lighting 2D";

        [MenuItem("Nymora/Setup/Add Torch Flicker to Hub Lights", priority = 65)]
        private static void AddFlicker()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Torch Flicker", "Impossible pendant Play Mode.", "OK");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.path.EndsWith("10_CommunityHub.unity"))
            {
                if (!EditorUtility.DisplayDialog("Torch Flicker",
                        $"Scene active : {scene.path}\nAttendu : {ScenePath}\n\nOuvrir 10_CommunityHub ?",
                        "Ouvrir", "Annuler"))
                    return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var root = GameObject.Find(LightingRootName);
            if (root == null)
            {
                EditorUtility.DisplayDialog("Torch Flicker", "Groupe '" + LightingRootName + "' introuvable.", "OK");
                return;
            }

            int added = 0;
            foreach (var light in root.GetComponentsInChildren<Light2D>(true))
            {
                if (light.lightType != Light2D.LightType.Point) continue;
                if (light.GetComponent<TorchLightFlicker>() != null) continue;
                Undo.AddComponent<TorchLightFlicker>(light.gameObject);
                added++;
            }

            EditorSceneManager.MarkSceneDirty(scene);

            string summary = added == 0
                ? "Toutes les point lights ont deja le flicker."
                : $"{added} torches animées (flicker flamme). Aucune valeur de light modifiée.\n" +
                  "Retire le composant TorchLightFlicker sur une light si tu n'en veux pas. Ctrl+S.";
            EditorUtility.DisplayDialog("Torch Flicker", summary, "OK");
            Debug.Log("[HubTorchFlickerTool] " + summary);
        }
    }
}
