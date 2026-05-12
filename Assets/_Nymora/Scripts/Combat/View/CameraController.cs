using UnityEngine;

namespace Nymora.Combat.View
{
    /// <summary>
    /// Caméra du combat (2.13.d). 3 actions :
    ///   - Molette : zoom centre sur le curseur (anchored, "grab the world point under the mouse").
    ///   - Clic molette maintenu + drag : pan libre.
    ///   - Home (ou double-clic molette) : reset a la vue de demarrage.
    ///
    /// Attache sur la Camera.main de la scene Quantum (auto par CreateCombatHUDTool).
    ///
    /// Sensitivity / bornes ortho size exposes dans l'Inspector pour tuner sans recompiler.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] private Camera _camera;

        [Header("Zoom (molette)")]
        [Tooltip("Multiplicateur de zoom par tick de molette. Plus eleve = zoom plus brutal.")]
        [SerializeField] private float _zoomSpeed = 2f;
        [SerializeField] private float _minOrthoSize = 2f;
        [SerializeField] private float _maxOrthoSize = 15f;

        [Header("Pan (clic molette maintenu)")]
        [Tooltip("1 = la case sous le curseur reste sous le curseur (drag naturel). " +
                 ">1 amplifie le pan, <1 le ralentit.")]
        [SerializeField] private float _panSensitivity = 1f;

        [Header("Reset")]
        [SerializeField] private KeyCode _resetKey = KeyCode.Home;
        [Tooltip("Fenetre en secondes entre 2 clics molette pour declencher le reset.")]
        [SerializeField] private float _doubleClickResetWindow = 0.35f;

        private Vector3 _initialPosition;
        private float _initialOrthoSize;
        private Vector3 _panAnchorWorld;
        private bool _panning;
        private float _lastMiddleClickTime = -1f;

        private void Awake()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            if (_camera == null) _camera = Camera.main;
            if (_camera == null)
            {
                Debug.LogWarning("[CameraController] Pas de Camera reference ni Camera.main — composant inactif.", this);
                enabled = false;
                return;
            }

            _initialPosition = _camera.transform.position;
            _initialOrthoSize = _camera.orthographic ? _camera.orthographicSize : 5f;
        }

        private void Update()
        {
            if (_camera == null) return;
            HandleZoom();
            HandlePan();
            HandleReset();
        }

        private void HandleZoom()
        {
            float wheel = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(wheel) < 0.0001f) return;
            if (!_camera.orthographic) return; // Phase 2 : caméra ortho iso

            // Zoom centre sur curseur : capture le point monde sous la souris, change la taille
            // ortho, puis re-positionne pour garder ce point monde sous la souris.
            Vector3 mouseWorldBefore = _camera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
            float newSize = _camera.orthographicSize * (1f - wheel * _zoomSpeed);
            _camera.orthographicSize = Mathf.Clamp(newSize, _minOrthoSize, _maxOrthoSize);
            Vector3 mouseWorldAfter = _camera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
            _camera.transform.position += (mouseWorldBefore - mouseWorldAfter);
        }

        private void HandlePan()
        {
            if (UnityEngine.Input.GetMouseButtonDown(2))
            {
                // Detection double-clic molette (avant d'initialiser le drag).
                if (Time.time - _lastMiddleClickTime < _doubleClickResetWindow)
                {
                    ResetView();
                    _lastMiddleClickTime = -1f;
                    _panning = false;
                    return;
                }
                _lastMiddleClickTime = Time.time;
                _panAnchorWorld = _camera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
                _panning = true;
            }
            if (UnityEngine.Input.GetMouseButtonUp(2))
            {
                _panning = false;
            }
            if (_panning && UnityEngine.Input.GetMouseButton(2))
            {
                Vector3 currentMouseWorld = _camera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
                Vector3 delta = (_panAnchorWorld - currentMouseWorld) * _panSensitivity;
                _camera.transform.position += delta;
            }
        }

        private void HandleReset()
        {
            if (UnityEngine.Input.GetKeyDown(_resetKey)) ResetView();
        }

        private void ResetView()
        {
            _camera.transform.position = _initialPosition;
            if (_camera.orthographic) _camera.orthographicSize = _initialOrthoSize;
        }
    }
}
