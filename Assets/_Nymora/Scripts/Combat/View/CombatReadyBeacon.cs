using Nymora.Core.SceneFlow;
using Quantum;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nymora.Combat.View
{
    /// <summary>
    /// Brique T2 — Pont entre la simulation Quantum et le routeur de transition.
    ///
    /// S'abonne à <see cref="CallbackGameStarted"/> (le moment où la sim démarre vraiment,
    /// premier frame vérifié + combattants spawnés) et appelle <see cref="SceneTransition.SignalReady"/>.
    /// → la transition vers le combat (lancée avec waitForReady) garde son voile + spinner
    /// jusqu'à CE moment, au lieu de révéler une scène combat vide pendant la connexion
    /// Photon/Quantum.
    ///
    /// Auto-créé dans TOUTE scène combat (IA / casual / ranked) — mirror du pattern
    /// CoinFlipIntroView/CombatantTooltipView. Pur code, aucun SerializeField, zéro setup scène.
    /// 100% View : lecture seule du démarrage de la sim, aucun impact déterministe.
    /// </summary>
    public sealed class CombatReadyBeacon : MonoBehaviour
    {
        private static CombatReadyBeacon _current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnAnySceneLoaded;
            SceneManager.sceneLoaded += OnAnySceneLoaded;
            TryCreateForScene(SceneManager.GetActiveScene());
        }

        private static void OnAnySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryCreateForScene(scene);
        }

        private static void TryCreateForScene(Scene scene)
        {
            // Toutes les scènes combat contiennent "Combat" (30_CombatIA, 33_CombatCasual,
            // 40_CombatRanked1v1). Hub/Login/Menu ne sont pas concernés.
            if (!scene.IsValid() || !scene.name.Contains("Combat")) return;
            if (_current != null) return;
            var go = new GameObject("CombatReadyBeacon");
            _current = go.AddComponent<CombatReadyBeacon>();
        }

        private void Awake()
        {
            _current = this;
            // Abonnement tôt (avant le démarrage Quantum) -> le callback est capté quand la
            // session démarre, comme CoinFlipIntroView.
            QuantumCallback.Subscribe(this, (CallbackGameStarted c) => SceneTransition.SignalReady());
        }

        private void OnDestroy()
        {
            if (_current == this) _current = null;
        }
    }
}
