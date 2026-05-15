using UnityEngine;
using UnityEngine.EventSystems;

namespace Nymora.Hub
{
    /// <summary>
    /// Phase 4 — Équivalent Hub du TileHoverView combat. Détecte la case survolée par la
    /// souris et applique un tint sur la tile correspondante. Au quit (out of grid ou
    /// changement de case), restore la couleur logique via HubGridRenderer.RefreshTileColor.
    ///
    /// Refs auto-résolues au Start si non assignées dans l'Inspector.
    /// Ignore les clics survolant UI (cohérent avec HubInputController).
    ///
    /// Performance : Update O(1) sans alloc.
    /// </summary>
    public sealed class HubTileHoverView : MonoBehaviour
    {
        [Header("Refs (auto-found si null au Start)")]
        [SerializeField] private Camera _camera;
        [SerializeField] private HubGridRenderer _grid;

        [Header("Style hover")]
        [Tooltip("Couleur appliquée à la tile sous la souris (écrase la couleur logique).")]
        [SerializeField] private Color _hoverColor = new Color(1f, 0.95f, 0.5f, 1f);

        [Tooltip("Désactive le hover (utile pendant ban edit ou popup modal).")]
        [SerializeField] private bool _enabled = true;

        private int _prevHoverX = int.MinValue;
        private int _prevHoverY = int.MinValue;

        private void Start()
        {
            if (_camera == null) _camera = Camera.main;
            if (_grid == null) _grid = FindFirstObjectByType<HubGridRenderer>();
        }

        private void OnDisable()
        {
            ClearHover();
        }

        private void Update()
        {
            if (!_enabled || _camera == null || _grid == null) return;

            // Ignore le hover si la souris est sur l'UI (chat, popup, etc.)
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                ClearHover();
                return;
            }

            Vector3 mouseWorld = _camera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0f;
            var (gx, gy) = IsoProjection.WorldToGrid(mouseWorld, _grid.TileWorldWidth, _grid.TileWorldHeight, _grid.CenterOffset);

            bool outOfGrid = gx < 0 || gx >= _grid.Width || gy < 0 || gy >= _grid.Height;

            if (!outOfGrid && gx == _prevHoverX && gy == _prevHoverY) return;
            if (outOfGrid && _prevHoverX == int.MinValue) return;

            // Restore l'ancienne tile (recalcule la couleur logique : ban ou normale)
            if (_prevHoverX != int.MinValue)
            {
                _grid.RefreshTileColor(_prevHoverX, _prevHoverY);
            }

            if (outOfGrid)
            {
                _prevHoverX = int.MinValue;
                _prevHoverY = int.MinValue;
                return;
            }

            _prevHoverX = gx;
            _prevHoverY = gy;

            if (_grid.TryGetTileRenderer(gx, gy, out var sr) && sr != null)
            {
                sr.color = _hoverColor;
            }
        }

        private void ClearHover()
        {
            if (_prevHoverX == int.MinValue) return;
            if (_grid != null) _grid.RefreshTileColor(_prevHoverX, _prevHoverY);
            _prevHoverX = int.MinValue;
            _prevHoverY = int.MinValue;
        }
    }
}
