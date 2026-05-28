using Nymora.Core.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Brique T1/T2 (Tutoriel) — Lancement DEV du tutoriel SANS la restriction « nouveau compte »
    /// (le gate post-login arrive en T4). Permet de rejouer le tuto en boucle pour le développer.
    ///
    /// Le flag est stocké dans <see cref="PlayerPrefs"/> (pas EditorPrefs) : il est ainsi lisible
    /// au RUNTIME par le bootstrap (TutorialContext.ApplyEditorDevOverride), donc robuste au
    /// domain-reload Unity — aucune dépendance à un timing de callback éditeur.
    ///
    /// Accès : barre de menu Unity → Nymora/Tutorial/…
    /// </summary>
    public static class TutorialDevLauncher
    {
        private const string IaScenePath = "Assets/_Nymora/Scenes/30_CombatIA.unity";
        private const string ForceMenu = "Nymora/Tutorial/Force Tutorial Mode (DEV)";
        private const string LaunchMenu = "Nymora/Tutorial/Launch Tutorial (DEV)";
        private static string Key => TutorialContext.DevForcePrefKey;

        // --- Toggle persistant : quand coché, lancer 30_CombatIA en Play démarre en mode tuto. ---
        [MenuItem(ForceMenu, priority = 0)]
        private static void ToggleForce()
        {
            PlayerPrefs.SetInt(Key, PlayerPrefs.GetInt(Key, 0) == 1 ? 0 : 1);
            PlayerPrefs.Save();
        }

        [MenuItem(ForceMenu, validate = true)]
        private static bool ToggleForceValidate()
        {
            Menu.SetChecked(ForceMenu, PlayerPrefs.GetInt(Key, 0) == 1);
            return true;
        }

        // --- Raccourci : active le mode tuto + ouvre la scène IA + entre en Play. ---
        [MenuItem(LaunchMenu, priority = 1)]
        private static void Launch()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            PlayerPrefs.SetInt(Key, 1);
            PlayerPrefs.Save();
            EditorSceneManager.OpenScene(IaScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
            Debug.Log("[TutorialDevLauncher] Lancement tuto DEV (Soulrender) sur 30_CombatIA. " +
                      "Décoche 'Force Tutorial Mode (DEV)' pour repasser en IA normale.");
        }
    }
}
