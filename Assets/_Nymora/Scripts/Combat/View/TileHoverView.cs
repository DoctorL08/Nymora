using Nymora.Combat.Grid;
using Nymora.Combat.View.Obstacles;
using Nymora.Combat.View.HUD;
using Nymora.Core.View;
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
        private DecoyHoverProxy _prevHoveredDecoy; // patch 5 juin : barre d'HP leurre au survol

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
            // Modèle hybride tolérant (juin 2026) — un combattant est ciblé si la souris est sur son
            // SPRITE (pixel opaque) OU sur sa CASE-PIEDS ; départage par la case-pieds la plus proche
            // du curseur (cf FindCombatantViewHybrid). Le hover reflète ainsi EXACTEMENT ce que le
            // clic de cast ciblera, et deux persos collés ne sont plus départagés par le seul sprite
            // du dessus.
            CombatantView hoveredCombatant = _enableCombatantHover
                ? FindCombatantViewHybrid(mouseWorld, _gridSettings, _centerOffset)
                : null;

            // Si pas de vrai CombatantView hovered, check les leurres Ghostra : leur proxy
            // pointe vers l'Entity du vrai Ghostra parent, ce qui permet d'afficher le MEME
            // tooltip (mindgame Bible V7.1 : adversaire indiscernable cote caster vs vrai).
            EntityRef tooltipEntity = hoveredCombatant != null ? hoveredCombatant.Entity : default;
            Transform tooltipAnchor = hoveredCombatant != null ? hoveredCombatant.transform : null;
            DecoyHoverProxy hoveredDecoy = null;
            if (hoveredCombatant == null && _enableCombatantHover)
            {
                hoveredDecoy = FindDecoyHoverProxyByMouse(mouseWorld);
                // Patch 5 juin (choix Lorenzo) — au survol d'un leurre on N'AFFICHE PLUS le tooltip du
                //   vrai Ghostra : on montre la BARRE D'HP du leurre (gérée par DecoyHoverProxy). Donc
                //   on ne renseigne PAS tooltipEntity ici (-> pas de tooltip Ghostra).
            }
            // Toggle de la barre d'HP du leurre survolé (et extinction du précédent).
            if (hoveredDecoy != _prevHoveredDecoy)
            {
                if (_prevHoveredDecoy != null) _prevHoveredDecoy.SetHovered(false);
                if (hoveredDecoy != null) hoveredDecoy.SetHovered(true);
                _prevHoveredDecoy = hoveredDecoy;
            }
            UpdateCombatantHover(hoveredCombatant, tooltipEntity, tooltipAnchor);

            // Fix piliers/murs juin 2026 — si on ne survole ni combattant ni leurre, on teste les
            // sprites d'obstacles (Pilier/Mur) : leur sprite déborde leur tuile en hauteur, donc
            // survoler leur corps doit faire suivre le glow + reveal HP sur LEUR case-pieds, pas sur
            // la case sol derrière. Cohérent avec le snap de ciblage (TryPickSpriteTargetCell).
            ObstacleView hoveredObstacle = null;
            if (hoveredCombatant == null && hoveredDecoy == null && _enableObstacleHpReveal)
            {
                hoveredObstacle = FindObstacleViewByMouse(mouseWorld);
            }

            // Cellule a highlighter. Quand on survole un perso (ou un leurre), on cible SA case
            // et pas la case sous la souris : le sprite deborde largement sa tuile (scale 1.16x +
            // Visual Y -0.22), donc la case logique sous le curseur tombe derriere/au-dessus de
            // lui. Lorenzo veut que le glow de tile suive le perso survole. Sinon, case souris.
            int targetX, targetY;
            bool hasTargetCell;
            if (hoveredCombatant != null)
            {
                targetX = hoveredCombatant.GridX;
                targetY = hoveredCombatant.GridY;
                hasTargetCell = true;
            }
            else if (hoveredDecoy != null)
            {
                // Le proxy leurre ne stocke pas sa case : on la derive de sa position monde.
                var (dx, dy) = IsoProjection.WorldToGrid(
                    hoveredDecoy.transform.position,
                    _gridSettings.TileWorldWidth,
                    _gridSettings.TileWorldHeight,
                    _centerOffset);
                targetX = dx;
                targetY = dy;
                hasTargetCell = dx >= 0 && dx < gridWidth && dy >= 0 && dy < gridHeight;
            }
            else if (hoveredObstacle != null)
            {
                // Obstacle survolé par son sprite : on cible SA case-pieds (stockée sur l'ObstacleView).
                targetX = hoveredObstacle.GridX;
                targetY = hoveredObstacle.GridY;
                hasTargetCell = targetX >= 0 && targetX < gridWidth && targetY >= 0 && targetY < gridHeight;
            }
            else
            {
                targetX = gx;
                targetY = gy;
                hasTargetCell = !outOfGrid;
            }

            // Pas de changement de cellule cible : rien a faire pour tile/obstacle.
            if (hasTargetCell && targetX == _prevHoverX && targetY == _prevHoverY) return;
            if (!hasTargetCell && _prevHoverX == int.MinValue) return;

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
            if (!hasTargetCell)
            {
                _prevHoverX = int.MinValue;
                _prevHoverY = int.MinValue;
                return;
            }

            _prevHoverX = targetX;
            _prevHoverY = targetY;

            // Apply nouveau hover tile/obstacle sur la case cible.
            if (_enableTileGlow && _gridRenderer != null)
            {
                _prevTile = _gridRenderer.GetTileView(targetX, targetY);
                if (_prevTile != null) _prevTile.ApplyHighlight(_hoverColor);
            }
            if (_enableObstacleHpReveal && _obstacleRenderer != null)
            {
                _prevObstacle = _obstacleRenderer.GetObstacleViewAt(targetX, targetY);
                if (_prevObstacle != null) _prevObstacle.SetHpVisible(true);
            }
        }

        /// <summary>
        /// POLISH-5d — Applique/restore le hover combatant detecte par sprite bounds.
        /// Diff par rapport au precedent _prevCombatant : si change, clear l'ancien et
        /// applique le nouveau (+ tooltip HP).
        ///
        /// 18 mai : <paramref name="tooltipEntity"/> peut differer de <paramref name="next"/>.Entity
        /// pour les leurres Ghostra : le hover est sur le leurre (highlight visuel optionnel)
        /// mais le tooltip affiche les HP du vrai Ghostra parent (mindgame indiscernable).
        /// </summary>
        private void UpdateCombatantHover(CombatantView next, EntityRef tooltipEntity, Transform tooltipAnchor)
        {
            // Track via prev pour le highlight visuel (sprite jaune) -- ne s'applique
            // qu'aux vrais CombatantView, pas aux leurres.
            bool combatantChanged = next != _prevCombatant;
            if (combatantChanged)
            {
                if (_prevCombatant != null) _prevCombatant.ClearHighlight();
                _prevCombatant = next;
                if (next != null) next.ApplyHighlight();
            }

            // Track tooltip separement : peut etre actif sur un leurre meme si pas de
            // CombatantView highlight. On re-affiche des que l'entite OU l'ancre change :
            // l'ancre distingue 2 leurres du MEME Ghostra (meme EntityRef parent, GameObjects
            // distincts) -> chaque leurre declenche/positionne son propre tooltip.
            bool tooltipChanged = tooltipEntity != _prevTooltipEntity || tooltipAnchor != _prevTooltipAnchor;
            if (tooltipChanged)
            {
                _prevTooltipEntity = tooltipEntity;
                _prevTooltipAnchor = tooltipAnchor;
                if (CombatantTooltipView.Instance != null)
                {
                    if (tooltipEntity != default && tooltipAnchor != null && TryGetCombatantHp(tooltipEntity, out int hp, out int maxHp))
                    {
                        // Ancre world-space : sprite du vrai combatant, ou sprite du leurre
                        // precis survole (passe par l'appelant, plus de re-resolution par entite).
                        CombatantTooltipView.Instance.Show(tooltipEntity, hp, maxHp, tooltipAnchor);
                    }
                    else
                    {
                        CombatantTooltipView.Instance.Hide();
                    }
                }
            }
        }

        private EntityRef _prevTooltipEntity;
        private Transform _prevTooltipAnchor;

        /// <summary>
        /// Fix ciblage juin 2026 — Résout la case CIBLE sous la souris en priorisant le SPRITE
        /// (pixel-parfait) d'un combattant, sinon d'un leurre Ghostra, plutôt que la case sol
        /// projetée. Le sprite pixel-art déborde largement sa tuile en hauteur (scale 1.16x +
        /// Visual Y -0.22) : survoler le buste/la tête d'un ennemi doit cibler SA case, pas la
        /// case sol derrière lui. Retourne false si aucun sprite opaque sous la souris (l'appelant
        /// retombe alors sur la case sol WorldToGrid classique).
        ///
        /// Statique + sans état : partagé par CombatInputController (clic de cast) et
        /// TargetingPreviewView (zone d'effet au survol) pour rester cohérent avec le hover tooltip.
        ///
        /// <paramref name="filter"/> gate le snap : on ne snappe que pour les sorts qui visent une
        /// UNITÉ (Enemy/Ally/AnyUnit) ou un LEURRE (TileWithLure). Les sorts qui visent une case sol
        /// (EmptyTile / obstacle : téléport, pose de mur/pilier/leurre) gardent la case sol sous le
        /// curseur — sinon survoler un ennemi snapperait sur sa case occupée (cast rejeté).
        ///
        /// <paramref name="isStraightLineSpell"/> force le snap même si le filtre est AnyTile : les
        /// sorts en ligne droite (Choc Sismique / Charge Brutale / Volée d'Épines) utilisent AnyTile
        /// mais visent en pratique un ennemi le long de la ligne -> survoler son sprite doit aligner
        /// la ligne sur SA case.
        /// </summary>
        public static bool TryPickSpriteTargetCell(Vector3 mouseWorld, TargetingFilter filter, bool isStraightLineSpell, GridSettings gridSettings, Vector3 centerOffset, out int gx, out int gy)
        {
            gx = -1;
            gy = -1;

            bool wantsUnit = FilterTargetsUnitSprite(filter) || isStraightLineSpell;
            bool wantsObstacle = FilterTargetsObstacleSprite(filter);
            if (!wantsUnit && !wantsObstacle) return false;

            // FIX 5 juin — PRIORITÉ OBSTACLE (choix Lorenzo). Quand un sort peut viser un obstacle ET
            // qu'un sprite d'obstacle (Pilier/Mur) est opaque-hit sous le curseur, on cible l'obstacle
            // EN PRIORITÉ, même si un combattant le chevauche visuellement. Sans ça, le sprite haut du
            // Colossar masquait ses propres piliers -> « très difficile de cibler ses piliers ». On
            // EXCLUT les sorts en LIGNE DROITE (Choc Sismique / Charge Brutale) : eux visent l'ennemi
            // le long de la ligne, on garde la priorité unité pour ne pas snapper sur un obstacle
            // traversé. Les unités n'occupant jamais une case-obstacle, le seul conflit est le
            // chevauchement visuel de sprites.
            if (wantsObstacle && !isStraightLineSpell)
            {
                var obstaclePriority = FindObstacleViewByMouse(mouseWorld);
                if (obstaclePriority != null)
                {
                    gx = obstaclePriority.GridX;
                    gy = obstaclePriority.GridY;
                    return true;
                }
            }

            if (wantsUnit)
            {
                // 1) Vrai combattant (modèle hybride tolérant juin 2026) : candidat si sprite-opaque OU
                //    case-pieds sous le curseur, départagé par la case-pieds la plus proche (cf
                //    FindCombatantViewHybrid). Il porte sa case logique (GridX/GridY).
                var combatant = FindCombatantViewHybrid(mouseWorld, gridSettings, centerOffset);
                if (combatant != null)
                {
                    gx = combatant.GridX;
                    gy = combatant.GridY;
                    return true;
                }

                // 2) Leurre Ghostra : pas de case stockée -> dérivée de sa position monde.
                if (gridSettings != null)
                {
                    var decoy = FindDecoyHoverProxyByMouse(mouseWorld);
                    if (decoy != null)
                    {
                        var (dx, dy) = IsoProjection.WorldToGrid(
                            decoy.transform.position,
                            gridSettings.TileWorldWidth,
                            gridSettings.TileWorldHeight,
                            centerOffset);
                        if (dx >= 0 && dx < Quantum.GridConstants.Width && dy >= 0 && dy < Quantum.GridConstants.Height)
                        {
                            gx = dx;
                            gy = dy;
                            return true;
                        }
                    }
                }
            }

            // 3) Fix piliers/murs juin 2026 — un obstacle (Pilier/Mur Colossar) a lui aussi un sprite
            //    qui déborde sa tuile en hauteur : survoler/cliquer son corps doit cibler SA case-pieds
            //    (GridX/GridY), pas la case sol projetée derrière. Sans ça, Éboulement (qui vise « un de
            //    TES Piliers », filtre AnyTile) ratait toujours sa cible -> piliers/murs « inciblables ».
            //    Gate sur les filtres qui peuvent légitimement viser une case-obstacle (AnyTile /
            //    TileWithObstacle) : les sorts de POSE (EmptyTile : Pilier, Mur, leurre) gardent la case
            //    sol sous le curseur pour ne pas snapper sur un obstacle déjà en place.
            if (wantsObstacle)
            {
                var obstacle = FindObstacleViewByMouse(mouseWorld);
                if (obstacle != null)
                {
                    gx = obstacle.GridX;
                    gy = obstacle.GridY;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True si le filtre du sort vise une UNITÉ (ou un leurre) -> le snap sprite a du sens.
        /// Les filtres de case sol (EmptyTile / TileWithObstacle / AnyTile) sont exclus.
        /// Self est inclus : le caster doit pouvoir cliquer SON propre sprite pour se cibler
        /// (l'appelant vérifie ensuite que la case résolue est bien la sienne).
        ///
        /// Public : CombatInputController s'en sert pour annuler proprement un cast quand un
        /// sort à cible-unité ne résout AUCUN combattant sous le curseur (pas de misfire sol).
        /// </summary>
        public static bool FilterTargetsUnitSprite(TargetingFilter filter)
        {
            switch (filter)
            {
                case TargetingFilter.Self:
                case TargetingFilter.Enemy:
                case TargetingFilter.Ally:
                case TargetingFilter.AllyIncludingSelf:
                case TargetingFilter.AnyUnit:
                case TargetingFilter.TileWithLure: // Permutation Ghostra : on vise le sprite du leurre
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Fix piliers/murs juin 2026 — True si le filtre peut viser une case occupée par un OBSTACLE
        /// (Pilier/Mur) -> le snap sprite obstacle a du sens. Couvre :
        ///   - TileWithObstacle : sorts qui ciblent explicitement un obstacle.
        ///   - AnyTile          : Éboulement vise « un de TES Piliers » via AnyTile (Bible V7.1) ;
        ///                        survoler le pilier doit cibler SA case et pas le sol derrière.
        ///   - Enemy / AnyUnit  : un sort OFFENSIF à cible-ennemi peut viser un OBSTACLE ADVERSE pour
        ///                        le détruire (Pilier/Mur destructibles, PATCH sim 22 mai dans
        ///                        SpellSystem.TryCastSpell). Les obstacles n'étant PAS dans l'occupancy
        ///                        combattant (ObstacleSingleton à part), le snap unité ne les trouvait
        ///                        pas et l'anti-misfire annulait le cast -> piliers/murs « inciblables ».
        ///                        Snapper vers l'obstacle rend snapped=true, donc plus d'annulation.
        ///                        Depuis brique juin 2026, la sim endommage AUSSI les obstacles OWN
        ///                        (le Colossar peut casser ses propres Piliers/Murs/Failles), pas que
        ///                        les adverses.
        /// EmptyTile est EXCLU : les sorts de pose (Pilier, Mur, leurre) doivent garder la case sol
        /// libre sous le curseur, jamais snapper sur un obstacle déjà présent. Self/Ally aussi exclus
        /// (on ne cible pas un obstacle en allié).
        /// </summary>
        public static bool FilterTargetsObstacleSprite(TargetingFilter filter)
        {
            switch (filter)
            {
                case TargetingFilter.TileWithObstacle:
                case TargetingFilter.AnyTile:
                case TargetingFilter.Enemy:
                case TargetingFilter.AnyUnit:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Fix piliers/murs juin 2026 — Détecte un ObstacleView (Pilier/Mur) sous la souris par test
        /// pixel-parfait (cf SpritePixelHitTester), comme FindDecoyHoverProxyByMouse pour les leurres.
        /// Le sprite obstacle déborde sa tuile en hauteur (anim « sort du sol », pivot bas), donc on ne
        /// peut pas se fier à la case sol projetée. Retourne l'obstacle au meilleur sortingOrder si
        /// plusieurs se chevauchent. O(N obstacles) — max ~5 concurrents (cap FD = 3 piliers + 1 mur).
        /// </summary>
        private static ObstacleView FindObstacleViewByMouse(Vector3 mouseWorld)
        {
            var views = Object.FindObjectsByType<ObstacleView>(FindObjectsSortMode.None);
            ObstacleView best = null;
            int bestSortingOrder = int.MinValue;
            for (int i = 0; i < views.Length; i++)
            {
                var v = views[i];
                if (v == null || !v.isActiveAndEnabled) continue;
                var sr = v.GetComponentInChildren<SpriteRenderer>();
                if (sr == null || !sr.enabled || sr.sprite == null) continue;
                if (!SpritePixelHitTester.OverlapsOpaque(sr, mouseWorld)) continue;
                if (sr.sortingOrder > bestSortingOrder)
                {
                    bestSortingOrder = sr.sortingOrder;
                    best = v;
                }
            }
            return best;
        }

        /// <summary>
        /// 18 mai — Detecte un DecoyHoverProxy survole (= leurre Ghostra). Test pixel-parfait
        /// (cf SpritePixelHitTester) ; retourne le proxy au meilleur sortingOrder si plusieurs
        /// leurres se chevauchent.
        /// </summary>
        private static DecoyHoverProxy FindDecoyHoverProxyByMouse(Vector3 mouseWorld)
        {
            var proxies = Object.FindObjectsByType<DecoyHoverProxy>(FindObjectsSortMode.None);
            DecoyHoverProxy best = null;
            int bestSortingOrder = int.MinValue;
            for (int i = 0; i < proxies.Length; i++)
            {
                var p = proxies[i];
                if (p == null || !p.isActiveAndEnabled) continue;
                var sr = p.GetComponent<SpriteRenderer>();
                if (sr == null || !sr.enabled || sr.sprite == null) continue;
                // Pixel-parfait : on ne retient le leurre que si la souris est sur un pixel
                // opaque, pas dans le vide transparent de l'AABB.
                if (!SpritePixelHitTester.OverlapsOpaque(sr, mouseWorld)) continue;
                if (sr.sortingOrder > bestSortingOrder)
                {
                    bestSortingOrder = sr.sortingOrder;
                    best = p;
                }
            }
            return best;
        }

        /// <summary>
        /// Modèle hybride tolérant (juin 2026) — Résout LE combattant ciblé par la souris.
        /// Un combattant est CANDIDAT si :
        ///   (a) sa case-pieds (GridX/GridY) == la case sous le curseur (WorldToGrid), OU
        ///   (b) un pixel OPAQUE de son sprite est sous le curseur (cf SpritePixelHitTester —
        ///       le sprite pixel-art déborde sa tuile en hauteur, scale 1.16x + Visual Y -0.22).
        /// Parmi les candidats, on retient celui dont le CENTRE de sa case-pieds (en monde) est le
        /// plus proche du curseur. Ça remplace l'ancien tri par sortingOrder : quand deux persos
        /// sont collés et que leurs sprites se chevauchent, on cible celui vers la BASE duquel on
        /// pointe, au lieu de toujours prendre le sprite au-dessus (back-perso devenait
        /// inaccessible). Retourne null si AUCUN candidat -> plus de misfire sur la case sol
        /// derrière un quasi-clic (le clic de cast est alors annulé plutôt que mal placé).
        ///
        /// Combine l'ancien FindCombatantViewByMouse (sprite-opaque) et FindCombatantViewAtCell
        /// (case logique) : le hover ET le clic de cast partagent désormais cette résolution, donc
        /// le glow montre exactement ce qui sera ciblé.
        ///
        /// <paramref name="gridSettings"/> null (scène de test minimale) -> fallback historique
        /// sprite-opaque + sortingOrder le plus haut.
        /// </summary>
        private static CombatantView FindCombatantViewHybrid(Vector3 mouseWorld, GridSettings gridSettings, Vector3 centerOffset)
        {
            bool hasGrid = gridSettings != null;
            int cellX = int.MinValue, cellY = int.MinValue;
            if (hasGrid)
            {
                var (cx, cy) = IsoProjection.WorldToGrid(
                    mouseWorld, gridSettings.TileWorldWidth, gridSettings.TileWorldHeight, centerOffset);
                cellX = cx;
                cellY = cy;
            }

            var views = Object.FindObjectsByType<CombatantView>(FindObjectsSortMode.None);
            CombatantView best = null;
            float bestFootDistSq = float.MaxValue;
            int bestSortingOrder = int.MinValue; // fallback quand gridSettings null

            for (int i = 0; i < views.Length; i++)
            {
                var v = views[i];
                if (v == null || !v.isActiveAndEnabled) continue;

                // (a) case-pieds exactement sous le curseur.
                bool onFootCell = hasGrid && v.GridX == cellX && v.GridY == cellY;

                // (b) pixel opaque du sprite sous le curseur.
                var sr = v.GetComponentInChildren<SpriteRenderer>();
                bool onSprite = sr != null && sr.enabled && sr.sprite != null
                                && SpritePixelHitTester.OverlapsOpaque(sr, mouseWorld);

                if (!onFootCell && !onSprite) continue;

                if (hasGrid)
                {
                    // Départage tolérant : la case-pieds dont le centre monde est le plus proche
                    // du curseur (deux persos collés -> on prend celui vers lequel on pointe).
                    Vector3 footWorld = IsoProjection.GridToWorld(
                        v.GridX, v.GridY, gridSettings.TileWorldWidth, gridSettings.TileWorldHeight) + centerOffset;
                    float distSq = ((Vector2)(mouseWorld - footWorld)).sqrMagnitude;
                    if (distSq < bestFootDistSq)
                    {
                        bestFootDistSq = distSq;
                        best = v;
                    }
                }
                else
                {
                    // Fallback historique sans grille : sprite le plus haut.
                    int order = sr != null ? sr.sortingOrder : int.MinValue;
                    if (order > bestSortingOrder)
                    {
                        bestSortingOrder = order;
                        best = v;
                    }
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
