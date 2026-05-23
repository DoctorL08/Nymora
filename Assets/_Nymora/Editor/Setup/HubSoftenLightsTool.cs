using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Outil opt-in (VP.3 helper) — adoucit les point lights du hub pour qu'elles eclairent
    /// vraiment les sprites (pools doux) au lieu d'un point minuscule qui claque en blanc.
    ///
    /// Probleme constate : les point lights avaient un Outer Radius ~0.5 (plus petit qu'une
    /// case) -> lumiere quasi invisible, et monter l'intensite blanchit le point (moche).
    /// Ce preset met : Outer Radius 4 / Inner 1 / Falloff 0.4 / Intensity 0.85.
    ///
    /// PRESERVE couleur, position, blend style, ombres. Ne touche QUE les 4 params ci-dessus,
    /// et UNIQUEMENT les Point lights (la Global n'est pas touchee). Lance par Lorenzo a la
    /// demande ; reajustable a la main ensuite.
    ///
    /// Menu : Nymora > Setup > Soften Hub Lights.
    /// </summary>
    public static class HubSoftenLightsTool
    {
        private const string ScenePath = "Assets/_Nymora/Scenes/10_CommunityHub.unity";
        private const string LightingRootName = "Scene Lighting 2D";

        private const float OuterRadius = 4f;
        private const float InnerRadius = 1f;
        private const float Falloff = 0.4f;
        private const float Intensity = 0.85f;

        [MenuItem("Nymora/Setup/Soften Hub Lights", priority = 64)]
        private static void Soften()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Soften Hub Lights", "Impossible pendant Play Mode.", "OK");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.path.EndsWith("10_CommunityHub.unity"))
            {
                if (!EditorUtility.DisplayDialog("Soften Hub Lights",
                        $"Scene active : {scene.path}\nAttendu : {ScenePath}\n\nOuvrir 10_CommunityHub ?",
                        "Ouvrir", "Annuler"))
                    return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var root = GameObject.Find(LightingRootName);
            if (root == null)
            {
                EditorUtility.DisplayDialog("Soften Hub Lights",
                    "Groupe '" + LightingRootName + "' introuvable.", "OK");
                return;
            }

            int count = 0;
            foreach (var light in root.GetComponentsInChildren<Light2D>(true))
            {
                if (light.lightType != Light2D.LightType.Point) continue;

                var so = new SerializedObject(light);
                SetFloat(so, "m_PointLightOuterRadius", OuterRadius);
                SetFloat(so, "m_PointLightInnerRadius", InnerRadius);
                SetFloat(so, "m_FalloffIntensity", Falloff);
                SetFloat(so, "m_Intensity", Intensity);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(light);
                count++;
            }

            EditorSceneManager.MarkSceneDirty(scene);

            string summary = count == 0
                ? "Aucune Point Light trouvee sous '" + LightingRootName + "'."
                : $"{count} point lights adoucies (radius {OuterRadius} / falloff {Falloff} / intensity {Intensity}).\n" +
                  "Couleurs/positions/ombres preservees. Reajuste a la main si besoin, puis Ctrl+S.";
            EditorUtility.DisplayDialog("Soften Hub Lights", summary, "OK");
            Debug.Log("[HubSoftenLightsTool] " + summary);
        }

        private static void SetFloat(SerializedObject so, string name, float value)
        {
            var p = so.FindProperty(name);
            if (p != null) p.floatValue = value;
        }
    }
}
