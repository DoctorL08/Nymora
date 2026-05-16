using System.Collections.Generic;
using Nymora.Combat.Grid;
using Quantum;
using UnityEngine;
using SpellCategory = Nymora.Core.Enums.SpellCategory;

namespace Nymora.Combat.View
{
    /// <summary>
    /// Spawn un GameObject par entity Combatant cote View au demarrage du combat,
    /// puis sync leurs positions a chaque CallbackUpdateView (placement iso depuis
    /// GridX/GridY).
    ///
    /// Pas de pooling en 2.2 (max 2 combattants en 1v1, futile). Pooling viendra
    /// en Phase 6 quand on aura les modes 2v2/3v3 (jusqu'a 6 combattants).
    /// </summary>
    public class CombatantRenderer : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private GridSettings _gridSettings;

        [Header("Prefabs par classe (ordre Bible V7.1)")]
        [SerializeField] private GameObject _soulrenderPrefab;
        [SerializeField] private GameObject _nightseerPrefab;
        [SerializeField] private GameObject _colossarPrefab;
        [SerializeField] private GameObject _necramPrefab;
        [SerializeField] private GameObject _ghostraPrefab;

        private readonly Dictionary<EntityRef, CombatantView> _views = new Dictionary<EntityRef, CombatantView>();
        // Reuse buffer pour eviter les allocations dans OnUpdateView (appele a chaque frame view).
        // Capacite 2 suffit pour le 1v1 actuel ; sera grow auto si on passe en 2v2/3v3 plus tard.
        private readonly List<CombatantSnapshot> _frameCombatants = new List<CombatantSnapshot>(2);
        // Tracking facing : on retient la derniere position grille pour deduire le sens du
        // mouvement, et le dernier facing pour conserver l'orientation a l'arret.
        private readonly Dictionary<EntityRef, GridPos> _lastGridPos = new Dictionary<EntityRef, GridPos>(2);
        private readonly Dictionary<EntityRef, IsoFacing> _lastFacings = new Dictionary<EntityRef, IsoFacing>(2);
        // 2.12.bis : tracking HP et LastCastSequence pour declencher les anims Hurt/Death/Cast/Attack.
        // 2.13.e : LastCastOnTurn ne suffit pas (plusieurs casts dans le meme tour =
        // 1 seule anim). Switch sur LastCastSequence (compteur monotone du DSL).
        private readonly Dictionary<EntityRef, int> _lastHP = new Dictionary<EntityRef, int>(2);
        private readonly Dictionary<EntityRef, int> _lastCastSeq = new Dictionary<EntityRef, int>(2);
        // 3.7.a.i — Track Permutation Ghostra pour snap instantané au lieu de walk lerp.
        // Si combatant.LastPermutationOnTurn > cache -> une Permutation vient d'avoir lieu
        // -> on snap le visuel (téléport, Bible "invisible cote adversaire") au lieu de
        // walk anim entre les 2 positions swappées.
        private readonly Dictionary<EntityRef, int> _lastPermutationOnTurn = new Dictionary<EntityRef, int>(2);
        private Vector3 _centerOffset;
        private bool _gridReady;

        // 2.16.c.vi — Buffer reutilise pour computer le path Manhattan (X puis Y) entre
        // 2 GridX/Y consecutifs d'un combatant. Capacite 16 = max ~10 cases + marge. Si
        // un move depasse (peu probable), le code re-alloue un nouveau buffer.
        private Vector3[] _waypointScratch = new Vector3[16];

        private readonly struct GridPos
        {
            public readonly int X;
            public readonly int Y;
            public GridPos(int x, int y) { X = x; Y = y; }
        }

        private readonly struct CombatantSnapshot
        {
            public readonly EntityRef Entity;
            public readonly Combatant Data;
            public CombatantSnapshot(EntityRef entity, Combatant data) { Entity = entity; Data = data; }
        }

