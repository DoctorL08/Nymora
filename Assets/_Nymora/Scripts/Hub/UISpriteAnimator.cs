using UnityEngine;
using UnityEngine.UI;

namespace Nymora.Hub
{
    /// <summary>
    /// Petit component qui anime une Image UI a partir d'un array de Sprites + FPS.
    /// Cree en 5.3.f pour le Class Selector (sprites Idle des classes), car les Animators
    /// du combat sont SpriteRenderer-based (incompatibles UI Image direct).
    ///
    /// Deux modes :
    ///   - Legacy (Play) : swap le sprite, l'Image gère le rendu (preserveAspect côté appelant).
    ///     Comportement historique, utilisé par le Class Selector.
    ///   - Aligné (PlayAligned) : rend chaque frame à une ÉCHELLE CONSTANTE et ancre le PIVOT du
    ///     sprite à un point fixe. Indispensable pour les frames de COMBAT trimmées par frame
    ///     (tailles/positions variables) : preserveAspect les recadrerait par bounding box, ce qui
    ///     fait "danser" (jiggle X) ET "zoomer" (rescale Y) le perso. En aligné, le pivot reste
    ///     planté et la taille ne bouge plus.
    ///
    /// Place dans le namespace Hub (asmdef Nymora.Hub) car utilise par HubClassSelectorPanel.
    /// L'asmdef Nymora.UI depend de Hub, donc pas l'inverse.
    /// </summary>
    public sealed class UISpriteAnimator : MonoBehaviour
    {
        private enum Mode { Legacy, Aligned }

        private Image _image;
        private Sprite[] _frames;
        private float _fps = 8f;
        private float _elapsed;

        private Mode _mode = Mode.Legacy;
        private Vector2 _boxCenter;    // centre visé (mode aligné) : le perso y est centré
        private Vector2 _boxSize;      // box cible servant à calculer l'échelle constante
        private Vector2 _pivotAnchor;  // position fixe où planter le pivot du sprite (calée frame 0)
        private float _scale;          // échelle px->UI, calée sur la 1re frame
        private bool _ready;

        /// <summary>Mode historique : swap simple du sprite (le rendu/preserveAspect est géré dehors).</summary>
        public void Play(Image image, Sprite[] frames, float fps)
        {
            Bind(image, frames, fps, Mode.Legacy);
            if (_image != null && _frames != null && _frames.Length > 0)
                _image.sprite = _frames[0];
        }

        /// <summary>
        /// Mode aligné : échelle constante (calée sur la 1re frame, façon preserveAspect dans
        /// `boxSize`) + perso CENTRÉ sur `boxCenter`. L'ancrage frame-à-frame se fait par le pivot
        /// du sprite (stable malgré le trim) -> anti jiggle/zoom ; le centrage est calculé une fois
        /// depuis la 1re frame -> chaque classe (hauteur/pivot différents) tombe au même endroit.
        /// L'appelant doit utiliser un rect à ancre ponctuelle (anchorMin == anchorMax).
        /// </summary>
        public void PlayAligned(Image image, Sprite[] frames, float fps, Vector2 boxCenter, Vector2 boxSize)
        {
            Bind(image, frames, fps, Mode.Aligned);
            _boxCenter = boxCenter;
            _boxSize = boxSize;
            _ready = false;
            if (_image != null)
            {
                _image.preserveAspect = false; // on dimensionne nous-mêmes, exactement
                if (_frames != null && _frames.Length > 0)
                {
                    _image.sprite = _frames[0];
                    ApplyAligned(_frames[0]);
                }
            }
        }

        private void Bind(Image image, Sprite[] frames, float fps, Mode mode)
        {
            _image = image;
            _frames = frames;
            _fps = fps > 0 ? fps : 8f;
            _elapsed = 0f;
            _mode = mode;
        }

        public void Stop()
        {
            _frames = null;
            _image = null;
        }

        private void Update()
        {
            if (_image == null || _frames == null || _frames.Length == 0) return;
            _elapsed += Time.deltaTime;
            int frame = Mathf.FloorToInt(_elapsed * _fps) % _frames.Length;
            _image.sprite = _frames[frame];
            if (_mode == Mode.Aligned) ApplyAligned(_frames[frame]);
        }

        // Calé une fois sur la 1re frame : échelle constante (façon preserveAspect dans _boxSize) +
        // _pivotAnchor = position du pivot telle que le CENTRE de la 1re frame tombe sur _boxCenter.
        // Ensuite chaque frame est dimensionnée px natifs * échelle (pas de re-fit -> pas de zoom)
        // et son pivot est planté sur _pivotAnchor (stable malgré le trim -> pas de jiggle).
        private void ApplyAligned(Sprite sprite)
        {
            if (sprite == null) return;
            Vector2 sp = sprite.rect.size;
            if (sp.x <= 0f || sp.y <= 0f) return;
            var rt = _image.rectTransform;

            if (!_ready && _boxSize.x > 0f && _boxSize.y > 0f)
            {
                _scale = Mathf.Min(_boxSize.x / sp.x, _boxSize.y / sp.y);
                // Décale le pivot pour que le centre du rect de la 1re frame tombe sur _boxCenter :
                // centre - pivot (en px scalés), puis pivotAnchor = boxCenter - (centre - pivot).
                Vector2 centerMinusPivot = ((sp * 0.5f) - sprite.pivot) * _scale;
                _pivotAnchor = _boxCenter - centerMinusPivot;
                _ready = true;
            }
            rt.pivot = new Vector2(sprite.pivot.x / sp.x, sprite.pivot.y / sp.y);
            rt.sizeDelta = sp * _scale;
            rt.anchoredPosition = _pivotAnchor;
        }
    }
}
