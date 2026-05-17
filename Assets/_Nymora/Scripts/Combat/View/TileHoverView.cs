using Nymora.Combat.Grid;
using Nymora.Combat.View.Obstacles;
using Nymora.Combat.View.HUD;
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

        [Tooltip("POLISH-5d (17 mai) : highlight le combatant sous la souris + tooltip HP. " +
                 "Requiert un CombatantTooltipView dans la scene (auto-singleton).")]
        [SerializeField] private bool _enableCombatantHover = true;

        private Vector3 _centerOffset;
        private bool _gridReady;

        // Tracking de la cellule survolee precedemment pour restore proprement.
        private int _prevHoverX = int.MinValue;
        private int _prevHoverY = int.MinValue;
        private TileView _prevTile;
        private ObstacleView _prevObstacle;
        private CombatantView _prevCombatant;

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

            // POLISH-5e (17 mai) : on consulte GridConstants au lieu de hardcoder 15/17.
            // Permet de resize la grille sans forker les bounds View.
            int gridWidth = Quantum.GridConstants.Width;
            int gridHeight = Quantum.GridConstants.Height;
            bool outOfGrid = gx < 0 || gx >= gridWidth || gy < 0 || gy >= gridHeight;

            // POLISH-5d (17 mai) — detection combatant sprite-based (independant de la
            // case logique). Le sprite combatant peut depasser sa tile en hauteur (sprite
            // 1.16x avec child Visual Y -0.22). Si la souris est au-dessus du sprite mais
            // pas sur sa case grille, on detecte quand meme. Le hover combatant est calcule
            // chaque frame (peu de combatants en jeu, O(N) negligeable) ; tile/obstacle
            // restent gerees par changement de case (cf early-return ci-dessous).
            CombatantView hoveredCombatant = _enableCombatantHover
                ? FindCombatantViewByMouse(mouseWorld)
                : null;
            UpdateCombatantHover(hoveredCombatant);

            // Pas de changement de cellule (tile/obstacle) : rien a faire pour ces 2.
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

            // Apply nouveau hover tile/obstacle.
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

        /// <summary>
        /// POLISH-5d — Applique/restore le hover combatant detecte par sprite bounds.
        /// Diff par rapport au precedent _prevCombatant : si change, clear l'ancien et
        /// applique le nouveau (+ tooltip HP).
        /// </summary>
        private void UpdateCombatantHover(CombatantView next)
        {
            if (next == _prevCombatant) return;

            // Restore l'ancien.
            if (_prevCombatant != null)
            {
                _prevCombatant.ClearHighlight();
                if (CombatantTooltipView.Instance != null) CombatantTooltipView.Instance.Hide();
            }
            _prevCombatant = next;

            // Apply le nouveau.
            if (next != null)
            {
                next.ApplyHighlight();
                if (CombatantTooltipView.Instance != null && TryGetCombatantHp(next.Entity, out int hp, out int maxHp))
                {
                    CombatantTooltipView.Instance.Show(hp, maxHp);
                }
            }
        }

        /// <summary>
        /// POLISH-5d — Detection sprite-based : iterate tous les CombatantView, check si la
        /// position monde de la souris est dans les bounds du SpriteRenderer du combatant.
        /// Plus precis que la detection grille quand le sprite deborde de sa tile (sprite
        /// scale 1.16x + child Visual Y -0.22 chez plusieurs classes). Si plusieurs sprites
        /// se chevauchent visuellement, le plus haut sortingOrder gagne.
        /// </summary>
        private static CombatantView FindCombatantViewByMouse(Vector3 mouseWorld)
        {
            var views = Object.FindObjectsByType<CombatantView>(FindObjectsSortMode.None);
            CombatantView best = null;
            int bestSortingOrder = int.MinValue;
            for (int i = 0; i < views.Length; i++)
            {
                var v = views[i];
                if (v == null || !v.isActiveAndEnabled) continue;
                var sr = v.GetComponentInChildren<SpriteRenderer>();
                if (sr == null || !sr.enabled || sr.sprite == null) continue;
                Bounds b = sr.bounds;
                // Aplatit le check sur (x, y) — z ignore car notre monde combat est 2D.
                if (mouseWorld.x < b.min.x || mouseWorld.x > b.max.x) continue;
                if (mouseWorld.y < b.min.y || mouseWorld.y > b.max.y) continue;
                if (sr.sortingOrder > bestSortingOrder)
                {
                    bestSortingOrder = sr.sortingOrder;
                    best = v;
                }
            }
            return best;
        }

        /// <summary>
        /// Query le frame Quantum verifie pour recuperer HP/MaxHP du combatant. Retourne false
        /// si le runner n'est pas pret, si l'entity n'existe pas ou si le component Combatant
        /// n'est pas trouve.
        /// </summary>
        private static bool TryGetCombatantHp(EntityRef entity, out int hp, out int maxHp)
        {
            hp = 0;
            maxHp = 0;
            var runner = QuantumRunner.Default;
            if (runner == null || runner.Game == null) return false;
            var frame = runner.Game.Frames.Verified;
            if (!frame.Exists(entity)) return false;
            if (!frame.Has<Combatant>(entity)) return false;
            var combatant = frame.Get<Combatant>(entity);
            hp = combatant.HP;
            maxHp = combatant.MaxHP;
            return true;
        }
    }
}