        private void Awake()
        {
            QuantumCallback.Subscribe(this, (CallbackGameStarted c) => OnGameStarted(c.Game));
            QuantumCallback.Subscribe(this, (CallbackUpdateView c) => OnUpdateView(c.Game));
        }

        private void OnDestroy()
        {
            ClearAll();
        }

        private void OnGameStarted(QuantumGame game)
        {
            if (_gridSettings == null)
            {
                Debug.LogError("[Nymora.CombatantRenderer] GridSettings manquant.", this);
                return;
            }

            ClearAll();

            var frame = game.Frames.Verified;

            // Recupere les dimensions de la grille pour calculer le centerOffset (meme que GridRenderer).
            var grid = frame.GetSingleton<GridSingleton>();
            _centerOffset = _gridSettings.CenterGrid
                ? IsoProjection.CenterOffset(grid.Width, grid.Height, _gridSettings.TileWorldWidth, _gridSettings.TileWorldHeight)
                : Vector3.zero;
            _gridReady = true;

            // Spawn 1 GameObject par entity Combatant existante (safe API : copie par valeur).
            var filter = frame.Filter<Combatant>();
            while (filter.Next(out EntityRef entity, out Combatant combatant))
            {
                SpawnView(entity, combatant);
            }
        }

        private void OnUpdateView(QuantumGame game)
        {
            if (!_gridReady) return;

            var frame = game.Frames.Verified;

            // Pass 1 : snapshot tous les combatants vivants (besoin de la position adverse
            // pour calculer le facing iso de chacun).
            _frameCombatants.Clear();
            var filter = frame.Filter<Combatant>();
            while (filter.Next(out EntityRef entity, out Combatant combatant))
            {
                _frameCombatants.Add(new CombatantSnapshot(entity, combatant));
            }

            // Pass 2 : sync position + stage + facing (auto-aim vers le 1er autre combatant).
            for (int i = 0; i < _frameCombatants.Count; i++)
            {
                var snap = _frameCombatants[i];
                var entity = snap.Entity;
                var combatant = snap.Data;

                if (!_views.TryGetValue(entity, out var view) || view == null)
                {
                    // Entity apparue apres GameStarted (ex : invocations futures, leurres Ghostra) — spawn a la volee.
                    SpawnView(entity, combatant);
                    continue;
                }

                Vector3 world = IsoProjection.GridToWorld(
                    combatant.GridX, combatant.GridY,
                    _gridSettings.TileWorldWidth, _gridSettings.TileWorldHeight) + _centerOffset + transform.position;

                // 3.3.d polish — Path cardinal cell-by-cell qui CONTOURNE les obstacles.
                // Avant : approximation X-puis-Y qui traversait les obstacles (bug visuel).
                // Maintenant : BFS 4-connexite cote View pour reconstruire le path le plus court
                // en respectant obstacles (Pilier/Mur/Faille) et autres combatants. Si le BFS
                // echoue (pas de chemin valide ou path trop long), on fallback sur l'approximation
                // X-puis-Y historique pour ne jamais bloquer l'animation.
                Vector3[] intermediates = null;
                int intermediatesCount = 0;
                int prevGx = view.GridX;
                int prevGy = view.GridY;
                int dx = combatant.GridX - prevGx;
                int dy = combatant.GridY - prevGy;
                int absDx = dx < 0 ? -dx : dx;
                int absDy = dy < 0 ? -dy : dy;
                int totalSteps = absDx + absDy;
                // Skip si pas encore de position posee (cas spawn 1er frame, prevGx/Gy = 0).
                if (totalSteps > 1 && (prevGx != 0 || prevGy != 0))
                {
                    // 3.5.b.iii Pas Spectral : si le combatant est un Necram porteur de
                    // PasSpectralReady, le path d'anim peut traverser les ennemis (cohérent
                    // avec la sim qui passe ignoreEnemyOccupants=true a A*).
                    bool animIgnoreEnemyOccupants = false;
                    if (combatant.Class == NymoraClass.Necram)
                    {
                        for (int s = 0; s < StatusHelper.SlotCount; s++)
                        {
                            if (combatant.Statuses[s].Kind == StatusKind.PasSpectralReady
                                && combatant.Statuses[s].TurnsLeft > 0)
                            {
                                animIgnoreEnemyOccupants = true;
                                break;
                            }
                        }
                    }

                    // Tentative 1 : BFS qui contourne. Path max = ~10 cases (3 PM Soulrender +
                    // marge contournement). On donne 16 = MaxWaypoints de CombatantView.
                    System.Span<(int x, int y)> pathBuf = stackalloc (int, int)[16];
                    int pathLen;
                    bool found = TryFindViewPath(frame, prevGx, prevGy,
                        combatant.GridX, combatant.GridY, entity, pathBuf, out pathLen,
                        animIgnoreEnemyOccupants);

                    if (found && pathLen > 1)
                    {
                        // pathLen inclut la case finale (index pathLen-1). On push les
                        // intermediaires (pathBuf[1..pathLen-2]) — la 1ere case (pathBuf[0])
                        // est la position de depart, qu'on skippe.
                        int intermediatesCapacity = pathLen - 2;
                        if (intermediatesCapacity > 0)
                        {
                            intermediates = _waypointScratch;
                            if (intermediates.Length < intermediatesCapacity)
                            {
                                intermediates = new Vector3[intermediatesCapacity];
                                _waypointScratch = intermediates;
                            }
                            for (int p = 1; p < pathLen - 1; p++)
                            {
                                var (px, py) = pathBuf[p];
                                intermediates[intermediatesCount++] = IsoProjection.GridToWorld(
                                    px, py, _gridSettings.TileWorldWidth, _gridSettings.TileWorldHeight)
                                    + _centerOffset + transform.position;
                            }
                        }
                    }
                    else
                    {
                        // Fallback historique : Manhattan X-puis-Y (traverse obstacles mais
                        // sert si BFS depasse 16 cases / pas de path / target inaccessible).
                        int intermediatesCapacity = totalSteps - 1;
                        intermediates = _waypointScratch;
                        if (intermediates.Length < intermediatesCapacity)
                        {
                            intermediates = new Vector3[intermediatesCapacity];
                            _waypointScratch = intermediates;
                        }
                        int stepX = dx > 0 ? 1 : -1;
                        int stepY = dy > 0 ? 1 : -1;
                        int cx = prevGx;
                        int cy = prevGy;
                        while (cx != combatant.GridX)
                        {
                            cx += stepX;
                            if (cx == combatant.GridX && cy == combatant.GridY) break;
                            intermediates[intermediatesCount++] = IsoProjection.GridToWorld(
                                cx, cy, _gridSettings.TileWorldWidth, _gridSettings.TileWorldHeight)
                                + _centerOffset + transform.position;
                        }
                        while (cy != combatant.GridY)
                        {
                            cy += stepY;
                            if (cx == combatant.GridX && cy == combatant.GridY) break;
                            intermediates[intermediatesCount++] = IsoProjection.GridToWorld(
                                cx, cy, _gridSettings.TileWorldWidth, _gridSettings.TileWorldHeight)
                                + _centerOffset + transform.position;
                        }
                    }
                }

                // 3.7.a.i — Detect Permutation Ghostra : si LastPermutationOnTurn a augmente
                // depuis le dernier tick, c'est qu'une permutation vient d'avoir lieu -> snap
                // instantane (Bible "invisible cote adversaire") au lieu de walk anim.
                bool isPermutationSnap = false;
                if (combatant.Class == NymoraClass.Ghostra)
                {
                    int prevPermutTurn = _lastPermutationOnTurn.TryGetValue(entity, out var pt) ? pt : int.MinValue;
                    if (combatant.LastPermutationOnTurn > prevPermutTurn && combatant.LastPermutationOnTurn > 0)
                    {
                        isPermutationSnap = true;
                        _lastPermutationOnTurn[entity] = combatant.LastPermutationOnTurn;
                    }
                }

                if (isPermutationSnap)
                {
                    // 3.7.a.i — VFX téléportation : fade out + snap + flash bleu spectral + fade in.
                    view.PlayTeleportEffect(combatant.GridX, combatant.GridY, world);
                }
                else
                {
                    view.UpdateGridPosition(combatant.GridX, combatant.GridY, world, intermediates, intermediatesCount);
                }

                // 2.12 : push stage visuel (selon ressource Bible V7.1) + facing iso selon mouvement.
                // 2.16.c.vi : pendant que le View consomme ses waypoints (animation cardinal
                // cell-by-cell), c'est lui qui owns le facing — chaque segment du path peut
                // avoir une orientation differente (East puis North = NE puis NW iso). Renderer
                // ne reprend la main qu'a l'arret (IsMoving = false).
                int stage = ComputeStage(combatant);
                if (!view.IsMoving)
                {
                    IsoFacing facing = ResolveFacing(entity, combatant);
                    view.SetStageAndFacing(stage, facing);
                }
                else
                {
                    // Stage peut changer pendant un move (gain HG en chemin via cast trap, etc.)
                    // — on push juste le stage avec le facing courant pour eviter de l'oublier.
                    view.SetStageAndFacing(stage, view.CurrentFacing);
                }

                // 2.12.bis : detection des changements d'etat -> triggers anims.
                DispatchAnimTriggers(entity, combatant, view);
            }
        }

