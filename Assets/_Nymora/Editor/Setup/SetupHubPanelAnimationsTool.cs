using System.Collections.Generic;
using System.Reflection;
using Nymora.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nymora.Editor.Setup
{
    /// <summary>
    /// Nymora > Setup > Setup Hub Panel Animations
    ///
    /// Attache un <see cref="UiPanelAnimator"/> sur le `_panelRoot` de chaque panneau hub
    /// (animation d'ouverture pop + fondu). Détection générique par réflexion : tout
    /// MonoBehaviour de la scène ayant un champ `_panelRoot` (GameObject) est couvert
    /// → les 11 panneaux hub d'un coup, sans toucher leur code.
    ///
    /// Idempotent : n'ajoute pas un 2e animator si déjà présent. À lancer dans
    /// 10_CommunityHub (scène ouverte), puis sauvegarder (Ctrl+S).
    /// </summary>
    public static class SetupHubPanelAnimationsTool
    {
        [MenuItem("Nymora/Setup/Setup Hub Panel Animations")]
        public static void Run()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.name.Contains("Hub"))
            {
                Debug.LogWarning($"[SetupHubPanelAnimations] La scène active est '{activeScene.name}', pas le hub. " +
                                 "Ouvre 10_CommunityHub avant de lancer l'outil. (On continue quand même sur ce qui est trouvé.)");
            }

            var roots = new HashSet<GameObject>();
            int attached = 0, already = 0;

            // Inclut les GameObjects inactifs (les panneaux sont SetActive(false) au repos).
            var all = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            foreach (var mb in all)
            {
                if (mb == null) continue;
                if (!mb.gameObject.scene.IsValid()) continue;   // scène uniquement (pas assets/prefabs)
                if (mb.hideFlags != HideFlags.None) continue;

                var field = mb.GetType().GetField("_panelRoot",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (field == null || field.FieldType != typeof(GameObject)) continue;

                var root = field.GetValue(mb) as GameObject;
                if (root == null) continue;
                if (!roots.Add(root)) continue; // dédoublonnage

                if (root.GetComponent<UiPanelAnimator>() != null) { already++; continue; }

                Undo.AddComponent<UiPanelAnimator>(root);
                attached++;
                Debug.Log($"[SetupHubPanelAnimations] Animator ajouté sur '{root.name}' (panneau {mb.GetType().Name}).");
            }

            if (attached > 0) EditorSceneManager.MarkSceneDirty(activeScene);

            Debug.Log($"[SetupHubPanelAnimations] Terminé : {attached} ajout(s), {already} déjà présent(s), " +
                      $"{roots.Count} root(s) panneau trouvé(s). Sauvegarde la scène (Ctrl+S).");
        }
    }
}
