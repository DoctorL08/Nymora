using Quantum;
using UnityEngine;

namespace Nymora.Combat.View
{
    /// <summary>
    /// 2.14 — Rendu du brouillard de guerre Nightseer.
    ///
    /// Pour chaque case voilee (FogSingleton.Tiles[idx].VeiledByPlayer > 0) :
    ///   - Si le viewer == owner du voile : pas de masque (l'auteur du voile voit normalement).
    ///   - Si le viewer != owner : overlay sombre par-dessus la case + tout son contenu
    ///     (combattants, marques, VFX...) pour masquer l'info a l'adversaire.
    ///
    /// Le "viewer" = LocalPlayerIndex du client courant (via LocalPlayerResolver). Chaque
    /// client a un POV fixe sur ses voiles propres (hint subtil) vs ceux des autres (brume
    /// sombre opaque), peu importe le tour. Fix bug 18 mai : auparavant base sur
    /// state.ActivePlayerIndex -> pendant le tour du Nightseer, l'adversaire voyait un hint
    /// subtle violet au lieu d'une brume sombre opaque, et les pieges devenaient visibles
    /// (cf TrapView meme bug).
    ///
    /// Mode GM "reveal all" : toggle via le checkbox `_revealAll` dans l'Inspector. Le raccourci
    /// clavier F12 a ete supprime a la cloture polish Phase 3 (17 mai 2026).
    ///
    /// Architecture similaire a TerrainView : spawn un overlay GO enfant de chaque TileView a
    /// l'init, puis poll en LateUpdate pour activer/desactiver selon l'etat sim.
    /// </summary>
    public class FogOfWarView : MonoBehaviour
    {
        [Header("Dependances")]
        [SerializeField] private GridRenderer _gridRenderer;

        [Header("Perspective (POV)")]
        [Tooltip("Si >= 0, force le POV sur ce playerIndex (pour tests).\n" +
                 "-1 (default) : POV = LocalPlayerIndex du client courant (via LocalPlayerResolver). " +
                 "Chaque client voit ses propres voiles comme hint et les voiles adverses comme brume.")]
        [SerializeField] private int _forcedViewerPlayer = -1;

        [Header("Rendu")]
        [Tooltip("Couleur overlay 'brume' cote adversaire (non-owner). Tuning 18 mai : passe " +
                 "de noir-opaque-0.92 a violet-sombre-semi-transparent-0.65 pour atmosphere " +
                 "Nightseer plus subtile. Alpha 0.65 = on devine le damier dessous, teinte " +
                 "violet matche l'identite mage des ombres.")]
        [SerializeField] private Color _fogColorEnemy = new Color(0.18f, 0.10f, 0.28f, 0.65f);

        [Tooltip("Indicateur subtil cote proprietaire (le caster voit ses propres voiles). " +
                 "Affiche uniquement sur les voiles SANS piege (sinon TrapView prend le relais).")]
        [SerializeField] private Color _fogColorOwner = new Color(0.45f, 0.30f, 0.85f, 0.30f);

        [Tooltip("SortingOrder du fog en mode ENNEMI. 3000 = au-dessus des combattants (~1000), " +
                 "marques (1500), VFX one-shot (2000). Garantit que le voile masque tout cote " +
                 "adversaire (brouillard veritable).")]
        [SerializeField] private int _sortingOrderEnemy = 3000;

        [Tooltip("SortingOrder du fog en mode OWNER (hint subtle violet 0.30 alpha). 999 = " +
                 "juste SOUS les combatants (~1000) mais au-dessus des terrains/marques/VFX. " +
                 "Le Nightseer voit ses propres voiles comme un hint au sol sans qu'ils cachent " +
                 "son perso. Different du mode ennemi qui doit masquer le combatant adverse.")]
        [SerializeField] private int _sortingOrderOwner = 999;

        [Tooltip("Scale du GO d'overlay (compense PPU des sheets — cf TerrainView).")]
        [SerializeField, Min(0.5f)] private float _overlayScale = 2f;

        [Header("Debug")]
        [Tooltip("Mode GM : revele toutes les cases voilees (utile pour tester le framework). " +
                 "Toggle via ce checkbox Inspector uniquement (raccourci F12 supprime polish Phase 3).")]
        [SerializeField] private bool _revealAll = false;

