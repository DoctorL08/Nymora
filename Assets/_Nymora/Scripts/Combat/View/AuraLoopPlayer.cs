using UnityEngine;

namespace Nymora.Combat.View
{
    /// <summary>
    /// Refonte Ghostra — petit lecteur de boucle de sprites pour les couches d'aura.
    ///
    /// Pourquoi pas un Animator : l'aura est un enfant du GameObject "Visual" qui porte deja
    /// l'Animator du skin de base. Imbriquer un Animator sous un autre Animator n'est PAS
    /// supporte par Unity (casse l'anim du parent — l'idle du Ghostra disparaissait). On pilote
    /// donc la boucle d'aura a la main, sans Animator, ce qui retire toute imbrication.
    ///
    /// Pose le sprite courant sur le SpriteRenderer du meme GameObject. No-op tant qu'aucun clip
    /// n'est pose (Stop) — l'appelant (CombatantView / DecoyView) gere l'activation du renderer.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class AuraLoopPlayer : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private Sprite[] _frames;
        private float _fps = 8f;
        private float _accum; // frames accumulees (en unites de frame)
        private int _index;

        private SpriteRenderer Sprite => _sr != null ? _sr : (_sr = GetComponent<SpriteRenderer>());

        /// <summary>
        /// Pose (ou remplace) la boucle. Memes frames que l'appel precedent -> ne reset pas la
        /// lecture (evite un saut a frame 0 a chaque frame). Frames null/vide = Stop.
        /// </summary>
        public void SetClip(Sprite[] frames, float fps)
        {
            if (frames == null || frames.Length == 0) { Stop(); return; }
            if (ReferenceEquals(frames, _frames)) { _fps = fps > 0f ? fps : _fps; return; }
            _frames = frames;
            _fps = fps > 0f ? fps : 8f;
            _accum = 0f;
            _index = 0;
            var sr = Sprite;
            if (sr != null) sr.sprite = _frames[0];
        }

        public void Stop()
        {
            _frames = null;
        }

        private void Update()
        {
            if (_frames == null || _frames.Length == 0) return;
            var sr = Sprite;
            if (sr == null) return;
            _accum += Time.deltaTime * _fps;
            if (_accum < 1f) return;
            int adv = (int)_accum;
            _accum -= adv;
            _index = (_index + adv) % _frames.Length;
            sr.sprite = _frames[_index];
        }
    }
}
