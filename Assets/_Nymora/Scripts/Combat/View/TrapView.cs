using Nymora.Combat.View.Animation;
using Quantum;
using UnityEngine;

namespace Nymora.Combat.View
{
    /// <summary>
    /// 2.15.b — Indicateur visuel des pieges Nightseer (Filet de Ronces, Mine).
    ///
    /// Visible UNIQUEMENT par le proprietaire du piege (le Nightseer poseur), du POV
    /// du CLIENT local (pas du joueur dont c'est le tour). Fix bug 18 mai : auparavant
    /// base sur ActivePlayerIndex -> pendant le tour du Nightseer, l'adversaire voyait
    /// les pieges (POV = ActivePlayer = Nightseer = match). Maintenant base sur
    /// LocalPlayerResolver.Resolve() -> chaque client voit ses propres pieges, peu
    /// importe le tour.
    ///
    /// L'adversaire continue de voir l'overlay sombre du Voile via FogOfWarView et ne sait
    /// pas si la case sombre cache un piege ou un voile vide (mindgame Bible V7.1).
    ///
    /// Architecture identique a TerrainView (Soulrender) : spawn d'un overlay GO enfant de
    /// chaque TileView, puis poll en LateUpdate selon FogSingleton.Tiles[].Trap. Quand un
    /// piege est actif cote owner, on cache le sol echiquier sous la tile (SetFloorVisible
    /// false) — le sprite runique remplace visuellement la case, comme Sang Coagule ou
    /// Vapeur Carmin. C'est ce qui donne le rendu "ancre au sol" plutot qu'un bloc
    /// superpose qui depasse.
    ///
    /// Visuel : sprite-sheet runique 4 frames (tiles_piege_runique_4frame.png) anime via
    /// SpriteAnimator, teinte au runtime selon TrapKind (vert = Filet, rouge = Mine). Un seul
    /// sprite generique pour tous les pieges — la teinte porte la lecture cote proprietaire,
    /// l'adversaire ne distingue rien sous son voile.
    /// </summary>
    public class TrapView : MonoBehaviour
    {
        [Header("Dependances")]
        [SerializeField] private GridRenderer _gridRenderer;

        [Header("Perspective (POV)")]
        [Tooltip("Si >= 0, force le POV sur ce playerIndex (tests). -1 (default) : POV = " +
                 "joueur LOCAL du client (via LocalPlayerResolver). Chaque client voit ses " +
                 "propres pieges peu importe le tour.")]
        [SerializeField] private int _forcedViewerPlayer = -1;

        [Header("Sprite runique anime")]
        [Tooltip("Frames du sprite-sheet tiles_piege_runique_4frame.png (slice Multiple). " +
                 "Drag les 4 sub-sprites dans l'ordre. Tous les TrapKind partagent ces frames, " +
                 "differenciation visuelle par teinte.")]
        [SerializeField] private Sprite[] _trapFrames;

        [Tooltip("Cadence d'animation des frames runiques (4 frames a 4 fps = ~1s par cycle, " +
                 "aligne sur la convention MarksView/TerrainView).")]
        [SerializeField, Min(0.5f)] private float _framesPerSecond = 4f;

        [Header("Teintes (multiply sur le sprite)")]
        [Tooltip("Filet de Ronces — overlay vert semi-transparent.")]
        [SerializeField] private Color _filetColor = new Color(0.20f, 0.85f, 0.20f, 0.95f);
        [Tooltip("Mine (Champ de Mines) — overlay rouge semi-transparent.")]
        [SerializeField] private Color _mineColor = new Color(0.95f, 0.20f, 0.20f, 0.95f);

        [Header("Rendu")]
        [Tooltip("Offset de sortingOrder par rapport a la tile de base (1 = juste au-dessus du sol, " +
                 "aligne sur TerrainView).")]
        [SerializeField] private int _sortingOrderOffset = 1;

        [Tooltip("Scale uniforme applique au GameObject de l'overlay. Sert a compenser la " +
                 "taille des sheets piege par rapport aux tiles : sheet 128x128 @ PPU 128 = 1 unit, " +
                 "alors qu'une case = 2 unit -> scale 2.0. Aligne sur TerrainView.")]
        [SerializeField, Min(0.1f)] private float _overlayScale = 2f;

        [Tooltip("Si vrai (recommande, aligne sur TerrainView), cache le sprite du sol echiquier " +
                 "sous la case quand un piege est actif cote owner, pour que la rune remplace " +
                 "visuellement la case au lieu de se superposer. C'est ce qui rend le piege " +
                 "ancre au sol comme Sang Coagule / Vapeur Carmin.")]
        [SerializeField] private bool _hideFloorWhenTrapActive = true;

        private bool _ready;
        private bool _spawned;
        private int _width;
        private int _height;
        private GameObject[] _overlayGOs;
        private SpriteRenderer[] _overlayRenderers;
        private SpriteAnimator[] _overlayAnimators;
        private TrapKind[] _currentKind;
        private int[] _currentOwner;

        private void Awake()
        {
            QuantumCallback.Subscribe(this, (CallbackGameStarted c) => OnGameStarted(c.Game));
        }

