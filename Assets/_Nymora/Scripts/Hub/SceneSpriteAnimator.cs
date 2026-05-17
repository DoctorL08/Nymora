using UnityEngine;

namespace Nymora.Hub
{
    /// <summary>
    /// Petit component qui anime un SpriteRenderer scene a partir d'un array Sprite[] + FPS.
    /// Equivalent SpriteRenderer du UISpriteAnimator (5.3.f). Cree en 5.3.g.bis pour les
    /// avatars hub : les Animators combat ciblent un child "Visual" via clip path, ce qui
    /// ne marche pas direct sur le root HubAvatar. SceneSpriteAnimator + IdleFrames stockes
    /// dans NymoraClassDefinition = anim live sans dependance Animator clip path.
    /// </summary>
    public sealed class SceneSpriteAnimator : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private Sprite[] _frames;
        private float _fps = 8f;
        private float _elapsed;

        public void Play(SpriteRenderer sr, Sprite[] frames, float fps)
        {
            float newFps = fps > 0 ? fps : 8f;
            // Idempotent : si on rappelle avec le meme SpriteRenderer + meme array de frames
            // + meme FPS, on garde l'elapsed/frame courante pour eviter le reset visuel a chaque
            // re-check (typique du switch idle/walk dans HubAvatar a chaque frame).
            if (ReferenceEquals(_sr, sr) && ReferenceEquals(_frames, frames) && Mathf.Approximately(_fps, newFps))
            {
                return;
            }
            _sr = sr;
            _frames = frames;
            _fps = newFps;
            _elapsed = 0f;
            if (_sr != null && _frames != null && _frames.Length > 0)
            {
                _sr.sprite = _frames[0];
            }
        }

        public void Stop()
        {
            _frames = null;
            _sr = null;
        }

        private void Update()
        {
            if (_sr == null || _frames == null || _frames.Length == 0) return;
            _elapsed += Time.deltaTime;
            int frame = Mathf.FloorToInt(_elapsed * _fps) % _frames.Length;
            _sr.sprite = _frames[frame];
        }
    }
}
