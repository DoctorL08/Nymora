using System.Collections;
using Nymora.Core.ScriptableObjects;
using Quantum;
using UnityEngine;
using SpellCategory = Nymora.Core.Enums.SpellCategory;

namespace Nymora.Combat.View
{
    /// <summary>
    /// Iso facing : 4 directions visibles dans l'iso 2:1 Nymora.
    /// Le designer livre les sprites NE et SE ; NW et SW sont obtenus par
    /// mirroir horizontal (flipX) du sprite correspondant.
    ///   - NE : ennemi en haut-droite ecran    (flipX = false, ctrl = NE)
    ///   - SE : ennemi en bas-droite  ecran    (flipX = false, ctrl = SE)
    ///   - NW : ennemi en haut-gauche ecran    (flipX = true , ctrl = NE)
    ///   - SW : ennemi en bas-gauche  ecran    (flipX = true , ctrl = SE)
    /// </summary>
    public enum IsoFacing
    {
        NE = 0,
        SE = 1,
        NW = 2,
        SW = 3,
    }

    /// <summary>
    /// MonoBehaviour leger attache a chaque GameObject combattant cote View.
    /// Pas de logique gameplay — uniquement la representation visuelle.
    /// Le CombatantRenderer pousse la position iso a chaque update verifie.
    ///
    /// 2.12 : ajout du stage swap selon ressource (Soulrender = HG) et du facing
    /// 4 directions iso (NE/SE/NW/SW) — sprites livres en NE+SE, miroir pour
    /// les directions W.
    ///   - Stage 0 : ressource = 0 ou 1 (peau normale)
    ///   - Stage 1 : ressource = 2 a 4 (aura rouge progressive)
    ///   - Stage 2 : ressource au cap (5/5 Soulrender, fissures ecarlates Bible V7.1)
    /// Convention : le combatant regarde toujours vers l'ennemi (auto-aim
    /// calcule par CombatantRenderer).
    /// </summary>
    public class CombatantView : MonoBehaviour
    {
        // 2.12.bis : vitesse constante (Vector3.MoveTowards) en unites/seconde.
        // 1 case = 1 unite Unity. A 2.5 u/s -> 1 case en 0.4s, soit ~3 cycles de walk
        // (clip walk 6 frames * 12 FPS Aseprite default = 0.5s par cycle). L'anim walk
        // tourne assez de fois pour etre visible. Plus tard on pourra moduler selon PM.
        private const float MoveSpeedUnitsPerSecond = 2.5f;
        // Snap quand on est tres proche (0.05 = 5% case). Limite l'oscillation finale.
        private const float SnapDistance = 0.05f;

        // J9 (juice combat) — DASH de charge : le mouvement d'un sort de charge (Charge Brutale)
        // est joue beaucoup plus vite + laisse une trainee spectrale (effet "mini-teleport").
        // Active par CombatantRenderer.TriggerDash() au cast, desactive en fin de mouvement.
        private const float DashSpeedMultiplier = 3.6f;
        private const float AfterimageInterval = 0.045f;
        private bool _dashing;
        private float _afterimageTimer;

        [SerializeField] private SpriteRenderer _sprite;
        [Tooltip("Optionnel. Si fourni avec des AnimatorController par stage, prend la priorite sur les sprites statiques.")]
        [SerializeField] private Animator _animator;

        [Header("Stages visuels SE (sprites idle statiques — fallback si pas d'Animator)")]
        [Tooltip("Sprite idle stage 0 SE (ressource basse, peau normale). Bible V7.1 Soulrender : HG 0-1.")]
        [SerializeField] private Sprite _stage0SpriteSE;
        [Tooltip("Sprite idle stage 1 SE (ressource mid, aura visible). Bible V7.1 Soulrender : HG 2-4.")]
        [SerializeField] private Sprite _stage1SpriteSE;
        [Tooltip("Sprite idle stage 2 SE (ressource au cap, signature debloquee). Bible V7.1 Soulrender : HG = 5.")]
        [SerializeField] private Sprite _stage2SpriteSE;

        [Header("Stages visuels NE (sprites idle statiques — fallback si pas d'Animator)")]
        [Tooltip("Sprite idle stage 0 NE.")]
        [SerializeField] private Sprite _stage0SpriteNE;
        [Tooltip("Sprite idle stage 1 NE.")]
        [SerializeField] private Sprite _stage1SpriteNE;
        [Tooltip("Sprite idle stage 2 NE.")]
        [SerializeField] private Sprite _stage2SpriteNE;

        [Header("Stages animes SE (AnimatorController par stage — prioritaire sur sprites statiques)")]
        [Tooltip("AnimatorController stage 0 SE (ressource basse). Idle/walk/attack/cast/hurt/death.")]
        [SerializeField] private RuntimeAnimatorController _stage0ControllerSE;
        [Tooltip("AnimatorController stage 1 SE (ressource mid).")]
        [SerializeField] private RuntimeAnimatorController _stage1ControllerSE;
        [Tooltip("AnimatorController stage 2 SE (ressource au cap, signature ready).")]
        [SerializeField] private RuntimeAnimatorController _stage2ControllerSE;

        [Header("Stages animes NE (AnimatorController par stage)")]
        [Tooltip("AnimatorController stage 0 NE.")]
        [SerializeField] private RuntimeAnimatorController _stage0ControllerNE;
        [Tooltip("AnimatorController stage 1 NE.")]
        [SerializeField] private RuntimeAnimatorController _stage1ControllerNE;
        [Tooltip("AnimatorController stage 2 NE.")]
        [SerializeField] private RuntimeAnimatorController _stage2ControllerNE;

