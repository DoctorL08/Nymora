using UnityEngine;
using UnityEngine.EventSystems;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 4.3.a — Caméra orthographique du hub.
    ///   - Follow optionnel d'un Transform target (avatar local en fallback) — désactivé par défaut.
    ///   - Zoom molette souris avec clamp min/max sur orthographicSize.
    ///   - Bloque le zoom si la souris est over UI (évite que le scroll du chat zoom la caméra).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class HubCamera : MonoBehaviour
    {
        [Header("Follow target")]
        [SerializeField] private Transform _target;
        [Tooltip("Si désactivé, la caméra reste fixe (pas de suivi avatar). Default = désactivé sur demande Lorenzo.")]
        [SerializeField] private bool _followLocalAvatar = false;
        [SerializeField, Range(0.01f, 1f)] private float _lerpFactor = 0.1f;
        [SerializeField] private Vector2 _offsetXY = Vector2.zero;

        [Header("Zoom molette souris")]
        [SerializeField] private bool _zoomEnabled = true;
        [SerializeField, Range(0.1f, 5f)] private float _zoomSpeed = 1f;
        [SerializeField, Range(1f, 50f)] private float _minOrthoSize = 3f;
        [SerializeField, Range(1f, 50f)] private float _maxOrthoSize = 20f;

        private Camera _camera;

        public void SetTarget(Transform target)
        {
            _target = target;
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void Update()
        {
            if (!_zoomEnabled || _camera == null || !_camera.orthographic) return;
            // Bloquer le zoom si la souris est over UI (chat panel scroll != zoom caméra)
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Approximately(scroll, 0f)) return;

            float newSize = _camera.orthographicSize - scroll * _zoomSpeed;
            _camera.orthographicSize = Mathf.Clamp(newSize, _minOrthoSize, _maxOrthoSize);
        }

        private void LateUpdate()
        {
            if (!_followLocalAvatar) return;

            Transform target = _target;
            // Fallback : 4.3.c — l'avatar est spawn par Fusion, pas en scene. On suit HubAvatar.Local.
            if (target == null && HubAvatar.Local != null) target = HubAvatar.Local.transform;
            if (target == null) return;

            Vector3 desired = new Vector3(target.position.x + _offsetXY.x, target.position.y + _offsetXY.y, transform.position.z);
            transform.position = Vector3.Lerp(transform.position, desired, _lerpFactor);
        }
    }
}
