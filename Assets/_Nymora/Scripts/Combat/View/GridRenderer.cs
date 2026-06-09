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

        // 5.4 — Dimensions LOGIQUES de la map courante (zone jouable), lues depuis GridSingleton au
        //   spawn. ≠ GridConstants.Width/Height (= MAX 15x15 du tableau sim). GetTileView /
        //   TryGetWorldBounds s'en servent pour rester cohérents avec le rendu (le 1v1 reste 10x10).
        private int _logicalWidth;
        private int _logicalHeight;

        /// <summary>Largeur monde d'une case iso (depuis GridSettings). 0 si settings manquant.
        /// Utilisé par le contour de case d'équipe (MirrorOutlineHelper) pour tracer le losange.</summary>
        public float TileWorldWidth => _settings != null ? _settings.TileWorldWidth : 0f;

        /// <summary>Hauteur monde d'une case iso (depuis GridSettings). 0 si settings manquant.</summary>
        public float TileWorldHeight => _settings != null ? _settings.TileWorldHeight : 0f;

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

            _logicalWidth = width;   // 5.4 — cache pour GetTileView / TryGetWorldBounds
            _logicalHeight = height;
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
        /// Bounds monde (AABB) de la map iso, dilatees d'une demi-tuile pour couvrir le footprint
        /// visible des cases extremes. Sert au clamp camera (CameraController) pour ne pas paner
        /// hors map. Calcule depuis GridConstants + GridSettings + transform.position (meme math
        /// que le gizmo / le spawn) -> dispo des que _settings est assigne, sans attendre le spawn.
        /// Retourne false si _settings manquant.
        /// </summary>
        public bool TryGetWorldBounds(out Bounds bounds)
        {
            bounds = default;
            if (_settings == null) return false;

            // 5.4 — clamp caméra sur les dims LOGIQUES une fois la grille spawn (la map réelle),
            //   fallback sur le MAX (const) avant le spawn. Le 1v1 (10x10) reste cadré comme avant.
            int w = _tiles != null ? _logicalWidth : Quantum.GridConstants.Width;
            int h = _tiles != null ? _logicalHeight : Quantum.GridConstants.Height;
            float tw = _settings.TileWorldWidth;
            float th = _settings.TileWorldHeight;
            Vector3 offset = _settings.CenterGrid
                ? IsoProjection.CenterOffset(w, h, tw, th)
                : Vector3.zero;
            Vector3 origin = transform.position;

            Vector3 cBL = origin + IsoProjection.GridToWorld(0,     0,     tw, th) + offset;
            Vector3 cBR = origin + IsoProjection.GridToWorld(w - 1, 0,     tw, th) + offset;
            Vector3 cTR = origin + IsoProjection.GridToWorld(w - 1, h - 1, tw, th) + offset;
            Vector3 cTL = origin + IsoProjection.GridToWorld(0,     h - 1, tw, th) + offset;

            float minX = Mathf.Min(Mathf.Min(cBL.x, cBR.x), Mathf.Min(cTR.x, cTL.x)) - tw * 0.5f;
            float maxX = Mathf.Max(Mathf.Max(cBL.x, cBR.x), Mathf.Max(cTR.x, cTL.x)) + tw * 0.5f;
            float minY = Mathf.Min(Mathf.Min(cBL.y, cBR.y), Mathf.Min(cTR.y, cTL.y)) - th * 0.5f;
            float maxY = Mathf.Max(Mathf.Max(cBL.y, cBR.y), Mathf.Max(cTR.y, cTL.y)) + th * 0.5f;

            bounds = new Bounds(
                new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f),
                new Vector3(maxX - minX, maxY - minY, 0f));
            return true;
        }

        /// <summary>
        /// Recupere le TileView a une position grille donnee. Retourne null si hors bornes
        /// ou si la grille n'a pas encore ete spawn.
        /// </summary>
        public TileView GetTileView(int gx, int gy)
        {
            if (_tiles == null) return null;
            // 5.4 — on indexe sur les dims LOGIQUES (celles utilisées au spawn pour `_tiles`),
            //   pas sur GridConstants.Width (= MAX 15). Sinon le stride ne correspond plus au
            //   tableau _tiles et le hover/highlight pointe la mauvaise case.
            if (gx < 0 || gx >= _logicalWidth || gy < 0 || gy >= _logicalHeight) return null;
            int idx = gy * _logicalWidth + gx;
            if (idx < 0 || idx >= _tiles.Length) return null;
            var go = _tiles[idx];
            return go != null ? go.GetComponent<TileView>() : null;
        }
    }
}
