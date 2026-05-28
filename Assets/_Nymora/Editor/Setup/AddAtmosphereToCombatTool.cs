using System.Collections.Generic;
using Nymora.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// GFX — Ajoute la couche d'atmosphère (HubAtmosphere) aux scènes de combat, REFLETS OFF.
    ///
    /// La colorimétrie (post-process) s'applique déjà au combat via le Volume de la scène + le profil
    /// graphique. Cet outil ajoute en plus le fog / lucioles / poussières. Halos, ombres de contact
    /// et reflets dépendent des torches/props iso du hub (absents en combat) -> seuls fog/lucioles/
    /// poussières apparaîtront en pratique. Reflets explicitement désactivés (demande de Lorenzo).
    ///
    /// Le composant est posé dans la SCÈNE (HubAtmosphere vit dans Nymora.Hub, que Nymora.Combat ne
    /// référence pas — on ne peut donc pas l'ajouter par code côté bootstrap combat). Tu peux décocher
    /// des couches dans l'Inspector du GameObject "CombatAtmosphere" si une ne va pas en combat.
    ///
    /// Idempotent. Menu : Nymora > Setup > Add Atmosphere to Combat Scenes.
    /// </summary>
    public static class AddAtmosphereToCombatTool
    {
        private static readonly string[] CombatScenes =
        {
            "Assets/_Nymora/Scenes/30_CombatIA.unity",
            "Assets/_Nymora/Scenes/33_CombatCasual.unity",
            "Assets/_Nymora/Scenes/40_CombatRanked1v1.unity",
        };
        private const string GoName = "CombatAtmosphere";

        [MenuItem("Nymora/Setup/Add Atmosphere to Combat Scenes", priority = 64)]
        private static void Run()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Combat Atmosphere", "Impossible pendant Play Mode.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            string originalScene = SceneManager.GetActiveScene().path;
            var log = new List<string>();

            foreach (var path in CombatScenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    log.Add($"- {System.IO.Path.GetFileName(path)} : introuvable, skip");
                    continue;
                }

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                if (GameObject.Find(GoName) != null)
                {
                    log.Add($"- {scene.name} : déjà présent");
                    continue;
                }

                var go = new GameObject(GoName);
                var atmo = go.AddComponent<HubAtmosphere>();
                var so = new SerializedObject(atmo);
                var refl = so.FindProperty("_enableReflections");
                if (refl != null) refl.boolValue = false; // reflets OFF en combat
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                log.Add($"- {scene.name} : atmosphère ajoutée (reflets OFF)");
            }

            // Revenir à la scène d'origine si possible.
            if (!string.IsNullOrEmpty(originalScene) &&
                AssetDatabase.LoadAssetAtPath<SceneAsset>(originalScene) != null)
                EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);

            string summary = "Atmosphère combat :\n\n" + string.Join("\n", log) +
                             "\n\nDécoche des couches dans l'Inspector de 'CombatAtmosphere' si besoin.";
            EditorUtility.DisplayDialog("Combat Atmosphere", summary, "OK");
            Debug.Log("[AddAtmosphereToCombatTool] " + summary);
        }
    }
}
