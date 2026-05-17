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

        [Header("Editor preview (POLISH-5e)")]
        [Tooltip("Dessine le contour de la grille iso en Scene View (Editor only, pas de cout runtime). " +
                 "Utile pour caler la map background sans avoir a lancer Play.")]
        [SerializeField] private bool _drawGridGizmos = true;
        [SerializeField] private Color _gridGizmoColor = new Color(1f, 0.9f, 0.3f, 0.7f);

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

#if UNITY_EDITOR
        /// <summary>
        /// POLISH-5e — Dessine le contour iso de la grille (losange) + le quadrillage interne
        /// dans la Scene View, meme hors Play. Permet de caler le BattleMapBackground sur la
        /// grille sans devoir lancer le combat. Editor only (#if UNITY_EDITOR), zero cout runtime.
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!_drawGridGizmos) return;
            if (_settings == null) return;
            int w = Quantum.GridConstants.Width;
            int h = Quantum.GridConstants.Height;
            float tw = _settings.TileWorldWidth;
            float th = _settings.TileWorldHeight;
            Vector3 offset = _settings.CenterGrid
                ? IsoProjection.CenterOffset(w, h, tw, th)
                : Vector3.zero;
            Vector3 origin = transform.position;

            Gizmos.color = _gridGizmoColor;

            // 4 coins du losange iso (positions des cases extremes).
            Vector3 cBL = origin + IsoProjection.GridToWorld(0,     0,     tw, th) + offset;
            Vector3 cBR = origin + IsoProjection.GridToWorld(w - 1, 0,     tw, th) + offset;
            Vector3 cTR = origin + IsoProjection.GridToWorld(w - 1, h - 1, tw, th) + offset;
            Vector3 cTL = origin + IsoProjection.GridToWorld(0,     h - 1, tw, th) + offset;

            // Bordure exterieure (epaisse, alpha plein).
            Gizmos.color = new Color(_gridGizmoColor.r, _gridGizmoColor.g, _gridGizmoColor.b, 1f);
            Gizmos.DrawLine(cBL, cBR);
            Gizmos.DrawLine(cBR, cTR);
            Gizmos.DrawLine(cTR, cTL);
            Gizmos.DrawLine(cTL, cBL);

            // Quadrillage interne (lignes constantes en x et en y, alpha plus faible).
            Gizmos.color = new Color(_gridGizmoColor.r, _gridGizmoColor.g, _gridGizmoColor.b, _gridGizmoColor.a * 0.4f);
            for (int x = 1; x < w; x++)
            {
                Vector3 a = origin + IsoProjection.GridToWorld(x, 0,     tw, th) + offset;
                Vector3 b = origin + IsoProjection.GridToWorld(x, h - 1, tw, th) + offset;
                Gizmos.DrawLine(a, b);
            }
            for (int y = 1; y < h; y++)
            {
                Vector3 a = origin + IsoProjection.GridToWorld(0,     y, tw, th) + offset;
                Vector3 b = origin + IsoProjection.GridToWorld(w - 1, y, tw, th) + offset;
                Gizmos.DrawLine(a, b);
            }
        }
#endif

        private void ClearTiles()
        {
            if (_tiles == null) return;
            foreach (var t in _tiles)
            {
                if (t != null) Destroy(t);
            }
            _tiles = null;
        }

        /// <summary>
        /// Recupere le TileView a une position grille donnee. Retourne null si hors bornes
        /// ou si la grille n'a pas encore ete spawn.
        /// </summary>
        public TileView GetTileView(int gx, int gy)
        {
            if (_tiles == null) return null;
            int width = (_settings != null) ? 0 : 0; // pas necessaire ici, on a la singleton via la frame
            // POLISH-5e (17 mai) : remplace les hardcoded 15/17 par GridConstants pour eviter
            // de fork avec la dimension Quantum a chaque resize.
            int gridWidth = Quantum.GridConstants.Width;
            int gridHeight = Quantum.GridConstants.Height;
            if (gx < 0 || gx >= gridWidth || gy < 0 || gy >= gridHeight) return null;
            int idx = gy * gridWidth + gx;
            // Securite : si la grille a ete resize (GridConstants change) sans re-spawn,
            // on protege contre un index out of range.
            if (idx >= _tiles.Length) return null;
            if (idx < 0 || idx >= _tiles.Length) return null;
            var go = _tiles[idx];
            return go != null ? go.GetComponent<TileView>() : null;
        }
    }
}