        /// <summary>
        /// 2.12.bis : compare le snapshot courant au dernier snapshot vu et trigger les
        /// anims appropriees (Hurt sur perte de HP, Death sur HP=0, Cast/Attack sur
        /// nouveau cast, MoveSpeed adapte pendant le mouvement).
        ///
        /// Polling-based : on lit Combatant.LastCastOnTurn + LastCastSpellId pour detecter
        /// les casts, et on compare HP au dernier vu pour detecter les degats. Aucun
        /// event Quantum requis, donc pas de modif lourde de la sim.
        /// </summary>
        private void DispatchAnimTriggers(EntityRef entity, Combatant combatant, CombatantView view)
        {
            // --- HP delta : Hurt / Death ---
            int prevHP = _lastHP.TryGetValue(entity, out var hp) ? hp : combatant.HP;
            int currHP = combatant.HP;
            if (currHP == 0 && prevHP > 0)
            {
                view.TriggerDeath();
            }
            else if (currHP < prevHP)
            {
                view.TriggerHurt();
            }
            _lastHP[entity] = currHP;

            // --- Cast delta : Attack (range 1) / Cast (range >1) ---
            // 2.13.e : switch a LastCastSequence (compteur monotone) pour declencher l'anim
            // a CHAQUE cast, meme plusieurs dans le meme tour.
            int prevCastSeq = _lastCastSeq.TryGetValue(entity, out var cs) ? cs : 0;
            int currCastSeq = combatant.LastCastSequence;
            if (currCastSeq != prevCastSeq)
            {
                var spellId = combatant.LastCastSpellId;
                if (SpellRegistry.TryGet(spellId, out var def))
                {
                    if (def.RangeMax <= 1)
                    {
                        view.TriggerAttack();
                    }
                    else
                    {
                        view.TriggerCast(CategoryForSpell(spellId));
                    }

                    // 2.13.e bugfix : reoriente le combatant vers l'ennemi au moment du cast
                    // (sauf sorts self-target Bible V7.1 : Pacte de Sang, Peau de Fer, etc.).
                    // ResolveFacing ne se declenche que sur deplacement grille — sans ca, un
                    // perso qui cast sans bouger reste oriente dans sa derniere direction.
                    if (def.Filter != TargetingFilter.Self)
                    {
                        IsoFacing castFacing = FacingTowardEnemy(entity, combatant);
                        _lastFacings[entity] = castFacing;
                        view.SetStageAndFacing(ComputeStage(combatant), castFacing);
                    }
                }
            }
            _lastCastSeq[entity] = currCastSeq;

            // --- Walk speed pendant le lerp ---
            // TODO 2.12.ter : differentier 1-2 PM (lent 0.8) vs 3+ PM (rapide 1.5) en regardant
            // le nb de cases parcourues dans la "sequence" de mouvement courante. Pour l'instant
            // on push 1.0 fixe quand l'entity vient de bouger. Le state Walk gere automatiquement
            // les transitions via MoveSpeed (CombatantView.Update push MoveSpeed pendant le lerp).
            view.SetDesiredMoveSpeed(1.0f);
        }

