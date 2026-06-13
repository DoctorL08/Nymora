using System.Collections.Generic;
using Nymora.Combat.Grid;
using Nymora.Core.ScriptableObjects;
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

        [Header("Definitions de classe (calibration visuelle de BASE — drag les 5 NymoraClassDefinition)")]
        [Tooltip("Sert a appliquer la calibration combat de la classe de BASE (X/Y par phase + scale) " +
                 "quand aucun skin cosmetique n'est equipe. Reglable en live via F10 (CombatSkinYTuner). " +
                 "Optionnel : si vide, la base garde les offsets du prefab CombatantView.")]
        [SerializeField] private NymoraClassDefinition[] _classDefinitions;

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
        // 3.7.b.ii — Track Pas dans l'Ombre Ghostra (meme pattern Permutation) : si
        // combatant.LastPasDansLOmbreOnTurn a augmente -> teleport visuel au lieu de walk.
        private readonly Dictionary<EntityRef, int> _lastPasDansLOmbreOnTurn = new Dictionary<EntityRef, int>(2);
        // 3.7.a.iii — Track Frappe Fantome Ghostra : pas de [Networked] field dedie (eviter regen
        // lourde). On detecte via LastCastOnTurn + LastCastSpellId == GhostraFrappeFantome.
        // Cache la derniere valeur de LastCastOnTurn QUAND le spell etait Frappe Fantome.
        private readonly Dictionary<EntityRef, int> _lastFrappeFantomeCastTurn = new Dictionary<EntityRef, int>(2);
        // J7 — teleport generalise (toutes classes) : suit LastCastSequence pour declencher l'anim
        // de TP (fade + flash spectral) quand un SORT DE TELEPORT est lance, comme sur la Ghostra.
        private readonly Dictionary<EntityRef, int> _lastTeleportCastSeq = new Dictionary<EntityRef, int>(2);
        // J9 — charge : suit LastCastSequence pour declencher le DASH (vitesse + trainee) au cast.
        private readonly Dictionary<EntityRef, int> _lastChargeCastSeq = new Dictionary<EntityRef, int>(2);
        // Refonte 29 mai — Piège Bondissant : suit LastEjectedSequence pour rendre l'éjection en snap (lancement).
        private readonly Dictionary<EntityRef, int> _lastEjectedSeq = new Dictionary<EntityRef, int>(2);
        // Sorts d'ÉCHANGE (Échange Spectral Necram / Effondrement Colossar) : suit LastCastSequence
        // pour détecter un swap et forcer le TP-snap sur l'ADVERSAIRE déplacé (pas que le caster).
        private readonly Dictionary<EntityRef, int> _lastSwapCastSeq = new Dictionary<EntityRef, int>(2);
        // Entités à téléporter ce tick parce qu'elles ont été échangées par un swap adverse.
        private readonly HashSet<EntityRef> _swapSnapTargets = new HashSet<EntityRef>();

        // 3.7.a.i — Track Permutation Ghostra pour snap instantané au lieu de walk lerp.
        // Si combatant.LastPermutationOnTurn > cache -> une Permutation vient d'avoir lieu
        // -> on snap le visuel (téléport, Bible "invisible cote adversaire") au lieu de
        // walk anim entre les 2 positions swappées.
        private readonly Dictionary<EntityRef, int> _lastPermutationOnTurn = new Dictionary<EntityRef, int>(2);
        private Vector3 _centerOffset;
        private bool _gridReady;

        // 5.10 (A2) — Catalogue des skins combat, chargé depuis Resources (pas de câblage de
        // scène, marche dans les 3 scènes combat + build). Résout cosmeticId -> CosmeticSkinDefinition.
        private const string SkinCatalogResourcePath = "Cosmetics/CosmeticSkinCatalog";
        private CosmeticSkinCatalog _skinCatalog;
        private bool _skinCatalogLoaded;

        private CosmeticSkinCatalog SkinCatalog
        {
            get
            {
                if (!_skinCatalogLoaded)
                {
                    _skinCatalog = Resources.Load<CosmeticSkinCatalog>(SkinCatalogResourcePath);
                    _skinCatalogLoaded = true;
                    if (_skinCatalog == null)
                        Debug.LogWarning($"[Nymora.CombatantRenderer] CosmeticSkinCatalog introuvable (Resources/{SkinCatalogResourcePath}) — skins combat désactivés.");
                }
                return _skinCatalog;
            }
        }

        // 5.10 (B5) — Familiers en combat : catalogue (Resources) + une vue par combattant équipé.
        private const string PetCatalogResourcePath = "Cosmetics/PetCatalog";
        private PetCatalog _petCatalog;
        private bool _petCatalogLoaded;
        private readonly Dictionary<EntityRef, CombatPetView> _pets = new Dictionary<EntityRef, CombatPetView>(2);

        private PetCatalog PetCatalog
        {
            get
            {
                if (!_petCatalogLoaded)
                {
                    _petCatalog = Resources.Load<PetCatalog>(PetCatalogResourcePath);
                    _petCatalogLoaded = true;
                    if (_petCatalog == null)
                        Debug.LogWarning($"[Nymora.CombatantRenderer] PetCatalog introuvable (Resources/{PetCatalogResourcePath}) — familiers combat désactivés.");
                }
                return _petCatalog;
            }
        }

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
                SpawnView(entity, combatant, frame);
            }
        }

        private void OnUpdateView(QuantumGame game)
        {
            if (!_gridReady) return;

            var frame = game.Frames.Verified;

            // PATCH 22 mai (test designer) — Voile d'Ombre : POV du client courant pour
            // decider quel combattant masquer (un Nightseer Untargetable disparait de
            // l'ecran adverse, pas du sien). Meme resolveur que FogOfWarView/TrapView.
            int localViewer = LocalPlayerResolver.Resolve();

            // #23 — contour de case d'équipe : actif UNIQUEMENT en match miroir (même classe).
            bool mirror = MatchViewHelpers.IsMirrorMatch(frame);

            // Pass 1 : snapshot tous les combatants vivants (besoin de la position adverse
            // pour calculer le facing iso de chacun).
            _frameCombatants.Clear();
            var filter = frame.Filter<Combatant>();
            while (filter.Next(out EntityRef entity, out Combatant combatant))
            {
                _frameCombatants.Add(new CombatantSnapshot(entity, combatant));
            }

            // Pré-passage SWAP : si un combattant vient de caster un sort d'échange (Échange Spectral
            // Necram / Effondrement Colossar), l'AUTRE combattant a été déplacé sur sa case → il doit
            // jouer l'anim de TP lui aussi (sinon il "marche"). On marque l'adversaire ici, avant le
            // calcul de isTeleportSnap dans la passe principale.
            _swapSnapTargets.Clear();
            for (int i = 0; i < _frameCombatants.Count; i++)
            {
                var c = _frameCombatants[i].Data;
                var e = _frameCombatants[i].Entity;
                int prevSwapSeq = _lastSwapCastSeq.TryGetValue(e, out var sv) ? sv : c.LastCastSequence;
                bool swapAdvanced = c.LastCastSequence != prevSwapSeq;
                _lastSwapCastSeq[e] = c.LastCastSequence;
                if (!swapAdvanced || !IsSwapSpell(c.LastCastSpellId)) continue;
                for (int j = 0; j < _frameCombatants.Count; j++)
                {
                    var other = _frameCombatants[j];
                    if (other.Entity != e && other.Data.PlayerIndex != c.PlayerIndex && other.Data.HP > 0)
                        _swapSnapTargets.Add(other.Entity);
                }
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
                    SpawnView(entity, combatant, frame);
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
                    // 3.5.b.iii Pas Spectral (Necram) / 3.7.c.v Pas de l'Au-Dela (Ghostra) :
                    // si le combatant porte le status correspondant (PasSpectralReady ou
                    // PasAuDelaReady), le path d'anim peut traverser les ennemis (cohérent avec
                    // la sim qui passe ignoreEnemyOccupants=true a A* dans les memes conditions).
                    bool animIgnoreEnemyOccupants = false;
                    if (combatant.Class == NymoraClass.Necram || combatant.Class == NymoraClass.Ghostra)
                    {
                        StatusKind expectedKind = combatant.Class == NymoraClass.Necram
                            ? StatusKind.PasSpectralReady
                            : StatusKind.PasAuDelaReady;
                        for (int s = 0; s < StatusHelper.SlotCount; s++)
                        {
                            if (combatant.Statuses[s].Kind == expectedKind
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
                    // FIX trajectoire : pathfinder View PARTAGÉ avec le preview de survol
                    // (MovementRangePreview) pour que la trajectoire animée == celle prévisualisée.
                    bool found = ViewPathfinder.TryFindPath(frame, prevGx, prevGy,
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

                // 3.7.a.i / 3.7.b.ii — Detect Permutation OU Pas dans l'Ombre Ghostra : si
                // LastPermutationOnTurn OU LastPasDansLOmbreOnTurn a augmente depuis le dernier
                // tick -> anim teleport (fade out/in + flash spectral) au lieu de walk lerp.
                bool isTeleportSnap = false;
                if (combatant.Class == NymoraClass.Ghostra)
                {
                    int prevPermutTurn = _lastPermutationOnTurn.TryGetValue(entity, out var pt) ? pt : int.MinValue;
                    if (combatant.LastPermutationOnTurn > prevPermutTurn && combatant.LastPermutationOnTurn > 0)
                    {
                        isTeleportSnap = true;
                        _lastPermutationOnTurn[entity] = combatant.LastPermutationOnTurn;
                    }
                    int prevPDOTurn = _lastPasDansLOmbreOnTurn.TryGetValue(entity, out var pdo) ? pdo : int.MinValue;
                    if (combatant.LastPasDansLOmbreOnTurn > prevPDOTurn && combatant.LastPasDansLOmbreOnTurn > 0)
                    {
                        isTeleportSnap = true;
                        _lastPasDansLOmbreOnTurn[entity] = combatant.LastPasDansLOmbreOnTurn;
                    }
                    // 3.7.a.iii — Frappe Fantome : detecte via LastCastSpellId. Pas de [Networked]
                    // dedie pour eviter regen. Trigger une fois par cast (LastCastOnTurn change).
                    if (combatant.LastCastSpellId == SpellId.GhostraFrappeFantome && combatant.LastCastOnTurn > 0)
                    {
                        int prevFFTurn = _lastFrappeFantomeCastTurn.TryGetValue(entity, out var ff) ? ff : int.MinValue;
                        if (combatant.LastCastOnTurn > prevFFTurn)
                        {
                            isTeleportSnap = true;
                            _lastFrappeFantomeCastTurn[entity] = combatant.LastCastOnTurn;
                        }
                    }
                }

                // J7 — Teleport GENERALISE (toutes classes) : tout sort de TP (Nightseer Pas Furtif /
                // Evanescence / Traquenard, Ghostra Dernier Pas, etc.) declenche l'anim de TP comme
                // la Ghostra. On compare LastCastSequence au tick precedent : si elle a avance ET que
                // le sort lance est un teleport -> snap + flash au lieu d'un walk. Fallback : ne
                // re-declenche pas si le bloc Ghostra ci-dessus a deja pose isTeleportSnap.
                {
                    int prevTpSeq = _lastTeleportCastSeq.TryGetValue(entity, out var tps) ? tps : combatant.LastCastSequence;
                    bool castAdvanced = combatant.LastCastSequence != prevTpSeq;
                    _lastTeleportCastSeq[entity] = combatant.LastCastSequence;
                    if (!isTeleportSnap && castAdvanced && IsTeleportSpell(combatant.LastCastSpellId))
                        isTeleportSnap = true;
                }

                // Adversaire déplacé par un swap (Échange Spectral / Effondrement) → TP-snap lui aussi
                // (marqué dans le pré-passage). Doit primer sur l'éjection-dash ci-dessous.
                if (!isTeleportSnap && _swapSnapTargets.Contains(entity))
                    isTeleportSnap = true;

                // Refonte 29 mai — Piège Bondissant : si LastEjectedSequence a avancé, ce combattant
                // vient d'être ÉJECTÉ -> LANCEMENT (DASH rapide + traînée), pas un snap/TP. Lit
                // comme une catapulte, plus comme une téléportation. Couplé à la fumée sur la case.
                {
                    int prevEjSeq = _lastEjectedSeq.TryGetValue(entity, out var ej) ? ej : combatant.LastEjectedSequence;
                    bool ejAdvanced = combatant.LastEjectedSequence != prevEjSeq;
                    _lastEjectedSeq[entity] = combatant.LastEjectedSequence;
                    if (!isTeleportSnap && ejAdvanced) view.TriggerDash();
                }

                // J9 — Charge : un sort de charge declenche le DASH (mouvement rapide + trainee
                // spectrale facon mini-teleport). Le deplacement reste un walk (pas un snap), juste
                // accelere. Doit etre arme AVANT UpdateGridPosition (qui pousse les waypoints).
                {
                    int prevChSeq = _lastChargeCastSeq.TryGetValue(entity, out var chs) ? chs : combatant.LastCastSequence;
                    bool chAdvanced = combatant.LastCastSequence != prevChSeq;
                    _lastChargeCastSeq[entity] = combatant.LastCastSequence;
                    if (chAdvanced && IsChargeSpell(combatant.LastCastSpellId))
                        view.TriggerDash();
                }

                if (isTeleportSnap)
                {
                    // 3.7.a.i / 3.7.b.ii — VFX téléportation : fade out + snap + flash bleu spectral + fade in.
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

                // PATCH 22 mai (test designer) — Voile d'Ombre Nightseer : masque le rendu
                // cote adversaire si le combattant est Untargetable (Bible V7.1 : il
                // "disparait visuellement de l'ecran adverse pendant 1 tour entier"). On
                // ne masque que les ENNEMIS du viewer local (le Nightseer se voit lui-meme)
                // et seulement s'ils sont vivants (un mort joue son anim Death normalement).
                bool cloakedForViewer = combatant.PlayerIndex != localViewer
                                        && combatant.HP > 0
                                        && HasUntargetable(combatant);
                view.SetCloaked(cloakedForViewer);

                // #23 — contour de CASE en couleur d'équipe sous le combattant (match miroir).
                //   Attaché au GO -> suit le lerp de marche ; caché si voilé / mort / hors miroir.
                //   Le vrai Ghostra ET ses leurres reçoivent le MÊME contour -> indiscernables côté adverse.
                if (view.Sprite != null)
                {
                    MirrorOutlineHelper.RefreshOnUnit(view.transform, view.transform.position,
                        _gridSettings.TileWorldWidth, _gridSettings.TileWorldHeight,
                        mirror && combatant.HP > 0 && !cloakedForViewer, combatant.PlayerIndex,
                        view.Sprite.sortingLayerID, view.Sprite.sortingOrder - 1);
                }

                // 5.10 (B5) — familier : colle au combattant (même case, offset combat), reprend
                // son facing, masqué quand le combattant est voilé pour le viewer.
                if (_pets.TryGetValue(entity, out var pet) && pet != null)
                {
                    pet.SetVisible(!cloakedForViewer);
                    if (!cloakedForViewer)
                    {
                        var sr = view.Sprite;
                        int petOrder = sr != null ? sr.sortingOrder : 0;
                        int petLayer = sr != null ? sr.sortingLayerID : 0;
                        bool isLocal = combatant.PlayerIndex == localViewer;
                        pet.Drive(view.transform.position, view.CurrentFacing, petOrder, petLayer,
                                  isLocal, Time.deltaTime);
                    }
                }

                // 2.12.bis : detection des changements d'etat -> triggers anims.
                DispatchAnimTriggers(entity, combatant, view);
            }
        }

        /// <summary>
        /// PATCH 22 mai — True si le combattant porte StatusKind.Untargetable actif
        /// (Voile d'Ombre Nightseer). Lecture directe du snapshot par valeur (meme pattern
        /// que la detection Pas Spectral / Pas de l'Au-Dela plus haut).
        /// </summary>
        private static bool HasUntargetable(Combatant combatant)
        {
            for (int s = 0; s < StatusHelper.SlotCount; s++)
            {
                if (combatant.Statuses[s].Kind == StatusKind.Untargetable
                    && combatant.Statuses[s].TurnsLeft > 0)
                {
                    return true;
                }
            }
            return false;
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
                // J2 — flash + squash sur le coup fatal (flinch max) + shake selon le coup.
                view.PlayHitReaction(1f);
                RequestHitShake(prevHP - currHP, combatant.MaxHP);
            }
            else if (currHP < prevHP)
            {
                view.TriggerHurt();
                // J2 — hit reaction proportionnelle aux degats encaisses.
                int dmg = prevHP - currHP;
                float intensity = combatant.MaxHP > 0 ? Mathf.Clamp01((float)dmg / combatant.MaxHP) : 0.3f;
                view.PlayHitReaction(intensity);
                RequestHitShake(dmg, combatant.MaxHP);
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
                    // POLISH KYAMI : l'anim se choisit sur « inflige des dégâts directs à une cible »
                    // (def.IsOffensive), plus sur la portée. Dégâts directs -> Attack ; self / marque /
                    // piège / heal / déplacement / utilitaire -> Cast.
                    // Exception : Choc Sismique inflige des dégâts mais porte IsOffensive=0 (bypass de la
                    // damage loop standard, dmg appliqué en handler custom) -> forcé en Attack ici.
                    bool dealsDirectDamage = def.IsOffensive == 1
                                             || spellId == SpellId.ColossarChocSismique;
                    if (dealsDirectDamage)
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

        // J2 (juice combat) — Screen-shake sur les coups, proportionnel a la fraction de PV
        // perdue. Les coups "chip" (< MinFraction) ne shakent pas ; au-dela, l'amplitude monte
        // lineairement jusqu'a MaxFraction. Passe par le CameraShaker central (J1, "le plus fort
        // gagne" -> pas de double-shake si un signature shake deja via FloatingTextManager).
        private const float HitShakeMinFraction = 0.08f;   // < 8% du MaxHP = pas de shake
        private const float HitShakeMaxFraction = 0.30f;    // >= 30% = shake max
        private const float HitShakeMinAmplitude = 0.05f;   // unites monde
        private const float HitShakeMaxAmplitude = 0.16f;
        private const float HitShakeDuration = 0.22f;

        private static void RequestHitShake(int damage, int maxHp)
        {
            if (CameraShaker.Instance == null || maxHp <= 0 || damage <= 0) return;
            float frac = (float)damage / maxHp;
            if (frac < HitShakeMinFraction) return;
            float k = Mathf.InverseLerp(HitShakeMinFraction, HitShakeMaxFraction, frac);
            float amp = Mathf.Lerp(HitShakeMinAmplitude, HitShakeMaxAmplitude, k);
            CameraShaker.Instance.Shake(amp, HitShakeDuration);
        }

        /// <summary>
        /// J7 — Sorts qui TELEPORTENT le caster (saut discret) -> anim de TP (fade + flash spectral)
        /// au lieu d'un walk. Exclut les sorts de MOUVEMENT traversant (Pas Spectral Necram /
        /// Pas de l'Au-Dela Ghostra) qui restent des deplacements animes case par case.
        /// </summary>
        private static bool IsTeleportSpell(SpellId id)
        {
            switch (id)
            {
                case SpellId.NightseerPasFurtif:
                case SpellId.NightseerEvanescence:
                case SpellId.NightseerTraquenard:
                case SpellId.GhostraPasDansLOmbre:
                case SpellId.GhostraFrappeFantome:
                case SpellId.GhostraDernierPas:
                case SpellId.GhostraPermutation: // refonte 30 mai : swap leurre = TP (meme anim que Frappe Fantome)
                case SpellId.NecramPasSpectral:    // Échange Spectral (refonte) : swap caster <-> ennemi
                case SpellId.ColossarEffondrement: // signature : swap caster <-> ennemi (anim TP)
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Sorts d'ÉCHANGE de position (caster ↔ ennemi) : Échange Spectral (Necram) + Effondrement
        /// (signature Colossar). Le caster téléporte via IsTeleportSpell ; l'ENNEMI déplacé par le
        /// swap doit AUSSI jouer l'anim de TP (sinon il "marche" jusqu'à la case échangée). Le
        /// pré-passage marque l'adversaire pour forcer son teleport-snap ce tick.
        /// </summary>
        private static bool IsSwapSpell(SpellId id)
        {
            switch (id)
            {
                case SpellId.NecramPasSpectral:
                case SpellId.ColossarEffondrement:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// J9 — Sorts de CHARGE : le caster fonce vers sa cible -> dash rapide + trainee.
        /// </summary>
        private static bool IsChargeSpell(SpellId id)
        {
            switch (id)
            {
                case SpellId.SoulrenderChargeBrutale:
                    return true;
                default:
                    return false;
            }
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
            // 3.7.a.i.0 / 3.7.b.iii — Quantum est source-of-truth pour le Facing.
            //   - Spawn : init dans CombatantSystem (P0=NE, P1=NW).
            //   - Move : MovementSystem.ApplyMove + MovementHelpers.MoveNonPM update depuis dx/dy.
            //   - Cast non-Self : SpellSystem.TryCastSpell update vers cible.
            //   - Volte-Face : flip 180° via FacingHelpers.Opposite + DirectionLocked 1 round.
            //   - Pas dans l'Ombre : pivot adj enemies vers Ghostra.
            // La View consomme juste cette valeur — pas de recalcul cote View.
            //
            // ⚠️ MISMATCH ENUM Quantum (SE=0/NE=1/NW=2/SW=3) vs View (NE=0/SE=1/NW=2/SW=3) :
            //   le cast par valeur entiere FAUSSE le mapping SE<->NE. Mapping explicite obligatoire.
            IsoFacing facingFromQuantum = QuantumToViewFacing(self.Facing);
            _lastGridPos[entity] = new GridPos(self.GridX, self.GridY);
            _lastFacings[entity] = facingFromQuantum;
            return facingFromQuantum;
        }

        /// <summary>
        /// 3.7.b.iii hotfix : Quantum IsoFacing (SE=0/NE=1/NW=2/SW=3) ne s'aligne pas
        /// par valeur avec View IsoFacing (NE=0/SE=1/NW=2/SW=3). Mapping explicite.
        /// </summary>
        private static IsoFacing QuantumToViewFacing(Quantum.IsoFacing q)
        {
            switch (q)
            {
                case Quantum.IsoFacing.NE: return IsoFacing.NE;
                case Quantum.IsoFacing.SE: return IsoFacing.SE;
                case Quantum.IsoFacing.NW: return IsoFacing.NW;
                case Quantum.IsoFacing.SW: return IsoFacing.SW;
                default:                   return IsoFacing.SE;
            }
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

        private void SpawnView(EntityRef entity, Combatant combatant, Frame frame)
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

            // 5.10 (A3) — Applique le skin combat équipé de CE joueur (local OU adverse), lu depuis
            // son RuntimePlayer (sync Quantum AddPlayer).
            var skin = ResolveSkinFor(combatant, frame);
            if (skin != null)
            {
                view.ApplySkin(skin);
                Debug.Log($"[Nymora.CombatantRenderer] Skin combat '{skin.CosmeticId}' appliqué à P{combatant.PlayerIndex} {combatant.Class}.");
            }
            else
            {
                // Pas de skin -> applique la calibration combat de la classe de BASE (X/Y par phase
                // + scale) si la NymoraClassDefinition est wirée. Permet au tuner F10 de régler la
                // base et que ça persiste (sinon la base garde les offsets du prefab).
                var cdef = FindClassDef((Nymora.Core.Enums.NymoraClass)(byte)combatant.Class);
                if (cdef != null)
                {
                    view.SetCombatXOffsets(cdef.Stage0CombatXOffset, cdef.Stage1CombatXOffset, cdef.Stage2CombatXOffset);
                    view.SetCombatYOffsets(cdef.Stage0CombatYOffset, cdef.Stage1CombatYOffset, cdef.Stage2CombatYOffset);
                    view.SetCombatVisualScale(cdef.CombatVisualScale);
                }
            }

            view.UpdateGridPosition(combatant.GridX, combatant.GridY, world);
            _views[entity] = view;

            // 5.10 (B5) — familier équipé de CE joueur (local OU adverse), lu depuis son
            // RuntimePlayer.PetId (sync Quantum AddPlayer, posé par le bootstrap).
            SpawnPetFor(entity, combatant, frame, view);

            Debug.Log($"[Nymora.CombatantRenderer] Spawn P{combatant.PlayerIndex} {combatant.Class} en ({combatant.GridX},{combatant.GridY}) HP={combatant.HP}/{combatant.MaxHP} PA={combatant.PA} PM={combatant.PM}");
        }

        /// <summary>
        /// 5.10 (B5) — Crée la vue du familier d'un combattant si son RuntimePlayer porte un PetId
        /// résoluble. GameObject autonome (non parenté), piloté chaque frame dans OnUpdateView.
        /// </summary>
        private void SpawnPetFor(EntityRef entity, Combatant combatant, Frame frame, CombatantView ownerView)
        {
            var catalog = PetCatalog;
            if (catalog == null || frame == null) return;

            var runtimePlayer = frame.GetPlayerData((PlayerRef)combatant.PlayerIndex);
            string petId = runtimePlayer != null ? runtimePlayer.PetId : null;
            if (string.IsNullOrEmpty(petId)) return;

            var def = catalog.Resolve(petId);
            if (def == null) return;

            int sortingLayerId = ownerView.Sprite != null ? ownerView.Sprite.sortingLayerID : 0;
            var go = new GameObject($"CombatPet_P{combatant.PlayerIndex}_{petId}");
            go.transform.position = ownerView.transform.position; // évite un flash à l'origine
            var pet = go.AddComponent<CombatPetView>();
            pet.Init(def, sortingLayerId);
            _pets[entity] = pet;
            Debug.Log($"[Nymora.CombatantRenderer] Familier '{petId}' spawné pour P{combatant.PlayerIndex}.");
        }

        /// <summary>
        /// 5.10 (A3) — Résout le skin combat à appliquer à un combattant (local OU adverse),
        /// depuis le cosmeticId porté par son RuntimePlayer (sync par Quantum AddPlayer, posé par
        /// le bootstrap depuis CombatCosmeticsContext). VIEW-ONLY, jamais lu par la sim.
        ///
        /// Garde-fou class-lock : le skin ne s'applique que si sa classe correspond à celle du
        /// combattant. Comparaison par nom car ClassId (Core enum) ≠ combatant.Class (Quantum enum).
        /// </summary>
        private CosmeticSkinDefinition ResolveSkinFor(Combatant combatant, Frame frame)
        {
            var catalog = SkinCatalog;
            if (catalog == null || frame == null) return null;

            var runtimePlayer = frame.GetPlayerData((PlayerRef)combatant.PlayerIndex);
            string cosmeticId = runtimePlayer != null ? runtimePlayer.SkinId : null;
            if (string.IsNullOrEmpty(cosmeticId)) return null;

            var skin = catalog.Resolve(cosmeticId);
            if (skin == null) return null;

            if (skin.ClassId != Nymora.Core.Enums.NymoraClass.None
                && skin.ClassId.ToString() != combatant.Class.ToString())
                return null;

            return skin;
        }

        /// <summary>
        /// PATCH — Materiau du SpriteRenderer d'un combattant (par entite). Utilise par DecoyView
        /// pour que les leurres rendent avec le MEME materiau que la vraie Ghostra : sinon le leurre
        /// (cree par AddComponent&lt;SpriteRenderer&gt; -> Sprites-Default UNLIT) ne reagit pas aux
        /// lights 2D comme la vraie Ghostra (materiau 2D Lit), d'ou la difference de rendu cote adverse.
        /// Null si la vue du combattant est inconnue.
        /// </summary>
        public Material GetCombatantSpriteMaterial(EntityRef entity)
        {
            if (_views.TryGetValue(entity, out var view) && view != null && view.Sprite != null)
                return view.Sprite.sharedMaterial;
            return null;
        }

        /// <summary>
        /// Vue d'un combattant (null si inconnue). Lu par DecoyView pour que les leurres Ghostra
        /// reprennent le controller de base + les boucles d'aura de la vraie Ghostra (indiscernable).
        /// </summary>
        public CombatantView GetCombatantView(EntityRef entity)
            => _views.TryGetValue(entity, out var view) ? view : null;

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

        /// <summary>Résout la NymoraClassDefinition wirée pour une classe (null si absente).
        /// Sert à la calibration de base + exposé au tuner F10 (CombatSkinYTuner).
        /// Prend le NymoraClass de Core.Enums (= type de ClassId) ; l'appelant combat caste
        /// depuis Quantum.NymoraClass via (byte) (mêmes valeurs, cf TimelineView).</summary>
        public NymoraClassDefinition FindClassDef(Nymora.Core.Enums.NymoraClass classId)
        {
            if (_classDefinitions == null) return null;
            foreach (var d in _classDefinitions)
                if (d != null && d.ClassId == classId) return d;
            return null;
        }

        private void ClearAll()
        {
            foreach (var pair in _views)
            {
                if (pair.Value != null) Destroy(pair.Value.gameObject);
            }
            _views.Clear();
            // 5.10 (B5) — détruit les vues de familiers (GameObjects autonomes non parentés).
            foreach (var pair in _pets)
            {
                if (pair.Value != null) Destroy(pair.Value.gameObject);
            }
            _pets.Clear();
            _lastGridPos.Clear();
            _lastFacings.Clear();
            _lastHP.Clear();
            _lastCastSeq.Clear();
            _lastTeleportCastSeq.Clear();
            _lastChargeCastSeq.Clear();
            _gridReady = false;
        }

        // FIX trajectoire — le BFS path View (contournement obstacles/combatants) a été
        // factorisé dans Nymora.Combat.View.ViewPathfinder, partagé avec MovementRangePreview
        // pour garantir trajectoire animée == trajectoire prévisualisée.
    }
}
