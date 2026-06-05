using UnityEngine;
using UnityEngine.EventSystems;

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

        [Header("Clamp aux limites de la map")]
        [Tooltip("Bloque le pan/zoom pour que le viewport ne sorte pas de la map (plus de vide aux bords).")]
        [SerializeField] private bool _clampToMap = true;
        [Tooltip("Marge monde ajoutee aux bounds de la grille. Monte-la si ton background deborde " +
                 "de la grille iso et que tu veux voir son liseré ; baisse-la si tu vois encore du vide.")]
        [SerializeField] private float _boundsPadding = 0f;
        [Tooltip("Leste de pan : laisse le centre camera depasser les bornes de cette marge monde. " +
                 "Permet de pousser un bord/coin de la map vers le centre de l'ecran (surtout en zoom). " +
                 "0 = clamp strict (viewport jamais hors map).")]
        [SerializeField] private float _panSlack = 2.5f;

        private Vector3 _initialPosition;
        private float _initialOrthoSize;
        private Vector3 _panAnchorWorld;
        private bool _panning;
        private float _lastMiddleClickTime = -1f;

        private GridRenderer _grid;
        private Bounds _mapBounds;
        private bool _mapBoundsValid;

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
            ClampToBounds();
        }

        /// <summary>
        /// Borne UNIQUEMENT la position de la camera pour que son viewport ne sorte pas de la map
        /// (plus de vide quand on pane). Ne touche PAS au zoom (orthographicSize laisse intact).
        /// Si la map est plus petite que le viewport sur un axe, on centre sur cet axe (le pan n'a
        /// nulle part ou aller). Bounds lues une fois sur le GridRenderer puis cachees.
        /// </summary>
        private void ClampToBounds()
        {
            if (!_clampToMap || !_camera.orthographic) return;
            if (!EnsureBounds()) return;

            Vector2 min = (Vector2)_mapBounds.min - Vector2.one * _boundsPadding;
            Vector2 max = (Vector2)_mapBounds.max + Vector2.one * _boundsPadding;
            float mapW = max.x - min.x;
            float mapH = max.y - min.y;
            float aspect = _camera.aspect > 0.0001f ? _camera.aspect : 1f;

            float halfH = _camera.orthographicSize;
            float halfW = halfH * aspect;
            float slack = Mathf.Max(0f, _panSlack);
            Vector3 p = _camera.transform.position;
            p.x = (mapW <= halfW * 2f) ? (min.x + max.x) * 0.5f : Mathf.Clamp(p.x, min.x + halfW - slack, max.x - halfW + slack);
            p.y = (mapH <= halfH * 2f) ? (min.y + max.y) * 0.5f : Mathf.Clamp(p.y, min.y + halfH - slack, max.y - halfH + slack);
            _camera.transform.position = p;
        }

        /// <summary>Trouve le GridRenderer et cache ses bounds monde (constantes une fois la scene chargee).</summary>
        private bool EnsureBounds()
        {
            if (_mapBoundsValid) return true;
            if (_grid == null) _grid = FindFirstObjectByType<GridRenderer>();
            if (_grid == null) return false;
            if (!_grid.TryGetWorldBounds(out _mapBounds)) return false;
            _mapBoundsValid = true;
            return true;
        }

        private void HandleZoom()
        {
            float wheel = UnityEngine.Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(wheel) < 0.0001f) return;
            if (!_camera.orthographic) return; // Phase 2 : caméra ortho iso

            // #1 (5 juin) — molette contextuelle : pas de zoom combat si la souris est sur de l'UI
            //   (chat / HUD via IsPointerOverGameObject) ou HORS de la fenêtre de jeu. Le chat
            //   (ScrollRect) garde ainsi sa molette pour lui ; la map se zoome normalement.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            Vector3 mp = UnityEngine.Input.mousePosition;
            if (mp.x < 0f || mp.x > Screen.width || mp.y < 0f || mp.y > Screen.height) return;

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