        /// <summary>
        /// Mappe SpellId -> SpellCategory pour driver la vitesse de l'anim Cast.
        /// Hardcode Soulrender + Nightseer (Bible V7.1). A etendre quand Phase 3 arrive.
        /// </summary>
        private static SpellCategory CategoryForSpell(SpellId id)
        {
            switch (id)
            {
                // SOULRENDER — Offensifs (5)
                case SpellId.SoulrenderTrancheAme:
                case SpellId.SoulrenderOuvrePlaie:
                case SpellId.SoulrenderChargeBrutale:
                case SpellId.SoulrenderDetonationSanglante:
                case SpellId.SoulrenderCuree:
                    return SpellCategory.Offensive;

                // SOULRENDER — Tactiques (5)
                case SpellId.SoulrenderPacteDeSang:
                case SpellId.SoulrenderMarqueDeCarnage:
                case SpellId.SoulrenderEmpoignade:
                case SpellId.SoulrenderRugissement:
                case SpellId.SoulrenderRageInsatiable:
                    return SpellCategory.Tactical;

                // SOULRENDER — Survie (5)
                case SpellId.SoulrenderRiposteCarmin:
                case SpellId.SoulrenderCauterisation:
                case SpellId.SoulrenderPeauDeFer:
                case SpellId.SoulrenderSeveVive:
                case SpellId.SoulrenderDernierSouffle:
                    return SpellCategory.Survival;

                // SOULRENDER — Signature
                case SpellId.SoulrenderAmeLaceree:
                    return SpellCategory.Signature;

                // NIGHTSEER — Offensifs (5)
                case SpellId.NightseerTirPrecis:
                case SpellId.NightseerVoleeDEpines:
                case SpellId.NightseerDetonationOnirique:
                case SpellId.NightseerFrappeDeLOmbre:
                case SpellId.NightseerSalveMortelle:
                    return SpellCategory.Offensive;

                // NIGHTSEER — Tactiques (5)
                case SpellId.NightseerMarqueDuChasseur:
                case SpellId.NightseerFiletDeRonces:
                case SpellId.NightseerChampDeMines:
                case SpellId.NightseerBourrasque:
                case SpellId.NightseerSouffleGlacial:
                    return SpellCategory.Tactical;

                // NIGHTSEER — Survie (5)
                case SpellId.NightseerVoileDOmbre:
                case SpellId.NightseerPasFurtif:
                case SpellId.NightseerCamouflageRonces:
                case SpellId.NightseerSeveSauvage:
                case SpellId.NightseerEvanescence:
                    return SpellCategory.Survival;

                // NIGHTSEER — Signature
                case SpellId.NightseerTraquenard:
                    return SpellCategory.Signature;

                default:
                    return SpellCategory.Tactical;
            }
        }