        // ==================================================================================
        // Refonte Ghostra (juin 2026) — Couches d'AURA "sandwich" par stage.
        //
        // Nouveau modele visuel : le skin de BASE (stage 0/1/2 controllers ci-dessus) est
        // IDENTIQUE aux 3 stages (le tool bind le meme controller de base sur les 3 slots).
        // La montee en puissance visuelle est portee par 2 couches d'aura qui prennent le
        // perso en sandwich :
        //   - AuraBack  : rendu DERRIERE le perso (sortingOrder = base - 1)
        //   - AuraFront : rendu DEVANT  le perso (sortingOrder = base + 1)
        // Chaque couche a son propre Animator (boucle ambiante independante du skin de base).
        //
        // Mapping (decide 13 juin) : 1 aura PAR stage (stage 2 REMPLACE stage 1, pas de cumul).
        //   - stage 0 : auras eteintes (renderers disabled)
        //   - stage 1 : aura stage 1 (back + front)
        //   - stage 2 : aura stage 2 (back + front)
        // Facing : memes regles que le skin de base (NE/SE livres, NW/SW = miroir flipX).
        //
        // Optionnel : si _auraBack/_auraFront sont null (= toutes les classes sauf Ghostra),
        // toute la logique d'aura est un no-op. Aucun impact sur Necram/Soulrender/Nightseer/
        // Colossar.
        // ==================================================================================
        // ⚠️ Les couches d'aura n'ont PAS d'Animator : imbriquer un Animator sous l'Animator du
        // skin de base (tous deux sur "Visual") casse l'anim du parent (l'idle disparaissait). On
        // pilote donc l'aura via AuraLoopPlayer (boucle de Sprite[] sans Animator).
        [Header("Couches d'aura par stage (refonte Ghostra — sandwich back/front). Null = pas d'aura.")]
        [Tooltip("SpriteRenderer de l'aura DERRIERE le perso (sorting = base - 1). Child de Visual.")]
        [SerializeField] private SpriteRenderer _auraBack;
        [Tooltip("SpriteRenderer de l'aura DEVANT le perso (sorting = base + 1). Child de Visual.")]
        [SerializeField] private SpriteRenderer _auraFront;
        [Tooltip("Lecteur de boucle de l'aura back (sur le meme GO que _auraBack).")]
        [SerializeField] private AuraLoopPlayer _auraBackPlayer;
        [Tooltip("Lecteur de boucle de l'aura front.")]
        [SerializeField] private AuraLoopPlayer _auraFrontPlayer;
        [Tooltip("Cadence des boucles d'aura (frames/s).")]
        [SerializeField] private float _auraFps = 8f;

        [Header("Aura stage 1 (frames back/front × SE/NE)")]
        [SerializeField] private Sprite[] _auraStage1BackSE;
        [SerializeField] private Sprite[] _auraStage1BackNE;
        [SerializeField] private Sprite[] _auraStage1FrontSE;
        [SerializeField] private Sprite[] _auraStage1FrontNE;

        [Header("Aura stage 2 (frames back/front × SE/NE)")]
        [SerializeField] private Sprite[] _auraStage2BackSE;
        [SerializeField] private Sprite[] _auraStage2BackNE;
        [SerializeField] private Sprite[] _auraStage2FrontSE;
        [SerializeField] private Sprite[] _auraStage2FrontNE;

        // Decalage de tri des couches d'aura relativement au sprite de base.
        private const int AuraBackSortingDelta = -1;
        private const int AuraFrontSortingDelta = 1;

        [Header("Y offset visuel par stage (workaround pivot Aseprite non standardise)")]
        [Tooltip("Y offset applique au sprite (child Visual) en stage 0. Default 0.")]
        [SerializeField] private float _stage0VisualYOffset = 0f;
        [Tooltip("Y offset applique au sprite (child Visual) en stage 1. Default 0.")]
        [SerializeField] private float _stage1VisualYOffset = 0f;
        [Tooltip("Y offset applique au sprite (child Visual) en stage 2. Default 0.")]
        [SerializeField] private float _stage2VisualYOffset = 0f;

        [Header("X offset visuel par stage (recalage horizontal — meme workaround pivot que Y)")]
        [Tooltip("X offset applique au sprite (child Visual) en stage 0. Default 0.")]
        [SerializeField] private float _stage0VisualXOffset = 0f;
        [Tooltip("X offset applique au sprite (child Visual) en stage 1. Default 0.")]
        [SerializeField] private float _stage1VisualXOffset = 0f;
        [Tooltip("X offset applique au sprite (child Visual) en stage 2. Default 0.")]
        [SerializeField] private float _stage2VisualXOffset = 0f;

        public EntityRef Entity { get; private set; }
        public int GridX { get; private set; }
        public int GridY { get; private set; }
        public NymoraClass Class { get; private set; }

        // 2.16.c.vi — Animation cardinal cell-by-cell : queue de waypoints au lieu
        // d'un single target. Le Renderer push une liste de cases intermediaires en
        // X puis Y (style Dofus), et l'Update consomme un waypoint a la fois via
        // MoveTowards. Resultat : meme si la sim teleporte le combattant, le visuel
        // animate chaque case du trajet.
        private const int MaxWaypoints = 16; // max ~10 cases path + marge
        private readonly Vector3[] _waypoints = new Vector3[MaxWaypoints];
        private int _waypointCount = 0;
        private int _waypointIdx = 0;
        private int _facingAppliedForWaypointIdx = -1; // pour ne re-calculer le facing qu'au changement de segment
        private bool _hasTarget;
        private int _currentStage = -1; // -1 = pas encore initialise
        private IsoFacing _currentFacing = (IsoFacing)(-1); // sentinelle invalide pour forcer le premier set

        // Tuner dev (CombatSkinYTuner / F10) — force le stage AFFICHE pour recaler une phase
        // precise sans devoir amener le perso a la bonne ressource. -1 = pas d'override (le stage
        // pousse par le renderer selon la ressource s'applique normalement). Toujours -1 en prod.
        private int _previewStageOverride = -1;

        // Skin combat (5.10) — échelle de base du child "Visual" de la classe, capturée au Bind, pour
        // y appliquer le multiplicateur CombatVisualScale du skin (réglable en live via F10). Mono-GO
        // (sprite sur le root) → non capturé, pas de scale (sinon casse hit-punch / worldPos).
        private Vector3 _visualBaseScale = Vector3.one;
        private bool _visualBaseCaptured;

        /// <summary>
        /// 2.16.c.vi — True tant qu'il reste des waypoints a consommer. Utilise par
        /// le Renderer pour ceder le controle du facing au View pendant le mouvement
        /// (chaque segment de path peut avoir une orientation iso differente).
        /// </summary>
        public bool IsMoving => _waypointIdx < _waypointCount;

        /// <summary>Facing iso courant (defaut SE si jamais set). Lu par le Renderer
        /// pendant les moves pour preserver le facing entre 2 updates de stage.</summary>
        public IsoFacing CurrentFacing => _currentFacing == (IsoFacing)(-1) ? IsoFacing.SE : _currentFacing;

        /// <summary>5.10 (B5) — SpriteRenderer du combattant, lu par le CombatantRenderer pour
        /// que le familier (CombatPetView) copie sa couche/ordre de tri et son facing.</summary>
        public SpriteRenderer Sprite => _sprite;

