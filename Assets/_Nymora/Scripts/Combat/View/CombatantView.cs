using Quantum;
using UnityEngine;

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
        // Lerp ~0.15s pour 1 case d'eloignement (assez rapide pour rester reactif, assez visible pour qu'on suive le pion a l'oeil).
        private const float MoveLerpSpeed = 8f;
        // En dessous de ce seuil on snap directement (evite de lerp eternellement sur des distances infimes).
        private const float SnapDistance = 0.01f;

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

        public EntityRef Entity { get; private set; }
        public int GridX { get; private set; }
        public int GridY { get; private set; }
        public NymoraClass Class { get; private set; }

        private Vector3 _targetWorldPosition;
        private bool _hasTarget;
        private int _currentStage = -1; // -1 = pas encore initialise
        private IsoFacing _currentFacing = (IsoFacing)(-1); // sentinelle invalide pour forcer le premier set

        public void Bind(EntityRef entity, NymoraClass nymoraClass)
        {
            Entity = entity;
            Class = nymoraClass;
            if (_sprite == null) _sprite = GetComponentInChildren<SpriteRenderer>();

            // Affichage initial : stage 0 + facing SE par defaut (sera override des le 1er OnUpdateView).
            SetStageAndFacing(0, IsoFacing.SE);
        }

        public void UpdateGridPosition(int gx, int gy, Vector3 worldPosition)
        {
            GridX = gx;
            GridY = gy;
            _targetWorldPosition = worldPosition;

            // Au tout premier set (juste apres Bind), snap directement pour eviter une animation
            // de spawn depuis (0,0,0) vers la case initiale.
            if (!_hasTarget)
            {
                transform.position = worldPosition;
                _hasTarget = true;
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

            // Priorite Animator si dispo.
            if (_animator != null && controller != null)
            {
                _animator.runtimeAnimatorController = controller;
                if (!_animator.enabled) _animator.enabled = true;
                return;
            }

            // Fallback : Sprite statique.
            if (_animator != null) _animator.enabled = false;
            if (_sprite == null) return;
            if (fallbackSprite != null) _sprite.sprite = fallbackSprite;
        }

        private RuntimeAnimatorController PickController(int stage, bool useNE)
        {
            if (useNE)
            {
                return stage == 0 ? _stage0ControllerNE
                     : stage == 1 ? _stage1ControllerNE
                     : _stage2ControllerNE;
            }
            return stage == 0 ? _stage0ControllerSE
                 : stage == 1 ? _stage1ControllerSE
                 : _stage2ControllerSE;
        }

        private Sprite PickSprite(int stage, bool useNE)
        {
            if (useNE)
            {
                return stage == 0 ? _stage0SpriteNE
                     : stage == 1 ? _stage1SpriteNE
                     : _stage2SpriteNE;
            }
            return stage == 0 ? _stage0SpriteSE
                 : stage == 1 ? _stage1SpriteSE
                 : _stage2SpriteSE;
        }

        private void Update()
        {
            if (!_hasTarget) return;
            Vector3 current = transform.position;
            if ((current - _targetWorldPosition).sqrMagnitude < SnapDistance * SnapDistance)
            {
                transform.position = _targetWorldPosition;
                return;
            }
            transform.position = Vector3.Lerp(current, _targetWorldPosition, Time.deltaTime * MoveLerpSpeed);
        }
    }
}
