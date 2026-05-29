using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Applique la DA du chat hub (HubChatRestyleTool) au chat des 3 scènes de COMBAT
    /// (30_CombatIA / 33_CombatCasual / 40_CombatRanked1v1), pour qu'il soit EXACTEMENT identique
    /// au chat du hub. Le chat combat est une instance du MÊME prefab (ChatPanel.prefab + HubChatUI),
    /// donc on réutilise tel quel la logique de restyle (police Ari, palette monochrome HubMenuTheme,
    /// fonds arrondis, onglets thémés + onglet Clan).
    ///
    /// Ouvre chaque scène, restyle, sauvegarde. Réversible par scène via Ctrl+Z (mais le tool
    /// sauvegarde directement — fais un commit avant si tu veux un filet).
    ///
    /// Menu : Nymora > Setup > UI Menu > Restyle Chat — Combat Scenes.
    /// </summary>
    public static class RestyleCombatChatsTool
    {
        private static readonly string[] CombatSceneNames =
        {
            "30_CombatIA",
            "33_CombatCasual",
            "40_CombatRanked1v1",
        };

        [MenuItem("Nymora/Setup/UI Menu/Restyle Chat — Combat Scenes")]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!EditorUtility.DisplayDialog("Restyle Chat — Combat",
                    "Ouvre et MODIFIE (sauvegarde) les 3 scènes de combat pour aligner leur chat sur la DA du hub.\n\nContinuer ?",
                    "Restyler + sauvegarder", "Annuler"))
                return;

            var report = new List<string>();
            foreach (var name in CombatSceneNames)
            {
                string path = ResolveScenePath(name);
                if (string.IsNullOrEmpty(path))
                {
                    report.Add($"⚠ {name} : scène introuvable");
                    continue;
                }

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                if (!scene.IsValid())
                {
                    report.Add($"⚠ {name} : ouverture KO");
                    continue;
                }

                // Réutilise exactement la logique du restyle hub (agit sur la scène active).
                HubChatRestyleTool.Restyle();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                report.Add($"✓ {name} : chat restylé + sauvegardé");
            }

            Debug.Log("[RestyleCombatChats]\n" + string.Join("\n", report));
            EditorUtility.DisplayDialog("Restyle Chat — Combat", string.Join("\n", report), "OK");
        }

        // Résout le chemin d'une scène par nom EXACT (évite les variantes type *_BACKUP).
        private static string ResolveScenePath(string sceneName)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Scene " + sceneName))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == sceneName) return path;
            }
            return null;
        }
    }
}
