using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Nymora.Hub
{
    /// <summary>
    /// Flicker "flamme" d'une Light2D. 100% View (aucune sim Quantum).
    ///
    /// Fait vaciller l'intensite (et, en option, tremble legerement la position) avec du bruit
    /// de Perlin -> la light vit comme la flamme animee de la torche. Chaque torche a un seed
    /// different pour ne pas vaciller a l'unisson.
    ///
    /// IMPORTANT : capture l'intensite actuelle de la light comme BASE (_autoCaptureBase) ->
    /// preserve le reglage de Lorenzo, le flicker ne fait qu'osciller autour.
    ///
    /// Reglages :
    /// - _flickerAmount : amplitude (% de la base).
    /// - _flickerSpeed  : rapidite du vacillement.
    /// - _jitterX / _jitterY : tremblement de position, axe par axe (0 = aucun). Une flamme
    ///   bouge en general plus sur l'axe Y (vertical) que X.
    /// </summary>
    [RequireComponent(typeof(Light2D))]
    public sealed class TorchLightFlicker : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _baseIntensity = 0.85f;
        [SerializeField, Range(0f, 1f)] private float _flickerAmount = 0.25f;
        [SerializeField, Min(0f)] private float _flickerSpeed = 6f;
        [SerializeField, Min(0f)] private float _jitterX = 0.03f;
        [SerializeField, Min(0f)] private float _jitterY = 0.05f;
        [SerializeField] private bool _autoCaptureBase = true;

        private Light2D _light;
        private Vector3 _basePos;
        private float _seedI;
        private float _seedX;
        private float _seedY;

        private void Awake()
        {
            _light = GetComponent<Light2D>();
            if (_autoCaptureBase && _light != null) _baseIntensity = _light.intensity;
            _basePos = transform.localPosition;

            // Seeds aleatoires (View only, hors simulation) pour desynchroniser les torches.
            _seedI = Random.value * 100f;
            _seedX = Random.value * 100f;
            _seedY = Random.value * 100f;
        }

        private void Update()
        {
            if (_light == null) return;

            float t = Time.time * _flickerSpeed;

            float n = Mathf.PerlinNoise(_seedI, t) * 2f - 1f; // [-1, 1]
            _light.intensity = Mathf.Max(0f, _baseIntensity * (1f + n * _flickerAmount));

            if (_jitterX > 0f || _jitterY > 0f)
            {
                float jx = (Mathf.PerlinNoise(_seedX, t) - 0.5f) * 2f * _jitterX;
                float jy = (Mathf.PerlinNoise(_seedY, t) - 0.5f) * 2f * _jitterY;
                transform.localPosition = _basePos + new Vector3(jx, jy, 0f);
            }
        }
    }
}
