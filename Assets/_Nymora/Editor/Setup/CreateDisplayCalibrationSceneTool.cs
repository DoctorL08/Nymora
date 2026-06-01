#if UNITY_EDITOR
using System.IO;
using Nymora.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nymora.EditorTools.Setup
{
    /// <summary>
    /// Tuto Brique C — Génère la scène de CALIBRATION D'AFFICHAGE en CLONANT 10_CommunityHub puis en
    /// l'épurant de tout ce qui démarre Photon/backend. Résultat : même décor / lights 2D / post-process
    /// que le hub, mais SANS réseau, avec un GameObject portant <see cref="DisplayCalibrationController"/>.
    ///
    /// Le clonage exact garantit le rendu identique au hub (choix Lorenzo). L'épuration retire les
    /// composants connus qui démarrent un NetworkRunner / le backend / les menus. La partie VISUELLE
    /// (frames Soulrender, position, caméra) reste à câbler à la main dans la scène générée.
    ///
    /// Accès : Nymora > Setup > Create Display Calibration Scene.
    /// </summary>
    public static class CreateDisplayCalibrationSceneTool
    {
        private const string HubScenePath = "Assets/_Nymora/Scenes/10_CommunityHub.unity";
        private const string TargetScenePath = "Assets/_Nymora/Scenes/05_DisplayCalibration.unity";

        // Types de composants à NEUTRALISER dans le clone (démarrent Photon/backend/menus). On retire
        // le GameObject entier s'il ne porte QUE de la logique réseau, sinon on désactive le composant.
        // Recherche par nom de type (pas de hard ref à tous, certains sont sealed internal côté Fusion).
        private static readonly string[] DisableComponentTypeNames =
        {
            "Nymora.Hub.HubBootstrap",
            "Nymora.Hub.HubChatClient",
            "Nymora.Hub.HubChatUI",
            "Nymora.Hub.HubTutorialOnboarding",
            "Nymora.Hub.HubInputController",
            "Nymora.Hub.HubMovementController",
            "Nymora.Hub.Menu.HubMenuShell",
            "Nymora.Hub.HubArenaPanel",
            "Nymora.Hub.HubChatBubbleRouter",
            "Nymora.Hub.HubCamera", // caméra statique : on fige sa position (Lorenzo la recalera)
            "Fusion.NetworkRunner",
        };

        [MenuItem("Nymora/Setup/Create Display Calibration Scene")]
        public static void Create()
        {
            if (!File.Exists(HubScenePath))
            {
                EditorUtility.DisplayDialog("Calibration", $"Scène hub introuvable :\n{HubScenePath}", "OK");
                return;
            }

            if (File.Exists(TargetScenePath))
            {
                if (!EditorUtility.DisplayDialog("Calibration",
                        $"{TargetScenePath} existe déjà. La régénérer écrasera tes réglages visuels.\nContinuer ?",
                        "Régénérer", "Annuler"))
                    return;
            }

            // Sauvegarde la scène ouverte si besoin.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // Clone le .unity (copie exacte -> rendu identique).
            AssetDatabase.DeleteAsset(TargetScenePath);
            if (!AssetDatabase.CopyAsset(HubScenePath, TargetScenePath))
            {
                EditorUtility.DisplayDialog("Calibration", "Échec de la copie de la scène.", "OK");
                return;
            }
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

            int disabled = StripNetworkComponents(scene);

            // Ajoute le contrôleur de calibration.
            if (Object.FindObjectOfType<DisplayCalibrationController>() == null)
            {
                var go = new GameObject("DisplayCalibration");
                go.AddComponent<DisplayCalibrationController>();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[Calibration] Scène générée : {TargetScenePath} ({disabled} composants réseau neutralisés). " +
                      "MANIP RESTANTE : (1) câbler les frames Soulrender + position + matériau 2D Lit sur " +
                      "DisplayCalibrationController, (2) positionner la caméra sur le centre de la map, " +
                      "(3) ajouter la scène aux Build Settings. Cf instructions Claude.");

            EditorUtility.DisplayDialog("Calibration",
                $"Scène générée :\n{TargetScenePath}\n\n{disabled} composants réseau neutralisés.\n\n" +
                "À FAIRE à la main :\n" +
                "1. Sur l'objet 'DisplayCalibration' : glisser les frames idle du Soulrender, sa position monde, le matériau 2D Lit.\n" +
                "2. Positionner la caméra sur le centre de la map.\n" +
                "3. Ajouter 05_DisplayCalibration aux Build Settings.\n" +
                "4. Vérifier qu'aucun objet réseau résiduel ne tente de se connecter (Console).",
                "OK");
        }

        private static int StripNetworkComponents(Scene scene)
        {
            int count = 0;
            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                foreach (var comp in root.GetComponentsInChildren<Component>(includeInactive: true))
                {
                    if (comp == null) continue; // script manquant
                    string typeName = comp.GetType().FullName;
                    for (int i = 0; i < DisableComponentTypeNames.Length; i++)
                    {
                        if (typeName == DisableComponentTypeNames[i])
                        {
                            Object.DestroyImmediate(comp);
                            count++;
                            break;
                        }
                    }
                }
            }
            return count;
        }
    }
}
#endif