        // 2.12.bis : Animator parameter hashes (perf, evite Animator.StringToHash a chaque call).
        // Doit matcher les const du BuildSoulrenderAnimator (ParamMoveSpeed, etc.).
        private static readonly int ParamMoveSpeedHash = Animator.StringToHash("MoveSpeed");
        private static readonly int ParamCastSpeedHash = Animator.StringToHash("CastSpeed");
        private static readonly int ParamCastHash = Animator.StringToHash("Cast");
        private static readonly int ParamAttackHash = Animator.StringToHash("Attack");
        private static readonly int ParamHurtHash = Animator.StringToHash("Hurt");
        private static readonly int ParamDeathHash = Animator.StringToHash("Death");

        // Vitesse desiree pendant le lerp de mouvement. Le Renderer la set selon le PM
        // depense (1-2 PM -> walk lent, 3+ PM -> walk rapide). 0 = arret (state Idle).
        private float _desiredMoveSpeed = 1f;

        public void Bind(EntityRef entity, NymoraClass nymoraClass)
        {
            Entity = entity;
            Class = nymoraClass;
            if (_sprite == null) _sprite = GetComponentInChildren<SpriteRenderer>();

            // Capture l'échelle de base du child "Visual" (avant tout scale de skin) pour pouvoir
            // appliquer le multiplicateur du skin sans compounding. Uniquement si le sprite est sur
            // un child (sinon scaler le root casserait le hit-punch / worldPos).
            if (!_visualBaseCaptured && _sprite != null && _sprite.transform != transform)
            {
                _visualBaseScale = _sprite.transform.localScale;
                _visualBaseCaptured = true;
            }

            // Affichage initial : stage 0 + facing SE par defaut (sera override des le 1er OnUpdateView).
            SetStageAndFacing(0, IsoFacing.SE);
        }

        /// <summary>
        /// Brique 5.10 (A2) — Applique un skin cosmetique en COMBAT : remplace les 6
        /// AnimatorController de classe (stage 0/1/2 × NE/SE) par ceux du skin + ses Y offsets.
        /// Le CombatantRenderer l'appelle au spawn quand le joueur a un skin equipe pour sa classe.
        ///
        /// No-op si le skin est null ou n'embarque pas de variante combat (HasCombatControllers) :
        /// dans ce cas le combattant garde le visuel de classe (degradation propre).
        ///
        /// Force un refresh du visuel courant (re-applique stage+facing) pour que le swap soit
        /// visible immediatement, meme si Bind a deja pose le stage 0.
        /// </summary>
        public void ApplySkin(CosmeticSkinDefinition skin)
        {
            if (skin == null || !skin.HasCombatControllers) return;

            AppliedSkinId = skin.CosmeticId;
            _stage0ControllerSE = skin.Stage0ControllerSE;
            _stage1ControllerSE = skin.Stage1ControllerSE;
            _stage2ControllerSE = skin.Stage2ControllerSE;
            _stage0ControllerNE = skin.Stage0ControllerNE;
            _stage1ControllerNE = skin.Stage1ControllerNE;
            _stage2ControllerNE = skin.Stage2ControllerNE;
            _stage0VisualYOffset = skin.Stage0CombatYOffset;
            _stage1VisualYOffset = skin.Stage1CombatYOffset;
            _stage2VisualYOffset = skin.Stage2CombatYOffset;
            _stage0VisualXOffset = skin.Stage0CombatXOffset;
            _stage1VisualXOffset = skin.Stage1CombatXOffset;
            _stage2VisualXOffset = skin.Stage2CombatXOffset;

            // Taille du skin en combat : échelle de base du Visual × multiplicateur du skin
            // (réglable en live via F10, persisté sur l'asset → toutes scènes). Child "Visual"
            // uniquement (jamais le root → hit-punch/worldPos préservés).
            SetCombatVisualScale(skin.CombatVisualScale);

            // Force le re-set : on relit le stage/facing courant puis on invalide les sentinelles
            // pour que SetStageAndFacing ne court-circuite pas (no-op si rien n'a change).
            int stage = _currentStage < 0 ? 0 : _currentStage;
            IsoFacing facing = _currentFacing == (IsoFacing)(-1) ? IsoFacing.SE : _currentFacing;
            _currentStage = -1;
            _currentFacing = (IsoFacing)(-1);
            SetStageAndFacing(stage, facing);
        }

        /// <summary>cosmeticId du skin combat applique (ApplySkin), "" si aucun. Lu par le tuner Y.</summary>
        public string AppliedSkinId { get; private set; } = "";

        /// <summary>True si le SpriteRenderer est sur un child (=> le Y offset par stage est applicable).
        /// False pour les prefabs mono-GO (Soulrender/Nightseer) ou l'offset est ignore.</summary>
        public bool SpriteOnChild => _sprite != null && _sprite.transform != transform;

        /// <summary>
        /// Tuner dev (CombatSkinYTuner) — pousse les 3 Y offsets de stage en LIVE et re-applique
        /// celui du stage courant SANS reset d'animation (contrairement a ApplySkin qui re-swap le
        /// controller). No-op visuel si mono-GO (cf SpriteOnChild).
        /// </summary>
        /// <summary>
        /// Tuner dev (CombatSkinYTuner F10) — applique en LIVE l'échelle du skin combat sur le child
        /// "Visual" (échelle de base × scale). No-op sur les prefabs mono-GO (sprite sur le root).
        /// </summary>
        public void SetCombatVisualScale(float scale)
        {
            if (!_visualBaseCaptured || _sprite == null || _sprite.transform == transform) return;
            _sprite.transform.localScale = _visualBaseScale * Mathf.Max(0.01f, scale);
        }

        public void SetCombatYOffsets(float s0, float s1, float s2)
        {
            _stage0VisualYOffset = s0;
            _stage1VisualYOffset = s1;
            _stage2VisualYOffset = s2;
            if (_sprite == null || _sprite.transform == transform) return;
            int stage = _currentStage < 0 ? 0 : _currentStage;
            float off = stage == 0 ? _stage0VisualYOffset
                      : stage == 1 ? _stage1VisualYOffset
                      : _stage2VisualYOffset;
            Vector3 pos = _sprite.transform.localPosition;
            if (!Mathf.Approximately(pos.y, off))
            {
                pos.y = off;
                _sprite.transform.localPosition = pos;
            }
        }

