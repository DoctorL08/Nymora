using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// POLISH KYAMI — fix éclairage des normals sur les PERSONNAGES. Diagnostic : les Point Light2D
    /// des scènes combat + hub ne ciblaient que le sorting layer « Default » (sol/torches), alors que
    /// les combattants sont sur « Personnages » — qui n'était éclairé QUE par le Global plat. Résultat :
    /// les normals des persos ne pouvaient pas ressortir (map/torches OK, persos plats).
    ///
    /// Ce tool ajoute le layer « Personnages » au <c>m_ApplyToSortingLayers</c> de CHAQUE Point Light2D
    /// des 3 scènes combat + du hub (idempotent : ne double pas si déjà présent). On ne touche à RIEN
    /// d'autre (intensité, couleur, rayon, Global). Réversible : il suffira de retirer le layer.
    ///
    /// ⚠️ Modifie des valeurs de scène (lights) → relancer un rebuild standalone avant test multi/ranked,
    /// mais aucune logique sim touchée (pas de bump CombatRulesVersion).
    ///
    /// Menu : Nymora > Setup > Polish Kyami > Add 'Personnages' to Point Lights (combat + hub).
    /// </summary>
    public static class AddPersonnagesToPointLightsTool
    {
        private static readonly string[] Scenes =
        {
            "Assets/_Nymora/Scenes/33_CombatCasual.unity",
            "Assets/_Nymora/Scenes/30_CombatIA.unity",
            "Assets/_Nymora/Scenes/40_CombatRanked1v1.unity",
            "Assets/_Nymora/Scenes/10_CommunityHub.unity",
        };
        private const string LayerName = "Personnages";

        [MenuItem("Nymora/Setup/Polish Kyami/Add 'Personnages' to Point Lights (combat + hub)", priority = 65)]
        public static void Run()
        {
            if (!SortingLayer.layers.Any(l => l.name == LayerName))
            {
                EditorUtility.DisplayDialog("Add Personnages to Lights",
                    $"Sorting layer « {LayerName} » introuvable dans le projet.", "OK");
                return;
            }
            int layerId = SortingLayer.NameToID(LayerName);

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var report = new List<string>();
            foreach (var scenePath in Scenes)
            {
                if (System.IO.File.Exists(scenePath) == false)
                {
                    report.Add($"⚠ scène absente : {scenePath}");
                    continue;
                }

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var lights = Object.FindObjectsOfType<Light2D>(true);
                int point = 0, changed = 0;

                foreach (var light in lights)
                {
                    if (light.lightType != Light2D.LightType.Point) continue;
                    point++;

                    var so = new SerializedObject(light);
                    var p = so.FindProperty("m_ApplyToSortingLayers");
                    if (p == null || !p.isArray) continue;

                    bool has = false;
                    for (int i = 0; i < p.arraySize; i++)
                        if (p.GetArrayElementAtIndex(i).intValue == layerId) { has = true; break; }
                    if (has) continue;

                    p.arraySize += 1;
                    p.GetArrayElementAtIndex(p.arraySize - 1).intValue = layerId;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changed++;
                }

                if (changed > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                report.Add($"{System.IO.Path.GetFileName(scenePath)} : +Personnages sur {changed}/{point} Point light(s)");
            }

            Debug.Log("[AddPersonnagesToPointLights]\n" + string.Join("\n", report));
            EditorUtility.DisplayDialog("Add Personnages to Lights",
                string.Join("\n", report) +
                "\n\nLes Point lights éclairent maintenant les persos → leurs normals ressortent.\n" +
                "Si le relief reste trop discret, le levier suivant = monter l'intensité de la Point light large (ton choix).",
                "OK");
        }
    }
}
