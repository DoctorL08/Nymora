using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Applique le pack post-process (meme stack que Hub_PostFX + tes LUT) sur la map combat IA
    /// (30_CombatIA). Etape 1 du chantier "post-FX combat".
    ///
    /// - Clone Hub_PostFX -> Combat_PostFX (profil DEDIE combat : tu peux le tweaker / swap LUT
    ///   sans impacter le hub). Idempotent : si Combat_PostFX existe deja, on le reutilise.
    /// - Pose un Global Volume "PostFX Volume" dans la scene, sharedProfile = Combat_PostFX.
    /// - Active le post-processing sur la camera combat (UniversalAdditionalCameraData).
    ///
    /// Etape 2 (a venir) : injectable qui pose le MEME Volume + Combat_PostFX sur 33_CombatCasual
    /// et 40_CombatRanked1v1 -> comme c'est le meme profil, tout reglage fait ici se propage.
    ///
    /// 100% View. Ne touche pas au bootstrap (cf combat-scene-bootstrap-isolation) ->
    /// pas de bump CombatRulesVersion.
    ///
    /// Menu : Nymora > Setup > Setup Combat PostFX (IA).
    /// </summary>
    public static class CombatPostFXTool
    {
        private const string IaScenePath = "Assets/_Nymora/Scenes/30_CombatIA.unity";
        private const string ProfileDir = "Assets/_Nymora/Settings/PostProcessing";
        private const string HubProfilePath = ProfileDir + "/Hub_PostFX.asset";
        private const string CombatProfilePath = ProfileDir + "/Combat_PostFX.asset";
        private const string VolumeObjectName = "PostFX Volume";

        [MenuItem("Nymora/Setup/Setup Combat PostFX (IA)", priority = 73)]
        private static void Setup()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Combat PostFX", "Impossible pendant Play Mode.", "OK");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.path.EndsWith("30_CombatIA.unity"))
            {
                if (!EditorUtility.DisplayDialog("Combat PostFX",
                        $"Scene active : {scene.path}\nAttendu : {IaScenePath}\n\nOuvrir 30_CombatIA ?",
                        "Ouvrir", "Annuler"))
                    return;
                scene = EditorSceneManager.OpenScene(IaScenePath, OpenSceneMode.Single);
            }

            var actions = new List<string>();
            var profile = EnsureCombatProfile(actions);
            if (profile == null)
            {
                EditorUtility.DisplayDialog("Combat PostFX",
                    "Echec : " + string.Join("\n", actions), "OK");
                return;
            }
            EnsureVolume(profile, actions);
            EnableCameraPostProcessing(actions);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);

            string summary = actions.Count == 0
                ? "OK Rien a faire, deja en place."
                : "Combat PostFX (IA) applique :\n\n" + string.Join("\n", actions) +
                  "\n\nAjuste le rendu dans l'Inspector de Combat_PostFX (swap la LUT du Color Lookup " +
                  "entre Cinematic/Cold/Warm/Neutral), puis Ctrl+S. L'injectable propagera vers casual/ranked.";
            EditorUtility.DisplayDialog("Combat PostFX (IA)", summary, "OK");
            Debug.Log("[CombatPostFXTool] " + summary);
        }

        /// <summary>
        /// Propage le post-FX combat vers 33_CombatCasual + 40_CombatRanked1v1 : garantit le
        /// Global Volume -> Combat_PostFX ET surtout active le post-processing camera (le copier/
        /// coller manuel du Volume ne touchait pas la camera -> grading non applique -> teinte
        /// differente de l'IA). Ouvre/sauve chaque scene, puis rouvre 30_CombatIA.
        /// </summary>
        [MenuItem("Nymora/Setup/Propagate Combat PostFX (casual + ranked)", priority = 74)]
        private static void Propagate()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Propagate Combat PostFX", "Impossible pendant Play Mode.", "OK");
                return;
            }

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(CombatProfilePath);
            if (profile == null)
            {
                EditorUtility.DisplayDialog("Propagate Combat PostFX",
                    "Combat_PostFX introuvable. Lance d'abord 'Setup Combat PostFX (IA)'.", "OK");
                return;
            }

            string[] targets =
            {
                "Assets/_Nymora/Scenes/33_CombatCasual.unity",
                "Assets/_Nymora/Scenes/40_CombatRanked1v1.unity",
            };

            var report = new List<string>();
            foreach (var scenePath in targets)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var actions = new List<string>();
                EnsureVolume(profile, actions);
                EnableCameraPostProcessing(actions);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                report.Add($"{System.IO.Path.GetFileNameWithoutExtension(scenePath)} : " +
                           (actions.Count == 0 ? "deja OK" : string.Join(" / ", actions)));
            }

            // Rouvre l'IA pour ne pas laisser Lorenzo sur une autre scene.
            EditorSceneManager.OpenScene(IaScenePath, OpenSceneMode.Single);

            string summary = "Propagation post-FX combat :\n\n" + string.Join("\n", report) +
                             "\n\nLes 3 scenes combat ont desormais le meme grading (Combat_PostFX + post-process camera).";
            EditorUtility.DisplayDialog("Propagate Combat PostFX", summary, "OK");
            Debug.Log("[CombatPostFXTool] " + summary);
        }

        /// <summary>Combat_PostFX = clone de Hub_PostFX (meme stack + meme LUT au depart), reutilise si deja la.</summary>
        private static VolumeProfile EnsureCombatProfile(List<string> actions)
        {
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(CombatProfilePath);
            if (existing != null) return existing;

            var hub = AssetDatabase.LoadAssetAtPath<VolumeProfile>(HubProfilePath);
            if (hub == null)
            {
                actions.Add("!! Hub_PostFX introuvable (" + HubProfilePath + ") : lance d'abord les tools post-FX du hub.");
                return null;
            }
            if (!AssetDatabase.CopyAsset(HubProfilePath, CombatProfilePath))
            {
                actions.Add("!! Clone Hub_PostFX -> Combat_PostFX echoue.");
                return null;
            }
            AssetDatabase.ImportAsset(CombatProfilePath);
            actions.Add("- Combat_PostFX cree (clone de Hub_PostFX) : " + CombatProfilePath);
            return AssetDatabase.LoadAssetAtPath<VolumeProfile>(CombatProfilePath);
        }

        private static void EnsureVolume(VolumeProfile profile, List<string> actions)
        {
            Volume volume = null;
            foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (v.gameObject.name == VolumeObjectName) { volume = v; break; }
            }

            if (volume == null)
            {
                var go = new GameObject(VolumeObjectName);
                volume = go.AddComponent<Volume>();
                actions.Add("- GameObject '" + VolumeObjectName + "' cree (Global Volume)");
            }

            volume.isGlobal = true;
            volume.priority = 1f;
            volume.weight = 1f;
            volume.sharedProfile = profile;
            EditorUtility.SetDirty(volume);
        }

        private static void EnableCameraPostProcessing(List<string> actions)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
                if (cams.Length > 0) cam = cams[0];
            }
            if (cam == null)
            {
                actions.Add("- !! Aucune camera trouvee : active 'Post Processing' a la main sur la camera combat.");
                return;
            }

            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null && !data.renderPostProcessing)
            {
                data.renderPostProcessing = true;
                EditorUtility.SetDirty(data);
                actions.Add("- Post-processing active sur la camera '" + cam.name + "'");
            }
            else
            {
                actions.Add("- Post-processing deja actif sur la camera '" + cam.name + "'");
            }
        }
    }
}