        /// <summary>
        /// Tuner dev (CombatSkinYTuner F10) — pendant horizontal de SetCombatYOffsets : pousse les
        /// 3 X offsets de stage en LIVE et re-applique celui du stage courant. No-op visuel si
        /// mono-GO (sprite sur le root, cf SpriteOnChild).
        /// </summary>
        public void SetCombatXOffsets(float s0, float s1, float s2)
        {
            _stage0VisualXOffset = s0;
            _stage1VisualXOffset = s1;
            _stage2VisualXOffset = s2;
            if (_sprite == null || _sprite.transform == transform) return;
            int stage = _currentStage < 0 ? 0 : _currentStage;
            float off = stage == 0 ? _stage0VisualXOffset
                      : stage == 1 ? _stage1VisualXOffset
                      : _stage2VisualXOffset;
            Vector3 pos = _sprite.transform.localPosition;
            if (!Mathf.Approximately(pos.x, off))
            {
                pos.x = off;
                _sprite.transform.localPosition = pos;
            }
        }

        /// <summary>
        /// Tuner dev (CombatSkinYTuner F10) — force le stage AFFICHE (preview de phase) ou le
        /// libere. <paramref name="stage"/> dans [0..2] force ; -1 (ou hors borne) rend la main
        /// au renderer (stage selon ressource). Applique immediatement le stage de preview.
        /// </summary>
        public void SetPreviewStageOverride(int stage)
        {
            _previewStageOverride = (stage < 0 || stage > 2) ? -1 : stage;
            // Re-applique le stage courant en respectant (ou non) le nouvel override. On invalide
            // la sentinelle pour forcer SetStageAndFacing a ne pas court-circuiter.
            int target = _previewStageOverride >= 0 ? _previewStageOverride
                       : (_currentStage < 0 ? 0 : _currentStage);
            IsoFacing facing = _currentFacing == (IsoFacing)(-1) ? IsoFacing.SE : _currentFacing;
            _currentStage = -1;
            SetStageAndFacing(target, facing);
        }

        // ======================================================================
        // POLISH-5d (17 mai) — Hover highlight (tint sprite). Drive par TileHoverView
        // quand la souris passe sur la case du combatant.
        // ======================================================================
        private static readonly Color HighlightTint = new Color(1.25f, 1.25f, 0.95f, 1f); // jaune doux > 1 pour glow

        public void ApplyHighlight()
        {
            if (_sprite == null) return;
            _sprite.color = HighlightTint;
        }

        public void ClearHighlight()
        {
            if (_sprite == null) return;
            // Restore couleur normale (blanc plein). Si une coroutine teleport modifie alpha,
            // ce reset peut entrer en conflit — mais le hover ne s'applique qu'a un combatant
            // immobile (la grille reste statique pendant un hover). Cas tres rare en pratique.
            _sprite.color = Color.white;
        }

        // ======================================================================
        // Brique J2 (juice combat) — Hit reaction : flash blanc + squash/recoil du sprite
        // quand le combatant encaisse des degats. Piloté par CombatantRenderer.DispatchAnimTriggers
        // (qui detecte deja le delta HP). 100% View, self-contained, safe si _sprite null.
        //
        // Le squash s'applique sur le ROOT transform.localScale : l'ancre etant aux pieds
        // (case iso), une compression Y vers le bas + elargissement X lit comme un encaissement.
        // Le flash sur-eclaire le sprite (couleur > 1, comme le HighlightTint glow).
        //
        // ⚠️ Edge connu (rare) : un hit pile pendant le VFX teleport Ghostra OU un hover peut
        // se chevaucher (les deux touchent _sprite.color). On capture la couleur courante comme
        // base et on la restaure en fin de reaction → le cas se resorbe en <0.2s.
        // ======================================================================
        private static readonly Color HitFlashColor = new Color(1.9f, 1.9f, 1.9f, 1f);
        private const float HitFlashUpDuration = 0.05f;     // montee flash + squash (snap rapide)
        private const float HitRecoverDuration = 0.16f;     // retour a la normale (ease-out)
        private const float HitSquashMin = 0.06f;           // deformation a faible degat
        private const float HitSquashMax = 0.16f;           // deformation a gros degat

        private Coroutine _hitReactionCo;
        private Vector3 _hitBaseScale = Vector3.one;
        private Color _hitBaseColor = Color.white;
        private bool _reacting;

        /// <summary>
        /// J2 — Joue la reaction d'encaissement (flash + squash). `intensity01` (0..1) module
        /// l'ampleur du squash (gros coup = flinch plus marque) ; calculee par le Renderer
        /// comme degats / MaxHP. No-op si pas de sprite.
        /// </summary>
        public void PlayHitReaction(float intensity01)
        {
            if (_sprite == null) return;
            intensity01 = Mathf.Clamp01(intensity01);

            if (!_reacting)
            {
                // 1re reaction : on memorise l'etat de repos a restaurer.
                _hitBaseScale = transform.localScale;
                _hitBaseColor = _sprite.color;
            }
            else if (_hitReactionCo != null)
            {
                // Re-hit pendant une reaction : on coupe et repart de la base propre.
                StopCoroutine(_hitReactionCo);
                transform.localScale = _hitBaseScale;
            }

            _hitReactionCo = StartCoroutine(HitReactionRoutine(intensity01));
        }

        private IEnumerator HitReactionRoutine(float intensity01)
        {
            _reacting = true;
            float squash = Mathf.Lerp(HitSquashMin, HitSquashMax, intensity01);
            Vector3 punch = new Vector3(
                _hitBaseScale.x * (1f + squash),
                _hitBaseScale.y * (1f - squash),
                _hitBaseScale.z);

            // Phase 1 : snap vers le punch + flash (temps non-scale, pret pour un futur hit-stop).
            float t = 0f;
            while (t < HitFlashUpDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / HitFlashUpDuration);
                transform.localScale = Vector3.Lerp(_hitBaseScale, punch, k);
                if (_sprite != null) _sprite.color = Color.Lerp(_hitBaseColor, HitFlashColor, k);
                yield return null;
            }

            // Phase 2 : retour ease-out vers le repos.
            t = 0f;
            while (t < HitRecoverDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / HitRecoverDuration);
                float ease = 1f - (1f - k) * (1f - k); // ease-out quad
                transform.localScale = Vector3.Lerp(punch, _hitBaseScale, ease);
                if (_sprite != null) _sprite.color = Color.Lerp(HitFlashColor, _hitBaseColor, ease);
                yield return null;
            }

