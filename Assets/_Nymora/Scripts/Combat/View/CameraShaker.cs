using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nymora.Combat.View
{
    /// <summary>
    /// Brique J1 (juice combat) — screen-shake CENTRAL et réutilisable. N'importe quel système
    /// View peut demander une secousse via <c>CameraShaker.Instance?.Shake(amplitude, durée)</c>
    /// (coups normaux, signatures, impacts…). 100% View — n'affecte jamais la simulation.
    ///
    /// Auto-instancié au chargement de toute scène de combat (nom contient "Combat"), exactement
    /// comme CombatAudioView : aucune manip Unity, aucune référence de scène.
    ///
    /// ⚠️ Coexistence avec CameraController (qui écrit lui aussi transform.position via pan/clamp) :
    /// le shake est un simple OFFSET de rendu, jamais une position « logique ». Pour éviter toute
    /// dérive, ce composant tourne en DefaultExecutionOrder(-200) → son Update s'exécute AVANT
    /// celui de CameraController et y RETIRE l'offset de la frame précédente (la caméra repart
    /// donc d'une position propre). Le nouvel offset n'est (ré)appliqué qu'en LateUpdate, après
    /// que CameraController a fait son pan/zoom/clamp. Résultat : le shake se superpose au rendu
    /// sans jamais polluer la logique caméra.
    ///
    /// Le décalage suit un bruit de Perlin (plus organique qu'un sin) avec une décroissance
    /// linéaire. Timers en temps NON-scalé → restera correct si un hit-stop ralentit Time.timeScale.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class CameraShaker : MonoBehaviour
    {
        public static CameraShaker Instance { get; private set; }

        private Camera _camera;
        private Vector3 _appliedOffset;

        // Secousse courante (une seule à la fois, "la plus forte gagne" — voir Shake()).
        private bool _active;
        private float _amplitude;   // unités monde
        private float _frequency;   // vitesse de variation du bruit
        private float _duration;
        private float _elapsed;

        // Graines de bruit indépendantes par axe pour des trajectoires X/Y décorrélées.
        private float _seedX;
        private float _seedY;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreateHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreateForActiveScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreateForActiveScene();

        private static void TryCreateForActiveScene()
        {
            if (Instance != null) return;
            if (!SceneManager.GetActiveScene().name.Contains("Combat")) return;
            var go = new GameObject("CameraShaker");
            go.AddComponent<CameraShaker>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            // Sécurité : si on disparaît en pleine secousse, on rend sa position propre à la caméra.
            if (_appliedOffset != Vector3.zero && _camera != null)
                _camera.transform.position -= _appliedOffset;
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Demande une secousse. "La plus forte gagne" : un appel plus faible que la secousse en
        /// cours (amplitude restante) est ignoré, pour ne pas écraser un gros impact par une suite
        /// de petits. Un appel plus fort (ou à amplitude résiduelle nulle) redémarre la secousse.
        /// </summary>
        /// <param name="amplitude">Décalage max en unités monde (ex: 0.08 = léger, 0.20 = costaud).</param>
        /// <param name="duration">Durée en secondes (temps non-scalé).</param>
        /// <param name="frequency">Vitesse de tremblement (≈ 25 doux, ≈ 40 nerveux).</param>
        public void Shake(float amplitude, float duration, float frequency = 32f)
        {
            if (amplitude <= 0f || duration <= 0f) return;

            float remaining = _active ? _amplitude * Mathf.Clamp01(1f - _elapsed / _duration) : 0f;
            if (amplitude < remaining) return;

            _amplitude = amplitude;
            _duration = duration;
            _frequency = Mathf.Max(1f, frequency);
            _elapsed = 0f;
            _active = true;
            _seedX = Random.value * 100f;
            _seedY = Random.value * 100f;
        }

        // Exécuté AVANT CameraController (ordre -200) : on efface l'offset de la frame précédente
        // pour que CameraController reparte d'une position propre.
        private void Update()
        {
            if (!EnsureCamera()) return;
            if (_appliedOffset != Vector3.zero)
            {
                _camera.transform.position -= _appliedOffset;
                _appliedOffset = Vector3.zero;
            }
        }

        // Exécuté APRÈS CameraController (LateUpdate) : on calcule et applique le nouvel offset
        // de rendu par-dessus la position propre fixée par CameraController.
        private void LateUpdate()
        {
            if (!_active || !EnsureCamera()) return;

            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed >= _duration)
            {
                _active = false;
                return; // l'offset de la frame précédente a déjà été retiré dans Update()
            }

            float decay = 1f - _elapsed / _duration;
            float t = _elapsed * _frequency;
            // Perlin renvoie 0..1 → on recentre sur -1..1.
            float dx = (Mathf.PerlinNoise(_seedX, t) * 2f - 1f) * _amplitude * decay;
            float dy = (Mathf.PerlinNoise(_seedY, t) * 2f - 1f) * _amplitude * decay;

            _appliedOffset = new Vector3(dx, dy, 0f);
            _camera.transform.position += _appliedOffset;
        }

        private bool EnsureCamera()
        {
            if (_camera != null) return true;
            _camera = Camera.main;
            return _camera != null;
        }
    }
}
