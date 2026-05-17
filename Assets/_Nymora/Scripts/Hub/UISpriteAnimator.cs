using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Petit component qui anime une Image UI a partir d'un array de Sprites + FPS.
    /// Cree en 5.3.f pour le Class Selector (sprites Idle des classes), car les Animators
    /// du combat sont SpriteRenderer-based (incompatibles UI Image direct).
    ///
    /// Place dans le namespace Hub (asmdef Nymora.Hub) car utilise par HubClassSelectorPanel.
    /// L'asmdef Nymora.UI depend de Hub, donc pas l'inverse.
    /// </summary>
    public sealed class UISpriteAnimator : MonoBehaviour
    {
        private Image _image;
        private Sprite[] _frames;
        private float _fps = 8f;
        private float _elapsed;

        public void Play(Image image, Sprite[] frames, float fps)
        {
            _image = image;
            _frames = frames;
            _fps = fps > 0 ? fps : 8f;
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
