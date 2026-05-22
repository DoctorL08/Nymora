using System.IO;
using System.Linq;
using Nymora.Combat.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Brique 6.2.b — Configure le bootstrap de la scene ranked.
    ///
    /// La scene 40_CombatRanked1v1 est un CLONE de 33_CombatCasual, donc son CombatBootstrapCasual
    /// a son champ _expectedSceneName a "33_CombatCasual" (default). Du coup la garde de scene
    /// le ferait NO-OP dans la scene ranked. Ce tool le passe a "40_CombatRanked1v1".
    ///
    /// Idempotent : si deja a la bonne valeur, ne fait rien. Rejouable.
    ///
    /// Menu : Nymora > Setup > Setup Ranked Scene Bootstrap.
    /// </summary>
    public static class SetupRankedSceneBootstrapTool
    {
        private const string ScenePath = "Assets/_Nymora/Scenes/40_CombatRanked1v1.unity";
        private const string ExpectedSceneName = "40_CombatRanked1v1";

        [MenuItem("Nymora/Setup/Setup Ranked Scene Bootstrap", priority = 22)]
        private static void Run()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Setup Ranked Bootstrap", "Impossible pendant Play Mode.", "OK");
                return;
            }
            if (!File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("Setup Ranked Bootstrap",
                    $"Scene introuvable : {ScenePath}\n\nLance d'abord 'Clone CombatCasual to Ranked1v1 Scene'.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var bootstrap = Object.FindObjectsByType<CombatBootstrapCasual>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault();
            if (bootstrap == null)
            {
                EditorUtility.DisplayDialog("Setup Ranked Bootstrap",
                    "Aucun CombatBootstrapCasual dans la scene ranked.\n\n" +
                    "Verifie que la scene a bien ete clonee depuis 33_CombatCasual (qui contient ce bootstrap).", "OK");
                return;
            }

            var so = new SerializedObject(bootstrap);
            var prop = so.FindProperty("_expectedSceneName");
            if (prop == null)
            {
                EditorUtility.DisplayDialog("Setup Ranked Bootstrap",
                    "Champ '_expectedSceneName' introuvable sur CombatBootstrapCasual (recompile ?).", "OK");
                return;
            }

            if (prop.stringValue == ExpectedSceneName)
            {
                EditorUtility.DisplayDialog("Setup Ranked Bootstrap",
                    $"Deja configure : _expectedSceneName = '{ExpectedSceneName}'. Rien a faire.", "OK");
                return;
            }

            prop.stringValue = ExpectedSceneName;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[Nymora.6.2b] CombatBootstrapCasual._expectedSceneName = '{ExpectedSceneName}' dans {ScenePath}.");
            EditorUtility.DisplayDialog("Setup Ranked Bootstrap",
                $"OK : le bootstrap de 40_CombatRanked1v1 cible maintenant '{ExpectedSceneName}'.\n\n" +
                "La scene ranked peut desormais demarrer un match Quantum online.", "OK");
        }
    }
}
