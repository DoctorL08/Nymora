using Quantum;
using UnityEngine;

namespace Nymora.Combat.View.Animation
{
    /// <summary>
    /// Affiche les terrains animes (Vapeur Carmin, Sang Coagule, ...) sur la grille de
    /// combat. Spawn un GameObject "TerrainOverlay" enfant de chaque TileView a l'init,
    /// puis poll chaque LateUpdate le TerrainKind de chaque case via
    /// Quantum.GridHelpers.GetTerrainKind (qui encapsule l'acces unsafe au singleton).
    ///
    /// Comportement :
    ///   - Au demarrage : overlay desactive sur chaque tile.
    ///   - Quand un terrain est pose : active l'overlay + branche le SpriteAnimator
    ///     avec les frames de la TerrainSpriteLibrary, FPS global du composant.
    ///   - Quand le terrain expire : clear + desactive l'overlay.
    ///
    /// L'overlay rend AU-DESSUS du sol echiquier (sortingOrder = TileView + offset).
    /// Pure View — n'affecte pas la simulation Quantum.
    /// </summary>
    public class TerrainView : MonoBehaviour
    {
        [Header("Dependances")]
        [SerializeField] private GridRenderer _gridRenderer;
        [SerializeField] private TerrainSpriteLibrary _library;

        [Header("Rendu")]
        [Tooltip("Offset de sortingOrder par rapport a la tile de base (1 = juste au-dessus du sol).")]
        [SerializeField] private int _sortingOrderOffset = 1;

        [Tooltip("Cadence de l'animation des terrains (frames par seconde).")]
        [SerializeField, Min(0.5f)] private float _framesPerSecond = 4f;

        [Tooltip("Scale uniforme applique au GameObject de l'overlay. Sert a compenser la " +
                 "taille des sheets terrain par rapport aux tiles : sheets 64x64 @ PPU 128 " +
                 "= 0.5 unit, alors qu'une case = 1.0 unit -> scale 2.0.")]
        [SerializeField, Min(0.1f)] private float _overlayScale = 2f;

        [Tooltip("Si vrai (recommande), cache le sprite du sol echiquier sous la case " +
                 "quand un terrain est pose, pour que le terrain remplace visuellement la " +
                 "case au lieu de se superposer.")]
        [SerializeField] private bool _hideFloorWhenTerrainActive = true;

        private bool _ready;          // Grid singleton lue (dimensions connues)
        private bool _overlaysSpawned; // TileViews disponibles ET overlays cree
        private int _width;
        private int _height;

        private GameObject[] _overlayGOs;
        private SpriteAnimator[] _overlayAnimators;
        private TerrainKind[] _currentKind;

        private void Awake()
        {
            QuantumCallback.Subscribe(this, (CallbackGameStarted c) => OnGameStarted(c.Game));
        }

        private void OnGameStarted(QuantumGame game)
        {
            if (_gridRenderer == null)
            {
                Debug.LogError("[Nymora.TerrainView] GridRenderer manquant — drag le composant.", this);
                return;
            }
            if (_library == null)
            {
                Debug.LogError("[Nymora.TerrainView] TerrainSpriteLibrary manquant — drag l'asset.", this);
                return;
            }

            var frame = game.Frames.Verified;
            if (!frame.TryGetSingleton<GridSingleton>(out var grid))
            {
                Debug.LogError("[Nymora.TerrainView] GridSingleton introuvable.", this);
                return;
            }

            _width = grid.Width;
            _height = grid.Height;
            _ready = true;
            // Le spawn des overlays est differe au 1er LateUpdate ou GetTileView retourne non-null
            // pour eviter une dependance d'ordre sur CallbackGameStarted entre GridRenderer
            // et TerrainView (l'ordre des subscribers n'est pas garanti par QuantumCallback).
        }

        private void TrySpawnOverlays()
        {
            // Verifie que toutes les tiles sont spawnees cote View.
            var tile00 = _gridRenderer.GetTileView(0, 0);
            var tileLast = _gridRenderer.GetTileView(_width - 1, _height - 1);
            if (tile00 == null || tileLast == null) return;

            int count = _width * _height;
            _overlayGOs = new GameObject[count];
            _overlayAnimators = new SpriteAnimator[count];
            _currentKind = new TerrainKind[count];

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    int idx = y * _width + x;
                    var tile = _gridRenderer.GetTileView(x, y);
                    if (tile == null) continue;

                    var go = new GameObject($"TerrainOverlay_{x}_{y}");
                    go.transform.SetParent(tile.transform, false);
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localScale = new Vector3(_overlayScale, _overlayScale, 1f);

                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sortingLayerName = tile.SortingLayerName;
                    sr.sortingOrder = tile.SortingOrder + _sortingOrderOffset;

                    var anim = go.AddComponent<SpriteAnimator>();

                    go.SetActive(false);

                    _overlayGOs[idx] = go;
                    _overlayAnimators[idx] = anim;
                    _currentKind[idx] = TerrainKind.None;
                }
            }

            _overlaysSpawned = true;
        }

        private void LateUpdate()
        {
            if (!_ready) return;

            if (!_overlaysSpawned)
            {
                TrySpawnOverlays();
                if (!_overlaysSpawned) return;
            }

            var game = QuantumRunner.Default?.Game;
            if (game == null) return;

            var frame = game.Frames.Verified;
            // Securite : si la GridSingleton n'est pas encore prete (resync), on skip.
            if (!frame.TryGetSingleton<GridSingleton>(out _)) return;

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    int idx = y * _width + x;
                    TerrainKind kind = GridHelpers.GetTerrainKind(frame, x, y);
                    if (_currentKind[idx] == kind) continue;

                    ApplyTerrain(idx, kind);
                    _currentKind[idx] = kind;
                }
            }
        }

        private void ApplyTerrain(int idx, TerrainKind kind)
        {
            var go = _overlayGOs[idx];
            var anim = _overlayAnimators[idx];
            if (go == null || anim == null) return;

            // Retrouve le TileView parent pour piloter la visibilite du sol echiquier.
            // (Garde l'API decouple : on n'a pas cache un ref direct au TileView par overlay
            //  pour pouvoir gerer si le tile est detruit/recree.)
            int width = _width;
            int x = idx % width;
            int y = idx / width;
            TileView tile = _gridRenderer != null ? _gridRenderer.GetTileView(x, y) : null;

            if (kind == TerrainKind.None)
            {
                anim.Clear();
                go.SetActive(false);
                if (_hideFloorWhenTerrainActive && tile != null) tile.SetFloorVisible(true);
                return;
            }

            Sprite[] frames = _library.GetFrames(kind);
            if (frames == null || frames.Length == 0)
            {
                anim.Clear();
                go.SetActive(false);
                if (_hideFloorWhenTerrainActive && tile != null) tile.SetFloorVisible(true);
                return;
            }

            go.SetActive(true);
            anim.SetFrames(frames, _framesPerSecond, loop: true);
            if (_hideFloorWhenTerrainActive && tile != null) tile.SetFloorVisible(false);
        }
    }
}
