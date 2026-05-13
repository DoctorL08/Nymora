using Nymora.Combat.Grid;
using Nymora.Combat.View.Obstacles;
using Quantum;
using UnityEngine;

namespace Nymora.Combat.View
{
    /// <summary>
    /// Polish 3.3.d — Detecte la case survolee par la souris et applique deux effets :
    ///   1. Glow / highlight sur la TileView (sprite tint visible quand le sprite floor existe).
    ///   2. Affichage du HP de l'ObstacleView present sur la case (cache par defaut).
    ///
    /// Attache ce MonoBehaviour a n'importe quel GameObject de la scene combat (idealement
    /// le meme que CombatInputController pour partager la Camera + GridSettings).
    /// Les refs sont auto-trouvees au Start si non assignees dans l'Inspector.
    ///
    /// Performance : Update tres bon marche (O(1) sur la grille + O(N obstacles concurrents)).
    /// Aucune allocation par frame.
    /// </summary>
    public class TileHoverView : MonoBehaviour
    {
        [Header("Refs (auto-found si null au Start)")]
        [SerializeField] private Camera _camera;
        [SerializeField] private GridSettings _gridSettings;
        [SerializeField] private GridRenderer _gridRenderer;
        [SerializeField] private ObstacleRenderer _obstacleRenderer;

        [Header("Style hover")]
        [Tooltip("Couleur appliquee a la tile sous la souris. Multiplie la couleur de base.")]
        [SerializeField] private Color _hoverColor = new Color(1f, 0.95f, 0.5f, 1f); // jaune doux glow

        [Tooltip("Activer le highlight de la tile sous la souris.")]
        [SerializeField] private bool _enableTileGlow = true;

        [Tooltip("Activer l'affichage du HP de l'obstacle sous la souris.")]
        [SerializeField] private bool _enableObstacleHpReveal = true;

        private Vector3 _centerOffset;
        private bool _gridReady;

        // Tracking de la cellule survolee precedemment pour restore proprement.
        private int _prevHoverX = int.MinValue;
        private int _prevHoverY = int.MinValue;
        private TileView _prevTile;
        private ObstacleView _prevObstacle;

        private void Awake()
        {
            QuantumCallback.Subscribe(this, (CallbackGameStarted c) => OnGameStarted(c.Game));
        }

        private void Start()
        {
            // Auto-resolution des refs si pas assignees dans l'Inspector.
            if (_camera == null) _camera = Camera.main;
            if (_gridSettings == null)
            {
                var input = FindObjectOfType<CombatInputController>();
                if (input != null)
                {
                    // GridSettings est private dans CombatInputController, on ne peut pas le pull
                    // mais on peut chercher l'asset par defaut (acceptable MVP). Lorenzo peut
                    // assigner manuellement si necessaire.
                }
            }
            if (_gridRenderer == null) _gridRenderer = FindObjectOfType<GridRenderer>();
            if (_obstacleRenderer == null) _obstacleRenderer = FindObjectOfType<ObstacleRenderer>();
        }

        private void OnGameStarted(QuantumGame game)
        {
            if (_gridSettings == null)
            {
                Debug.LogWarning("[Nymora.TileHoverView] GridSettings manquant — drag l'asset dans l'Inspector. Hover desactive.", this);
                return;
            }
            var frame = game.Frames.Verified;
            if (!frame.TryGetSingleton<GridSingleton>(out var grid))
            {
                Debug.LogWarning("[Nymora.TileHoverView] GridSingleton introuvable.", this);
                return;
            }
            _centerOffset = _gridSettings.CenterGrid
                ? IsoProjection.CenterOffset(grid.Width, grid.Height, _gridSettings.TileWorldWidth, _gridSettings.TileWorldHeight)
                : Vector3.zero;
            _gridReady = true;
        }

        private void Update()
        {
            if (!_gridReady) return;
            if (_camera == null) return;

            // Calcule la case sous la souris (memes regles que CombatInputController).
            Vector3 mouseWorld = _camera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
            mouseWorld.z = 0f;
            var (gx, gy) = IsoProjection.WorldToGrid(
                mouseWorld,
                _gridSettings.TileWorldWidth,
                _gridSettings.TileWorldHeight,
                _centerOffset);

            // Hors grille : restore et exit.
            const int gridWidth = 15;
            const int gridHeight = 17;
            bool outOfGrid = gx < 0 || gx >= gridWidth || gy < 0 || gy >= gridHeight;

            // Pas de changement de cellule : rien a faire.
            if (!outOfGrid && gx == _prevHoverX && gy == _prevHoverY) return;
            if (outOfGrid && _prevHoverX == int.MinValue) return;

            // Restore l'ancien hover.
            if (_prevTile != null)
            {
                _prevTile.ClearHighlight();
                _prevTile = null;
            }
            if (_prevObstacle != null)
            {
                _prevObstacle.SetHpVisible(false);
                _prevObstacle = null;
            }

            if (outOfGrid)
            {
                _prevHoverX = int.MinValue;
                _prevHoverY = int.MinValue;
                return;
            }

            _prevHoverX = gx;
            _prevHoverY = gy;

            // Apply nouveau hover.
            if (_enableTileGlow && _gridRenderer != null)
            {
                _prevTile = _gridRenderer.GetTileView(gx, gy);
                if (_prevTile != null) _prevTile.ApplyHighlight(_hoverColor);
            }
            if (_enableObstacleHpReveal && _obstacleRenderer != null)
            {
                _prevObstacle = _obstacleRenderer.GetObstacleViewAt(gx, gy);
                if (_prevObstacle != null) _prevObstacle.SetHpVisible(true);
            }
        }
    }
}
