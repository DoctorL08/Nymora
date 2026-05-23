using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Calme la fumee des torches (23 mai) — les ParticleSystem "Smoke" avaient une velocite
    /// horizontale aleatoire (X en TwoConstants -0.06..0.06) qui faisait deriver la fumee dans
    /// tous les sens. Une fumee de torche doit monter droit.
    ///
    /// Ce tool met X et Z a 0 (TwoConstants 0,0 pour rester dans le meme mode que Y et ne pas
    /// re-declencher le warning "curves must all be in the same mode") sur les PS nommes "Smoke".
    /// PRESERVE Y (la montee) et tout le reste. Cible par nom exact "Smoke".
    ///
    /// Menu : Nymora > Setup > Calm Smoke Particles.
    /// </summary>
    public static class HubSmokeCalmTool
    {
        [MenuItem("Nymora/Setup/Calm Smoke Particles", priority = 72)]
        private static void Calm()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Calm Smoke Particles", "Impossible pendant Play Mode.", "OK");
                return;
            }

            var zero = new ParticleSystem.MinMaxCurve
            {
                mode = ParticleSystemCurveMode.TwoConstants,
                constantMin = 0f,
                constantMax = 0f,
            };

            int count = 0;
            foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            {
                if (ps.gameObject.name != "Smoke") continue;
                var vol = ps.velocityOverLifetime;
                if (!vol.enabled) continue;
                vol.x = zero;
                vol.z = zero;
                EditorUtility.SetDirty(ps);
                count++;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);

            string summary = count == 0
                ? "Aucun ParticleSystem 'Smoke' avec Velocity over Lifetime actif trouve."
                : $"{count} fumees calmees (Velocity X/Z -> 0, montee Y preservee). Ctrl+S pour sauver.";
            EditorUtility.DisplayDialog("Calm Smoke Particles", summary, "OK");
            Debug.Log("[HubSmokeCalmTool] " + summary);
        }
    }
}
