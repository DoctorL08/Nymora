using System.Collections;
using Nymora.Combat.View.Animation;
using Nymora.Core.View;
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
        [Tooltip("Piège Bondissant (catapulte) — overlay JAUNE pour le distinguer (refonte 29 mai).")]
        [SerializeField] private Color _bondissantColor = new Color(0.98f, 0.85f, 0.15f, 0.95f);

        [Header("Flèche de direction (Piège Bondissant, owner only — patch 5 juin)")]
        [Tooltip("Couleur de la flèche SVG indiquant la direction d'éjection choisie. Visible " +
                 "UNIQUEMENT par le poseur (le casteur).")]
        [SerializeField] private Color _bondissantArrowColor = new Color(1f, 0.97f, 0.55f, 1f);
        [Tooltip("Taille MONDE cible de la flèche, en units (une case ≈ 2 units -> ~0.4-0.7 pour une " +
                 "petite flèche au centre du piège). Le scale réel est CALCULÉ depuis les bounds du " +
                 "sprite SVG importé (dont la taille monde est imprévisible), donc cette valeur reste " +
                 "stable quel que soit l'import.")]
        [SerializeField, Min(0.05f)] private float _arrowWorldSize = 0.3f;
        [Tooltip("Offset de sortingOrder de la flèche au-dessus de la rune du piège.")]
        [SerializeField] private int _arrowSortingOffset = 3;

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
        private Coroutine[] _emergeCos; // J8 — anim "sort du sol" par overlay

        // Patch 5 juin — flèche de direction du Piège Bondissant (owner only). Un overlay par case,
        // créé inactif, activé/orienté dans LateUpdate quand un Bondissant du viewer est posé.
        private GameObject[] _arrowGOs;
        private SpriteRenderer[] _arrowRenderers;
        // Patch 8 juin — compteur de tours restants au-dessus de chaque piège (CASTER-only).
        private TextMesh[] _counterTMs;
        private static Sprite _arrowSpriteCache;
        private static bool _arrowSpriteLoaded;

        private static Sprite GetArrowSprite()
        {
            if (_arrowSpriteLoaded) return _arrowSpriteCache;
            _arrowSpriteLoaded = true;
            _arrowSpriteCache = Resources.Load<Sprite>("UI/Icons/direction_arrow");
            if (_arrowSpriteCache == null)
            {
                Debug.LogWarning("[Nymora.TrapView] direction_arrow.svg introuvable dans Resources/UI/Icons " +
                                 "(import SVG -> TexturedSprite). La flèche de direction du Piège Bondissant " +
                                 "ne s'affichera pas tant que l'asset n'est pas importé.");
            }
            return _arrowSpriteCache;
        }

        // Convertit _arrowWorldSize (units) en scale local, depuis la plus grande dimension monde du
        // sprite (bounds = taille à scale 1). Rend la flèche petite et stable quelle que soit la
        // taille monde aléatoire du SVG TexturedSprite importé.
        private float ComputeArrowScale(Sprite sprite)
        {
            if (sprite == null) return _arrowWorldSize;
            Vector3 size = sprite.bounds.size;
            float maxDim = Mathf.Max(size.x, size.y);
            return maxDim > 0.0001f ? _arrowWorldSize / maxDim : _arrowWorldSize;
        }

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
            _emergeCos = new Coroutine[count];
            _arrowGOs = new GameObject[count];
            _arrowRenderers = new SpriteRenderer[count];
            _counterTMs = new TextMesh[count];
            Sprite arrowSprite = GetArrowSprite();
            // Scale calculé depuis les bounds RÉELS du sprite (taille monde SVG imprévisible) pour
            // atteindre _arrowWorldSize units, peu importe l'import. Évite la flèche géante.
            float arrowScale = ComputeArrowScale(arrowSprite);

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
                    // #8 (5 juin) — les pièges rendent AU-DESSUS du surlignage de portée (highlight à
                    //   +HighlightSortingOffset) pour rester visibles pendant le ciblage : sans ça le bleu
                    //   castable recouvrait la rune et on ne voyait plus qu'il y a un piège sous la case.
                    //   Reste largement sous les combattants (~700+). +_sortingOrderOffset = réglage fin.
                    sr.sortingOrder = tile.SortingOrder + TileView.HighlightSortingOffset + _sortingOrderOffset;
                    sr.color = _filetColor;

                    var anim = go.AddComponent<SpriteAnimator>();
                    anim.SetFrames(_trapFrames, _framesPerSecond, loop: true);

                    go.SetActive(false);

                    _overlayGOs[idx] = go;
                    _overlayRenderers[idx] = sr;
                    _overlayAnimators[idx] = anim;
                    _currentKind[idx] = TrapKind.None;
                    _currentOwner[idx] = -1;

                    // Patch 5 juin — flèche de direction (Piège Bondissant, owner only). Enfant de la
                    //   tile (pas de l'overlay rune : évite d'hériter de son scale 2x / flip mines).
                    //   Sortant au-dessus de la rune. Sprite null toléré (warning loggé une fois) -> juste
                    //   pas de flèche tant que le SVG n'est pas importé.
                    var arrowGo = new GameObject($"TrapDirArrow_{x}_{y}");
                    arrowGo.transform.SetParent(tile.transform, false);
                    arrowGo.transform.localPosition = Vector3.zero;
                    arrowGo.transform.localScale = new Vector3(arrowScale, arrowScale, 1f);
                    var asr = arrowGo.AddComponent<SpriteRenderer>();
                    asr.sortingLayerName = tile.SortingLayerName;
                    asr.sortingOrder = sr.sortingOrder + _arrowSortingOffset;
                    asr.sprite = arrowSprite;
                    asr.color = _bondissantArrowColor;
                    arrowGo.SetActive(false);
                    _arrowGOs[idx] = arrowGo;
                    _arrowRenderers[idx] = asr;

                    // Patch 8 juin — compteur de tours restants (CASTER-only), au TOP de la case (au-dessus
                    //   de la rune ET de la flèche du Bondissant, qui sont centrées -> évite la superposition).
                    var cntGo = new GameObject($"TrapCounter_{x}_{y}");
                    cntGo.transform.SetParent(tile.transform, false);
                    cntGo.transform.localPosition = new Vector3(0f, 0.2f, 0f); // top de la case
                    var tm = cntGo.AddComponent<TextMesh>();
                    tm.anchor = TextAnchor.MiddleCenter;
                    tm.alignment = TextAlignment.Center;
                    tm.characterSize = 0.035f;
                    tm.fontSize = 72;
                    tm.color = new Color(0.75f, 1f, 0.6f, 0.95f);
                    tm.richText = false;
                    var cntFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (cntFont != null)
                    {
                        tm.font = cntFont;
                        var cmr = cntGo.GetComponent<MeshRenderer>();
                        if (cmr != null)
                        {
                            cmr.sharedMaterial = cntFont.material;
                            cmr.sortingLayerName = tile.SortingLayerName;
                            cmr.sortingOrder = sr.sortingOrder + 1000; // au-dessus de la rune et du highlight
                        }
                    }
                    cntGo.SetActive(false);
                    _counterTMs[idx] = tm;
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
            if (frame == null) return; // seek replay : runner relance, frame verifiee pas encore prete
            if (!frame.TryGetSingleton<CombatState>(out var state)) return;
            if (!frame.TryGetSingleton<FogSingleton>(out _)) return;

            int viewer = _forcedViewerPlayer >= 0 ? _forcedViewerPlayer : LocalPlayerResolver.Resolve();
            // #23 — liseré d'équipe (silhouette) sur les pièges UNIQUEMENT en match miroir.
            bool mirror = MatchViewHelpers.IsMirrorMatch(frame);

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    int idx = y * _width + x;
                    if (_overlayGOs[idx] == null) continue;

                    TrapKind kind = FogHelpers.GetTrapKind(frame, x, y);
                    int owner = FogHelpers.GetTrapOwner(frame, x, y);
                    // Refonte 29 mai — pièges VISIBLES par défaut, visibilité DYNAMIQUE (pas de voile) :
                    //   le propriétaire voit toujours ses pièges ; l'adversaire les voit SAUF si le
                    //   Nightseer propriétaire est ACTUELLEMENT en phase 3 (PR 5) -> tous ses pièges
                    //   (même posés avant 5/5) disparaissent totalement, et réapparaissent s'il redescend.
                    //   Le filtre ne tourne que sur les cases à piège ennemi (rares).
                    bool show = kind != TrapKind.None
                                && (owner == viewer || !NightseerPassif.TrapsInvisibleForOwner(frame, owner));

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
                            // J8 — la rune SORT DU SOL : pop d'apparition (back-out) a l'activation.
                            if (_emergeCos[idx] != null) StopCoroutine(_emergeCos[idx]);
                            _emergeCos[idx] = StartCoroutine(EmergeTrap(idx));
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
                    // Patch 5 juin — flèche de direction du Piège Bondissant, visible UNIQUEMENT par le
                    //   poseur (owner == viewer). Orientée vers la case d'éjection : on convertit la
                    //   direction grille (TrapDir) en angle ÉCRAN via la projection iso (la flèche SVG
                    //   pointe +x à 0°). Cachée si pas un Bondissant, pas le viewer, pas de direction
                    //   stockée (dir 0 = fallback runtime), ou sprite SVG absent.
                    var arrowGo = _arrowGOs[idx];
                    if (arrowGo != null)
                    {
                        var asr = _arrowRenderers[idx];
                        byte dir = FogHelpers.GetTrapDir(frame, x, y);
                        bool showArrow = show && kind == TrapKind.Bondissant && owner == viewer
                                         && dir != 0 && asr != null && asr.sprite != null;
                        if (showArrow)
                        {
                            int adx = 0, ady = 0;
                            switch (dir)
                            {
                                case 1: adx = 1; break;
                                case 2: adx = -1; break;
                                case 3: ady = 1; break;
                                case 4: ady = -1; break;
                            }
                            Vector3 hereW = IsoProjection.GridToWorld(x, y,
                                _gridRenderer.TileWorldWidth, _gridRenderer.TileWorldHeight);
                            Vector3 thereW = IsoProjection.GridToWorld(x + adx, y + ady,
                                _gridRenderer.TileWorldWidth, _gridRenderer.TileWorldHeight);
                            Vector2 dWorld = (Vector2)(thereW - hereW);
                            float ang = Mathf.Atan2(dWorld.y, dWorld.x) * Mathf.Rad2Deg;
                            arrowGo.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
                            asr.color = _bondissantArrowColor;
                            if (!arrowGo.activeSelf) arrowGo.SetActive(true);
                        }
                        else if (arrowGo.activeSelf)
                        {
                            arrowGo.SetActive(false);
                        }
                    }

                    // Patch 8 juin — compteur de tours restants au-dessus du piège, CASTER-only (owner == viewer).
                    //   remaining = NightseerTrapLifetimeTurns - (tour courant - tour de pose), clampé >= 1.
                    var cntTm = _counterTMs[idx];
                    if (cntTm != null)
                    {
                        bool showCnt = show && kind != TrapKind.None && owner == viewer;
                        if (showCnt)
                        {
                            int placed = FogHelpers.GetTrapAppliedOnTurn(frame, x, y);
                            int remaining = SpellRegistry.NightseerTrapLifetimeTurns - (state.TurnNumber - placed);
                            if (remaining < 1) remaining = 1;
                            string txt = remaining.ToString();
                            if (cntTm.text != txt) cntTm.text = txt;
                            if (!cntTm.gameObject.activeSelf) cntTm.gameObject.SetActive(true);
                        }
                        else if (cntTm.gameObject.activeSelf)
                        {
                            cntTm.gameObject.SetActive(false);
                        }
                    }

                    // #23 (révisé) — contour de CASE en couleur d'équipe : visible UNIQUEMENT quand
                    //   le piège est affiché ET en match miroir. Géré hors du if(show)/else pour aussi
                    //   CACHER le contour quand le piège disparaît. GetTileView = lookup tableau (cheap).
                    MirrorOutlineHelper.Refresh(_gridRenderer.GetTileView(x, y),
                        _gridRenderer.TileWorldWidth, _gridRenderer.TileWorldHeight,
                        show && mirror, owner);

                    // Refonte 29 mai — Piège Bondissant DÉCLENCHÉ : la case passe de Bondissant à
                    //   rien (ClearTrap au trigger) -> bouffée de FUMÉE à la position de la case,
                    //   pour montrer clairement où/quand le piège a sauté. Spawn une seule fois.
                    if (_currentKind[idx] == TrapKind.Bondissant && kind == TrapKind.None)
                    {
                        var psr = _overlayRenderers[idx];
                        ProceduralVfx.Smoke(transform, _overlayGOs[idx].transform.position,
                            psr.sortingLayerName, psr.sortingOrder + 5);
                    }

                    _currentKind[idx] = kind;
                    _currentOwner[idx] = owner;
                }
            }
        }

        // J8 — Pop "sort du sol" de la rune a l'apparition (scale uniforme 0 -> overshoot -> plein).
        private IEnumerator EmergeTrap(int idx)
        {
            var go = _overlayGOs[idx];
            if (go == null) yield break;
            Transform t = go.transform;
            float full = _overlayScale;
            const float dur = 0.28f;
            float e = 0f;
            while (e < dur && go != null && go.activeSelf)
            {
                e += Time.deltaTime;
                float k = Mathf.Clamp01(e / dur);
                float s = GroundEmergeEase.BackOut(k);
                t.localScale = new Vector3(full * s, full * s, 1f);
                yield return null;
            }
            if (go != null) t.localScale = new Vector3(full, full, 1f);
        }

        private Color ColorForKind(TrapKind kind)
        {
            switch (kind)
            {
                case TrapKind.FiletRonces: return _filetColor;
                case TrapKind.Mine:        return _mineColor;
                case TrapKind.Bondissant:  return _bondissantColor; // jaune (refonte 29 mai)
                default:                    return _filetColor;
            }
        }
    }
}
