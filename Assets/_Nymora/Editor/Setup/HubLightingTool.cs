using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Brique VP.3 — Eclairage 2D du hub (View-side only, aucun bump CombatRulesVersion).
    ///
    /// Pose un groupe "Scene Lighting 2D" dans 10_CommunityHub :
    /// - Global Light 2D (blend Multiply, intensite 1, teinte chaude) = base, n'assombrit rien.
    /// - Torch Light + Magic Halo (Point, blend Additive) = exemples de glow a dupliquer/deplacer.
    /// Toutes les lights ciblent TOUS les sorting layers.
    ///
    /// Les ombres portees (ShadowCaster2D) sont volontairement HORS de cette brique (VP.3b) :
    /// elles demandent un setup par-sprite.
    ///
    /// Idempotent : ne cree que ce qui manque (par nom). Valeurs = SEED, ajustables dans
    /// l'Inspector de chaque Light2D.
    ///
    /// VALIDATION CLE : en Play, baisse l'intensite de "Global Light" -> si la scene
    /// s'assombrit, les sprites monde sont bien lit (controle total). Si rien ne bouge,
    /// ils sont unlit et on basculera leur materiau en suivi.
    ///
    /// Menu : Nymora > Setup > Setup Hub 2D Lighting (VP3).
    /// </summary>
    public static class HubLightingTool
    {
        private const string ScenePath = "Assets/_Nymora/Scenes/10_CommunityHub.unity";
        private const string RootName = "Scene Lighting 2D";

        // Blend styles definis dans Renderer2D.asset : 0 = Multiply, 1 = Additive.
        private const int BlendMultiply = 0;
        private const int BlendAdditive = 1;

        [MenuItem("Nymora/Setup/Setup Hub 2D Lighting (VP3)", priority = 61)]
        private static void Setup()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Hub 2D Lighting", "Impossible pendant Play Mode.", "OK");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.path.EndsWith("10_CommunityHub.unity"))
            {
                if (!EditorUtility.DisplayDialog("Hub 2D Lighting",
                        $"Scene active : {scene.path}\nAttendu : {ScenePath}\n\nOuvrir 10_CommunityHub ?",
                        "Ouvrir", "Annuler"))
                    return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var actions = new List<string>();

            // Centre les lights sur ce que voit la camera (sinon elles tombent hors champ).
            Vector3 center = Vector3.zero;
            var cam = Camera.main ?? FindAnyCamera();
            if (cam != null) center = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);

            var root = GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                root.transform.position = center;
                actions.Add("- Groupe '" + RootName + "' cree");
            }

            EnsureGlobalLight(root.transform, actions);
            EnsurePointLight(root.transform, "Torch Light (sample)",
                new Color(1f, 0.62f, 0.28f), intensity: 0.85f, outerRadius: 3.2f,
                center + new Vector3(-1.5f, 0.5f, 0f), actions);
            EnsurePointLight(root.transform, "Magic Halo (sample)",
                new Color(0.46f, 0.40f, 0.95f), intensity: 0.70f, outerRadius: 2.6f,
                center + new Vector3(1.5f, 0.5f, 0f), actions);

            EditorSceneManager.MarkSceneDirty(scene);

            string summary = actions.Count == 0
                ? "OK Rien a faire, deja en place."
                : "VP.3 applique :\n\n" + string.Join("\n", actions) +
                  "\n\nDeplace/duplique les lights d'accent, ajuste dans l'Inspector,\npuis Ctrl+S.";
            EditorUtility.DisplayDialog("Hub 2D Lighting (VP3)", summary, "OK");
            Debug.Log("[HubLightingTool] " + summary);
        }

        private static void EnsureGlobalLight(Transform parent, List<string> actions)
        {
            if (FindChild(parent, "Global Light") != null) return;

            var go = new GameObject("Global Light");
            go.transform.SetParent(parent, false);
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.color = new Color(1f, 0.95f, 0.88f);
            light.intensity = 1f;
            light.blendStyleIndex = BlendMultiply;
            TargetAllSortingLayers(light);
            actions.Add("- Global Light 2D ajoutee (base, Multiply)");
        }

        private static void EnsurePointLight(Transform parent, string name, Color color,
            float intensity, float outerRadius, Vector3 position, List<string> actions)
        {
            if (FindChild(parent, name) != null) return;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.pointLightInnerRadius = outerRadius * 0.15f;
            light.pointLightOuterRadius = outerRadius;
            light.blendStyleIndex = BlendAdditive;
            TargetAllSortingLayers(light);
            actions.Add("- Point Light '" + name + "' ajoutee (Additive)");
        }

        /// <summary>Cible TOUS les sorting layers du projet (sinon la light n'eclaire qu'un sous-ensemble).</summary>
        private static void TargetAllSortingLayers(Light2D light)
        {
            var so = new SerializedObject(light);
            var prop = so.FindProperty("m_ApplyToSortingLayers");
            if (prop == null) return;
            var layers = SortingLayer.layers;
            prop.arraySize = layers.Length;
            for (int i = 0; i < layers.Length; i++)
                prop.GetArrayElementAtIndex(i).intValue = layers[i].id;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform FindChild(Transform parent, string name)
        {
            foreach (Transform c in parent)
                if (c.name == name) return c;
            return null;
        }

        private static Camera FindAnyCamera()
        {
            var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            return cams.Length > 0 ? cams[0] : null;
        }
    }
}
