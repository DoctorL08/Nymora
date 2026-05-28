using Nymora.Core.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Brique T1 (Tutoriel) — Lancement DEV du tutoriel SANS la restriction « nouveau compte »
    /// (le gate post-login arrive en T4). Permet de rejouer le tuto en boucle pour le développer.
    ///
    /// Pourquoi pas un simple <c>TutorialContext.Active = true</c> dans le menu : les champs
    /// statiques sont remis à zéro par le domain-reload Unity à l'entrée en Play Mode. On stocke
    /// donc l'intention dans EditorPrefs (survit au reload) et on repose le flag runtime dans le
    /// callback <see cref="EditorApplication.playModeStateChanged"/> EnteredPlayMode, qui s'exécute
    /// après le reload et avant le <c>Start</c> async du CombatBootstrapIA.
    ///
    /// Accès : barre de menu Unity → Nymora/Tutorial/…
    /// </summary>
    [InitializeOnLoad]
    public static class TutorialDevLauncher
    {
        private const string DevForcePref = "Nymora.Tutorial.DevForce";
        private const string IaScenePath = "Assets/_Nymora/Scenes/30_CombatIA.unity";
        private const string ForceMenu = "Nymora/Tutorial/Force Tutorial Mode (DEV)";
        private const string LaunchMenu = "Nymora/Tutorial/Launch Tutorial (DEV)";

        static TutorialDevLauncher()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            // Après le domain-reload : on (re)pose le flag runtime selon la préférence DEV.
            if (state == PlayModeStateChange.EnteredPlayMode)
                TutorialContext.Active = EditorPrefs.GetBool(DevForcePref, false);
            // À l'arrêt du Play Mode : on nettoie pour ne pas laisser un état traînant en éditeur.
            else if (state == PlayModeStateChange.EnteredEditMode)
                TutorialContext.Active = false;
        }

        // --- Toggle persistant : quand coché, lancer 30_CombatIA en Play démarre en mode tuto. ---
        [MenuItem(ForceMenu, priority = 0)]
        private static void ToggleForce() => EditorPrefs.SetBool(DevForcePref, !EditorPrefs.GetBool(DevForcePref, false));

        [MenuItem(ForceMenu, validate = true)]
        private static bool ToggleForceValidate()
        {
            Menu.SetChecked(ForceMenu, EditorPrefs.GetBool(DevForcePref, false));
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

            EditorPrefs.SetBool(DevForcePref, true); // le callback EnteredPlayMode le consommera
            EditorSceneManager.OpenScene(IaScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
            Debug.Log("[TutorialDevLauncher] Lancement tuto DEV (Soulrender) sur 30_CombatIA. " +
                      "Décoche 'Force Tutorial Mode (DEV)' pour repasser en IA normale.");
        }
    }
}
