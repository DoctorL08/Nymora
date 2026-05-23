using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Brique VP.1 — Passe "polish visuel" du hub (View-side only, aucun bump CombatRulesVersion).
    ///
    /// Cree/charge un Volume Profile (Hub_PostFX.asset), pose un Global Volume dans
    /// 10_CommunityHub et active le post-process sur la camera. Regle un stack de base :
    /// Tonemapping (Neutral) + Color Adjustments + White Balance + Split Toning (colorimetrie
    /// ombres froides / lumieres chaudes) + Vignette + Bloom (seuil haut, ne bave que sur les
    /// zones lumineuses). Le LUT (Color Lookup) arrive en VP.2.
    ///
    /// Idempotent : reutilise le profil + le Volume existants, ne fait que (re)cabler.
    /// Les valeurs ci-dessous sont des SEED ; la source de verite reste l'asset, ajustable
    /// dans l'Inspector du Volume Profile.
    ///
    /// Menu : Nymora > Setup > Setup Hub Visual Polish (VP1).
    /// </summary>
    public static class HubVisualPolishTool
    {
        private const string ScenePath = "Assets/_Nymora/Scenes/10_CommunityHub.unity";
        private const string ProfileDir = "Assets/_Nymora/Settings/PostProcessing";
        private const string ProfilePath = ProfileDir + "/Hub_PostFX.asset";
        private const string VolumeObjectName = "PostFX Volume";

        [MenuItem("Nymora/Setup/Setup Hub Visual Polish (VP1)", priority = 60)]
        private static void Setup()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Hub Visual Polish", "Impossible pendant Play Mode.", "OK");
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.path.EndsWith("10_CommunityHub.unity"))
            {
                if (!EditorUtility.DisplayDialog("Hub Visual Polish",
                        $"Scene active : {scene.path}\nAttendu : {ScenePath}\n\nOuvrir 10_CommunityHub ?",
                        "Ouvrir", "Annuler"))
                    return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var actions = new List<string>();

            var profile = EnsureProfile(actions);
            ConfigureProfile(profile, actions);
            EnsureVolume(profile, actions);
            EnableCameraPostProcessing(actions);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);

            string summary = actions.Count == 0
                ? "OK Rien a faire, deja en place."
                : "VP.1 applique :\n\n" + string.Join("\n", actions) +
                  "\n\nAjuste le rendu dans l'Inspector du Volume Profile (Hub_PostFX),\npuis Ctrl+S pour sauver la scene.";
            EditorUtility.DisplayDialog("Hub Visual Polish (VP1)", summary, "OK");
            Debug.Log("[HubVisualPolishTool] " + summary);
        }

        private static VolumeProfile EnsureProfile(List<string> actions)
        {
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder(ProfileDir))
            {
                AssetDatabase.CreateFolder("Assets/_Nymora/Settings", "PostProcessing");
                actions.Add("- Dossier cree : " + ProfileDir);
            }

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            actions.Add("- Volume Profile cree : " + ProfilePath);
            return profile;
        }

        private static void ConfigureProfile(VolumeProfile profile, List<string> actions)
        {
            // Tonemapping : base filmique. Neutral plutot qu'ACES (ACES ecrase les couleurs
            // saturees du pixel art).
            var tone = GetOrAdd<Tonemapping>(profile);
            tone.mode.overrideState = true;
            tone.mode.value = TonemappingMode.Neutral;

            // Color Adjustments : contraste/saturation legers.
            var color = GetOrAdd<ColorAdjustments>(profile);
            color.postExposure.overrideState = true; color.postExposure.value = 0f;
            color.contrast.overrideState = true;     color.contrast.value = 8f;
            color.saturation.overrideState = true;   color.saturation.value = 4f;
            color.colorFilter.overrideState = true;  color.colorFilter.value = Color.white;

            // White Balance : tres legere chaleur globale.
            var wb = GetOrAdd<WhiteBalance>(profile);
            wb.temperature.overrideState = true; wb.temperature.value = 4f;
            wb.tint.overrideState = true;        wb.tint.value = 0f;

            // Split Toning : colorimetrie cinema -> ombres froides bleutees, lumieres chaudes.
            var split = GetOrAdd<SplitToning>(profile);
            split.shadows.overrideState = true;    split.shadows.value = new Color(0.20f, 0.31f, 0.47f, 1f);
            split.highlights.overrideState = true; split.highlights.value = new Color(0.78f, 0.63f, 0.43f, 1f);
            split.balance.overrideState = true;    split.balance.value = 0f;

            // Vignette : focalise le regard, douce.
            var vig = GetOrAdd<Vignette>(profile);
            vig.intensity.overrideState = true;  vig.intensity.value = 0.26f;
            vig.smoothness.overrideState = true; vig.smoothness.value = 0.45f;
            vig.color.overrideState = true;      vig.color.value = Color.black;

            // Bloom : seuil haut pour ne faire rayonner que les zones lumineuses
            // (torches, halos, UI brillante) sans baver sur tout le pixel art.
            var bloom = GetOrAdd<Bloom>(profile);
            bloom.threshold.overrideState = true; bloom.threshold.value = 0.95f;
            bloom.intensity.overrideState = true; bloom.intensity.value = 0.55f;
            bloom.scatter.overrideState = true;   bloom.scatter.value = 0.6f;

            actions.Add("- Overrides configures : Tonemapping, Color Adjustments, White Balance, Split Toning, Vignette, Bloom");
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var comp)) return comp;
            comp = profile.Add<T>(false);
            AssetDatabase.AddObjectToAsset(comp, profile);
            return comp;
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
                actions.Add("- !! Aucune camera trouvee : active 'Post Processing' a la main sur la camera du hub.");
                return;
            }

            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null && !data.renderPostProcessing)
            {
                data.renderPostProcessing = true;
                EditorUtility.SetDirty(data);
                actions.Add("- Post-processing active sur la camera '" + cam.name + "'");
            }
        }
    }
}
