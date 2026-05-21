using UnityEngine;

namespace Nymora.Combat.View
{
    /// <summary>
    /// Anime le SpriteRenderer de la map de combat (Map_Combat_1) en cyclant une liste
    /// de frames a un FPS reglable. Cote View uniquement (aucun impact simulation Quantum),
    /// donc Time.deltaTime autorise. Le calage / scale / sortingOrder restent geres par le
    /// SpriteRenderer existant : ce component ne touche que le sprite affiche.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public sealed class MapSpriteAnimator : MonoBehaviour
    {
        [Tooltip("Frames de l'animation, dans l'ordre de lecture (boucle).")]
        [SerializeField] private Sprite[] _frames;

        [Tooltip("Vitesse de lecture en images par seconde. 12 frames a 10 fps = boucle de 1,2s.")]
        [SerializeField, Range(1f, 30f)] private float _fps = 10f;

        private SpriteRenderer _renderer;
        private float _elapsed;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            if (_frames != null && _frames.Length > 0)
            {
                _renderer.sprite = _frames[0];
            }
        }

        private void Update()
        {
            if (_renderer == null || _frames == null || _frames.Length == 0) return;
            _elapsed += Time.deltaTime;
            int frame = Mathf.FloorToInt(_elapsed * _fps) % _frames.Length;
            _renderer.sprite = _frames[frame];
        }
    }
}