        /// <summary>
        /// Determine le facing iso du combatant selon son dernier deplacement :
        ///   - Si la position grille a change depuis le dernier frame : nouveau facing
        ///     deduit du sens du mouvement.
        ///   - Sinon : on conserve le facing precedent (le perso ne se retourne pas).
        ///   - Au tout premier frame (juste apres spawn) : facing initial dirige vers
        ///     l'ennemi pour que les deux combatants se regardent au depart.
        /// </summary>
        private IsoFacing ResolveFacing(EntityRef entity, Combatant self)
        {
            int gx = self.GridX;
            int gy = self.GridY;

            if (_lastGridPos.TryGetValue(entity, out var last))
            {
                int dxGrid = gx - last.X;
                int dyGrid = gy - last.Y;
                if (dxGrid != 0 || dyGrid != 0)
                {
                    var moved = FacingFromGridDelta(dxGrid, dyGrid);
                    _lastFacings[entity] = moved;
                    _lastGridPos[entity] = new GridPos(gx, gy);
                    return moved;
                }
                // Pas de mouvement : conserve le dernier facing connu.
                return _lastFacings.TryGetValue(entity, out var prev) ? prev : IsoFacing.SE;
            }

            // 1er frame : pas de position precedente -> facing initial vers l'ennemi.
            var initial = FacingTowardEnemy(entity, self);
            _lastGridPos[entity] = new GridPos(gx, gy);
            _lastFacings[entity] = initial;
            return initial;
        }

