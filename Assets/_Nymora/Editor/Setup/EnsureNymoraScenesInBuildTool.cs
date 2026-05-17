using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Garde-fou : ajoute les scenes Nymora critiques dans EditorBuildSettings.scenes
    /// si elles ne sont pas deja presentes (sinon SceneManager.LoadScene fail).
    ///
    /// Menu : Nymora > Setup > Ensure Nymora Scenes In Build.
    /// </summary>
    public static class EnsureNymoraScenesInBuildTool
    {
        private static readonly string[] _requiredScenes =
        {
            "Assets/_Nymora/Scenes/00_Login.unity",
            "Assets/_Nymora/Scenes/01_MainMenu.unity",
            "Assets/_Nymora/Scenes/10_CommunityHub.unity",
            "Assets/_Nymora/Scenes/30_CombatIA.unity",
            "Assets/_Nymora/Scenes/33_CombatCasual.unity",
        };

        [MenuItem("Nymora/Setup/Ensure Nymora Scenes In Build", priority = 10)]
        private static void Run()
        {
            var current = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            var existingPaths = new HashSet<string>();
            foreach (var s in current) existingPaths.Add(s.path);

            int added = 0;
            foreach (var path in _requiredScenes)
            {
                if (!System.IO.File.Exists(path))
                {
                    Debug.LogWarning($"[Nymora.Setup] Scene introuvable : {path} — skip.");
                    continue;
                }
                if (existingPaths.Contains(path)) continue;
                current.Add(new EditorBuildSettingsScene(path, enabled: true));
                added++;
                Debug.Log($"[Nymora.Setup] Ajout {path} aux Build Settings.");
            }

            EditorBuildSettings.scenes = current.ToArray();
            Debug.Log($"[Nymora.Setup] EnsureNymoraScenesInBuild : {added} scene(s) ajoutee(s). Total = {current.Count}.");
            EditorUtility.DisplayDialog("Ensure Scenes",
                added == 0 ? "OK : toutes les scenes deja presentes." : $"{added} scene(s) ajoutee(s) aux Build Settings.", "OK");
        }
    }
}
