using UnityEngine;

namespace Nymora.Hub
{
    /// <summary>
    /// Flipbook autonome pour un SpriteRenderer de scene (torches, props animes...). Contrairement
    /// a SceneSpriteAnimator (qui attend un Play() pilote par code chaque frame, pour l'avatar),
    /// celui-ci porte SES frames + fps en serialise et s'anime tout seul au runtime via Time.time.
    ///
    /// _startOffset desynchronise plusieurs instances (ex : 12 torches qui ne flickent pas en
    /// unisson). 100% View, aucun impact sim. Anime en Play Mode / build (pas en edit mode :
    /// le sprite reste sur la frame 0, ce qui suffit pour le placement/preview de tri).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteFlipbook : MonoBehaviour
    {
        [SerializeField] private Sprite[] _frames;
        [SerializeField] private float _fps = 10f;
        [Tooltip("Decalage temporel (s) pour desync plusieurs instances.")]
        [SerializeField] private float _startOffset;

        private SpriteRenderer _sr;

        private void OnEnable()
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (_sr != null && _frames != null && _frames.Length > 0) _sr.sprite = _frames[0];
        }

        /// <summary>Config par l'editor tool (HubTorchSlicerTool) a l'instanciation.</summary>
        public void Configure(Sprite[] frames, float fps, float startOffset)
        {
            _frames = frames;
            _fps = fps > 0 ? fps : 10f;
            _startOffset = startOffset;
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (_sr != null && _frames != null && _frames.Length > 0) _sr.sprite = _frames[0];
        }

        private void Update()
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (_sr == null || _frames == null || _frames.Length == 0 || _fps <= 0f) return;
            int frame = Mathf.FloorToInt((Time.time + _startOffset) * _fps) % _frames.Length;
            if (frame < 0) frame += _frames.Length;
            _sr.sprite = _frames[frame];
        }
    }
}