        private void OnGameStarted(QuantumGame game)
        {
            if (_gridRenderer == null)
            {
                Debug.LogError("[Nymora.TrapView] GridRenderer manquant — drag le composant.", this);
                return;
            }
            if (_trapFrames == null || _trapFrames.Length == 0)
            {
                Debug.LogError("[Nymora.TrapView] _trapFrames vide — drag les 4 sub-sprites de " +
                               "tiles_piege_runique_4frame.png dans l'inspector.", this);
                return;
            }
            var frame = game.Frames.Verified;
            if (!frame.TryGetSingleton<GridSingleton>(out var grid))
            {
                Debug.LogError("[Nymora.TrapView] GridSingleton introuvable.", this);
                return;
            }
            _width = grid.Width;
            _height = grid.Height;
            _ready = true;
        }

        private void TrySpawnOverlays()
        {
            var tile00 = _gridRenderer.GetTileView(0, 0);
            var tileLast = _gridRenderer.GetTileView(_width - 1, _height - 1);
            if (tile00 == null || tileLast == null) return;

            int count = _width * _height;
            _overlayGOs = new GameObject[count];
            _overlayRenderers = new SpriteRenderer[count];
            _overlayAnimators = new SpriteAnimator[count];
            _currentKind = new TrapKind[count];
            _currentOwner = new int[count];

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    int idx = y * _width + x;
                    var tile = _gridRenderer.GetTileView(x, y);
                    if (tile == null) continue;

                    var go = new GameObject($"TrapIndicator_{x}_{y}");
                    go.transform.SetParent(tile.transform, false);
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localScale = new Vector3(_overlayScale, _overlayScale, 1f);

                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sortingLayerName = tile.SortingLayerName;
                    sr.sortingOrder = tile.SortingOrder + _sortingOrderOffset;
                    sr.color = _filetColor;

                    var anim = go.AddComponent<SpriteAnimator>();
                    anim.SetFrames(_trapFrames, _framesPerSecond, loop: true);

                    go.SetActive(false);

                    _overlayGOs[idx] = go;
                    _overlayRenderers[idx] = sr;
                    _overlayAnimators[idx] = anim;
                    _currentKind[idx] = TrapKind.None;
                    _currentOwner[idx] = -1;
                }
            }

            _spawned = true;
        }

        private void LateUpdate()
        {
            if (!_ready) return;
            if (!_spawned)
            {
                TrySpawnOverlays();
                if (!_spawned) return;
            }

            var game = QuantumRunner.Default?.Game;
            if (game == null) return;
            var frame = game.Frames.Verified;
            if (!frame.TryGetSingleton<CombatState>(out var state)) return;
            if (!frame.TryGetSingleton<FogSingleton>(out _)) return;

            int viewer = _forcedViewerPlayer >= 0 ? _forcedViewerPlayer : LocalPlayerResolver.Resolve();

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    int idx = y * _width + x;
                    if (_overlayGOs[idx] == null) continue;

                    TrapKind kind = FogHelpers.GetTrapKind(frame, x, y);
                    int owner = FogHelpers.GetTrapOwner(frame, x, y);
                    bool show = kind != TrapKind.None && owner == viewer;

                    bool wasActive = _overlayGOs[idx].activeSelf;
                    bool kindOrOwnerChanged = _currentKind[idx] != kind || _currentOwner[idx] != owner;

                    if (show)
                    {
                        // Applique color + flip CHAQUE frame quand actif (idempotent, robuste
                        // au hot-reload Unity et au tweak Inspector en Play Mode).
                        var sr = _overlayRenderers[idx];
                        sr.color = ColorForKind(kind);
                        // Champ de Mines pose 3 mines en cluster 3x3 — varier flipX/flipY
                        // selon un hash deterministe de (x,y) pour casser le pattern visuel
                        // repetitif. Filet de Ronces est unique, reste en orientation normale.
                        if (kind == TrapKind.Mine)
                        {
                            int variant = (x * 7 + y * 13) & 3;
                            sr.flipX = (variant & 1) != 0;
                            sr.flipY = (variant & 2) != 0;
                        }
                        else
                        {
                            sr.flipX = false;
                            sr.flipY = false;
                        }
                        if (!wasActive)
                        {
                            _overlayGOs[idx].SetActive(true);
                            if (_hideFloorWhenTrapActive)
                            {
                                var tile = _gridRenderer.GetTileView(x, y);
                                if (tile != null) tile.SetFloorVisible(false);
                            }
                        }
                    }
                    else
                    {
                        if (wasActive)
                        {
                            _overlayGOs[idx].SetActive(false);
                            if (_hideFloorWhenTrapActive)
                            {
                                var tile = _gridRenderer.GetTileView(x, y);
                                if (tile != null) tile.SetFloorVisible(true);
                            }
                        }
                    }
                    _currentKind[idx] = kind;
                    _currentOwner[idx] = owner;
                }
            }
        }

        private Color ColorForKind(TrapKind kind)
        {
            switch (kind)
            {
                case TrapKind.FiletRonces: return _filetColor;
                case TrapKind.Mine:        return _mineColor;
                default:                    return _filetColor;
            }
        }
    }
}
