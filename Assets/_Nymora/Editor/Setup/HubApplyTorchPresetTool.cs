using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Nymora.Hub;
using ComponentUtility = UnityEditorInternal.ComponentUtility;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Outil opt-in — copie les reglages de la torche SELECTIONNEE (Light2D + TorchLightFlicker)
    /// vers toutes les autres Point Light du groupe "Scene Lighting 2D".
    ///
    /// Preserve la POSITION de chaque torche (on ne touche jamais au Transform). Tout le reste
    /// (couleur, intensite, radius, falloff, blend, ombres, flicker...) devient identique a la
    /// torche modele. Ajoute le composant TorchLightFlicker aux torches qui ne l'ont pas.
    ///
    /// Usage : selectionne dans la Hierarchy la torche parfaitement reglee, puis lance le menu.
    ///
    /// Menu : Nymora > Setup > Apply Selected Torch Settings to All.
    /// </summary>
    public static class HubApplyTorchPresetTool
    {
        private const string LightingRootName = "Scene Lighting 2D";

        [MenuItem("Nymora/Setup/Apply Selected Torch Settings to All", priority = 66)]
        private static void Apply()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Apply Torch Preset", "Impossible pendant Play Mode.", "OK");
                return;
            }

            var sel = Selection.activeGameObject;
            var srcLight = sel != null ? sel.GetComponent<Light2D>() : null;
            if (srcLight == null)
            {
                EditorUtility.DisplayDialog("Apply Torch Preset",
                    "Selectionne d'abord la torche modele (un GameObject avec un composant Light 2D) dans la Hierarchy.", "OK");
                return;
            }

            var root = GameObject.Find(LightingRootName);
            if (root == null)
            {
                EditorUtility.DisplayDialog("Apply Torch Preset", "Groupe '" + LightingRootName + "' introuvable.", "OK");
                return;
            }

            var srcFlicker = sel.GetComponent<TorchLightFlicker>();

            // Cibles = toutes les Point Light du groupe sauf la source.
            var allLights = root.GetComponentsInChildren<Light2D>(true);

            int lightCount = 0;
            int flickerCount = 0;

            // 1) Light2D : copie une fois, colle sur chaque cible.
            ComponentUtility.CopyComponent(srcLight);
            foreach (var target in allLights)
            {
                if (target == srcLight) continue;
                if (target.lightType != Light2D.LightType.Point) continue; // jamais la Global
                ComponentUtility.PasteComponentValues(target);
                EditorUtility.SetDirty(target);
                lightCount++;
            }

            // 2) TorchLightFlicker : ensure + copie/colle (si la source en a un).
            if (srcFlicker != null)
            {
                foreach (var target in allLights)
                {
                    if (target == srcLight) continue;
                    if (target.lightType != Light2D.LightType.Point) continue;
                    if (target.GetComponent<TorchLightFlicker>() == null)
                        Undo.AddComponent<TorchLightFlicker>(target.gameObject);
                }

                ComponentUtility.CopyComponent(srcFlicker);
                foreach (var target in allLights)
                {
                    if (target == srcLight) continue;
                    if (target.lightType != Light2D.LightType.Point) continue;
                    var tf = target.GetComponent<TorchLightFlicker>();
                    if (tf == null) continue;
                    ComponentUtility.PasteComponentValues(tf);
                    EditorUtility.SetDirty(tf);
                    flickerCount++;
                }
            }

            var scene = sel.scene;
            if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);

            string summary =
                $"Reglages de '{sel.name}' appliques :\n" +
                $"- Light2D copie sur {lightCount} torches\n" +
                $"- TorchLightFlicker copie sur {flickerCount} torches\n" +
                "Positions PRESERVEES. Ctrl+S pour sauver.";
            EditorUtility.DisplayDialog("Apply Torch Preset", summary, "OK");
            Debug.Log("[HubApplyTorchPresetTool] " + summary);
        }
    }
}
