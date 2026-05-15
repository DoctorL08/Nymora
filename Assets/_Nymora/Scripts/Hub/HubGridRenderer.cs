using System.Collections.Generic;
using UnityEngine;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 4.3.a — Genere une grille iso 2:1 runtime au Start.
    /// 400 tiles 20x20 par defaut, sprite TilePlaceholder partage avec Combat.
    /// Tiles sont des SpriteRenderer enfants du transform de ce GameObject.
    ///
    /// 4.X — Support visualisation des tiles bannées (HubGridBanList).
    /// Les tiles bannies sont colorées différemment et accessibles via TryGetTileRenderer
    /// pour le mode Edit Bans cliquable.
    /// </summary>
    public sealed class HubGridRenderer : MonoBehaviour
    {
        [Header("Grid Config")]
        [SerializeField] private int _width = 20;
        [SerializeField] private int _height = 20;
        [SerializeField] private float _tileWorldWidth = 1f;
        [SerializeField] private float _tileWorldHeight = 0.5f;

        [Header("Visual")]
        [SerializeField] private Sprite _tileSprite;
        [SerializeField] private Color _tileColor = new Color(0.55f, 0.55f, 0.6f, 1f);
        [SerializeField] private Color _bannedTileColor = new Color(0.85f, 0.25f, 0.25f, 0.9f);
        [SerializeField] private int _baseSortingOrder = 0;
        [SerializeField] private string _sortingLayerName = "Default";

        [Header("Bans (optionnel)")]
        [SerializeField] private HubGridBanList _banList;

        public int Width => _width;
        public int Height => _height;
        public float TileWorldWidth => _tileWorldWidth;
        public float TileWorldHeight => _tileWorldHeight;
        public Vector3 CenterOffset => IsoProjection.CenterOffset(_width, _height, _tileWorldWidth, _tileWorldHeight);
        public HubGridBanList BanList => _banList;

        private readonly Dictionary<Vector2Int, SpriteRenderer> _tileRenderers = new Dictionary<Vector2Int, SpriteRenderer>();

        private void Start()
        {
            BuildGrid();
        }

        public void BuildGrid()
        {
            // Clear existing children (idempotent en cas de regen runtime)
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying) Destroy(transform.GetChild(i).gameObject);
                else DestroyImmediate(transform.GetChild(i).gameObject);
            }
            _tileRenderers.Clear();

            if (_tileSprite == null)
            {
                Debug.LogError("[HubGridRenderer] Tile sprite non assigne. L'Editor Tool doit l'auto-cabler.");
                return;
            }

            Vector3 centerOffset = IsoProjection.CenterOffset(_width, _height, _tileWorldWidth, _tileWorldHeight);

            for (int gx = 0; gx < _width; gx++)
            {
                for (int gy = 0; gy < _height; gy++)
                {
                    var tileGo = new GameObject($"Tile_{gx}_{gy}", typeof(SpriteRenderer));
                    tileGo.transform.SetParent(transform, worldPositionStays: false);
                    tileGo.transform.localPosition = IsoProjection.GridToWorld(gx, gy, _tileWorldWidth, _tileWorldHeight) + centerOffset;

                    var sr = tileGo.GetComponent<SpriteRenderer>();
                    sr.sprite = _tileSprite;
                    sr.sortingLayerName = _sortingLayerName;
                    sr.sortingOrder = IsoProjection.SortingOrderFor(gx, gy, _baseSortingOrder);
                    sr.color = ColorForTile(gx, gy);
                    _tileRenderers[new Vector2Int(gx, gy)] = sr;
                }
            }

            Debug.Log($"[HubGridRenderer] Grille generee {_width}x{_height} = {_width * _height} tiles");
        }

        /// <summary>
        /// Re-applique la couleur des tiles selon l'état actuel du HubGridBanList.
        /// À appeler après toute modif du ban list pendant le Play (HubBanEditMode).
        /// </summary>
        public void RefreshBanColors()
        {
            foreach (var kv in _tileRenderers)
            {
                if (kv.Value == null) continue;
                kv.Value.color = ColorForTile(kv.Key.x, kv.Key.y);
            }
        }

        public bool TryGetTileRenderer(int gx, int gy, out SpriteRenderer sr)
        {
            return _tileRenderers.TryGetValue(new Vector2Int(gx, gy), out sr);
        }

        /// <summary>
        /// Recalcule la couleur d'une seule tile (utile pour HubTileHoverView qui doit
        /// restaurer la couleur logique courante sans dépendre d'un snapshot couleur).
        /// </summary>
        public void RefreshTileColor(int gx, int gy)
        {
            if (_tileRenderers.TryGetValue(new Vector2Int(gx, gy), out var sr) && sr != null)
            {
                sr.color = ColorForTile(gx, gy);
            }
        }

        private Color ColorForTile(int gx, int gy)
        {
            if (_banList != null && _banList.IsBanned(gx, gy)) return _bannedTileColor;
            return _tileColor;
        }
    }
}
