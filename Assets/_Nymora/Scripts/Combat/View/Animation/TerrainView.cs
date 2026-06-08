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
                 "taille des sheets terrain par rapport aux tiles. POLISH-5b (17 mai) : default " +
                 "passe de 2.0 -> 1.0 car les nouveaux PNG '-export[1-4]' du designer ont une " +
                 "resolution plus haute (~128-256px) @ PPU=192, rendant deja ~0.7-1.3 unit. Avec " +
                 "scale=2 ils devenaient enormes (commentaire historique 'sheets 64x64 @ PPU 128 " +
                 "= 0.5 unit -> scale 2.0' n'est plus valide). A re-tweak Inspector si tiles trop " +
                 "petites (0.75-1.5 testable selon la resolution exacte des nouveaux assets).")]
        [SerializeField, Min(0.1f)] private float _overlayScale = 1f;

        [Tooltip("Si vrai (recommande), cache le sprite du sol echiquier sous la case " +
                 "quand un terrain est pose, pour que le terrain remplace visuellement la " +
                 "case au lieu de se superposer.")]
        [SerializeField] private bool _hideFloorWhenTerrainActive = true;

        [Tooltip("POLISH-5c (17 mai) : log verbeux a chaque changement de terrain sur une " +
                 "tile. A desactiver une fois le bug 'tiles invisibles' resolu.")]
        [SerializeField] private bool _verboseLog = true;

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
            // Securite : frame null pendant un seek replay (runner relance, pas encore de frame
            // verifiee) OU GridSingleton pas prete (resync) -> skip cette frame.
            if (frame == null) return;
            if (!frame.TryGetSingleton<GridSingleton>(out _)) return;

            // #23 — contour de case d'équipe (uniquement en match miroir). Constant sur le match.
            bool mirror = MatchViewHelpers.IsMirrorMatch(frame);

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    int idx = y * _width + x;
                    TerrainKind kind = GridHelpers.GetTerrainKind(frame, x, y);

                    // Le SPRITE de terrain (coûteux) n'est ré-appliqué qu'au changement de kind.
                    if (_currentKind[idx] != kind)
                    {
                        ApplyTerrain(idx, kind);
                        _currentKind[idx] = kind;
                    }

                    // #23 — contour de CASE en couleur d'équipe (aligné TrapView). Patch 8 juin :
                    //   rafraîchi CHAQUE frame (et non plus seulement au changement de terrain) pour
                    //   GARANTIR qu'il reste visible sur la Brume — y compris si un autre système le
                    //   masque, ou en superposition de brumes adverses. Cheap : hide pour les cases
                    //   vides (owner -1), losange pour les cases terrain. Owner relu en direct.
                    if (mirror && kind != TerrainKind.None)
                    {
                        // Patch 8 juin — masque 2 bits : 1=P0, 2=P1, 3=case contestée (owner 2 = violet).
                        byte mask = FogHelpers.GetTerrainOwnerMask(frame, x, y);
                        int owner = mask == 3 ? 2 : (mask == 1 ? 0 : (mask == 2 ? 1 : -1));
                        MirrorOutlineHelper.Refresh(_gridRenderer.GetTileView(x, y),
                            _gridRenderer.TileWorldWidth, _gridRenderer.TileWorldHeight,
                            owner >= 0, owner, MirrorOutlineHelper.ChannelTerrain);
                    }
                    else
                    {
                        // Pas de terrain (ou pas miroir) : cache le contour DU CANAL TERRAIN (sans toucher
                        // au contour des pièges, qui vit sur son propre enfant).
                        MirrorOutlineHelper.Refresh(_gridRenderer.GetTileView(x, y),
                            _gridRenderer.TileWorldWidth, _gridRenderer.TileWorldHeight,
                            false, -1, MirrorOutlineHelper.ChannelTerrain);
                    }
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
                if (_verboseLog) Debug.LogWarning($"[Nymora.Terrain] SKIP tile ({x},{y}) kind={kind} : frames=null/empty (SO field non rempli — relance CombatAssetsNormalizer).");
                anim.Clear();
                go.SetActive(false);
                if (_hideFloorWhenTerrainActive && tile != null) tile.SetFloorVisible(true);
                return;
            }

            // POLISH-5c diag : detecte les frames avec sprite null (asset fantome).
            int nullCount = 0;
            for (int i = 0; i < frames.Length; i++) if (frames[i] == null) nullCount++;
            if (_verboseLog)
            {
                string firstName = frames[0] != null ? frames[0].name : "<null>";
                Debug.Log($"[Nymora.Terrain] Apply tile ({x},{y}) kind={kind} : {frames.Length} frame(s), nullCount={nullCount}, first='{firstName}', scale={_overlayScale}, sortingOffset={_sortingOrderOffset}");
                if (nullCount > 0)
                {
                    Debug.LogError($"[Nymora.Terrain] /!\\ Terrain {kind} a {nullCount}/{frames.Length} sprite null (asset fantome). Relance Nymora > Validation > Normalize Combat Assets > Do All.");
                }
            }

            go.SetActive(true);
            anim.SetFrames(frames, _framesPerSecond, loop: true);
            if (_hideFloorWhenTerrainActive && tile != null) tile.SetFloorVisible(false);
        }
    }
}
