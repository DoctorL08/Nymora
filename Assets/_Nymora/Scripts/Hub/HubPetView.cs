using Nymora.Core.ScriptableObjects;
using UnityEngine;

namespace Nymora.Hub
{
    /// <summary>
    /// Brique 5.10 (B4) — Familier qui accompagne un avatar dans le hub.
    ///
    /// GameObject autonome (non parenté à l'avatar pour avoir un mouvement de "traîne" propre)
    /// piloté chaque frame par HubAvatar via Drive() : il rejoint en MoveTowards un point ancré
    /// près de l'avatar (un peu plus vite que lui pour rattraper), joue walk quand il se déplace
    /// et idle à l'arrêt, et reprend le facing de son propriétaire (frames SE/NE + flipX).
    ///
    /// 100% View. Pas de logique réseau ici : c'est HubAvatar (qui porte NetPetId) qui décide
    /// quel familier afficher, local comme distant.
    /// </summary>
    public sealed class HubPetView : MonoBehaviour
    {
        // Vitesse de rattrapage (u/s). > vitesse avatar (~4 t/s) pour que le familier suive.
        private const float FollowSpeed = 4.5f;
        // Au-delà de ce reste de distance, le familier est considéré "en marche" (anim walk).
        private const float WalkThreshold = 0.05f;

        private SpriteRenderer _sr;
        private SceneSpriteAnimator _anim;
        private PetDefinition _def;
        private bool _initialized;

        public void Init(PetDefinition def, string sortingLayer, Vector3 startWorld)
        {
            _def = def;
            if (_sr == null)
            {
                _sr = gameObject.GetComponent<SpriteRenderer>();
                if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();
            }
            if (!string.IsNullOrEmpty(sortingLayer)) _sr.sortingLayerName = sortingLayer;
            _sr.color = Color.white;
            if (_anim == null) _anim = gameObject.GetComponent<SceneSpriteAnimator>();
            if (_anim == null) _anim = gameObject.AddComponent<SceneSpriteAnimator>();

            ApplyScale();
            transform.position = startWorld;
            _initialized = true;

            // 1re frame idle SE pour ne pas apparaître vide avant le 1er Drive.
            if (def != null && def.IdleFrames != null && def.IdleFrames.Length > 0)
                _sr.sprite = def.IdleFrames[0];
        }

        /// <summary>
        /// Avance le familier vers `anchorWorld` (point cible près de l'avatar) et met à jour son
        /// anim/facing. `useNE`/`flipX` = facing du propriétaire ; `ownerSortingOrder` sert à le
        /// trier juste devant l'avatar (il est décalé vers la caméra).
        /// </summary>
        public void Drive(Vector3 anchorWorld, bool useNE, bool flipX, int ownerSortingOrder, float dt,
                          bool snap = false)
        {
            if (!_initialized || _def == null) return;

            // Taille relue chaque frame -> le curseur du tuner s'applique en direct.
            ApplyScale();

            Vector3 cur = transform.position;
            float remaining = Vector3.Distance(cur, anchorWorld);
            // snap = pivot sur place de l'avatar -> on téléporte le familier (pas de "marche" vers
            // l'autre côté). Sinon il rejoint l'ancre en glissant (effet de traîne).
            transform.position = snap ? anchorWorld : Vector3.MoveTowards(cur, anchorWorld, FollowSpeed * dt);

            bool walking = !snap && remaining > WalkThreshold;
            // Sélection des frames selon walk/idle + direction (NW/SW = mirror des NE/SE).
            Sprite[] frames = walking
                ? (useNE ? _def.WalkFramesNE : _def.WalkFrames)
                : (useNE ? _def.IdleFramesNE : _def.IdleFrames);
            float fps = walking ? _def.WalkFps : _def.IdleFps;
            // Fallback idle si walk pas peuplé.
            if ((frames == null || frames.Length == 0) && walking)
            {
                frames = useNE ? _def.IdleFramesNE : _def.IdleFrames;
                fps = _def.IdleFps;
            }
            if (frames != null && frames.Length > 0 && _anim != null)
                _anim.Play(_sr, frames, fps);

            if (_sr != null)
            {
                _sr.flipX = flipX;
                // Au premier plan (devant les pieds, vers la caméra) -> rendu devant l'avatar.
                _sr.sortingOrder = ownerSortingOrder + 1;
            }
        }

        // Échelle = taille native du familier × facteur de taille global (PetPlacementConfig).
        private void ApplyScale()
        {
            float sizeFactor = PetPlacementConfig.Instance.SizeFactor;
            float scale = (_def != null && _def.VisualScale > 0f ? _def.VisualScale : 1f) * sizeFactor;
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void OnDestroy()
        {
            if (_anim != null) _anim.Stop();
        }
    }
}
