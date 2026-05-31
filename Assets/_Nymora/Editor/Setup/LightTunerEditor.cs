using Nymora.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// POLISH KYAMI — pilote le <see cref="LightTuner"/> : menu pour le spawn en Play Mode, et
    /// applicateur qui réécrit les valeurs sauvées dans la scène à la SORTIE du Play Mode (les modifs
    /// de Play Mode étant sinon perdues). C'est le seul moment où des valeurs de light sont écrites
    /// sur disque — uniquement celles que Lorenzo a validées via [S].
    /// </summary>
    [InitializeOnLoad]
    public static class LightTunerEditor
    {
        static LightTunerEditor()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Nymora/Setup/Polish Kyami/Spawn Light Tuner (Play Mode)", priority = 72)]
        public static void Toggle()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Light Tuner",
                    "Entre d'abord en Play Mode (hub ou combat), puis relance ce menu.", "OK");
                return;
            }
            var existing = Object.FindObjectOfType<LightTuner>();
            if (existing != null) { Object.Destroy(existing.gameObject); return; }
            var go = new GameObject("~LightTuner");
            go.AddComponent<LightTuner>();
            Debug.Log("[LightTuner] Spawné — règle les sliders, [S] pour sauver.");
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;

            string json = SessionState.GetString(LightTuner.PendingKey, "");
            if (string.IsNullOrEmpty(json)) return;
            SessionState.EraseString(LightTuner.PendingKey);

            var buf = JsonUtility.FromJson<LightTuner.Buffer>(json);
            if (buf == null || buf.items == null || buf.items.Count == 0) return;

            // Index des lights de la scène active par chemin de hiérarchie.
            var byPath = new System.Collections.Generic.Dictionary<string, Light2D>();
            foreach (var l in Object.FindObjectsOfType<Light2D>(true))
                byPath[LightTuner.PathOf(l.transform)] = l;

            int applied = 0;
            foreach (var e in buf.items)
            {
                if (!byPath.TryGetValue(e.path, out var l) || l == null) continue;
                l.intensity = e.intensity;
                if (!e.isGlobal && e.radius > 0f) l.pointLightOuterRadius = e.radius;
                LightTuner.SetNormalDist(l, e.normalDist);
                EditorUtility.SetDirty(l);
                applied++;
            }

            if (applied > 0)
            {
                for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetSceneAt(i));
                EditorSceneManager.SaveOpenScenes();
            }

            Debug.Log($"[LightTuner] {applied}/{buf.items.Count} lights appliquées à la scène et sauvegardées.");
        }
    }
}