            transform.localScale = _hitBaseScale;
            if (_sprite != null) _sprite.color = _hitBaseColor;
            _reacting = false;
            _hitReactionCo = null;
        }

        // ======================================================================
        // PATCH 22 mai (test designer) — Voile d'Ombre Nightseer : invisibilite
        // cote adversaire. Le combattant porteur de StatusKind.Untargetable doit
        // DISPARAITRE VISUELLEMENT de l'ecran adverse (Bible V7.1 l.530), tout en
        // restant visible pour lui-meme. Le check viewer/owner est fait par le
        // CombatantRenderer ; ici on ne fait que masquer/reveler le rendu.
        //
        // On toggle TOUS les Renderer enfants (sprite + marques ajoutees dynamiquement
        // par CombatantMarksView qui sont enfants de ce GO) plutot que SetActive(false)
        // sur le GO racine : ca evite de figer le sync de position (Update) et les
        // coroutines (teleport) tant que le combattant reste actif cote sim.
        //
        // Re-query GetComponentsInChildren UNIQUEMENT au changement d'etat (rare : 1x
        // par round) — pas d'alloc par frame.
        // ======================================================================
        private bool _cloaked;

        public void SetCloaked(bool cloaked)
        {
            if (cloaked == _cloaked) return;
            _cloaked = cloaked;
            // include inactive = true : capture aussi les renderers de GO enfants desactives.
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) renderers[i].enabled = !cloaked;
            }
        }

        /// <summary>
        /// 2.16.c.vi — Maj position + path cardinal cell-by-cell.
        ///
        /// `worldPosition` est la destination finale. Si waypoints != null et > 0, l'animation
        /// passera par chaque case intermediaire avant d'arriver a la destination (style Dofus).
        /// Si waypoints null/vide, comportement legacy : un seul target final (lerp direct).
        ///
        /// Appele par CombatantRenderer chaque frame. Le Renderer detecte si GridX/Y a change
        /// et calcule le path Manhattan en consequence.
        /// </summary>
        public void UpdateGridPosition(int gx, int gy, Vector3 worldPosition, Vector3[] intermediates = null, int intermediatesCount = 0)
        {
            // BUG fix 2.16.c.vi : ne reset la queue de waypoints QUE si la destination
            // grille change ou si un nouveau path est fourni. Sinon le Renderer (qui
            // appelle ici chaque frame) ecrase l'intermediaire pose au tick precedent
            // et le visuel revient en ligne droite (perception diagonale).
            bool destinationChanged = (GridX != gx || GridY != gy);
            bool newPath = intermediates != null && intermediatesCount > 0;

            GridX = gx;
            GridY = gy;

            if (destinationChanged || newPath)
            {
                _waypointCount = 0;
                if (intermediates != null)
                {
                    int copy = intermediatesCount;
                    if (copy > MaxWaypoints - 1) copy = MaxWaypoints - 1; // garde 1 slot pour final
                    for (int i = 0; i < copy; i++)
                    {
                        _waypoints[_waypointCount++] = intermediates[i];
                    }
                }
                _waypoints[_waypointCount++] = worldPosition;
                _waypointIdx = 0;
                _facingAppliedForWaypointIdx = -1; // force le recalcul facing pour le 1er segment
            }

            // Au tout premier set (juste apres Bind), snap directement pour eviter une animation
            // de spawn depuis (0,0,0) vers la case initiale.
            if (!_hasTarget)
            {
                transform.position = worldPosition;
                _hasTarget = true;
                _waypointIdx = _waypointCount; // tout consomme (snap)
            }

            if (_sprite != null)
            {
                // Base 1000 pour garantir que les combattants passent toujours devant
                // les tiles (max sortingOrder tile = 0). Multiplicateur 10 sur (gx + gy)
                // pour preserver l'ordre iso entre combattants (celui qui a (gx+gy) plus
                // petit est plus pres de la camera, donc devant).
                // Range pour une grille 15x17 : 1000 - 30*10 = 700 (min). Toujours > 0.
                _sprite.sortingOrder = 1000 - (gx + gy) * 10;
                UpdateAuraSorting(_sprite.sortingOrder);
            }
        }

        /// <summary>
        /// Refonte Ghostra — place les 2 couches d'aura autour du sprite de base (back derriere,
        /// front devant) sur le meme sorting layer. No-op si les renderers d'aura sont absents.
        /// </summary>
        private void UpdateAuraSorting(int baseOrder)
        {
            if (_sprite == null) return;
            if (_auraBack != null)
            {
                _auraBack.sortingLayerID = _sprite.sortingLayerID;
                _auraBack.sortingOrder = baseOrder + AuraBackSortingDelta;
            }
            if (_auraFront != null)
            {
                _auraFront.sortingLayerID = _sprite.sortingLayerID;
                _auraFront.sortingOrder = baseOrder + AuraFrontSortingDelta;
            }
        }

        /// <summary>
        /// 3.7.a.i — Snap instantané à une position (téléport visuel sans lerp/walk).
        /// Utilisé par la Permutation Ghostra (Bible "INVISIBLE cote adversaire") pour
        /// éviter une animation de marche entre les 2 positions swappées. Reset waypoints.
        ///
        /// Met aussi à jour GridX/Y + sorting order (comme UpdateGridPosition) mais skip
        /// toute interpolation. Le facing est laissé inchangé (la Permutation n'a pas de
        /// "direction de mouvement" Bible-cohérente).
        /// </summary>
        public void SnapToPosition(int gx, int gy, Vector3 worldPosition)
        {
            GridX = gx;
            GridY = gy;
            transform.position = worldPosition;

            // Reset queue de waypoints : tout consommé, pas de move en cours.
            _waypointCount = 0;
            _waypointIdx = 0;
            _hasTarget = true;
            _facingAppliedForWaypointIdx = -1;

            if (_sprite != null)
            {
                _sprite.sortingOrder = 1000 - (gx + gy) * 10;
                UpdateAuraSorting(_sprite.sortingOrder);
            }
        }

        // 3.7.a.i — Coroutine VFX téléportation Permutation Ghostra (Bible "invisible
        // cote adversaire"). Sequence : fade out 0.15s → snap + flash bleu spectral 0.08s
        // → fade in 0.20s. Couleur de flash mappée sur le fantasy Ghostra (bleu pâle).
        private Coroutine _teleportCoroutine;
        private const float TeleportFadeOutDuration = 0.15f;
        private const float TeleportFlashDuration = 0.08f;
        private const float TeleportFadeInDuration = 0.20f;
        private static readonly Color TeleportFlashColor = new Color(0.6f, 0.85f, 1.0f, 1.0f); // bleu spectral

        /// <summary>
        /// 3.7.a.i — Joue le VFX téléportation Permutation Ghostra : fade out à la position
        /// actuelle → snap à la nouvelle position avec flash bleu spectral → fade in. Plus
        /// joli que le snap pur. Override toute coroutine VFX en cours.
        ///
        /// Si _sprite est null (rare), fallback sur SnapToPosition pur.
        /// </summary>
        public void PlayTeleportEffect(int gx, int gy, Vector3 worldPosition)
        {
            if (_sprite == null)
            {
                SnapToPosition(gx, gy, worldPosition);
                return;
            }
            if (_teleportCoroutine != null) StopCoroutine(_teleportCoroutine);
            _teleportCoroutine = StartCoroutine(TeleportRoutine(gx, gy, worldPosition));
        }

        private IEnumerator TeleportRoutine(int gx, int gy, Vector3 worldPosition)
        {
            Color baseColor = _sprite.color;
            // Si on a déjà été interrompu en pleine animation, baseColor peut être altéré.
            // On force la base à blanc opaque pour normaliser.
            Color normalColor = new Color(1f, 1f, 1f, 1f);

            // Phase 1 : fade out alpha 1→0 à la position courante.
            float t = 0f;
            while (t < TeleportFadeOutDuration)
            {
                t += Time.deltaTime;
                float k = t / TeleportFadeOutDuration;
                if (k > 1f) k = 1f;
                _sprite.color = new Color(normalColor.r, normalColor.g, normalColor.b, 1f - k);
                yield return null;
            }

            // Phase 2 : snap à la nouvelle position + flash bleu spectral (alpha 1).
            SnapToPosition(gx, gy, worldPosition);
            _sprite.color = TeleportFlashColor;
            yield return new WaitForSeconds(TeleportFlashDuration);

            // Phase 3 : fade flash → couleur normale.
            t = 0f;
            while (t < TeleportFadeInDuration)
            {
                t += Time.deltaTime;
                float k = t / TeleportFadeInDuration;
                if (k > 1f) k = 1f;
                _sprite.color = Color.Lerp(TeleportFlashColor, normalColor, k);
                yield return null;
            }
            _sprite.color = normalColor;
            _teleportCoroutine = null;
        }

        /// <summary>
        /// Met a jour le visuel selon le stage de ressource ET la direction iso.
        /// Stage clamp [0..2]. No-op si rien n'a change.
        ///
        /// Mapping facing -> assets :
        ///   NE : controller/sprite NE, flipX = false
        ///   SE : controller/sprite SE, flipX = false
        ///   NW : controller/sprite NE, flipX = true   (miroir)
        ///   SW : controller/sprite SE, flipX = true   (miroir)
        ///
        /// Priorite :
        ///   1. Si Animator + AnimatorController dispo pour (stage, dir) -> swap controller, anims actives.
        ///   2. Sinon, fallback sur le Sprite statique correspondant.
        /// </summary>
        public void SetStageAndFacing(int stage, IsoFacing facing)
        {
            // Tuner dev : si un stage de preview est force, il prend le pas sur le stage pousse
            // par le renderer (clamp a chaque appel) -> pas de thrash d'animation, le renderer
            // n'arrive plus a ramener le perso a son stage de ressource tant que l'override tient.
            if (_previewStageOverride >= 0) stage = _previewStageOverride;
            if (stage < 0) stage = 0;
            if (stage > 2) stage = 2;
            if (stage == _currentStage && facing == _currentFacing) return;
            _currentStage = stage;
            _currentFacing = facing;

            bool useNE = facing == IsoFacing.NE || facing == IsoFacing.NW;
            bool flipX = facing == IsoFacing.NW || facing == IsoFacing.SW;

            var controller = PickController(stage, useNE);
            var fallbackSprite = PickSprite(stage, useNE);

            if (_sprite != null) _sprite.flipX = flipX;

            // Refonte Ghostra — (re)pose les 2 couches d'aura selon le stage + la direction.
            // No-op pour les classes sans aura (renderers null). Fait avant les branches du skin
            // de base pour que l'aura suive le facing meme si le skin tombe en fallback.
            ApplyAura(stage, useNE, flipX);

            // Y offset visuel par stage (workaround pivot Aseprite non standardise — cf
            // incident Colossar 18 mai ou stage1/2 montaient en Y par rapport au stage0).
            // No-op pour les classes ou les 3 offsets sont a 0 (Necram/Ghostra/Soulrender/
            // Nightseer). Pour Colossar : Lorenzo tune stage1/2 dans l'Inspector du prefab.
            //
            // PRE-REQUIS : le SpriteRenderer doit etre sur un child "Visual" du prefab
            // (cf RestructureColossarPrefabTool). Sinon modifier _sprite.transform.localPosition
            // shifte le root et casse worldPos. Pour les classes mono-GO (Soulrender, Nightseer
            // sans restructure), garder les 3 offsets a 0 = pas de modif transform.
            if (_sprite != null && _sprite.transform != transform)
            {
                float offY = stage == 0 ? _stage0VisualYOffset
                           : stage == 1 ? _stage1VisualYOffset
                           : _stage2VisualYOffset;
                float offX = stage == 0 ? _stage0VisualXOffset
                           : stage == 1 ? _stage1VisualXOffset
                           : _stage2VisualXOffset;
                Vector3 pos = _sprite.transform.localPosition;
                if (!Mathf.Approximately(pos.y, offY) || !Mathf.Approximately(pos.x, offX))
                {
                    pos.y = offY;
                    pos.x = offX;
                    _sprite.transform.localPosition = pos;
                }
            }

            // Priorite Animator si dispo.
            if (_animator != null && controller != null)
            {
                // Garde anti-reset : ne re-assigne le controller que s'il change reellement.
                // Refonte Ghostra : le skin de base est IDENTIQUE aux 3 stages (meme controller
                // bind sur les 3 slots) ; sans ce garde, chaque changement de stage (gain/perte
                // de leurre) re-assignerait le meme controller et ferait repartir l'anim a Idle.
                // Pour les autres classes les controllers de stage DIFFERENT -> garde sans effet.
                if (_animator.runtimeAnimatorController != controller)
                {
                    _animator.runtimeAnimatorController = controller;
                }
                if (!_animator.enabled) _animator.enabled = true;
                return;
            }

            // Fallback : Sprite statique (uniquement si on a un sprite assigne).
            if (fallbackSprite != null)
            {
                if (_animator != null) _animator.enabled = false;
                if (_sprite != null) _sprite.sprite = fallbackSprite;
                return;
            }

            // Ni controller ni sprite assigne pour ce (stage, facing) — assets designer manquants.
            // Plutot que de DESACTIVER l'Animator (= perte totale d'anim, bug Lorenzo 3.3.d),
            // on garde le dernier controller actif. Le sprite restera celui du stage precedent,
            // ce qui est moins joli mais coherent visuellement.
            Debug.LogWarning($"[Nymora.CombatantView] {Class} stage {stage} facing {facing} : aucun controller ni sprite assigne dans le prefab. Garde le dernier controller actif (fallback).", this);
        }

        /// <summary>
        /// Pick le controller pour (stage, direction). Fallback DIRECTIONNEL : si le stage demande
        /// n'a pas de controller dans la direction voulue, on descend vers stage-1, stage-2, ...
        /// dans la MEME direction. Preserve la direction visuelle quand le designer n'a livre
        /// que Stage 0 (bug 3.3.d : Colossar passe stage 1 a FD=2 sans assets stage 1 livres,
        /// gardait le dernier controller NE -> SE/SW restaient orientees NE).
        /// </summary>
        private RuntimeAnimatorController PickController(int stage, bool useNE)
        {
            for (int s = stage; s >= 0; s--)
            {
                RuntimeAnimatorController c = useNE
                    ? (s == 0 ? _stage0ControllerNE : s == 1 ? _stage1ControllerNE : _stage2ControllerNE)
                    : (s == 0 ? _stage0ControllerSE : s == 1 ? _stage1ControllerSE : _stage2ControllerSE);
                if (c != null) return c;
            }
            return null;
        }

        /// <summary>
        /// Pick le sprite pour (stage, direction). Meme fallback directionnel que PickController.
        /// </summary>
        private Sprite PickSprite(int stage, bool useNE)
        {
            for (int s = stage; s >= 0; s--)
            {
                Sprite sp = useNE
                    ? (s == 0 ? _stage0SpriteNE : s == 1 ? _stage1SpriteNE : _stage2SpriteNE)
                    : (s == 0 ? _stage0SpriteSE : s == 1 ? _stage1SpriteSE : _stage2SpriteSE);
                if (sp != null) return sp;
            }
            return null;
        }

        // ==================================================================================
        // Refonte Ghostra — application des couches d'aura "sandwich".
        // ==================================================================================

        /// <summary>
        /// (Re)pose les 2 couches d'aura (back + front) pour le stage et la direction donnes.
        /// stage 0 -> auras eteintes ; stage 1/2 -> boucle du stage correspondant, miroir si W.
        /// No-op total si les renderers d'aura sont absents (classes hors Ghostra).
        /// </summary>
        private void ApplyAura(int stage, bool useNE, bool flipX)
        {
            ApplyAuraLayer(_auraBack, _auraBackPlayer, PickAuraBack(stage, useNE), flipX, _auraFps);
            ApplyAuraLayer(_auraFront, _auraFrontPlayer, PickAuraFront(stage, useNE), flipX, _auraFps);
        }

        /// <summary>
        /// Allume/eteint une couche d'aura. frames null/vide -> renderer disabled (stage 0 ou
        /// direction non livree). Sinon renderer visible + flipX + boucle posee sur le player.
        /// </summary>
        private static void ApplyAuraLayer(SpriteRenderer sr, AuraLoopPlayer player, Sprite[] frames, bool flipX, float fps)
        {
            if (sr == null) return;
            if (frames == null || frames.Length == 0)
            {
                if (player != null) player.Stop();
                if (sr.enabled) sr.enabled = false;
                return;
            }
            if (!sr.enabled) sr.enabled = true;
            sr.flipX = flipX;
            if (player != null) player.SetClip(frames, fps);
        }

        private Sprite[] PickAuraBack(int stage, bool useNE)
        {
            if (stage <= 0) return null;
            if (stage == 1) return useNE ? _auraStage1BackNE : _auraStage1BackSE;
            return useNE ? _auraStage2BackNE : _auraStage2BackSE;
        }

        private Sprite[] PickAuraFront(int stage, bool useNE)
        {
            if (stage <= 0) return null;
            if (stage == 1) return useNE ? _auraStage1FrontNE : _auraStage1FrontSE;
            return useNE ? _auraStage2FrontNE : _auraStage2FrontSE;
        }

        // ----------------------------------------------------------------------------------
        // Accessors lus par DecoyView : un leurre Ghostra doit etre INDISCERNABLE de la vraie
        // Ghostra (Bible V7.1). Plutot que de re-cabler les controllers sur chaque scene, le
        // leurre lit le controller de base + les boucles d'aura directement sur la vue du
        // combattant parent (suit aussi automatiquement un skin cosmetique equipe).
        // ----------------------------------------------------------------------------------

        /// <summary>Controller du skin de BASE pour la direction (stage 0 ; identique aux 3 stages
        /// pour Ghostra, ou skin equipe si ApplySkin a swappe). Lu par les leurres.</summary>
        public RuntimeAnimatorController GhostraBaseController(bool useNE)
            => useNE ? _stage0ControllerNE : _stage0ControllerSE;

        /// <summary>Frames de la boucle d'aura (back/front) du stage + direction, ou null (stage 0 /
        /// classe sans aura). Lu par les leurres pour reproduire le sandwich d'aura de la vraie Ghostra.</summary>
        public Sprite[] GhostraAuraFrames(int stage, bool front, bool useNE)
            => front ? PickAuraFront(stage, useNE) : PickAuraBack(stage, useNE);

        /// <summary>Cadence des boucles d'aura (frames/s), pour que les leurres jouent au meme rythme.</summary>
        public float AuraFps => _auraFps;

        private void Update()
        {
            if (!_hasTarget) return;
            if (_waypointIdx >= _waypointCount)
            {
                PushAnimMoveSpeed(0f);
                _dashing = false; // J9 — fin de mouvement : plus de dash
                return;
            }

            Vector3 target = _waypoints[_waypointIdx];
            Vector3 current = transform.position;

            // 2.16.c.vi — Set le facing iso pour ce segment dès qu'on entre dedans.
            // Recalcule uniquement quand on change de waypoint (sinon spam SetStageAndFacing
            // a chaque frame). Le Renderer ne touche pas au facing pendant IsMoving (cf bloc
            // de garde dans CombatantRenderer).
            if (_facingAppliedForWaypointIdx != _waypointIdx)
            {
                Vector3 delta = target - current;
                IsoFacing segmentFacing = IsoFacingFromWorldDelta(delta, _currentFacing);
                SetStageAndFacing(_currentStage < 0 ? 0 : _currentStage, segmentFacing);
                _facingAppliedForWaypointIdx = _waypointIdx;
            }

            bool reached = (current - target).sqrMagnitude < SnapDistance * SnapDistance;
            if (reached)
            {
                transform.position = target;
                _waypointIdx++;
                if (_waypointIdx >= _waypointCount)
                {
                    PushAnimMoveSpeed(0f);
                }
                return;
            }

            // Vitesse constante (vs Lerp exponentiel Zeno) : mouvement uniforme = clip walk
            // lisible. J9 : x DashSpeedMultiplier pendant une charge (dash rapide + trainee).
            float speed = MoveSpeedUnitsPerSecond * (_dashing ? DashSpeedMultiplier : 1f);
            transform.position = Vector3.MoveTowards(current, target, speed * Time.deltaTime);
            PushAnimMoveSpeed(_desiredMoveSpeed);
            if (_dashing) SpawnAfterimageTick();
        }

        /// <summary>
        /// 2.16.c.vi — Mappe un world delta (vecteur vers le prochain waypoint) sur une
        /// des 4 directions iso. En projection iso 2:1, les 4 cardinaux grille tombent
        /// pile sur les 4 quadrants ecran :
        ///   East  grille (gx+1) -> world (+x, +y) -> iso NE
        ///   West  grille (gx-1) -> world (-x, -y) -> iso SW
        ///   North grille (gy+1) -> world (-x, +y) -> iso NW
        ///   South grille (gy-1) -> world (+x, -y) -> iso SE
        ///
        /// Si delta est negligeable (waypoint quasi-atteint), conserve le facing courant
        /// pour eviter un flicker.
        /// </summary>
        private static IsoFacing IsoFacingFromWorldDelta(Vector3 delta, IsoFacing fallback)
        {
            const float Eps = 0.01f;
            if (delta.sqrMagnitude < Eps * Eps) return fallback;
            if (delta.x >= 0f && delta.y >= 0f) return IsoFacing.NE;
            if (delta.x <  0f && delta.y >= 0f) return IsoFacing.NW;
            if (delta.x >= 0f && delta.y <  0f) return IsoFacing.SE;
            return IsoFacing.SW;
        }

        // ======================================================================
        // 2.12.bis : API anims pour driver l'Animator depuis le Renderer.
        //
        // Tous ces helpers sont safe-by-default :
        //   - no-op si _animator est null (cas placeholder ou prefab non bind)
        //   - no-op si runtimeAnimatorController est null (controller pas encore pose)
        // Donc le Renderer peut spammer sans verif.
        // ======================================================================

        /// <summary>
        /// Set la vitesse desiree pendant le walk (1.0 = normal, &lt;1 = lent, &gt;1 = rapide).
        /// Sera pushee a l'Animator a chaque frame pendant le lerp de mouvement.
        /// Convention Bible : 1-2 PM depense = 0.8 (lent), 3 PM = 1.5 (rapide).
        /// </summary>
        public void SetDesiredMoveSpeed(float speed)
        {
            if (speed < 0f) speed = 0f;
            _desiredMoveSpeed = speed;
        }

        /// <summary>
        /// Trigger l'anim Cast (sort a distance). La vitesse depend de la SpellCategory :
        ///   - Survival  : 0.7 (lent, geste defensif)
        ///   - Tactical  : 1.0 (neutre)
        ///   - Offensive : 1.3 (agressif)
        ///   - Signature : 1.5 (incantation puissante)
        /// </summary>
        public void TriggerCast(SpellCategory category)
        {
            if (!IsAnimatorReady()) return;
            _animator.SetFloat(ParamCastSpeedHash, CastSpeedForCategory(category));
            _animator.SetTrigger(ParamCastHash);
        }

        /// <summary>
        /// Trigger l'anim Attack (mêlée). Pour les sorts Range=1 (Tranche-Ame, Empoignade,
        /// Charge Brutale terminale, Ame Lacéree, etc.). Le Renderer choisit Cast vs Attack
        /// selon RangeMax du SpellDef.
        /// </summary>
        public void TriggerAttack()
        {
            if (!IsAnimatorReady()) return;
            _animator.SetTrigger(ParamAttackHash);
        }

        public void TriggerHurt()
        {
            if (!IsAnimatorReady()) return;
            _animator.SetTrigger(ParamHurtHash);
        }

        public void TriggerDeath()
        {
            if (!IsAnimatorReady()) return;
            _animator.SetTrigger(ParamDeathHash);
        }

        /// <summary>
        /// J9 — Active le DASH pour le mouvement courant (charge) : vitesse x DashSpeedMultiplier +
        /// trainee spectrale. Se desactive automatiquement en fin de mouvement (Update).
        /// </summary>
        public void TriggerDash()
        {
            _dashing = true;
            _afterimageTimer = 0f;
        }

        // J9 — Spawn periodique d'une image fantome (copie figee du sprite) qui s'estompe -> motion blur.
        private void SpawnAfterimageTick()
        {
            _afterimageTimer -= Time.deltaTime;
            if (_afterimageTimer > 0f) return;
            _afterimageTimer = AfterimageInterval;
            if (_sprite == null || _sprite.sprite == null) return;

            var go = new GameObject("DashAfterimage");
            var st = _sprite.transform;
            go.transform.position = st.position;
            go.transform.rotation = st.rotation;
            go.transform.localScale = st.lossyScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _sprite.sprite;
            sr.flipX = _sprite.flipX;
            sr.sortingLayerID = _sprite.sortingLayerID;
            sr.sortingOrder = _sprite.sortingOrder - 1; // juste derriere le perso
            sr.color = new Color(0.65f, 0.82f, 1f, 0.5f); // teinte spectrale "mini-tp"

            StartCoroutine(FadeAfterimage(sr));
        }

        private IEnumerator FadeAfterimage(SpriteRenderer sr)
        {
            const float life = 0.22f;
            float e = 0f;
            Color baseC = sr.color;
            while (e < life && sr != null)
            {
                e += Time.deltaTime;
                float k = Mathf.Clamp01(1f - e / life);
                var c = baseC;
                c.a = baseC.a * k;
                sr.color = c;
                yield return null;
            }
            if (sr != null) Destroy(sr.gameObject);
        }

        private void PushAnimMoveSpeed(float speed)
        {
            if (!IsAnimatorReady()) return;
            _animator.SetFloat(ParamMoveSpeedHash, speed);
        }

        private bool IsAnimatorReady()
        {
            return _animator != null && _animator.runtimeAnimatorController != null;
        }

        private static float CastSpeedForCategory(SpellCategory category)
        {
            switch (category)
            {
                case SpellCategory.Survival: return 0.7f;
                case SpellCategory.Tactical: return 1.0f;
                case SpellCategory.Offensive: return 1.3f;
                case SpellCategory.Signature: return 1.5f;
                default: return 1.0f;
            }
        }
    }
}
