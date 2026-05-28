using Nymora.Core.ScriptableObjects;
using UnityEngine;

namespace Nymora.Combat.View
{
    /// <summary>
    /// Brique 5.10 (B5) — Familier qui accompagne un combattant en combat.
    ///
    /// GameObject autonome (non parenté, comme HubPetView) piloté chaque frame par
    /// CombatantRenderer via Drive() : il colle à la position monde du combattant (même case,
    /// décalé via PetPlacementConfig.Combat*), reprend son facing (auto-aim vers l'ennemi) et
    /// joue idle/walk selon que le combattant se déplace.
    ///
    /// 100% View. Anim interne (timer) car SceneSpriteAnimator vit dans l'asmdef Hub, hors de
    /// portée du combat. Aucune lecture de la sim Quantum -> pas de bump CombatRulesVersion.
    /// </summary>
    public sealed class CombatPetView : MonoBehaviour
    {
        // Au-delà de ce delta de position monde sur une frame, le combattant est "en marche".
        private const float WalkThreshold = 0.01f;

        // Facing du familier du joueur LOCAL (lu par CombatPetPlacementTuner pour surligner le
        // bloc actif). -1 si aucun. Convention View IsoFacing (NE=0, SE=1, NW=2, SW=3).
        public static int LastLocalFacingIndex = -1;

        private SpriteRenderer _sr;
        private PetDefinition _def;
        private bool _initialized;

        // État d'anim (lecture de frames maison).
        private Sprite[] _frames;
        private float _fps;
        private float _timer;
        private int _frameIdx;

        private Vector3 _lastOwnerPos;
        private bool _hasLastPos;

        public void Init(PetDefinition def, int sortingLayerId)
        {
            _def = def;
            if (_sr == null) _sr = gameObject.GetComponent<SpriteRenderer>();
            if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sortingLayerID = sortingLayerId;
            _sr.color = Color.white;
            _initialized = true;
            _hasLastPos = false;

            // 1re frame idle SE pour ne pas apparaître vide avant le 1er Drive.
            if (def != null && def.IdleFrames != null && def.IdleFrames.Length > 0)
                _sr.sprite = def.IdleFrames[0];
        }

        /// <summary>
        /// Place + anime le familier près de `ownerWorldPos`. `facing` = orientation du combattant,
        /// `ownerSortingOrder`/`sortingLayerId` viennent du sprite du combattant (familier rendu
        /// juste devant). `isLocal` => publie LastLocalFacingIndex pour le tuner.
        /// </summary>
        public void Drive(Vector3 ownerWorldPos, IsoFacing facing, int ownerSortingOrder,
                          int sortingLayerId, bool isLocal, float dt)
        {
            if (!_initialized || _def == null) return;

            var cfg = PetPlacementConfig.Instance;
            int facingIndex = (int)facing;
            if (isLocal) LastLocalFacingIndex = facingIndex;

            // Échelle (relue chaque frame -> le curseur du tuner s'applique en direct).
            float scale = (_def.VisualScale > 0f ? _def.VisualScale : 1f) * cfg.CombatSizeFactor;
            transform.localScale = new Vector3(scale, scale, 1f);

            // Position = case du combattant + offset combat (par direction) + Y propre au familier.
            Vector2 off = cfg.CombatOffsetForFacing(facingIndex);
            transform.position = ownerWorldPos
                                 + new Vector3(off.x, off.y + _def.VisualYOffset, 0f);

            // Détection marche via delta de la position du combattant entre 2 frames.
            bool walking = _hasLastPos && (ownerWorldPos - _lastOwnerPos).sqrMagnitude > WalkThreshold * WalkThreshold;
            _lastOwnerPos = ownerWorldPos;
            _hasLastPos = true;

            // Sélection frames : SE par défaut, NE pour les directions "haut" ; W = mirror flipX.
            bool useNE = facing == IsoFacing.NE || facing == IsoFacing.NW;
            bool flipX = facing == IsoFacing.NW || facing == IsoFacing.SW;

            Sprite[] frames = walking
                ? (useNE ? _def.WalkFramesNE : _def.WalkFrames)
                : (useNE ? _def.IdleFramesNE : _def.IdleFrames);
            float fps = walking ? _def.WalkFps : _def.IdleFps;
            if ((frames == null || frames.Length == 0))
            {
                frames = useNE ? _def.IdleFramesNE : _def.IdleFrames; // fallback idle
                fps = _def.IdleFps;
            }

            Animate(frames, fps, dt);

            if (_sr != null)
            {
                _sr.flipX = flipX;
                _sr.sortingLayerID = sortingLayerId;
                _sr.sortingOrder = ownerSortingOrder + 1; // juste devant le combattant
            }
        }

        // Avance le lecteur de frames maison. Reset l'index si on change de set (idle<->walk / dir).
        private void Animate(Sprite[] frames, float fps, float dt)
        {
            if (frames == null || frames.Length == 0 || _sr == null) return;
            if (!ReferenceEquals(frames, _frames))
            {
                _frames = frames;
                _frameIdx = 0;
                _timer = 0f;
            }
            _fps = fps > 0f ? fps : 6f;
            _timer += dt;
            float step = 1f / _fps;
            while (_timer >= step)
            {
                _timer -= step;
                _frameIdx = (_frameIdx + 1) % _frames.Length;
            }
            _sr.sprite = _frames[Mathf.Clamp(_frameIdx, 0, _frames.Length - 1)];
        }

        public void SetVisible(bool visible)
        {
            if (_sr != null) _sr.enabled = visible;
        }

        /// <summary>
        /// Teinte le sprite (utilisé pour les familiers de LEURRES Ghostra : cyan/translucide
        /// côté caster, opaque côté adverse — comme le sprite du leurre).
        /// </summary>
        public void SetTint(Color c)
        {
            if (_sr != null) _sr.color = c;
        }
    }
}