        /// <summary>
        /// Mappe un delta grille (dxGrid, dyGrid) au quadrant ecran iso (NE/SE/NW/SW).
        ///
        /// Math iso (cf IsoProjection.cs) :
        ///   worldX = (gx - gy) * (tw/2)   -> dx_world = dx_grid - dy_grid
        ///   worldY = (gx + gy) * (th/2)   -> dy_world = dx_grid + dy_grid
        /// Quadrant ecran -> facing :
        ///   droite + haut -> NE
        ///   droite + bas  -> SE
        ///   gauche + haut -> NW
        ///   gauche + bas  -> SW
        /// Cas pile aligne (0) : on prefere east (sans flip) et north (par defaut).
        /// </summary>
        private static IsoFacing FacingFromGridDelta(int dxGrid, int dyGrid)
        {
            int dxWorld = dxGrid - dyGrid;
            int dyWorld = dxGrid + dyGrid;
            bool east = dxWorld >= 0;
            bool north = dyWorld >= 0;
            if (east && north) return IsoFacing.NE;
            if (east && !north) return IsoFacing.SE;
            if (!east && north) return IsoFacing.NW;
            return IsoFacing.SW;
        }

        private IsoFacing FacingTowardEnemy(EntityRef selfEntity, Combatant self)
        {
            for (int j = 0; j < _frameCombatants.Count; j++)
            {
                if (_frameCombatants[j].Entity == selfEntity) continue;
                var enemy = _frameCombatants[j].Data;
                return FacingFromGridDelta(enemy.GridX - self.GridX, enemy.GridY - self.GridY);
            }
            return IsoFacing.SE;
        }

        /// <summary>
        /// Mappe la ressource du combatant a un stage visuel [0..2].
        /// Convention Bible V7.1 :
        ///   Soulrender (HG cap 5) : 0 si HG<2, 1 si 2-4, 2 si HG=5 (fissures ecarlates).
        /// Pour les autres classes (cap different), on garde la meme heuristique :
        ///   stage 0 si ressource &lt; cap*0.4, stage 2 si au cap, stage 1 entre.
        /// </summary>
        private static int ComputeStage(Combatant combatant)
        {
            // 3.6 — Ghostra : stage mappe sur l'Angle Mort (decoys actifs au lieu de Resource).
            //   Angle 1 (0 leurre)   -> stage 0
            //   Angle 2 (1-2 leurres) -> stage 1
            //   Angle 3 (3 leurres)  -> stage 2
            // Cohenrent visuellement : plus la Ghostra a de leurres, plus elle est "spectrale".
            if (combatant.Class == NymoraClass.Ghostra)
            {
                int active = 0;
                for (int i = 0; i < 3; i++)
                {
                    if (combatant.Decoys[i].Kind != DecoyKind.None) active++;
                }
                if (active >= 3) return 2;
                if (active >= 1) return 1;
                return 0;
            }

            int max = Quantum.CombatantStats.GetMaxResource(combatant.Class);
            if (max <= 0) return 0;
            if (combatant.Resource >= max) return 2;
            // Stage 0 si < 40% du cap, stage 1 sinon.
            return combatant.Resource * 5 < max * 2 ? 0 : 1;
        }

