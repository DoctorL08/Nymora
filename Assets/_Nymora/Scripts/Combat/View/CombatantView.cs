using System.Collections;
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

        [Header("Y offset visuel par stage (workaround pivot Aseprite non standardise)")]
        [Tooltip("Y offset applique au sprite (child Visual) en stage 0. Default 0.")]
        [SerializeField] private float _stage0VisualYOffset = 0f;
        [Tooltip("Y offset applique au sprite (child Visual) en stage 1. Default 0.")]
        [SerializeField] private float _stage1VisualYOffset = 0f;
        [Tooltip("Y offset applique au sprite (child Visual) en stage 2. Default 0.")]
        [SerializeField] private float _stage2VisualYOffset = 0f;

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

        /// <summary>
        /// 2.16.c.vi — True tant qu'il reste des waypoints a consommer. Utilise par
        /// le Renderer pour ceder le controle du facing au View pendant le mouvement
        /// (chaque segment de path peut avoir une orientation iso differente).
        /// </summary>
        public bool IsMoving => _waypointIdx < _waypointCount;

        /// <summary>Facing iso courant (defaut SE si jamais set). Lu par le Renderer
        /// pendant les moves pour preserver le facing entre 2 updates de stage.</summary>
        public IsoFacing CurrentFacing => _currentFacing == (IsoFacing)(-1) ? IsoFacing.SE : _currentFacing;

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

            // Affichage initial : stage 0 + facing SE par defaut (sera override des le 1er OnUpdateView).
            SetStageAndFacing(0, IsoFacing.SE);
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

            // Priorite Animator si dispo.
            if (_animator != null && controller != null)
            {
                _animator.runtimeAnimatorController = controller;
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

        private void Update()
        {
            if (!_hasTarget) return;
            if (_waypointIdx >= _waypointCount)
            {
                PushAnimMoveSpeed(0f);
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
            // lisible. On clamp aussi a un step max pour eviter de teleporter sur une frame
            // de gros dt (lag spike).
            transform.position = Vector3.MoveTowards(
                current, target, MoveSpeedUnitsPerSecond * Time.deltaTime);
            PushAnimMoveSpeed(_desiredMoveSpeed);
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
