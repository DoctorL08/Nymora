using Nymora.Combat.Grid;
using Quantum;
using UnityEngine;

namespace Nymora.Combat.View
{
    /// <summary>
    /// Spawn la grille iso 2D dans la scene au demarrage de la simulation Quantum.
    /// Lit GridSingleton (singleton component Quantum) en safe API pour eviter
    /// l'unsafe cote View. Re-spawn proprement si CallbackGameStarted retentit
    /// (cas resync / re-init).
    /// </summary>
    public class GridRenderer : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private GridSettings _settings;
        [SerializeField] private GameObject _tilePrefab;

        [Header("Style placeholder (Phase 2 — remplace en Phase 7 polish)")]
        [SerializeField] private Color _tileColorEven = new Color(0.85f, 0.85f, 0.85f, 1f);
        [SerializeField] private Color _tileColorOdd = new Color(0.75f, 0.75f, 0.75f, 1f);

        private GameObject[] _tiles;

        private void Awake()
        {
            QuantumCallback.Subscribe(this, (CallbackGameStarted c) => OnGameStarted(c.Game));
        }

        private void OnDestroy()
        {
            ClearTiles();
        }

        private void OnGameStarted(QuantumGame game)
        {
            if (_settings == null)
            {
                Debug.LogError("[Nymora.GridRenderer] GridSettings manquant — drag l'asset dans le slot.", this);
                return;
            }
            if (_tilePrefab == null)
            {
                Debug.LogError("[Nymora.GridRenderer] TilePrefab manquant — drag le prefab dans le slot.", this);
                return;
            }

            ClearTiles();

            var frame = game.Frames.Verified;
            var grid = frame.GetSingleton<GridSingleton>();
            int width = grid.Width;
            int height = grid.Height;
            int count = width * height;

            _tiles = new GameObject[count];

            Vector3 centerOffset = _settings.CenterGrid
                ? IsoProjection.CenterOffset(width, height, _settings.TileWorldWidth, _settings.TileWorldHeight)
                : Vector3.zero;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector3 worldPos = IsoProjection.GridToWorld(
                        x, y, _settings.TileWorldWidth, _settings.TileWorldHeight) + centerOffset;

                    var go = Instantiate(_tilePrefab, transform.position + worldPos, Quaternion.identity, transform);
                    go.name = $"Tile_{x}_{y}";

                    bool even = ((x + y) & 1) == 0;
                    Color color = even ? _tileColorEven : _tileColorOdd;

                    var view = go.GetComponent<TileView>();
                    if (view != null)
                    {
                        view.Setup(x, y, color);
                        view.SetSortingOrder(
                            _settings.SortingLayer,
                            IsoProjection.SortingOrderFor(x, y, _settings.BaseSortingOrder));
                    }

                    _tiles[y * width + x] = go;
                }
            }

            Debug.Log($"[Nymora.GridRenderer] Grille {width}x{height} = {count} tiles instanciees.");
        }

        private void ClearTiles()
        {
            if (_tiles == null) return;
            foreach (var t in _tiles)
            {
                if (t != null) Destroy(t);
            }
            _tiles = null;
        }
    }
}