        // Runtime state.
        private bool _ready;
        private bool _overlaysSpawned;
        private int _width;
        private int _height;
        private GameObject[] _overlayGOs;
        private SpriteRenderer[] _overlayRenderers;
        // Cache pour eviter SetActive/color a chaque frame (perf + evite glitches d'anim).
        // 0 = off, 1 = enemy-dark, 2 = owner-hint
        private byte[] _currentMode;

        private void Awake()
        {
            QuantumCallback.Subscribe(this, (CallbackGameStarted c) => OnGameStarted(c.Game));
        }

        private void OnGameStarted(QuantumGame game)
        {
            if (_gridRenderer == null)
            {
                Debug.LogError("[Nymora.FogView] GridRenderer manquant — drag le composant.", this);
                return;
            }
            var frame = game.Frames.Verified;
            if (!frame.TryGetSingleton<GridSingleton>(out var grid))
            {
                Debug.LogError("[Nymora.FogView] GridSingleton introuvable.", this);
                return;
            }
            _width = grid.Width;
            _height = grid.Height;
            _ready = true;
            // Spawn des overlays differe au 1er LateUpdate (cf pattern TerrainView).
        }

        private void TrySpawnOverlays()
        {
            var tile00 = _gridRenderer.GetTileView(0, 0);
            var tileLast = _gridRenderer.GetTileView(_width - 1, _height - 1);
            if (tile00 == null || tileLast == null) return;

            int count = _width * _height;
            _overlayGOs = new GameObject[count];
            _overlayRenderers = new SpriteRenderer[count];
            _currentMode = new byte[count];

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    int idx = y * _width + x;
                    var tile = _gridRenderer.GetTileView(x, y);
                    if (tile == null) continue;

                    var go = new GameObject($"FogOverlay_{x}_{y}");
                    go.transform.SetParent(tile.transform, false);
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localScale = new Vector3(_overlayScale, _overlayScale, 1f);

                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = tile.FloorSprite;
                    sr.color = _fogColorEnemy;
                    sr.sortingLayerName = tile.SortingLayerName;
                    sr.sortingOrder = _sortingOrderEnemy;

                    go.SetActive(false);

                    _overlayGOs[idx] = go;
                    _overlayRenderers[idx] = sr;
                    _currentMode[idx] = 0;
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
            if (frame == null) return; // seek replay : runner relance, frame verifiee pas encore prete
            if (!frame.TryGetSingleton<CombatState>(out var state)) return;
            if (!frame.TryGetSingleton<FogSingleton>(out _)) return;

            int viewer = _forcedViewerPlayer >= 0 ? _forcedViewerPlayer : LocalPlayerResolver.Resolve();

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    int idx = y * _width + x;
                    if (_overlayGOs[idx] == null) continue;

                    int owner = FogHelpers.GetVeilOwner(frame, x, y);
                    byte mode = 0; // 0 = off

                    if (owner >= 0)
                    {
                        if (_revealAll)
                        {
                            mode = 1; // GM mode : tout revele en sombre
                        }
                        else if (owner != viewer)
                        {
                            mode = 1; // adversaire : overlay sombre
                        }
                        else
                        {
                            // Owner : indicateur subtil UNIQUEMENT si pas de piege
                            // (sinon TrapView a deja affiche un overlay vert/rouge plus parlant).
                            bool hasTrap = FogHelpers.GetTrapKind(frame, x, y) != TrapKind.None;
                            if (!hasTrap) mode = 2;
                        }
                    }

                    // Applique color + sortingOrder CHAQUE frame quand mode != 0. Idempotent :
                    // robuste au hot-reload Unity et au tweak des sortingOrders en Play Mode.
                    if (mode == 0)
                    {
                        if (_currentMode[idx] != 0) _overlayGOs[idx].SetActive(false);
                    }
                    else
                    {
                        var sr = _overlayRenderers[idx];
                        if (mode == 1) // ennemi : masque tout (au-dessus des combatants)
                        {
                            sr.color = _fogColorEnemy;
                            sr.sortingOrder = _sortingOrderEnemy;
                        }
                        else // owner : hint subtle sous les combatants
                        {
                            sr.color = _fogColorOwner;
                            sr.sortingOrder = _sortingOrderOwner;
                        }
                        if (_currentMode[idx] != mode) _overlayGOs[idx].SetActive(true);
                    }
                    _currentMode[idx] = mode;
                }
            }
        }
    }
}
