using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Combat.View.HUD
{
    /// <summary>
    /// Mirror du Hub UISpriteAnimator (5.3.f) pour utilisation cote Combat asmdef.
    /// Anime une Image UI avec un array Sprite[] + FPS. Idempotent : re-play avec
    /// les memes refs ne reset pas l'elapsed.
    /// </summary>
    public sealed class CombatUISpriteAnimator : MonoBehaviour
    {
        private Image _image;
        private Sprite[] _frames;
        private float _fps = 8f;
        private float _elapsed;

        public void Play(Image image, Sprite[] frames, float fps)
        {
            float newFps = fps > 0 ? fps : 8f;
            if (ReferenceEquals(_image, image) && ReferenceEquals(_frames, frames) && Mathf.Approximately(_fps, newFps))
            {
                return; // idempotent
            }
            _image = image;
            _frames = frames;
            _fps = newFps;
            _elapsed = 0f;
            if (_image != null && _frames != null && _frames.Length > 0)
            {
                _image.sprite = _frames[0];
            }
        }

        private void Update()
        {
            if (_image == null || _frames == null || _frames.Length == 0) return;
            _elapsed += Time.deltaTime;
            int frame = Mathf.FloorToInt(_elapsed * _fps) % _frames.Length;
            _image.sprite = _frames[frame];
        }
    }
}