        private void SpawnView(EntityRef entity, Combatant combatant)
        {
            GameObject prefab = GetPrefabForClass(combatant.Class);
            if (prefab == null)
            {
                Debug.LogError($"[Nymora.CombatantRenderer] Prefab manquant pour classe {combatant.Class}.", this);
                return;
            }

            Vector3 world = IsoProjection.GridToWorld(
                combatant.GridX, combatant.GridY,
                _gridSettings.TileWorldWidth, _gridSettings.TileWorldHeight) + _centerOffset + transform.position;

            var go = Instantiate(prefab, world, Quaternion.identity, transform);
            go.name = $"Combatant_P{combatant.PlayerIndex}_{combatant.Class}";

            var view = go.GetComponent<CombatantView>();
            if (view == null)
            {
                Debug.LogError($"[Nymora.CombatantRenderer] CombatantView manquant sur prefab {prefab.name}.", this);
                Destroy(go);
                return;
            }

            view.Bind(entity, combatant.Class);
            view.UpdateGridPosition(combatant.GridX, combatant.GridY, world);
            _views[entity] = view;

            Debug.Log($"[Nymora.CombatantRenderer] Spawn P{combatant.PlayerIndex} {combatant.Class} en ({combatant.GridX},{combatant.GridY}) HP={combatant.HP}/{combatant.MaxHP} PA={combatant.PA} PM={combatant.PM}");
        }

        private GameObject GetPrefabForClass(NymoraClass nymoraClass)
        {
            switch (nymoraClass)
            {
                case NymoraClass.Soulrender: return _soulrenderPrefab;
                case NymoraClass.Nightseer: return _nightseerPrefab;
                case NymoraClass.Colossar: return _colossarPrefab;
                case NymoraClass.Necram: return _necramPrefab;
                case NymoraClass.Ghostra: return _ghostraPrefab;
                default: return null;
            }
        }

        private void ClearAll()
        {
            foreach (var pair in _views)
            {
                if (pair.Value != null) Destroy(pair.Value.gameObject);
            }
            _views.Clear();
            _lastGridPos.Clear();
            _lastFacings.Clear();
            _lastHP.Clear();
            _lastCastSeq.Clear();
            _gridReady = false;
        }

        // ====================================================================
        // 3.3.d polish — BFS path cote View pour contourner les obstacles.
        // ====================================================================

        private const int GridWidth = 15;
        private const int GridHeight = 17;
        private const int GridCount = GridWidth * GridHeight;
        // Buffers reutilises pour eviter alloc heap par appel (BFS).
        private readonly int[] _bfsPrev = new int[GridCount];
        private readonly bool[] _bfsVisited = new bool[GridCount];
        private readonly int[] _bfsQueue = new int[GridCount];

