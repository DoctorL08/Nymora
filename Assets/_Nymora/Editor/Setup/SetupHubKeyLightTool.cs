using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// GFX — Relief plein écran via normal maps dans le hub. 100% View (aucun bump).
    ///
    /// Deux actions, idempotentes et non destructives sur les valeurs existantes :
    /// 1. ACTIVE les normal maps sur toutes les Light2D Point de la scène (torches) — par défaut
    ///    elles sont sur Quality=Disabled (m_NormalMapQuality=2) donc la normal map est ignorée.
    /// 2. CRÉE une "HubKeyLight" : une Light2D Point large, douce, qui utilise les normales et couvre
    ///    toute la scène -> la normal map de la map (générée par NormalMapGeneratorTool) sculpte le
    ///    relief PARTOUT, pas seulement autour des torches (la lumière GLOBALE 2D, elle, ignore les
    ///    normales en URP).
    ///
    /// Valeurs = SEED, à calibrer à l'œil dans l'Inspector du HubKeyLight :
    /// - Intensity : dose le fill / relief.
    /// - Transform Y : monte la light pour un éclairage plus rasant "par le haut".
    /// - Normal Maps > Distance : profondeur apparente du relief.
    /// - Inner/Outer Radius : couverture.
    ///
    /// Menu : Nymora > Setup > Setup Hub Key Light (relief).
    /// </summary>
    public static class SetupHubKeyLightTool
    {
        private const string ScenePath = "Assets/_Nymora/Scenes/10_CommunityHub.unity";
        private const string KeyLightName = "HubKeyLight";
        private const int BlendAdditive = 1; // cf HubLightingTool : 0=Multiply, 1=Additive
        private const int QualityAccurate = 1; // enum URP NormalMapQuality : Fast=0, Accurate=1, Disabled=2

        [MenuItem("Nymora/Setup/Setup Hub Key Light (relief)", priority = 62)]
        private static void Setup()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Hub Key Light", "Impossible pendant Play Mode.", "OK");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.path.EndsWith("10_CommunityHub.unity"))
            {
                if (!EditorUtility.DisplayDialog("Hub Key Light",
                        $"Scene active : {scene.path}\nAttendu : {ScenePath}\n\nOuvrir 10_CommunityHub ?",
                        "Ouvrir", "Annuler"))
                    return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var actions = new List<string>();

            // 1. Active les normales sur toutes les Point lights existantes (torches).
            int enabled = 0;
            var lights = Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None);
            foreach (var l in lights)
            {
                if (l == null || l.lightType != Light2D.LightType.Point) continue;
                if (l.gameObject.name == KeyLightName) continue;
                if (EnableNormalMaps(l)) enabled++;
            }
            if (enabled > 0) actions.Add($"- Normal maps activées (Accurate) sur {enabled} Point light(s) / torches");

            // 2. Crée la key light de relief si absente.
            Vector3 center = Vector3.zero;
            var cam = Camera.main ?? FindAnyCamera();
            if (cam != null) center = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);

            if (GameObject.Find(KeyLightName) == null)
            {
                var go = new GameObject(KeyLightName);
                // Légèrement au-dessus du centre -> éclairage un peu rasant "par le haut".
                go.transform.position = center + new Vector3(0f, 8f, 0f);

                var light = go.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Point;
                light.color = new Color(0.88f, 0.91f, 1f); // moonlight neutre/légèrement froid
                // INTENSITÉ FAIBLE : un fill additif blanc trop fort lave la couleur de la map
                // (tout devient gris). On veut juste assez de lumière pour révéler le relief.
                light.intensity = 0.12f;
                light.pointLightInnerRadius = 30f;  // cœur large -> fill quasi uniforme sur la map
                light.pointLightOuterRadius = 60f;  // couvre toute la scène
                light.blendStyleIndex = BlendAdditive;
                EnableNormalMaps(light);
                SetNormalMapDistance(light, 6f);    // distance haute = relief plus DOUX (moins embossé)
                SetFalloff(light, 0.3f);
                TargetAllSortingLayers(light);

                Undo.RegisterCreatedObjectUndo(go, "Create HubKeyLight");
                actions.Add("- 'HubKeyLight' créée (Point, Additive, normales ON, couvre la scène)");
            }

            EditorSceneManager.MarkSceneDirty(scene);

            string summary = actions.Count == 0
                ? "OK Rien à faire, déjà en place."
                : "Relief plein écran appliqué :\n\n" + string.Join("\n", actions) +
                  "\n\nCalibre HubKeyLight dans l'Inspector (Intensity, Transform Y, Normal Maps > Distance, " +
                  "Radius) jusqu'au rendu voulu, puis Ctrl+S.";
            EditorUtility.DisplayDialog("Hub Key Light (relief)", summary, "OK");
            Debug.Log("[SetupHubKeyLightTool] " + summary);
        }

        // Active les normal maps sur une Light2D via SerializedObject (champs non publics).
        // Retourne true si un changement a été appliqué.
        private static bool EnableNormalMaps(Light2D light)
        {
            var so = new SerializedObject(light);
            bool changed = false;

            var quality = so.FindProperty("m_NormalMapQuality");
            if (quality != null && quality.intValue != QualityAccurate)
            {
                quality.intValue = QualityAccurate;
                changed = true;
            }
            // Champ legacy gardé pour migration : on le met aussi à 1 par sécurité s'il existe.
            var use = so.FindProperty("m_UseNormalMap");
            if (use != null && use.boolValue != true)
            {
                use.boolValue = true;
                changed = true;
            }
            if (changed) so.ApplyModifiedProperties();
            return changed;
        }

        private static void SetFalloff(Light2D light, float value)
        {
            var so = new SerializedObject(light);
            var p = so.FindProperty("m_FalloffIntensity");
            if (p != null) { p.floatValue = value; so.ApplyModifiedProperties(); }
        }

        // Distance simulée de la light au-dessus de la surface : PLUS HAUT = relief plus doux/plat.
        private static void SetNormalMapDistance(Light2D light, float value)
        {
            var so = new SerializedObject(light);
            var p = so.FindProperty("m_NormalMapDistance");
            if (p != null) { p.floatValue = value; so.ApplyModifiedProperties(); }
        }

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

        private static Camera FindAnyCamera()
        {
            var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            return cams.Length > 0 ? cams[0] : null;
        }
    }
}
