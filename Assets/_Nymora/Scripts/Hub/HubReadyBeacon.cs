using Nymora.Core.SceneFlow;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique T2-hub — Garde le voile de chargement (SceneTransition lancé avec waitForReady)
    /// jusqu'à ce que les PERSONNAGES soient visibles dans le hub, au lieu de révéler une scène
    /// hub vide pendant la connexion Fusion + le spawn des avatars.
    ///
    /// Mirror de CombatReadyBeacon : auto-créé dans la scène hub (RuntimeInitializeOnLoadMethod +
    /// sceneLoaded), poll chaque frame jusqu'à ce que HubAvatar.Local existe ET ait son sprite
    /// posé (IsVisualReady), puis appelle SceneTransition.SignalReady(). Le timeout de sécurité de
    /// SceneTransition révèle quand même si le signal n'arrive jamais (échec de connexion).
    ///
    /// 100% View. Aucune dépendance simulation.
    /// </summary>
    public sealed class HubReadyBeacon : MonoBehaviour
    {
        private static HubReadyBeacon _current;
        private bool _signaled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnAnySceneLoaded;
            SceneManager.sceneLoaded += OnAnySceneLoaded;
            TryCreateForScene(SceneManager.GetActiveScene());
        }

        private static void OnAnySceneLoaded(Scene scene, LoadSceneMode mode) => TryCreateForScene(scene);

        private static void TryCreateForScene(Scene scene)
        {
            // Seule la scène hub (10_CommunityHub) est concernée.
            if (!scene.IsValid() || !scene.name.Contains("Community")) return;
            if (_current != null) return;
            var go = new GameObject("HubReadyBeacon");
            _current = go.AddComponent<HubReadyBeacon>();
        }

        private void OnDestroy()
        {
            if (_current == this) _current = null;
        }

        private void Update()
        {
            if (_signaled) return;
            // Avatar local spawné (Fusion connecté) + son visuel posé -> persos visibles.
            if (HubAvatar.Local != null && HubAvatar.Local.IsVisualReady)
            {
                _signaled = true;
                SceneTransition.SignalReady();
            }
        }
    }
}