        /// <summary>
        /// BFS 4-connexite cote View qui contourne les obstacles (Pilier/Mur/Faille) et autres
        /// combatants (sauf `self` qui est le combatant qu'on deplace). Reconstruit le path le
        /// plus court de (sx,sy) inclus a (tx,ty) inclus dans `outPath`. Retourne false si pas
        /// de path ou si depasse `outPath.Length`.
        ///
        /// Safe API Quantum (Filter.Next par valeur). O(N*M) ou N=cases visitees, M=obstacles
        /// + combatants. Pour 1v1 typique : ~20 cases visitees x ~5 entites = 100 ops, ~0.01ms.
        /// </summary>
        private bool TryFindViewPath(
            Quantum.Frame frame,
            int sx, int sy, int tx, int ty,
            Quantum.EntityRef self,
            System.Span<(int x, int y)> outPath,
            out int outLen,
            bool ignoreEnemyOccupants = false)
        {
            outLen = 0;
            if (sx < 0 || sx >= GridWidth || sy < 0 || sy >= GridHeight) return false;
            if (tx < 0 || tx >= GridWidth || ty < 0 || ty >= GridHeight) return false;
            if (sx == tx && sy == ty) return false;

            // Reset buffers.
            for (int i = 0; i < GridCount; i++) { _bfsPrev[i] = -1; _bfsVisited[i] = false; }

            int startIdx = sy * GridWidth + sx;
            int targetIdx = ty * GridWidth + tx;
            _bfsVisited[startIdx] = true;
            int head = 0, tail = 0;
            _bfsQueue[tail++] = startIdx;

            int[] dxArr = { 1, -1, 0, 0 };
            int[] dyArr = { 0, 0, 1, -1 };

            bool found = false;
            while (head < tail)
            {
                int idx = _bfsQueue[head++];
                if (idx == targetIdx) { found = true; break; }
                int cx = idx % GridWidth;
                int cy = idx / GridWidth;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + dxArr[d];
                    int ny = cy + dyArr[d];
                    if (nx < 0 || nx >= GridWidth || ny < 0 || ny >= GridHeight) continue;
                    int nidx = ny * GridWidth + nx;
                    if (_bfsVisited[nidx]) continue;
                    // La case cible est traversable meme si on detecte un blocking (la sim
                    // a deja valide qu'elle est libre, sinon le combatant ne s'y serait pas
                    // teleporte). Pour les cases intermediaires, on check obstacle/combatant.
                    // 3.5.b.iii Pas Spectral : si flag actif, on autorise traverser les
                    // combatants (mais pas les obstacles Pilier/Mur/Faille — IsBlockedForView
                    // sait distinguer via le param).
                    if (nidx != targetIdx && IsBlockedForView(frame, nx, ny, self, ignoreEnemyOccupants)) continue;
                    _bfsVisited[nidx] = true;
                    _bfsPrev[nidx] = idx;
                    _bfsQueue[tail++] = nidx;
                }
            }

            if (!found) return false;

            // Reconstruct path : remonter prev de targetIdx a startIdx, puis inverser.
            // Compte d'abord la longueur pour valider la capacite du buffer.
            int len = 1; // target lui-meme
            int cur = targetIdx;
            while (cur != startIdx)
            {
                cur = _bfsPrev[cur];
                if (cur < 0) return false;
                len++;
            }
            if (len > outPath.Length) return false;

            // Re-remonte et fill outPath (du start au target).
            cur = targetIdx;
            for (int i = len - 1; i >= 0; i--)
            {
                outPath[i] = (cur % GridWidth, cur / GridWidth);
                if (i > 0) cur = _bfsPrev[cur];
            }
            outLen = len;
            return true;
        }

        /// <summary>
        /// Une case est bloquante pour le pathfinding View si elle contient :
        ///   - Un Obstacle (Pilier/Mur/Faille) actif (HP > 0)
        ///   - Un Combatant vivant autre que `self` (sauf si ignoreEnemyOccupants=true,
        ///     cas Pas Spectral 3.5.b.iii ou les Combatants sont traversables a l'anim).
        /// </summary>
        private static bool IsBlockedForView(Quantum.Frame frame, int x, int y, Quantum.EntityRef self, bool ignoreEnemyOccupants = false)
        {
            // Obstacles : O(N) sur le nb d'obstacles dans la frame (max ~6 typiques).
            var obsFilter = frame.Filter<Quantum.Obstacle>();
            while (obsFilter.Next(out _, out Quantum.Obstacle obs))
            {
                if (obs.HP <= 0) continue;
                if (obs.GridX == x && obs.GridY == y) return true;
            }
            // Combatants : O(N) sur le nb de combatants (max 2 en 1v1, 6 en 3v3).
            if (!ignoreEnemyOccupants)
            {
                var combFilter = frame.Filter<Quantum.Combatant>();
                while (combFilter.Next(out Quantum.EntityRef ent, out Quantum.Combatant c))
                {
                    if (ent == self) continue;
                    if (c.HP <= 0) continue;
                    if (c.GridX == x && c.GridY == y) return true;
                }
            }
            return false;
        }
    }
}
