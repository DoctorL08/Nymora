using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Fix warning spam "Particle Velocity curves must all be in the same mode" (23 mai).
    /// Les TorchParticles du pack d'eclairage ont, dans Velocity over Lifetime, l'axe Y en
    /// TwoConstants (montee aleatoire des etincelles) mais X/Z en Constant -> Unity exige que
    /// les 3 axes partagent le MEME mode et logue a chaque frame.
    ///
    /// Ce tool harmonise X/Y/Z en TwoConstants en PRESERVANT les valeurs (un axe Constant c
    /// devient TwoConstants (c, c) -> comportement identique). Ne touche que les ParticleSystem
    /// dont le module velocityOverLifetime est actif ET en modes mixtes Constant/TwoConstants.
    /// Les axes en mode Curve/TwoCurves sont laisses tels quels (PS signale, non modifie).
    ///
    /// Menu : Nymora > Setup > Fix Particle Velocity Modes.
    /// </summary>
    public static class HubParticleVelocityFixTool
    {
        [MenuItem("Nymora/Setup/Fix Particle Velocity Modes", priority = 71)]
        private static void Fix()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Fix Particle Velocity Modes", "Impossible pendant Play Mode.", "OK");
                return;
            }

            int fixedCount = 0;
            var skipped = new List<string>();
            foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            {
                var vol = ps.velocityOverLifetime;
                if (!vol.enabled) continue;

                var modes = new[] { vol.x.mode, vol.y.mode, vol.z.mode };
                bool sameMode = modes[0] == modes[1] && modes[1] == modes[2];
                if (sameMode) continue;

                // On ne sait convertir proprement que Constant <-> TwoConstants.
                bool hasCurve = false;
                foreach (var m in modes)
                    if (m == ParticleSystemCurveMode.Curve || m == ParticleSystemCurveMode.TwoCurves) hasCurve = true;
                if (hasCurve) { skipped.Add(ps.gameObject.name); continue; }

                vol.x = ToTwoConstants(vol.x);
                vol.y = ToTwoConstants(vol.y);
                vol.z = ToTwoConstants(vol.z);
                EditorUtility.SetDirty(ps);
                fixedCount++;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);

            string summary = $"{fixedCount} ParticleSystem harmonises (Velocity X/Y/Z -> TwoConstants, valeurs preservees).";
            if (skipped.Count > 0)
                summary += $"\nIgnores (axes en mode Curve, a regler a la main) : {string.Join(", ", skipped)}.";
            if (fixedCount == 0 && skipped.Count == 0)
                summary = "Aucun ParticleSystem en mode mixte trouve (rien a faire).";
            summary += "\nCtrl+S pour sauver.";
            EditorUtility.DisplayDialog("Fix Particle Velocity Modes", summary, "OK");
            Debug.Log("[HubParticleVelocityFixTool] " + summary);
        }

        private static ParticleSystem.MinMaxCurve ToTwoConstants(ParticleSystem.MinMaxCurve c)
        {
            float min, max;
            if (c.mode == ParticleSystemCurveMode.TwoConstants) { min = c.constantMin; max = c.constantMax; }
            else { min = c.constant; max = c.constant; } // Constant -> (c, c)
            return new ParticleSystem.MinMaxCurve { mode = ParticleSystemCurveMode.TwoConstants, constantMin = min, constantMax = max };
        }
    }
}
