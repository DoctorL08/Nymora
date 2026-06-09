using Nymora.Combat.Grid;
using Quantum;
using UnityEngine;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Preview des cases atteignables au mouvement (2.13.b + polish 3.3.d hover).
    ///
    /// Comportement :
    ///   - Si un sort est arme sur le HUD -> skip (priorite au spell preview).
    ///   - Sinon, BFS depuis le combatant actif jusqu'a PM courant en CONTINU (toutes les ticks
    ///     sim) pour avoir _bestCost a jour. Mais on n'AFFICHE plus la range en permanence.
    ///   - Affichage pilote par la souris (Update Unity) :
    ///       * Souris sur la case du combatant actif -> highlight TOUTE la range (vert)
    ///       * Souris sur une case dans la range -> highlight le PATH du caster a cette case (jaune)
    ///       * Souris ailleurs -> rien
    ///
    /// Algo BFS (SPFA-like) inchange : variable cost (Vapeur Carmin +1). Buffers prealloues
    /// -> zero allocation par tick.
    /// </summary>
    public class MovementRangePreview : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private GridRenderer _gridRenderer;
        [SerializeField] private CombatHUDController _hudController;

        [Header("Hover detection (auto-found si null)")]
        [SerializeField] private Camera _camera;
        [SerializeField] private GridSettings _gridSettings;

        [Header("Couleurs preview")]
        [Tooltip("Cases atteignables affichees quand on survole le combatant actif.")]
        [SerializeField] private Color _reachableColor = new Color(0.65f, 0.92f, 0.65f, 1f);
        [Tooltip("Path affiche quand on survole une case dans la range.")]
        [SerializeField] private Color _pathColor = new Color(0.95f, 0.85f, 0.35f, 1f);

        // Buffers prealloues — reutilises chaque tick.
        private readonly int[] _bestCost = new int[GridConstants.Count];
        private readonly int[] _queueBuf = new int[GridConstants.Count * 4];
        private readonly int[] _highlightedIdx = new int[GridConstants.Count];
        // 3.5.b.iii Pas Spectral : marque les cases TRAVERSEES par un occupant ennemi
        // (autorisees par le BFS quand le Necram a PasSpectralReady, mais non-stopables).
        // Skip dans HighlightFullRange + HighlightPathTo (la case n'est pas une dest valide).
        private readonly bool[] _occupiedTraversable = new bool[GridConstants.Count];
        private int _highlightedCount;
        private bool _gridReady;
        private Vector3 _centerOffset;
        private bool _hoverReady;

        // Snapshot du combatant actif (mis a jour a chaque CallbackUpdateView).
        private int _activeCasterX = -1;
        private int _activeCasterY = -1;
        private int _activeCasterPM = 0;
        private bool _hasActiveCaster;
        private bool _spellArmedThisFrame;
        // FIX trajectoire — données capturées au CallbackUpdateView pour reconstruire le path
        // jaune via le pathfinder PARTAGÉ (ViewPathfinder) dans l'Update Unity (piloté souris).
        private QuantumGame _game;
        private EntityRef _activeCasterEntity;
        private bool _ignoreEnemyOccupants;
        // Buffer réutilisé pour le path retourné par ViewPathfinder (zéro alloc par frame).
        private readonly (int x, int y)[] _pathScratch = new (int x, int y)[ViewPathfinder.Count];

        // Offsets 4 cardinales.
        private static readonly int[] NeighborDX = { 1, -1, 0, 0 };
        private static readonly int[] NeighborDY = { 0, 0, 1, -1 };

        private void Awake()
        {
            QuantumCallback.Subscribe(this, (CallbackGameStarted c) => OnGameStarted(c.Game));
            QuantumCallback.Subscribe(this, (CallbackUpdateView c) => OnUpdateView(c.Game));
        }

        private void Start()
        {
            if (_camera == null) _camera = Camera.main;
        }

        private void OnDestroy()
        {
            ClearHighlights();
        }

        private void OnGameStarted(QuantumGame game)
        {
            _gridReady = _gridRenderer != null;
            if (!_gridReady)
            {
                Debug.LogWarning("[MovementRangePreview] GridRenderer manquant — drag dans l'Inspector.", this);
            }
            // Setup center offset pour la projection souris -> grille (memes regles que CombatInputController).
            if (_gridSettings != null)
            {
                var frame = game.Frames.Verified;
                if (frame.TryGetSingleton<GridSingleton>(out var grid))
                {
                    _centerOffset = _gridSettings.CenterGrid
                        ? IsoProjection.CenterOffset(grid.Width, grid.Height, _gridSettings.TileWorldWidth, _gridSettings.TileWorldHeight)
                        : Vector3.zero;
                    _hoverReady = true;
                }
            }
            else
            {
                Debug.LogWarning("[MovementRangePreview] GridSettings manquant — drag dans l'Inspector. Hover desactive.", this);
            }
        }

        private void OnUpdateView(QuantumGame game)
        {
            _game = game; // capturé pour ViewPathfinder dans l'Update Unity (path jaune au survol)
            _hasActiveCaster = false;
            _spellArmedThisFrame = false;
            if (!_gridReady) return;
            // Spectateur (B, 8 juin) : aucune prévisu de portée PM (le spectateur ne joue pas).
            if (Nymora.Combat.Spectate.LiveSpectateController.LiveSpectateActive) return;

            // Priorite spell preview : si un sort est arme, on cache la preview movement.
            if (_hudController != null && _hudController.ArmedSpell.HasValue)
            {
                _spellArmedThisFrame = true;
                return;
            }

            var frame = game.Frames.Verified;
            if (frame == null) return;
            if (!frame.TryGetSingleton<CombatState>(out var state)) return;
            if (state.CurrentPhase != CombatPhase.TurnActive) return;

            // Perspective JOUEUR (PvP comme IA) : la portee de mouvement n'est affichee que pour
            // le combattant du joueur LOCAL, et UNIQUEMENT pendant son tour. LocalPlayerResolver
            // renvoie le slot reseau en PvP et 0 en IA (le bot = slot adverse) -> plus de preview
            // de la portee du bot pendant son tour.
            // 5.5e — en hot-seat équipe, le « joueur local » = le joueur ACTIF (tous locaux) -> la
            // portée s'affiche autour du combattant dont c'est le tour. Le gate devient un no-op.
            int casterPlayerIndex = LocalPlayerResolver.ResolveControllable(state.ActivePlayerIndex);
            if (state.ActivePlayerIndex != casterPlayerIndex) return;

            int casterX = -1, casterY = -1, casterPM = 0;
            bool hasCaster = false;
            // 3.5.b.iii : detection PasSpectralReady inline pour autoriser BFS a traverser ennemis.
            bool casterHasPasSpectral = false;
            // 3.7.c.v : detection NextMoveIgnoresUnits (Pas de l'Au-Dela Ghostra) — meme effet.
            bool casterHasPasAuDela = false;
            NymoraClass casterClass = NymoraClass.None;
            EntityRef casterEntity = EntityRef.None;
            var filter = frame.Filter<Combatant>();
            while (filter.Next(out EntityRef ce, out Combatant c))
            {
                if (c.PlayerIndex == casterPlayerIndex)
                {
                    casterEntity = ce;
                    casterX = c.GridX;
                    casterY = c.GridY;
                    casterPM = c.PM;
                    casterClass = c.Class;
                    for (int s = 0; s < StatusHelper.SlotCount; s++)
                    {
                        if (c.Statuses[s].Kind == StatusKind.PasSpectralReady && c.Statuses[s].TurnsLeft > 0)
                        {
                            casterHasPasSpectral = true;
                        }
                        else if (c.Statuses[s].Kind == StatusKind.PasAuDelaReady && c.Statuses[s].TurnsLeft > 0)
                        {
                            casterHasPasAuDela = true;
                        }
                    }
                    hasCaster = true;
                    break;
                }
            }
            if (!hasCaster || casterPM <= 0) return;

            _activeCasterX = casterX;
            _activeCasterY = casterY;
            _activeCasterPM = casterPM;
            _activeCasterEntity = casterEntity;
            _hasActiveCaster = true;

            // 3.5.b.iii / 3.7.c.v : Pas Spectral (Necram) OU Pas de l'Au-Dela (Ghostra) autorisent
            // le BFS a traverser cases occupees par ennemis (cohérent avec MovementSystem qui passe
            // ignoreEnemyOccupants=true a A* dans les memes conditions).
            bool ignoreEnemyOccupants = (casterHasPasSpectral && casterClass == NymoraClass.Necram)
                                     || (casterHasPasAuDela && casterClass == NymoraClass.Ghostra);
            _ignoreEnemyOccupants = ignoreEnemyOccupants;

            // BFS / SPFA avec relaxation. Met a jour _bestCost (utilise dans Update Unity pour highlight).
            for (int i = 0; i < _bestCost.Length; i++) _bestCost[i] = int.MaxValue;
            for (int i = 0; i < _occupiedTraversable.Length; i++) _occupiedTraversable[i] = false;

            int width = GridConstants.Width;
            int startIdx = casterY * width + casterX;
            _bestCost[startIdx] = 0;

            int head = 0, tail = 0;
            _queueBuf[tail++] = startIdx;

            while (head < tail)
            {
                int idx = _queueBuf[head++];
                int x = idx % width;
                int y = idx / width;
                int curCost = _bestCost[idx];
                if (curCost >= casterPM) continue;

                for (int d = 0; d < 4; d++)
                {
                    int nx = x + NeighborDX[d];
                    int ny = y + NeighborDY[d];
                    if (!GridHelpers.InBounds(nx, ny)) continue;
                    if (!GridHelpers.IsWalkable(frame, nx, ny)) continue;
                    EntityRef occ = GridHelpers.GetOccupant(frame, nx, ny);
                    bool occupied = occ != EntityRef.None;
                    if (occupied && !ignoreEnemyOccupants) continue;
                    // FIX prévisu PM (5 juin) : les obstacles (Pilier/Mur/Faille) ne posent PAS
                    // Walkable=0 (ils sont trackés séparément via ObstacleHelpers, comme dans la sim
                    // et ViewPathfinder). L'ancien commentaire « IsWalkable les rejette déjà » était
                    // FAUX : ce BFS croyait traverser un pilier en ligne droite -> il marquait la case
                    // derrière le pilier comme atteignable (coût court) alors que le vrai chemin
                    // (ViewPathfinder) la contourne -> le chemin jaune affichait 5 cases pour une case
                    // inatteignable avec les PM dispos. On exclut désormais les obstacles, exactement
                    // comme ViewPathfinder.IsBlocked, pour que portée verte + chemin jaune soient cohérents.
                    if (ObstacleHelpers.HasObstacleAt(frame, nx, ny)) continue;
                    // PATCH #8 — un leurre Ghostra bloque le deplacement (silhouette, comme dans la
                    // sim via HasAnyDecoyAt) : la portee verte s'arrete aux leurres au lieu de les
                    // traverser. Coherent avec ViewPathfinder.IsBlocked.
                    if (!ignoreEnemyOccupants && DecoyHelpers.HasAnyDecoyAt(frame, nx, ny)) continue;

                    // Patch 8 juin — owner-immune (aligne sur la sim) : SA propre Vapeur Carmin ne coute
                    //   pas +1 PM, seule la Vapeur ADVERSE ralentit. Sans ça la portée verte sous-estimait
                    //   (on devait avancer case par case sur sa propre traînée de Charge Brutale).
                    int extra = (GridHelpers.GetTerrainKind(frame, nx, ny) == TerrainKind.VapeurCarmin
                                 && FogHelpers.IsEnemyTerrainAt(frame, nx, ny, casterPlayerIndex)) ? 1 : 0;
                    int newCost = curCost + 1 + extra;
                    if (newCost > casterPM) continue;

                    int nidx = ny * width + nx;
                    if (newCost < _bestCost[nidx])
                    {
                        _bestCost[nidx] = newCost;
                        // 3.5.b.iii : si la case est occupee (cas Pas Spectral), on la marque
                        // pour la skip dans le highlight (case non-stopable mais traversee par
                        // le path). Sinon flag reset car la case est une dest valide.
                        _occupiedTraversable[nidx] = occupied;
                        if (tail < _queueBuf.Length) _queueBuf[tail++] = nidx;
                    }
                }
            }
        }

        private void Update()
        {
            // Clear l'ancien highlight a chaque frame (sera re-applique selon hover).
            ClearHighlights();

            if (_spellArmedThisFrame) return;
            if (!_hasActiveCaster) return;
            if (!_hoverReady || _camera == null) return;

            // Calcule la case sous la souris.
            Vector3 mouseWorld = _camera.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
            mouseWorld.z = 0f;
            var (gx, gy) = IsoProjection.WorldToGrid(
                mouseWorld,
                _gridSettings.TileWorldWidth,
                _gridSettings.TileWorldHeight,
                _centerOffset);

            // Cas 1 : souris sur le combatant actif -> highlight TOUTE la range.
            if (gx == _activeCasterX && gy == _activeCasterY)
            {
                HighlightFullRange();
                return;
            }

            // Cas 2 : souris sur une case dans la range -> highlight le path du caster a cette case.
            if (!GridHelpers.InBounds(gx, gy)) return;
            int targetIdx = gy * GridConstants.Width + gx;
            if (_bestCost[targetIdx] != int.MaxValue && _bestCost[targetIdx] > 0)
            {
                // 3.5.b.iii : si hover sur case occupee (traversable mais non-stopable),
                // ne pas tracer le path : le clic serait rejete par la sim.
                if (_occupiedTraversable[targetIdx]) return;
                HighlightPathTo(gx, gy);
                return;
            }

            // Cas 3 : ailleurs -> rien (deja clear).
        }

        /// <summary>
        /// Highlight toutes les cases atteignables (cost > 0, exclut la case du caster).
        /// </summary>
        private void HighlightFullRange()
        {
            int width = GridConstants.Width;
            for (int idx = 0; idx < _bestCost.Length; idx++)
            {
                if (_bestCost[idx] == int.MaxValue || _bestCost[idx] == 0) continue;
                // 3.5.b.iii : skip les cases ennemies traversables (non-stopables) pour ne
                // pas afficher la case du Soulrender comme destination valide.
                if (_occupiedTraversable[idx]) continue;
                int gx = idx % width;
                int gy = idx / width;
                var tile = _gridRenderer.GetTileView(gx, gy);
                if (tile == null) continue;
                tile.ApplyHighlight(_reachableColor);
                _highlightedIdx[_highlightedCount++] = idx;
            }
        }

        /// <summary>
        /// FIX trajectoire — Highlight le path du caster jusqu'a (targetX, targetY) en utilisant
        /// le MÊME pathfinder que l'animation de l'avatar (ViewPathfinder, partagé avec
        /// CombatantRenderer). Avant, ce path était reconstruit par descente gloutonne sur
        /// _bestCost, dont le tie-break différait de celui du BFS d'animation -> le perso ne
        /// suivait pas le trajet prévisualisé sur les chemins de coût égal. Le BFS de portée
        /// (_bestCost) reste la source pour décider QUELLES cases sont atteignables (gating dans
        /// Update) ; ViewPathfinder donne le TRAJET exact vers la case survolée.
        /// </summary>
        private void HighlightPathTo(int targetX, int targetY)
        {
            if (_game == null) return;
            var frame = _game.Frames.Verified;
            if (frame == null) return;

            if (!ViewPathfinder.TryFindPath(
                    frame,
                    _activeCasterX, _activeCasterY,
                    targetX, targetY,
                    _activeCasterEntity,
                    _pathScratch,
                    out int len,
                    _ignoreEnemyOccupants))
                return;

            int width = GridConstants.Width;
            // Le path inclut le start (index 0 = case du caster) et la cible (index len-1).
            // On skippe le start ; on highlight chaque case jusqu'a la cible.
            for (int i = 1; i < len; i++)
            {
                var (gx, gy) = _pathScratch[i];
                int idx = gy * width + gx;
                // 3.5.b.iii : skip les cases ennemies traversees (Pas Spectral / Pas de l'Au-Dela) :
                // non-stopables, l'utilisateur voit le trajet passer dessus via les cases voisines.
                if (_occupiedTraversable[idx]) continue;
                var tile = _gridRenderer.GetTileView(gx, gy);
                if (tile == null) continue;
                tile.ApplyHighlight(_pathColor);
                _highlightedIdx[_highlightedCount++] = idx;
            }
        }

        private void ClearHighlights()
        {
            if (_gridRenderer == null) { _highlightedCount = 0; return; }
            int width = GridConstants.Width;
            for (int i = 0; i < _highlightedCount; i++)
            {
                int idx = _highlightedIdx[i];
                int gx = idx % width;
                int gy = idx / width;
                var tile = _gridRenderer.GetTileView(gx, gy);
                if (tile != null) tile.ClearHighlight();
            }
            _highlightedCount = 0;
        }
    }
}
